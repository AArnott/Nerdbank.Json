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

internal sealed class JsonRuntimeUnionCaseConverter<TBase> : JsonConverter<TBase>, IJsonUnionObjectConverter, IUnionDiscriminatorSource
{
	private readonly JsonConverter inner;

	internal JsonRuntimeUnionCaseConverter(JsonConverter converter)
	{
		this.inner = converter is IJsonReferencePreservingWrapper wrapper ? wrapper.InnerConverter : converter;
	}

	bool IJsonUnionObjectConverter.SupportsInlineObject
		=> this.inner is IJsonUnionObjectConverter { SupportsInlineObject: true };

	UnionDiscriminatorBinding? IUnionDiscriminatorSource.Discriminator
		=> UnionDiscriminatorBinding.FromConverter(this.inner);

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
	internal RuntimeUnionCaseEntry(Type caseType, int? tag, string? name, JsonConverter<TBase> converter, HashSet<string> requiredPropertyNames)
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

	internal HashSet<string> RequiredPropertyNames { get; }
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

		JsonConverter objectConverter = this.objectEnvelope.ReadDiscriminator(ref reader, context.Cache.AllowOutOfOrderDiscriminator, context, out bool discriminatorConsumed);
		return (TBase?)(objectConverter is IJsonUnionObjectConverter { SupportsInlineObject: true } inlineConverter
			? discriminatorConsumed
				? inlineConverter.ReadObjectWithMetadataAfterDiscriminator(ref reader, context)
				: inlineConverter.ReadObjectWithMetadata(ref reader, context)
			: JsonUnionObjectEnvelope.ReadValue(ref reader, objectConverter, context));
	}

	internal override bool TryNavigate(ref JsonReader reader, in JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
	{
		context.DepthStep();
		JsonConverter objectConverter = this.objectEnvelope.ReadDiscriminator(ref reader, context.Cache.AllowOutOfOrderDiscriminator, context);
		if (objectConverter is not IJsonUnionObjectConverter { SupportsInlineObject: true })
		{
			JsonUnionObjectEnvelope.ReadValueStart(ref reader, context);
		}

		return objectConverter.SkipToValue(ref reader, in segment, context);
	}

	internal override async ValueTask<int> TryNavigateAsync(JsonAsyncReader reader, JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
	{
		context.DepthStep();
		(JsonConverter objectConverter, bool discriminatorConsumed) = await this.objectEnvelope.ReadDiscriminatorAsync(reader, context.Cache.AllowOutOfOrderDiscriminator, context).ConfigureAwait(false);
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
	private readonly bool ignoreCase;
	private readonly int catchAllIndex;
	private readonly string[] evidenceNames;
	private readonly byte[][] evidenceUtf8;
	private readonly int wordCount;
	private readonly ulong[] caseMasks;

	internal JsonDuckTypingUnionConverter(RuntimeUnionCaseEntry<TBase>[] cases, StringComparer nameComparer)
	{
		this.cases = cases;
		this.nameComparer = nameComparer;
		this.ignoreCase = nameComparer.Equals("A", "a");
		this.byType = new(cases.Length);
		this.catchAllIndex = -1;
		(Type CaseType, HashSet<string> Required)[] signatures = new (Type, HashSet<string>)[cases.Length];
		Dictionary<string, int> evidenceIndex = new(nameComparer);
		List<string> names = [];
		for (int i = 0; i < cases.Length; i++)
		{
			this.byType[cases[i].CaseType] = i;
			HashSet<string> required = cases[i].RequiredPropertyNames;
			signatures[i] = (cases[i].CaseType, required);
			if (required.Count == 0)
			{
				this.catchAllIndex = i;
			}

			foreach (string name in required)
			{
				if (!evidenceIndex.ContainsKey(name))
				{
					evidenceIndex.Add(name, names.Count);
					names.Add(name);
				}
			}
		}

		RuntimeUnionHelpers.ValidateDuckTypingSignatures(typeof(TBase).FullName ?? typeof(TBase).Name, signatures);
		this.evidenceNames = names.ToArray();
		this.evidenceUtf8 = new byte[names.Count][];
		for (int i = 0; i < names.Count; i++)
		{
			this.evidenceUtf8[i] = Encoding.UTF8.GetBytes(names[i]);
		}

		this.wordCount = names.Count == 0 ? 0 : ((names.Count - 1) / 64) + 1;
		this.caseMasks = new ulong[cases.Length * Math.Max(this.wordCount, 1)];
		if (this.wordCount > 0)
		{
			for (int i = 0; i < cases.Length; i++)
			{
				foreach (string name in cases[i].RequiredPropertyNames)
				{
					int bit = evidenceIndex[name];
					this.caseMasks[(i * this.wordCount) + (bit / 64)] |= 1UL << (bit % 64);
				}
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

		JsonReader peek = reader.CreatePeekReader();
		Span<ulong> present = this.wordCount <= 4 ? stackalloc ulong[4] : new ulong[this.wordCount];
		present = present[..this.wordCount];
		present.Clear();
		this.ScanEvidence(ref peek, context, present);
		int match = this.SelectCase(present);
		return this.cases[match].Converter.Read(ref reader, context);
	}

	private int SelectCase(scoped ReadOnlySpan<ulong> present)
	{
		int matchCount = 0;
		Span<int> matches = this.cases.Length <= 16 ? stackalloc int[16] : new int[this.cases.Length];
		matches = matches[..this.cases.Length];
		for (int i = 0; i < this.cases.Length; i++)
		{
			if (i == this.catchAllIndex || !this.Covers(present, i))
			{
				continue;
			}

			matches[matchCount++] = i;
		}

		if (matchCount == 0)
		{
			return this.catchAllIndex >= 0
				? this.catchAllIndex
				: throw new JsonSerializationException($"The JSON object provides insufficient evidence to select a duck-typing union case for '{typeof(TBase).FullName}'; no configured case had all its required properties present.")
				{
					Code = JsonSerializationException.ErrorCode.NoMatchingUnionCase,
				};
		}

		if (matchCount == 1)
		{
			return matches[0];
		}

		for (int i = 0; i < matchCount; i++)
		{
			bool strictOverAll = true;
			for (int j = 0; j < matchCount; j++)
			{
				if (i != j && !this.IsStrictSuperset(matches[i], matches[j]))
				{
					strictOverAll = false;
					break;
				}
			}

			if (strictOverAll)
			{
				return matches[i];
			}
		}

		StringBuilder builder = new();
		builder.Append("The JSON object is ambiguous for duck-typing union '");
		builder.Append(typeof(TBase).FullName);
		builder.Append("': it matches ");
		for (int i = 0; i < matchCount; i++)
		{
			if (i > 0)
			{
				builder.Append(i == matchCount - 1 ? " and " : ", ");
			}

			builder.Append('\'');
			builder.Append(this.cases[matches[i]].CaseType.FullName);
			builder.Append('\'');
		}

		builder.Append('.');
		throw new JsonSerializationException(builder.ToString())
		{
			Code = JsonSerializationException.ErrorCode.AmbiguousUnionCase,
		};
	}

	private bool Covers(scoped ReadOnlySpan<ulong> present, int caseIndex)
	{
		int offset = caseIndex * this.wordCount;
		for (int word = 0; word < this.wordCount; word++)
		{
			ulong required = this.caseMasks[offset + word];
			if ((present[word] & required) != required)
			{
				return false;
			}
		}

		return true;
	}

	private bool IsStrictSuperset(int candidate, int other)
	{
		bool extra = false;
		int candidateOffset = candidate * this.wordCount;
		int otherOffset = other * this.wordCount;
		for (int word = 0; word < this.wordCount; word++)
		{
			ulong left = this.caseMasks[candidateOffset + word];
			ulong right = this.caseMasks[otherOffset + word];
			if ((left & right) != right)
			{
				return false;
			}

			if (left != right)
			{
				extra = true;
			}
		}

		return extra;
	}

	private void ScanEvidence(ref JsonReader reader, SerializationContext context, scoped Span<ulong> present)
	{
		context.DepthStep();
		int maxMembers = context.Security.MaxObjectMemberCount;
		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			return;
		}

		int count = 0;
		while (true)
		{
			if (count++ >= maxMembers)
			{
				throw new FormatException($"The JSON object exceeds the configured maximum of {maxMembers} members.");
			}

			int evidence = this.ReadEvidenceIndex(ref reader);
			if (evidence >= 0)
			{
				present[evidence / 64] |= 1UL << (evidence % 64);
			}

			reader.ReadNameSeparator();
			reader.Skip(context);
			if (reader.TryReadEndObject())
			{
				return;
			}

			reader.ReadValueSeparator();
		}
	}

	private int ReadEvidenceIndex(ref JsonReader reader)
	{
		if (reader.TryReadUnescapedUtf8StringToken(out ReadOnlySpan<byte> token, out _, out _))
		{
			if (!token.IsEmpty)
			{
				return this.FindEvidence(token[1..^1]);
			}
		}

		return this.FindEvidence(reader.ReadRequiredString());
	}

	private int FindEvidence(ReadOnlySpan<byte> utf8Name)
	{
		if (this.ignoreCase && ContainsNonAscii(utf8Name))
		{
			return this.FindEvidence(Encoding.UTF8.GetString(utf8Name));
		}

		for (int i = 0; i < this.evidenceUtf8.Length; i++)
		{
			if (NamesEqual(utf8Name, this.evidenceUtf8[i], this.ignoreCase))
			{
				return i;
			}
		}

		return -1;
	}

	private int FindEvidence(string name)
	{
		for (int i = 0; i < this.evidenceNames.Length; i++)
		{
			if (this.nameComparer.Equals(name, this.evidenceNames[i]))
			{
				return i;
			}
		}

		return -1;
	}

	private static bool ContainsNonAscii(ReadOnlySpan<byte> utf8Name)
	{
		foreach (byte value in utf8Name)
		{
			if (value >= 0x80)
			{
				return true;
			}
		}

		return false;
	}

	private static bool NamesEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, bool ignoreCase)
	{
		if (left.Length != right.Length)
		{
			return false;
		}

		if (!ignoreCase)
		{
			return left.SequenceEqual(right);
		}

		for (int i = 0; i < left.Length; i++)
		{
			byte x = left[i];
			byte y = right[i];
			if (x >= (byte)'A' && x <= (byte)'Z')
			{
				x += 32;
			}

			if (y >= (byte)'A' && y <= (byte)'Z')
			{
				y += 32;
			}

			if (x != y)
			{
				return false;
			}
		}

		return true;
	}
}

internal static class RuntimeUnionBuilder
{
	internal static JsonConverter<TBase> Build<TBase>(ConverterCache owner, ITypeShape<TBase> shape, DerivedTypeUnion entry, TypeShapeVisitor visitor)
	{
		StringComparer nameComparer = owner.PropertyNameComparer;
		ITypeShape baseTypeShape = shape is IUnionTypeShape unionForBase ? unionForBase.BaseType : shape;
		JsonConverter<TBase>? baseConverter = IsConstructible(baseTypeShape as IObjectTypeShape)
			? (JsonConverter<TBase>)baseTypeShape.Accept(visitor, JsonStandardVisitor.RuntimeUnionBaseShape)!
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
			HashSet<string> required = RuntimeUnionHelpers.GetRequiredSerializedPropertyNames(caseShape, owner);
			caseEntries.Add(new(caseShape.Type, alias.Type == DerivedTypeIdentifier.AliasType.Integer ? alias.IntAlias : null, alias.Type == DerivedTypeIdentifier.AliasType.String ? alias.StringAlias : null, caseConverter, required));
		}

		if (caseEntries.Count == 0)
		{
			throw new NotSupportedException($"The union configuration for '{typeof(TBase).FullName}' defines no cases.");
		}

		VerifyUniqueAliases(caseEntries);
		UnionDiscriminatorBinding.ValidateUnion(caseEntries.Select(static entry => (JsonConverter)entry.Converter), nameComparer);

		return entry is DerivedTypeDuckTyping
			? new JsonDuckTypingUnionConverter<TBase>([.. caseEntries], nameComparer)
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
	/// <returns>The required properties of the case's bare object contract that can be read back.</returns>
	internal static IEnumerable<IPropertyShape> GetRequiredProperties(ITypeShape shape)
	{
		IObjectTypeShape? objectShape = GetObjectShape(shape);
		if (objectShape is null)
		{
			yield break;
		}

		foreach (IPropertyShape property in objectShape.Properties)
		{
			IParameterShape? parameter = GetMatchingConstructorParameter(objectShape, property);
			if (CanReadProperty(property, parameter) && property.PropertyType.Type != typeof(UnusedDataPacket) && IsRequiredProperty(property, parameter))
			{
				yield return property;
			}
		}
	}

	/// <summary>Reports whether the case is an object contract.</summary>
	/// <param name="shape">The case shape.</param>
	/// <returns><see langword="true"/> when the bare contract is an object.</returns>
	internal static bool IsObjectShape(ITypeShape shape) => GetObjectShape(shape) is not null;

	/// <summary>Reports a required member that cannot contribute evidence because it cannot be read.</summary>
	/// <param name="shape">The case shape.</param>
	/// <returns><see langword="true"/> when such a member exists.</returns>
	internal static bool HasRequiredPropertyThatCannotBeRead(ITypeShape shape)
	{
		IObjectTypeShape? objectShape = GetObjectShape(shape);
		if (objectShape is null)
		{
			return false;
		}

		foreach (IPropertyShape property in objectShape.Properties)
		{
			IParameterShape? parameter = GetMatchingConstructorParameter(objectShape, property);
			if (property.PropertyType.Type == typeof(UnusedDataPacket) || !IsRequiredProperty(property, parameter))
			{
				continue;
			}

			if (!CanReadProperty(property, parameter))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>Resolves required property names using the serializer's naming and comparison policies.</summary>
	/// <param name="shape">The case shape.</param>
	/// <param name="owner">The converter cache with the effective serializer configuration.</param>
	/// <returns>The required serialized property names.</returns>
	internal static HashSet<string> GetRequiredSerializedPropertyNames(ITypeShape shape, ConverterCache owner)
	{
		HashSet<string> required = new(owner.PropertyNameComparer);
		foreach (IPropertyShape property in GetRequiredProperties(shape))
		{
			required.Add(owner.GetSerializedPropertyName(property.Name, property.AttributeProvider));
		}

		return required;
	}

	/// <summary>Rejects duck-typing unions whose required names cannot choose a case.</summary>
	/// <param name="unionTypeName">The closed union type name, for the error message.</param>
	/// <param name="cases">Each case and its required serialized names.</param>
	/// <exception cref="NotSupportedException">More than one case has no required names, or two cases require the same names.</exception>
	internal static void ValidateDuckTypingSignatures(string unionTypeName, IReadOnlyList<(Type CaseType, HashSet<string> Required)> cases)
	{
		int catchAll = -1;
		for (int i = 0; i < cases.Count; i++)
		{
			if (cases[i].Required.Count != 0)
			{
				continue;
			}

			if (catchAll >= 0)
			{
				throw new NotSupportedException($"Duck-typing union '{unionTypeName}' can include only one case with no required properties. Both '{cases[catchAll].CaseType.FullName}' and '{cases[i].CaseType.FullName}' qualify.");
			}

			catchAll = i;
		}

		for (int i = 0; i < cases.Count; i++)
		{
			HashSet<string> required = cases[i].Required;
			if (required.Count == 0)
			{
				continue;
			}

			for (int j = i + 1; j < cases.Count; j++)
			{
				HashSet<string> other = cases[j].Required;
				if (other.Count > 0 && required.SetEquals(other))
				{
					string names = string.Join(", ", required.OrderBy(static name => name, StringComparer.Ordinal));
					throw new NotSupportedException($"Duck-typing union '{unionTypeName}' cannot distinguish '{cases[i].CaseType.FullName}' from '{cases[j].CaseType.FullName}' because they require the same properties: {names}.");
				}
			}
		}
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

	private static IObjectTypeShape? GetObjectShape(ITypeShape shape)
	{
		ITypeShape bareShape = shape is IUnionTypeShape unionShape ? unionShape.BaseType : shape;
		return bareShape as IObjectTypeShape;
	}

	private static bool CanReadProperty(IPropertyShape property, IParameterShape? parameter)
		=> property.HasSetter || parameter is not null
			|| (property.HasGetter && property.PropertyType is IEnumerableTypeShape { ConstructionStrategy: CollectionConstructionStrategy.Mutable } or IDictionaryTypeShape { ConstructionStrategy: CollectionConstructionStrategy.Mutable });
}
