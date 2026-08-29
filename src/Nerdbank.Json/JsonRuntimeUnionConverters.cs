// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1201 // Types may be ordered for locality in this multi-type helper file.
#pragma warning disable SA1204 // Static ordering relaxed for this multi-type helper file.
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Internal helper elements are intentionally undocumented in this file.
#pragma warning disable SA1649 // File name intentionally differs from the first type in this multi-type helper file.

using System.Globalization;
using System.Reflection;
using System.Text;

namespace Nerdbank.Json;

internal abstract class RuntimeUnionCase<TBase>
{
	internal RuntimeUnionCase(int? tag, string? name)
	{
		this.Tag = tag;
		this.Name = name;
	}

	internal int? Tag { get; }

	internal string? Name { get; }

	internal abstract Type CaseType { get; }

	internal abstract JsonConverter<TBase> BuildConverter(ConverterCache owner);

	internal abstract IObjectTypeShape? GetObjectShape();

	internal abstract ITypeShape GetCaseShape();
}

internal sealed class RuntimeUnionCase<TBase, TCase> : RuntimeUnionCase<TBase>
	where TCase : TBase
{
	private readonly ITypeShape<TCase> caseShape;

	internal RuntimeUnionCase(ITypeShape<TCase> caseShape, int? tag, string? name)
		: base(tag, name)
	{
		this.caseShape = caseShape;
	}

	internal override Type CaseType => typeof(TCase);

	internal override JsonConverter<TBase> BuildConverter(ConverterCache owner)
		=> new JsonRuntimeUnionCaseConverter<TBase, TCase>((JsonConverter<TCase>)owner.GetOrAddConverter(this.caseShape));

	internal override IObjectTypeShape? GetObjectShape() => this.caseShape as IObjectTypeShape;

	internal override ITypeShape GetCaseShape() => this.caseShape;
}

internal sealed class JsonRuntimeUnionCaseConverter<TBase, TCase> : JsonConverter<TBase>
	where TCase : TBase
{
	private readonly JsonConverter<TCase> inner;

	internal JsonRuntimeUnionCaseConverter(JsonConverter<TCase> inner)
	{
		this.inner = inner;
	}

	public override void Write(ref JsonWriter writer, TBase? value, SerializationContext context)
		=> this.inner.Write(ref writer, (TCase?)value, context);

	public override TBase? Read(ref JsonReader reader, SerializationContext context)
		=> this.inner.Read(ref reader, context);
}

internal readonly struct RuntimeUnionCaseEntry<TBase>
{
	internal RuntimeUnionCaseEntry(Type caseType, int? tag, string? name, JsonConverter<TBase> converter, IReadOnlyCollection<string> requiredPropertyNames)
	{
		this.CaseType = caseType;
		this.Tag = tag;
		this.Name = name;
		this.Converter = converter;
		this.RequiredPropertyNames = requiredPropertyNames;
	}

	internal Type CaseType { get; }

	internal int? Tag { get; }

	internal string? Name { get; }

	internal JsonConverter<TBase> Converter { get; }

	internal IReadOnlyCollection<string> RequiredPropertyNames { get; }

	internal void WriteAlias(ref JsonWriter writer)
	{
		if (this.Tag is int tag)
		{
			writer.WriteNumberValue(tag);
		}
		else
		{
			writer.WriteStringValue(this.Name);
		}
	}
}

internal sealed class JsonRuntimeUnionConverter<TBase> : JsonConverter<TBase>
{
	private readonly JsonConverter<TBase>? baseConverter;
	private readonly RuntimeUnionCaseEntry<TBase>[] cases;
	private readonly Dictionary<Type, int> byType;
	private readonly Dictionary<int, int> byTag;
	private readonly Dictionary<string, int> byName;

	internal JsonRuntimeUnionConverter(JsonConverter<TBase>? baseConverter, RuntimeUnionCaseEntry<TBase>[] cases, StringComparer nameComparer)
	{
		this.baseConverter = baseConverter;
		this.cases = cases;
		this.byType = new(cases.Length);
		this.byTag = new(cases.Length);
		this.byName = new(cases.Length, nameComparer);
		for (int i = 0; i < cases.Length; i++)
		{
			this.byType[cases[i].CaseType] = i;
			if (cases[i].Tag is int tag)
			{
				this.byTag[tag] = i;
			}
			else if (cases[i].Name is string name)
			{
				this.byName[name] = i;
			}
		}
	}

	public override void Write(ref JsonWriter writer, TBase? value, SerializationContext context)
	{
		if (!typeof(TBase).IsValueType && value is null)
		{
			writer.WriteNullValue();
			return;
		}

		context.DepthStep();

		writer.WriteStartArray();
		if (value is not null && this.byType.TryGetValue(value.GetType(), out int index))
		{
			RuntimeUnionCaseEntry<TBase> entry = this.cases[index];
			entry.WriteAlias(ref writer);
			writer.WriteValueSeparator();
			entry.Converter.Write(ref writer, value, context);
		}
		else
		{
			writer.WriteNullValue();
			writer.WriteValueSeparator();
			this.WriteBase(ref writer, value, context);
		}

		writer.WriteEndArray();
	}

	public override TBase? Read(ref JsonReader reader, SerializationContext context)
	{
		if (!typeof(TBase).IsValueType && reader.TryReadNull())
		{
			return default;
		}

		context.DepthStep();

		reader.ReadStartArray();
		JsonConverter<TBase>? converter;
		if (reader.TryReadNull())
		{
			converter = this.baseConverter ?? throw new FormatException($"The JSON specified a base '{typeof(TBase).FullName}' value, but the configured union has no serializable base type.");
		}
		else if (reader.PeekValueToken() == '"')
		{
			string name = reader.ReadRequiredString();
			converter = this.byName.TryGetValue(name, out int index) ? this.cases[index].Converter : throw new FormatException($"Unrecognized union discriminator '{name}'.");
		}
		else
		{
			int tag = int.Parse(reader.ReadNumberToken(), CultureInfo.InvariantCulture);
			converter = this.byTag.TryGetValue(tag, out int index) ? this.cases[index].Converter : throw new FormatException($"Unrecognized union discriminator '{tag}'.");
		}

		reader.ReadValueSeparator();
		TBase? value = converter.Read(ref reader, context);
		reader.ReadEndArray();
		return value;
	}

	private void WriteBase(ref JsonWriter writer, TBase? value, SerializationContext context)
	{
		if (this.baseConverter is null)
		{
			throw new NotSupportedException($"No union case is registered for type '{value?.GetType().FullName}', and the base type '{typeof(TBase).FullName}' cannot be serialized directly.");
		}

		this.baseConverter.Write(ref writer, value, context);
	}
}

internal sealed class JsonDuckTypingUnionConverter<TBase> : JsonConverter<TBase>
{
	private readonly RuntimeUnionCaseEntry<TBase>[] cases;
	private readonly Dictionary<Type, int> byType;
	private readonly StringComparer nameComparer;
	private readonly bool allowTrailingCommas;
	private readonly JsonCommentHandling commentHandling;

	internal JsonDuckTypingUnionConverter(RuntimeUnionCaseEntry<TBase>[] cases, StringComparer nameComparer, bool allowTrailingCommas, JsonCommentHandling commentHandling)
	{
		this.cases = cases;
		this.nameComparer = nameComparer;
		this.allowTrailingCommas = allowTrailingCommas;
		this.commentHandling = commentHandling;
		this.byType = new(cases.Length);
		for (int i = 0; i < cases.Length; i++)
		{
			this.byType[cases[i].CaseType] = i;
			if (cases[i].RequiredPropertyNames.Count == 0)
			{
				throw new NotSupportedException($"Duck-typing union case '{cases[i].CaseType.FullName}' has no required properties, so it cannot be identified without a discriminator. Add a required property or use the default (discriminator) strategy.");
			}
		}
	}

	public override void Write(ref JsonWriter writer, TBase? value, SerializationContext context)
	{
		if (!typeof(TBase).IsValueType && value is null)
		{
			writer.WriteNullValue();
			return;
		}

		if (value is not null && this.byType.TryGetValue(value.GetType(), out int index))
		{
			this.cases[index].Converter.Write(ref writer, value, context);
			return;
		}

		throw new NotSupportedException($"No duck-typing union case is registered for type '{value?.GetType().FullName}'.");
	}

	public override TBase? Read(ref JsonReader reader, SerializationContext context)
	{
		if (!typeof(TBase).IsValueType && reader.TryReadNull())
		{
			return default;
		}

		context.DepthStep();

		string raw = reader.ReadRawValue();
		byte[] rawUtf8 = Encoding.UTF8.GetBytes(raw);

		HashSet<string> presentNames = this.ScanPropertyNames(rawUtf8);

		int match = -1;
		for (int i = 0; i < this.cases.Length; i++)
		{
			if (AllPresent(this.cases[i].RequiredPropertyNames, presentNames))
			{
				if (match >= 0)
				{
					throw new FormatException($"The JSON object is ambiguous for duck-typing union '{typeof(TBase).FullName}': it matches both '{this.cases[match].CaseType.FullName}' and '{this.cases[i].CaseType.FullName}'.");
				}

				match = i;
			}
		}

		if (match < 0)
		{
			throw new FormatException($"The JSON object provides insufficient evidence to select a duck-typing union case for '{typeof(TBase).FullName}'; no configured case had all its required properties present.");
		}

		JsonReader inner = new(rawUtf8, this.allowTrailingCommas, this.commentHandling);
		return this.cases[match].Converter.Read(ref inner, context);
	}

	private static bool AllPresent(IReadOnlyCollection<string> required, HashSet<string> present)
	{
		foreach (string name in required)
		{
			if (!present.Contains(name))
			{
				return false;
			}
		}

		return true;
	}

	private HashSet<string> ScanPropertyNames(byte[] rawUtf8)
	{
		HashSet<string> names = new(this.nameComparer);
		JsonReader reader = new(rawUtf8, this.allowTrailingCommas, this.commentHandling);
		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			return names;
		}

		while (true)
		{
			string name = reader.ReadRequiredString();
			reader.ReadNameSeparator();
			reader.SkipValue();
			names.Add(name);
			if (reader.TryReadEndObject())
			{
				break;
			}

			reader.ReadValueSeparator();
		}

		return names;
	}
}

internal static class RuntimeUnionBuilder
{
	internal static JsonConverter<TBase> Build<TBase>(ConverterCache owner, ITypeShape<TBase> shape, object entry, TypeShapeVisitor visitor)
	{
		StringComparer nameComparer = owner.PropertyNameComparer;
		ITypeShape baseTypeShape = shape is IUnionTypeShape unionForBase ? unionForBase.BaseType : shape;
		JsonConverter<TBase>? baseConverter = IsConstructible(baseTypeShape as IObjectTypeShape)
			? (JsonConverter<TBase>)baseTypeShape.Accept(visitor)!
			: null;

		if (ReferenceEquals(entry, JsonUnionConfiguration.DisabledSentinel))
		{
			return baseConverter ?? throw new NotSupportedException($"Union handling for '{typeof(TBase).FullName}' was disabled, but the base type cannot be serialized directly.");
		}

		var definition = (JsonUnion<TBase>)entry;
		List<RuntimeUnionCaseEntry<TBase>> caseEntries = new(definition.Cases.Count);

		if (definition.IncludeGeneratedCases && shape is IUnionTypeShape unionShape)
		{
			foreach (IUnionCaseShape generatedCase in unionShape.UnionCases)
			{
				var caseConverter = (JsonConverter<TBase>)generatedCase.Accept(visitor)!;
				int? tag = generatedCase.IsTagSpecified ? generatedCase.Tag : null;
				string? name = generatedCase.IsTagSpecified ? null : generatedCase.Name;
				IReadOnlyCollection<string> required = RuntimeUnionHelpers.GetRequiredSerializedPropertyNames(generatedCase.UnionCaseType as IObjectTypeShape, owner);
				caseEntries.Add(new(generatedCase.UnionCaseType.Type, tag, name, caseConverter, required));
			}
		}

		foreach (RuntimeUnionCase<TBase> runtimeCase in definition.Cases)
		{
			JsonConverter<TBase> caseConverter = runtimeCase.BuildConverter(owner);
			IReadOnlyCollection<string> required = RuntimeUnionHelpers.GetRequiredSerializedPropertyNames(runtimeCase.GetObjectShape(), owner);
			caseEntries.Add(new(runtimeCase.CaseType, runtimeCase.Tag, runtimeCase.Name, caseConverter, required));
		}

		if (caseEntries.Count == 0)
		{
			throw new NotSupportedException($"The union configuration for '{typeof(TBase).FullName}' defines no cases.");
		}

		VerifyUniqueAliases(caseEntries);

		return definition.DuckTyping
			? new JsonDuckTypingUnionConverter<TBase>([.. caseEntries], nameComparer, owner.AllowTrailingCommas, owner.ReadCommentHandling)
			: new JsonRuntimeUnionConverter<TBase>(baseConverter, [.. caseEntries], nameComparer);
	}

	private static bool IsConstructible(IObjectTypeShape? objectShape)
	{
		if (objectShape is null)
		{
			return false;
		}

		return objectShape.Constructor is { Parameters.Count: > 0 } || objectShape.GetDefaultConstructor() is not null;
	}

	private static void VerifyUniqueAliases<TBase>(List<RuntimeUnionCaseEntry<TBase>> cases)
	{
		HashSet<int> tags = [];
		HashSet<string> names = new(StringComparer.Ordinal);
		foreach (RuntimeUnionCaseEntry<TBase> entry in cases)
		{
			if (entry.Tag is int tag && !tags.Add(tag))
			{
				throw new NotSupportedException($"The union configuration for '{typeof(TBase).FullName}' uses the integer discriminator '{tag}' more than once.");
			}

			if (entry.Name is string name && !names.Add(name))
			{
				throw new NotSupportedException($"The union configuration for '{typeof(TBase).FullName}' uses the string discriminator '{name}' more than once.");
			}
		}
	}
}

internal static class RuntimeUnionHelpers
{
	internal static IReadOnlyCollection<string> GetRequiredSerializedPropertyNames(IObjectTypeShape? objectShape, ConverterCache owner)
	{
		if (objectShape is null)
		{
			return [];
		}

		HashSet<string> requiredClrNames = new(StringComparer.Ordinal);
		if (objectShape.Constructor is { } constructor)
		{
			foreach (IParameterShape parameter in constructor.Parameters)
			{
				if (parameter.IsRequired)
				{
					requiredClrNames.Add(parameter.Name);
				}
			}
		}

		HashSet<string> required = new(owner.PropertyNameComparer);
		foreach (IPropertyShape property in objectShape.Properties)
		{
			if (requiredClrNames.Contains(property.Name) || HasRequiredMemberAttribute(property.MemberInfo))
			{
				required.Add(owner.GetSerializedPropertyName(property.Name, property.AttributeProvider));
			}
		}

		return required;
	}

	private static bool HasRequiredMemberAttribute(MemberInfo? memberInfo)
	{
		if (memberInfo is null)
		{
			return false;
		}

		foreach (CustomAttributeData attribute in memberInfo.CustomAttributes)
		{
			if (attribute.AttributeType.FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute")
			{
				return true;
			}
		}

		return false;
	}
}
