// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable DuckTyping // Implements the experimental union strategy.
#pragma warning disable SA1201 // Types may be ordered for locality in this multi-type helper file.
#pragma warning disable SA1204 // Static ordering relaxed for this multi-type helper file.
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Internal helper elements are intentionally undocumented in this file.
#pragma warning disable SA1649 // File name intentionally differs from the first type in this multi-type helper file.

using System.Reflection;
using System.Text;

namespace Nerdbank.Json;

internal sealed class JsonRuntimeUnionCaseConverter<TBase> : JsonConverter<TBase>, IJsonUnionObjectConverter
{
	private readonly JsonConverter inner;

	internal JsonRuntimeUnionCaseConverter(JsonConverter converter)
	{
		this.inner = converter is IJsonReferencePreservingWrapper wrapper ? wrapper.InnerConverter : converter;
	}

	bool IJsonUnionObjectConverter.SupportsInlineObject
		=> this.inner is IJsonUnionObjectConverter { SupportsInlineObject: true };

	void IJsonUnionObjectConverter.WriteObjectProperties(ref JsonWriter writer, object value, SerializationContext context, bool first)
		=> this.GetObjectConverter().WriteObjectProperties(ref writer, value, context, first);

	ValueTask IJsonUnionObjectConverter.WriteObjectPropertiesAsync(JsonAsyncWriter writer, object value, SerializationContext context, bool first)
		=> this.GetObjectConverter().WriteObjectPropertiesAsync(writer, value, context, first);

	object? IJsonUnionObjectConverter.ReadObjectWithMetadata(ref JsonReader reader, SerializationContext context)
		=> this.GetObjectConverter().ReadObjectWithMetadata(ref reader, context);

	object? IJsonUnionObjectConverter.ReadObjectWithMetadataAfterDiscriminator(ref JsonReader reader, SerializationContext context)
		=> this.GetObjectConverter().ReadObjectWithMetadataAfterDiscriminator(ref reader, context);

	ValueTask<object?> IJsonUnionObjectConverter.ReadObjectWithMetadataAsync(JsonAsyncReader reader, SerializationContext context)
		=> this.GetObjectConverter().ReadObjectWithMetadataAsync(reader, context);

	ValueTask<object?> IJsonUnionObjectConverter.ReadObjectWithMetadataAfterDiscriminatorAsync(JsonAsyncReader reader, SerializationContext context)
		=> this.GetObjectConverter().ReadObjectWithMetadataAfterDiscriminatorAsync(reader, context);

	ValueTask<int> IJsonUnionObjectConverter.TryNavigateAfterDiscriminatorAsync(JsonAsyncReader reader, JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
		=> this.GetObjectConverter().TryNavigateAfterDiscriminatorAsync(reader, segment, options, context);

	public override void Write(ref JsonWriter writer, in TBase? value, SerializationContext context)
		=> this.inner.WriteObject(ref writer, value, context);

	public override TBase? Read(ref JsonReader reader, SerializationContext context)
		=> (TBase?)this.inner.ReadObject(ref reader, context);

	internal override bool TryNavigate(ref JsonReader reader, in JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
		=> this.inner.SkipToValue(ref reader, in segment, context);

	internal override ValueTask<int> TryNavigateAsync(JsonAsyncReader reader, JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
		=> this.inner.SkipToValueAsync(reader, segment, context);

	private IJsonUnionObjectConverter GetObjectConverter()
		=> this.inner is IJsonUnionObjectConverter { SupportsInlineObject: true } objectConverter
			? objectConverter
			: throw new InvalidOperationException("The union case converter does not support inline object metadata.");
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
}

internal sealed class JsonRuntimeUnionConverter<TBase> : JsonConverter<TBase>
{
	private readonly JsonConverter<TBase>? baseConverter;
	private readonly RuntimeUnionCaseEntry<TBase>[] cases;
	private readonly Dictionary<Type, int> byType;
	private readonly JsonUnionObjectEnvelope objectEnvelope;

	internal JsonRuntimeUnionConverter(JsonConverter<TBase>? baseConverter, RuntimeUnionCaseEntry<TBase>[] cases)
	{
		this.baseConverter = baseConverter;
		this.cases = cases;
		this.byType = new(cases.Length);
		this.objectEnvelope = new(baseConverter);
		for (int i = 0; i < cases.Length; i++)
		{
			this.objectEnvelope.Add(JsonUnionObjectEnvelope.GetAlias(cases[i].Tag, cases[i].Name), cases[i].Converter);
			this.byType[cases[i].CaseType] = i;
		}
	}

#pragma warning disable NBJson031 // JsonUnionObjectEnvelope.Write emits one complete JSON object.
	public override void Write(ref JsonWriter writer, in TBase? value, SerializationContext context)
	{
		if (!typeof(TBase).IsValueType && value is null)
		{
			writer.WriteNull();
			return;
		}

		context.DepthStep();

		if (value is not null && this.byType.TryGetValue(value.GetType(), out int objectIndex))
		{
			RuntimeUnionCaseEntry<TBase> entry = this.cases[objectIndex];
			JsonUnionObjectEnvelope.Write(ref writer, JsonUnionObjectEnvelope.GetAlias(entry.Tag, entry.Name), entry.Converter, value, context);
		}
		else
		{
			JsonUnionObjectEnvelope.Write(ref writer, JsonUnionObjectEnvelope.BaseAlias, this.baseConverter ?? throw new NotSupportedException($"No union case is registered for type '{value?.GetType().FullName}', and the base type '{typeof(TBase).FullName}' cannot be serialized directly."), value, context);
		}
	}
#pragma warning restore NBJson031

	public override TBase? Read(ref JsonReader reader, SerializationContext context)
	{
		if (!typeof(TBase).IsValueType && reader.TryReadNull())
		{
			return default;
		}

		context.DepthStep();

		JsonConverter objectConverter = this.objectEnvelope.ReadDiscriminator(ref reader, context.Cache.AllowOutOfOrderMetadataProperties, context, out bool discriminatorConsumed);
		return (TBase?)(objectConverter is IJsonUnionObjectConverter { SupportsInlineObject: true } inlineConverter
			? discriminatorConsumed
				? inlineConverter.ReadObjectWithMetadataAfterDiscriminator(ref reader, context)
				: inlineConverter.ReadObjectWithMetadata(ref reader, context)
			: JsonUnionObjectEnvelope.ReadValue(ref reader, objectConverter, context));
	}

	internal override bool TryNavigate(ref JsonReader reader, in JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
	{
		context.DepthStep();
		JsonConverter objectConverter = this.objectEnvelope.ReadDiscriminator(ref reader, context.Cache.AllowOutOfOrderMetadataProperties, context);
		if (objectConverter is not IJsonUnionObjectConverter { SupportsInlineObject: true })
		{
			JsonUnionObjectEnvelope.ReadValueStart(ref reader, context);
		}

		return objectConverter.SkipToValue(ref reader, in segment, context);
	}

	internal override async ValueTask<int> TryNavigateAsync(JsonAsyncReader reader, JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
	{
		context.DepthStep();
		(JsonConverter objectConverter, bool discriminatorConsumed) = await this.objectEnvelope.ReadDiscriminatorAsync(reader, context.Cache.AllowOutOfOrderMetadataProperties, context).ConfigureAwait(false);
		if (discriminatorConsumed)
		{
			int inlineInner = objectConverter is IJsonUnionObjectConverter { SupportsInlineObject: true } inlineConverter
				? await inlineConverter.TryNavigateAfterDiscriminatorAsync(reader, segment, options, context).ConfigureAwait(false)
				: await ReadValueAndNavigateAfterDiscriminatorAsync(reader, objectConverter, segment, context).ConfigureAwait(false);
			return inlineInner < 0 ? -1 : inlineInner + 1;
		}

		if (objectConverter is not IJsonUnionObjectConverter { SupportsInlineObject: true })
		{
			await JsonUnionObjectEnvelope.ReadValueStartAsync(reader, context).ConfigureAwait(false);
		}

		int objectInner = await objectConverter.SkipToValueAsync(reader, segment, context).ConfigureAwait(false);
		return objectInner < 0 ? -1 : objectInner + 1;
	}

	private static async ValueTask<int> ReadValueAndNavigateAfterDiscriminatorAsync(JsonAsyncReader reader, JsonConverter converter, JsonNavigationSegment segment, SerializationContext context)
	{
		await JsonUnionObjectEnvelope.ReadValueStartAsync(reader, context, discriminatorConsumed: true).ConfigureAwait(false);
		int inner = await converter.SkipToValueAsync(reader, segment, context).ConfigureAwait(false);
		return inner < 0 ? -1 : inner + 1;
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

	public override void Write(ref JsonWriter writer, in TBase? value, SerializationContext context)
	{
		if (!typeof(TBase).IsValueType && value is null)
		{
			writer.WriteNull();
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

		string raw = reader.ReadRawValue(context);
		byte[] rawUtf8 = Encoding.UTF8.GetBytes(raw);

		HashSet<string> presentNames = this.ScanPropertyNames(rawUtf8, context);

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

	private HashSet<string> ScanPropertyNames(byte[] rawUtf8, SerializationContext context)
	{
		context.DepthStep();
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
			reader.Skip(context);
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
	internal static JsonConverter<TBase> Build<TBase>(ConverterCache owner, ITypeShape<TBase> shape, DerivedTypeUnion entry, TypeShapeVisitor visitor)
	{
		StringComparer nameComparer = owner.PropertyNameComparer;
		ITypeShape baseTypeShape = shape is IUnionTypeShape unionForBase ? unionForBase.BaseType : shape;
		JsonConverter<TBase>? baseConverter = IsConstructible(baseTypeShape as IObjectTypeShape)
			? (JsonConverter<TBase>)baseTypeShape.Accept(visitor)!
			: null;

		if (entry.Disabled)
		{
			return baseConverter ?? throw new NotSupportedException($"Union handling for '{typeof(TBase).FullName}' was disabled, but the base type cannot be serialized directly.");
		}

		List<RuntimeUnionCaseEntry<TBase>> caseEntries = [];
		foreach (KeyValuePair<DerivedTypeIdentifier, ITypeShape> pair in GetCases(entry))
		{
			DerivedTypeIdentifier alias = pair.Key;
			ITypeShape caseShape = pair.Value;
			JsonConverter<TBase> caseConverter = caseShape.Type == typeof(TBase) && baseConverter is not null
				? baseConverter
				: new JsonRuntimeUnionCaseConverter<TBase>(owner.GetOrAddConverter(caseShape));
			IReadOnlyCollection<string> required = RuntimeUnionHelpers.GetRequiredSerializedPropertyNames(caseShape, owner);
			caseEntries.Add(new(caseShape.Type, alias.Type == DerivedTypeIdentifier.AliasType.Integer ? alias.IntAlias : null, alias.Type == DerivedTypeIdentifier.AliasType.String ? alias.StringAlias : null, caseConverter, required));
		}

		if (caseEntries.Count == 0)
		{
			throw new NotSupportedException($"The union configuration for '{typeof(TBase).FullName}' defines no cases.");
		}

		VerifyUniqueAliases(caseEntries);

		return entry is DerivedTypeDuckTyping
			? new JsonDuckTypingUnionConverter<TBase>([.. caseEntries], nameComparer, owner.AllowTrailingCommas, owner.ReadCommentHandling)
			: new JsonRuntimeUnionConverter<TBase>(baseConverter, [.. caseEntries]);
	}

	internal static IEnumerable<KeyValuePair<DerivedTypeIdentifier, ITypeShape>> GetCases(DerivedTypeUnion entry)
	{
		if (entry is IDerivedTypeMapping mapping)
		{
			return mapping.GetDerivedTypesMapping();
		}

		if (entry is DerivedTypeDuckTyping duck)
		{
			List<KeyValuePair<DerivedTypeIdentifier, ITypeShape>> cases = [];
			if (!duck.BaseType.IsAbstract && !duck.BaseType.IsInterface)
			{
				cases.Add(new(duck.BaseType.FullName!, duck.BaseShape));
			}

			foreach (ITypeShape caseShape in duck.DerivedShapes.ToArray())
			{
				cases.Add(new(caseShape.Type.FullName!, caseShape));
			}

			return cases;
		}

		throw new NotSupportedException($"Unsupported derived type union '{entry.GetType().FullName}'.");
	}

	/// <summary>Determines whether an object shape can be deserialized directly.</summary>
	/// <param name="objectShape">The object shape to inspect.</param>
	/// <returns><see langword="true"/> if the type has a parameterized or default constructor; otherwise, <see langword="false"/>.</returns>
	internal static bool IsConstructible(IObjectTypeShape? objectShape)
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
	/// <summary>Gets required object properties using the same metadata as the object converter.</summary>
	/// <param name="shape">The case shape, possibly including generated union metadata.</param>
	/// <returns>The required properties of the case's bare object contract.</returns>
	internal static IEnumerable<IPropertyShape> GetRequiredProperties(ITypeShape shape)
	{
		ITypeShape bareShape = shape is IUnionTypeShape unionShape ? unionShape.BaseType : shape;
		if (bareShape is not IObjectTypeShape objectShape)
		{
			yield break;
		}

		foreach (IPropertyShape property in objectShape.Properties)
		{
			IParameterShape? parameter = GetMatchingConstructorParameter(objectShape, property);
			bool canReadProperty = property.HasSetter || parameter is not null
				|| (property.HasGetter && property.PropertyType is IEnumerableTypeShape { ConstructionStrategy: CollectionConstructionStrategy.Mutable } or IDictionaryTypeShape { ConstructionStrategy: CollectionConstructionStrategy.Mutable });
			if (canReadProperty && property.PropertyType.Type != typeof(UnusedDataPacket) && IsRequiredProperty(property, parameter))
			{
				yield return property;
			}
		}
	}

	/// <summary>Resolves required property names using the serializer's naming and comparison policies.</summary>
	/// <param name="shape">The case shape.</param>
	/// <param name="owner">The converter cache with the effective serializer configuration.</param>
	/// <returns>The required serialized property names.</returns>
	internal static IReadOnlyCollection<string> GetRequiredSerializedPropertyNames(ITypeShape shape, ConverterCache owner)
	{
		HashSet<string> required = new(owner.PropertyNameComparer);
		foreach (IPropertyShape property in GetRequiredProperties(shape))
		{
			required.Add(owner.GetSerializedPropertyName(property.Name, property.AttributeProvider));
		}

		return required;
	}

	/// <summary>Matches constructor metadata to a property, preferring an exact name over a unique case-insensitive match.</summary>
	/// <param name="objectShape">The declaring object shape.</param>
	/// <param name="property">The property shape.</param>
	/// <returns>The matching parameter, if any.</returns>
	internal static IParameterShape? GetMatchingConstructorParameter(IObjectTypeShape objectShape, IPropertyShape property)
	{
		IParameterShape? match = objectShape.Constructor?.Parameters.FirstOrDefault(p => p.Name == property.Name);
		if (match is null && objectShape.Constructor is { } constructor)
		{
			IParameterShape[] candidates = constructor.Parameters.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Name, property.Name)).ToArray();
			if (candidates.Length == 1)
			{
				match = candidates[0];
			}
		}

		return match;
	}

	/// <summary>Gets the effective required flag, including constructor and property shape overrides.</summary>
	/// <param name="property">The property shape.</param>
	/// <param name="constructorParameter">The matching constructor parameter, if any.</param>
	/// <returns>Whether the object converter requires the property.</returns>
	internal static bool IsRequiredProperty(IPropertyShape property, IParameterShape? constructorParameter)
		=> constructorParameter?.IsRequired
			?? property.AttributeProvider?.GetCustomAttributes<PropertyShapeAttribute>(inherit: true).FirstOrDefault()?.IsRequired
			?? HasRequiredMemberAttribute(property.MemberInfo);

	/// <summary>Checks required-member metadata without discovering types dynamically.</summary>
	/// <param name="memberInfo">The member metadata, if available.</param>
	/// <returns>Whether the member declares the required-member attribute.</returns>
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
