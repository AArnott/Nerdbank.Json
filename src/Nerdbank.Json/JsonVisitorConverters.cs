// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1649 // File name should not need to match the first type in this multi-type helper file.

using System.Globalization;

namespace Nerdbank.Json;

internal readonly struct JsonUnionCaseMetadata<TUnion>
{
	private readonly string? stringAlias;
	private readonly int intAlias;
	private readonly bool usesIntegerAlias;

	private JsonUnionCaseMetadata(string alias, JsonConverter converter, JsonCSharpUnionCaseRepresentation representation, bool isNullable)
	{
		this.stringAlias = alias;
		this.intAlias = default;
		this.usesIntegerAlias = false;
		this.Converter = converter;
		this.Representation = representation;
		this.IsNullable = isNullable;
	}

	private JsonUnionCaseMetadata(int alias, JsonConverter converter, JsonCSharpUnionCaseRepresentation representation, bool isNullable)
	{
		this.stringAlias = null;
		this.intAlias = alias;
		this.usesIntegerAlias = true;
		this.Converter = converter;
		this.Representation = representation;
		this.IsNullable = isNullable;
	}

	internal JsonConverter Converter { get; }

	internal JsonCSharpUnionCaseRepresentation Representation { get; }

	internal bool IsNullable { get; }

	internal string ObjectAlias => JsonUnionObjectEnvelope.GetAlias(this.usesIntegerAlias ? this.intAlias : null, this.stringAlias);

	internal static JsonUnionCaseMetadata<TUnion> Create(string alias, JsonConverter converter, JsonCSharpUnionCaseRepresentation representation = JsonCSharpUnionCaseRepresentation.Tagged, bool isNullable = false) => new(alias, converter, representation, isNullable);

	internal static JsonUnionCaseMetadata<TUnion> Create(int alias, JsonConverter converter, JsonCSharpUnionCaseRepresentation representation = JsonCSharpUnionCaseRepresentation.Tagged, bool isNullable = false) => new(alias, converter, representation, isNullable);

	internal void WriteAlias(ref JsonWriter writer)
	{
		if (this.usesIntegerAlias)
		{
			writer.Write(this.intAlias);
		}
		else
		{
			writer.Write(this.stringAlias);
		}
	}
}

internal sealed class JsonOptionalConverter<TOptional, TElement> : JsonConverter<TOptional>
{
	private readonly JsonConverter<TElement> elementConverter;
	private readonly OptionDeconstructor<TOptional, TElement> deconstructor;
	private readonly Func<TOptional> createNone;
	private readonly Func<TElement, TOptional> createSome;

	internal JsonOptionalConverter(JsonConverter<TElement> elementConverter, OptionDeconstructor<TOptional, TElement> deconstructor, Func<TOptional> createNone, Func<TElement, TOptional> createSome)
	{
		this.elementConverter = elementConverter;
		this.deconstructor = deconstructor;
		this.createNone = createNone;
		this.createSome = createSome;
	}

	public override void Write(ref JsonWriter writer, in TOptional? value, SerializationContext context)
	{
		if (!this.deconstructor(value, out TElement? element))
		{
			writer.WriteNull();
			return;
		}

		this.elementConverter.Write(ref writer, element, context);
	}

	public override TOptional? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return this.createNone();
		}

		return this.createSome(this.elementConverter.Read(ref reader, context)!);
	}
}

internal sealed class JsonEnumConverter<TEnum, TUnderlying> : JsonConverter<TEnum>
	where TEnum : struct
	where TUnderlying : struct
{
	private readonly Dictionary<string, TUnderlying>? valuesByName;
	private readonly Dictionary<TUnderlying, string>? namesByValue;
	private readonly JsonConverter<TUnderlying> underlyingConverter;

	internal JsonEnumConverter(JsonConverter<TUnderlying> underlyingConverter, IReadOnlyDictionary<string, TUnderlying> members, bool serializeByName, JsonNamingPolicy? namingPolicy)
	{
		this.underlyingConverter = underlyingConverter;
		if (serializeByName)
		{
			(this.valuesByName, this.namesByValue) = CreateNameMaps(members, namingPolicy);
		}
	}

	public override void Write(ref JsonWriter writer, in TEnum value, SerializationContext context)
	{
		var underlyingValue = (TUnderlying)(object)value;
		if (this.namesByValue?.TryGetValue(underlyingValue, out string? name) == true)
		{
			writer.Write(name);
			return;
		}

		this.underlyingConverter.Write(ref writer, underlyingValue, context);
	}

	public override TEnum Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.PeekValueToken() == '"')
		{
			string name = reader.ReadRequiredString();
			if (this.valuesByName?.TryGetValue(name, out TUnderlying value) == true)
			{
				return (TEnum)(object)value;
			}

			throw new FormatException($"Unrecognized enum value name '{name}'.");
		}

		return (TEnum)(object)this.underlyingConverter.Read(ref reader, context)!;
	}

	private static (Dictionary<string, TUnderlying> ValuesByName, Dictionary<TUnderlying, string> NamesByValue) CreateNameMaps(IReadOnlyDictionary<string, TUnderlying> members, JsonNamingPolicy? namingPolicy)
	{
		Dictionary<string, TUnderlying> valuesByName = new(StringComparer.OrdinalIgnoreCase);
		Dictionary<TUnderlying, string> namesByValue = [];

		if (!TryPopulate(valuesByName, namesByValue))
		{
			valuesByName = new(StringComparer.Ordinal);
			namesByValue = [];
			if (!TryPopulate(valuesByName, namesByValue))
			{
				throw new InvalidOperationException($"Failed to build enum name map for {typeof(TEnum).FullName}.");
			}
		}

		return (valuesByName, namesByValue);

		bool TryPopulate(Dictionary<string, TUnderlying> nameMap, Dictionary<TUnderlying, string> reverseMap)
		{
			foreach (KeyValuePair<string, TUnderlying> pair in members)
			{
				string name = namingPolicy?.ConvertName(pair.Key) ?? pair.Key;
				if (nameMap.ContainsKey(name))
				{
					if (!EqualityComparer<TUnderlying>.Default.Equals(nameMap[name], pair.Value))
					{
						return false;
					}
				}
				else
				{
					nameMap.Add(name, pair.Value);
				}

				if (!reverseMap.ContainsKey(pair.Value))
				{
					reverseMap.Add(pair.Value, name);
				}
			}

			return true;
		}
	}
}

internal sealed class JsonSurrogateConverter<T, TSurrogate> : JsonConverter<T>
{
	private readonly ISurrogateTypeShape<T, TSurrogate> shape;
	private readonly JsonConverter<TSurrogate> surrogateConverter;

	internal JsonSurrogateConverter(ISurrogateTypeShape<T, TSurrogate> shape, JsonConverter<TSurrogate> surrogateConverter)
	{
		this.shape = shape;
		this.surrogateConverter = surrogateConverter;
	}

	public override void Write(ref JsonWriter writer, in T? value, SerializationContext context)
		=> this.surrogateConverter.Write(ref writer, this.shape.Marshaler.Marshal(value), context);

	public override T? Read(ref JsonReader reader, SerializationContext context)
		=> this.shape.Marshaler.Unmarshal(this.surrogateConverter.Read(ref reader, context));
}

internal sealed class JsonUnionConverter<TUnion> : JsonConverter<TUnion>
{
	private readonly JsonConverter<TUnion>? baseConverter;
	private readonly Getter<TUnion, int> getUnionCaseIndex;
	private readonly JsonUnionCaseMetadata<TUnion>[] serializers;
	private readonly Dictionary<JsonCSharpUnionCaseRepresentation, JsonConverter>? nativeDeserializers;
	private readonly JsonConverter? nullDeserializer;
	private readonly bool isCSharpUnion;
	private readonly JsonUnionObjectEnvelope? objectEnvelope;
	private readonly bool tagObjectCases;

	internal JsonUnionConverter(JsonConverter<TUnion>? baseConverter, Getter<TUnion, int> getUnionCaseIndex, JsonUnionCaseMetadata<TUnion>[] serializers, bool isCSharpUnion)
	{
		this.baseConverter = baseConverter;
		this.getUnionCaseIndex = getUnionCaseIndex;
		this.serializers = serializers;
		this.isCSharpUnion = isCSharpUnion;
		bool useObjectEnvelope = !isCSharpUnion;
		if (isCSharpUnion)
		{
			JsonCSharpUnionFormat format = new(serializers.Select(static entry => entry.Representation).ToArray());
			this.tagObjectCases = format.TagObjectCases;
			this.nativeDeserializers = new();
			foreach (JsonUnionCaseMetadata<TUnion> entry in serializers)
			{
				if (entry.Representation == JsonCSharpUnionCaseRepresentation.Tagged ||
					(this.tagObjectCases && entry.Representation == JsonCSharpUnionCaseRepresentation.Object))
				{
					continue;
				}

				this.nativeDeserializers.Add(entry.Representation, entry.Converter);
				if (entry.IsNullable && this.nullDeserializer is null)
				{
					this.nullDeserializer = entry.Converter;
				}
			}

			useObjectEnvelope = serializers.Any(static entry => entry.Representation == JsonCSharpUnionCaseRepresentation.Tagged) || this.tagObjectCases;
		}

		if (useObjectEnvelope)
		{
			this.objectEnvelope = new(baseConverter);
			foreach (JsonUnionCaseMetadata<TUnion> entry in serializers)
			{
				if (!isCSharpUnion ||
					entry.Representation == JsonCSharpUnionCaseRepresentation.Tagged ||
					(this.tagObjectCases && entry.Representation == JsonCSharpUnionCaseRepresentation.Object))
				{
					this.objectEnvelope.Add(entry.ObjectAlias, entry.Converter);
				}
			}
		}
	}

	public override bool PreferAsyncSerialization => true;

	public override void Write(ref JsonWriter writer, in TUnion? value, SerializationContext context)
	{
		if (!typeof(TUnion).IsValueType && value is null)
		{
			writer.WriteNull();
			return;
		}

		context.DepthStep();

		if (this.isCSharpUnion && value is not null && this.TryGetSerializer(value, out JsonUnionCaseMetadata<TUnion> nativeCase) && this.IsNativeCase(nativeCase))
		{
			nativeCase.Converter.WriteObject(ref writer, value, context);
			return;
		}

		if (this.objectEnvelope is not null)
		{
			JsonConverter objectConverter;
			string alias;
			if (value is not null && this.TryGetSerializer(value, out JsonUnionCaseMetadata<TUnion> objectCase))
			{
				alias = objectCase.ObjectAlias;
				objectConverter = objectCase.Converter;
			}
			else
			{
				alias = JsonUnionObjectEnvelope.BaseAlias;
				objectConverter = this.baseConverter ?? throw new NotSupportedException($"The base union type '{typeof(TUnion).FullName}' cannot be serialized directly.");
			}

			JsonUnionObjectEnvelope.Write(ref writer, alias, objectConverter, value, context);
			return;
		}

		throw new NotSupportedException($"No union case is registered for type '{value?.GetType().FullName}', and the base type '{typeof(TUnion).FullName}' cannot be serialized directly.");
	}

	public override async ValueTask WriteAsync(JsonAsyncWriter writer, TUnion? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (!typeof(TUnion).IsValueType && value is null)
		{
			await writer.WriteNullAsync(context).ConfigureAwait(false);
			return;
		}

		context.DepthStep();

		JsonWriter syncWriter = writer.CreateWriter();
		JsonConverter converter;
		if (this.isCSharpUnion && value is not null && this.TryGetSerializer(value, out JsonUnionCaseMetadata<TUnion> nativeCase) && this.IsNativeCase(nativeCase))
		{
			writer.ReturnWriter(ref syncWriter);
			await nativeCase.Converter.WriteObjectAsync(writer, value, context).ConfigureAwait(false);
			return;
		}

		if (this.objectEnvelope is not null)
		{
			string alias;
			if (value is not null && this.TryGetSerializer(value, out JsonUnionCaseMetadata<TUnion> unionCase))
			{
				alias = unionCase.ObjectAlias;
				converter = unionCase.Converter;
			}
			else
			{
				alias = JsonUnionObjectEnvelope.BaseAlias;
				converter = this.baseConverter ?? throw new NotSupportedException($"The base union type '{typeof(TUnion).FullName}' cannot be serialized directly.");
			}

			writer.ReturnWriter(ref syncWriter);
			await JsonUnionObjectEnvelope.WriteAsync(writer, alias, converter, value, context).ConfigureAwait(false);
			return;
		}

		throw new NotSupportedException($"No union case is registered for type '{value?.GetType().FullName}', and the base type '{typeof(TUnion).FullName}' cannot be serialized directly.");
	}

	public override TUnion? Read(ref JsonReader reader, SerializationContext context)
	{
		if (!typeof(TUnion).IsValueType && reader.TryReadNull())
		{
			return default;
		}

		context.DepthStep();

		if (this.isCSharpUnion && this.TryGetNativeDeserializer(reader.PeekValueToken(), out JsonConverter? nativeConverter))
		{
			return (TUnion?)nativeConverter.ReadObject(ref reader, context);
		}

		JsonUnionObjectEnvelope objectEnvelope = this.objectEnvelope
			?? throw new FormatException($"No tagged cases are registered for union '{typeof(TUnion).FullName}'.");
		JsonConverter objectConverter = objectEnvelope.ReadDiscriminator(ref reader, context.Cache.AllowOutOfOrderMetadataProperties, context, out bool discriminatorConsumed);
		return (TUnion?)(objectConverter is IJsonUnionObjectConverter { SupportsInlineObject: true } inlineConverter
			? discriminatorConsumed
				? inlineConverter.ReadObjectWithMetadataAfterDiscriminator(ref reader, context)
				: inlineConverter.ReadObjectWithMetadata(ref reader, context)
			: JsonUnionObjectEnvelope.ReadValue(ref reader, objectConverter, context));
	}

	public override async ValueTask<TUnion?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
	{
		Requires.NotNull(reader);
		if (!typeof(TUnion).IsValueType && await reader.TryReadNullAsync(context).ConfigureAwait(false))
		{
			return default;
		}

		context.DepthStep();

		if (this.isCSharpUnion && this.TryGetNativeDeserializer((char)await reader.PeekNextByteAsync().ConfigureAwait(false), out JsonConverter? nativeConverter))
		{
			return (TUnion?)await nativeConverter.ReadObjectAsync(reader, context).ConfigureAwait(false);
		}

		(JsonConverter objectConverter, bool discriminatorConsumed) = await (this.objectEnvelope?.ReadDiscriminatorAsync(reader, context.Cache.AllowOutOfOrderMetadataProperties, context)
			?? throw new FormatException($"No tagged cases are registered for union '{typeof(TUnion).FullName}'.")).ConfigureAwait(false);
		return (TUnion?)(objectConverter is IJsonUnionObjectConverter { SupportsInlineObject: true } inlineConverter
			? discriminatorConsumed
				? await inlineConverter.ReadObjectWithMetadataAfterDiscriminatorAsync(reader, context).ConfigureAwait(false)
				: await inlineConverter.ReadObjectWithMetadataAsync(reader, context).ConfigureAwait(false)
			: await JsonUnionObjectEnvelope.ReadValueAsync(reader, objectConverter, context, discriminatorConsumed).ConfigureAwait(false));
	}

	internal override bool TryNavigate(ref JsonReader reader, in JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
	{
		if (this.isCSharpUnion && this.TryGetNativeDeserializer(reader.PeekValueToken(), out JsonConverter? nativeConverter))
		{
			context.DepthStep();
			return nativeConverter.SkipToValue(ref reader, in segment, context);
		}

		if (this.objectEnvelope is not null)
		{
			JsonConverter objectConverter = this.objectEnvelope.ReadDiscriminator(ref reader, context.Cache.AllowOutOfOrderMetadataProperties, context);
			context.DepthStep();
			if (objectConverter is not IJsonUnionObjectConverter { SupportsInlineObject: true })
			{
				JsonUnionObjectEnvelope.ReadValueStart(ref reader, context);
			}

			return objectConverter.SkipToValue(ref reader, in segment, context);
		}

		return false;
	}

	internal override async ValueTask<int> TryNavigateAsync(JsonAsyncReader reader, JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
	{
		if (this.isCSharpUnion && this.TryGetNativeDeserializer((char)await reader.PeekNextByteAsync().ConfigureAwait(false), out JsonConverter? nativeConverter))
		{
			context.DepthStep();
			return await nativeConverter.SkipToValueAsync(reader, segment, context).ConfigureAwait(false);
		}

		if (this.objectEnvelope is not null)
		{
			(JsonConverter objectConverter, bool discriminatorConsumed) = await this.objectEnvelope.ReadDiscriminatorAsync(reader, context.Cache.AllowOutOfOrderMetadataProperties, context).ConfigureAwait(false);
			context.DepthStep();
			if (discriminatorConsumed)
			{
				int inner = objectConverter is IJsonUnionObjectConverter { SupportsInlineObject: true } inlineConverter
					? await inlineConverter.TryNavigateAfterDiscriminatorAsync(reader, segment, options, context).ConfigureAwait(false)
					: await ReadValueAndNavigateAfterDiscriminatorAsync(reader, objectConverter, segment, context).ConfigureAwait(false);
				return inner < 0 ? -1 : inner + 1;
			}

			if (objectConverter is not IJsonUnionObjectConverter { SupportsInlineObject: true })
			{
				await JsonUnionObjectEnvelope.ReadValueStartAsync(reader, context).ConfigureAwait(false);
			}

			int objectInner = await objectConverter.SkipToValueAsync(reader, segment, context).ConfigureAwait(false);
			return objectInner < 0 ? -1 : objectInner + 1;
		}

		return -1;
	}

	private static async ValueTask<int> ReadValueAndNavigateAfterDiscriminatorAsync(JsonAsyncReader reader, JsonConverter converter, JsonNavigationSegment segment, SerializationContext context)
	{
		await JsonUnionObjectEnvelope.ReadValueStartAsync(reader, context, discriminatorConsumed: true).ConfigureAwait(false);
		int inner = await converter.SkipToValueAsync(reader, segment, context).ConfigureAwait(false);
		return inner < 0 ? -1 : inner + 1;
	}

	private bool TryGetNativeDeserializer(char token, out JsonConverter converter)
	{
		converter = null!;
		if (token == 'n')
		{
			if (this.nullDeserializer is null)
			{
				return false;
			}

			converter = this.nullDeserializer;
			return true;
		}

		JsonCSharpUnionCaseRepresentation representation = token switch
		{
			't' or 'f' => JsonCSharpUnionCaseRepresentation.Boolean,
			'"' => JsonCSharpUnionCaseRepresentation.String,
			'[' => JsonCSharpUnionCaseRepresentation.Array,
			'{' => JsonCSharpUnionCaseRepresentation.Object,
			'-' or (>= '0' and <= '9') => JsonCSharpUnionCaseRepresentation.Number,
			_ => JsonCSharpUnionCaseRepresentation.Tagged,
		};
		return representation != JsonCSharpUnionCaseRepresentation.Tagged
			&& this.nativeDeserializers is not null
			&& this.nativeDeserializers.TryGetValue(representation, out converter!);
	}

	private bool TryGetSerializer(TUnion value, out JsonUnionCaseMetadata<TUnion> unionCase)
	{
		int index = this.getUnionCaseIndex(ref value);
		if (index >= 0 && index < this.serializers.Length)
		{
			unionCase = this.serializers[index];
			return true;
		}

		unionCase = default;
		return false;
	}

	private bool IsNativeCase(JsonUnionCaseMetadata<TUnion> unionCase)
		=> unionCase.Representation != JsonCSharpUnionCaseRepresentation.Tagged &&
			!(this.tagObjectCases && unionCase.Representation == JsonCSharpUnionCaseRepresentation.Object);
}

internal sealed class JsonUnionCaseConverter<TUnionCase, TUnion> : JsonConverter<TUnion>, IJsonUnionObjectConverter
{
	private readonly JsonConverter<TUnionCase> inner;
	private readonly IMarshaler<TUnionCase, TUnion> marshaler;
	private readonly bool isNullable;

	internal JsonUnionCaseConverter(JsonConverter<TUnionCase> inner, IMarshaler<TUnionCase, TUnion> marshaler, bool isNullable)
	{
		this.inner = inner;
		this.marshaler = marshaler;
		this.isNullable = isNullable;
	}

	public override bool PreferAsyncSerialization => true;

	bool IJsonUnionObjectConverter.SupportsInlineObject
		=> !this.isNullable && this.inner is IJsonUnionObjectConverter { SupportsInlineObject: true };

	void IJsonUnionObjectConverter.WriteObjectProperties(ref JsonWriter writer, object value, SerializationContext context, bool first)
		=> this.GetObjectConverter().WriteObjectProperties(ref writer, this.marshaler.Unmarshal((TUnion)value)!, context, first);

	ValueTask IJsonUnionObjectConverter.WriteObjectPropertiesAsync(JsonAsyncWriter writer, object value, SerializationContext context, bool first)
		=> this.GetObjectConverter().WriteObjectPropertiesAsync(writer, this.marshaler.Unmarshal((TUnion)value)!, context, first);

	object? IJsonUnionObjectConverter.ReadObjectWithMetadata(ref JsonReader reader, SerializationContext context)
		=> this.marshaler.Marshal((TUnionCase?)this.GetObjectConverter().ReadObjectWithMetadata(ref reader, context));

	object? IJsonUnionObjectConverter.ReadObjectWithMetadataAfterDiscriminator(ref JsonReader reader, SerializationContext context)
		=> this.marshaler.Marshal((TUnionCase?)this.GetObjectConverter().ReadObjectWithMetadataAfterDiscriminator(ref reader, context));

	async ValueTask<object?> IJsonUnionObjectConverter.ReadObjectWithMetadataAsync(JsonAsyncReader reader, SerializationContext context)
		=> this.marshaler.Marshal((TUnionCase?)await this.GetObjectConverter().ReadObjectWithMetadataAsync(reader, context).ConfigureAwait(false));

	async ValueTask<object?> IJsonUnionObjectConverter.ReadObjectWithMetadataAfterDiscriminatorAsync(JsonAsyncReader reader, SerializationContext context)
		=> this.marshaler.Marshal((TUnionCase?)await this.GetObjectConverter().ReadObjectWithMetadataAfterDiscriminatorAsync(reader, context).ConfigureAwait(false));

	ValueTask<int> IJsonUnionObjectConverter.TryNavigateAfterDiscriminatorAsync(JsonAsyncReader reader, JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
		=> this.GetObjectConverter().TryNavigateAfterDiscriminatorAsync(reader, segment, options, context);

	public override void Write(ref JsonWriter writer, in TUnion? value, SerializationContext context)
		=> this.inner.Write(ref writer, this.marshaler.Unmarshal(value), context);

	public override ValueTask WriteAsync(JsonAsyncWriter writer, TUnion? value, SerializationContext context)
		=> writer.WriteValueAsync(this.inner, this.marshaler.Unmarshal(value), context);

	public override TUnion? Read(ref JsonReader reader, SerializationContext context)
		=> this.marshaler.Marshal(this.inner.Read(ref reader, context));

	public override async ValueTask<TUnion?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
		=> this.marshaler.Marshal(await reader.ReadValueAsync(this.inner, context).ConfigureAwait(false));

	internal override bool TryNavigate(ref JsonReader reader, in JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
		=> this.inner.SkipToValue(ref reader, in segment, context);

	internal override ValueTask<int> TryNavigateAsync(JsonAsyncReader reader, JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
		=> this.inner.SkipToValueAsync(reader, segment, context);

	private IJsonUnionObjectConverter GetObjectConverter()
		=> !this.isNullable && this.inner is IJsonUnionObjectConverter { SupportsInlineObject: true } objectConverter
			? objectConverter
			: throw new InvalidOperationException("The union case converter does not support inline object metadata.");
}

internal sealed class JsonConstructorVisitorState<TDeclaring>
{
	private readonly Dictionary<string, string> serializedPropertyNamesByClrName;

	/// <summary>Initializes a new instance of the <see cref="JsonConstructorVisitorState{TDeclaring}"/> class.</summary>
	/// <param name="properties">The serialized properties.</param>
	/// <param name="serializedPropertyNamesByClrName">Maps CLR property names to serialized names.</param>
	/// <param name="extensionData">The extension-data property, if any.</param>
	/// <param name="ignoreDerivedTypeUnion">Whether the converter represents a runtime union's plain base object.</param>
	internal JsonConstructorVisitorState(JsonProperty<TDeclaring>[] properties, Dictionary<string, string> serializedPropertyNamesByClrName, JsonExtensionData<TDeclaring>? extensionData = null, bool ignoreDerivedTypeUnion = false)
	{
		this.Properties = properties;
		this.serializedPropertyNamesByClrName = serializedPropertyNamesByClrName;
		this.ExtensionData = extensionData;
		this.IgnoreDerivedTypeUnion = ignoreDerivedTypeUnion;
	}

	internal JsonProperty<TDeclaring>[] Properties { get; }

	internal JsonExtensionData<TDeclaring>? ExtensionData { get; }

	/// <summary>Gets a value indicating whether this converter represents a runtime union's plain base object.</summary>
	internal bool IgnoreDerivedTypeUnion { get; }

	internal bool TryGetSerializedPropertyName(string clrName, out string serializedPropertyName)
		=> this.serializedPropertyNamesByClrName.TryGetValue(clrName, out serializedPropertyName!);
}

internal sealed class JsonParameterVisitorState
{
	internal JsonParameterVisitorState(string serializedPropertyName)
	{
		this.SerializedPropertyName = serializedPropertyName;
	}

	internal string SerializedPropertyName { get; }
}

internal abstract class JsonConstructorParameter<TArgumentState>
{
	internal JsonConstructorParameter(string parameterName, string serializedPropertyName, bool isRequired)
	{
		this.ParameterName = parameterName;
		this.SerializedPropertyName = serializedPropertyName;
		this.IsRequired = isRequired;
	}

	internal string ParameterName { get; }

	internal string SerializedPropertyName { get; }

	internal bool IsRequired { get; }

	internal abstract void Read(ref JsonReader reader, ref TArgumentState argumentState, SerializationContext context);

	internal abstract ValueTask<TArgumentState> ReadIntoAsync(JsonAsyncReader reader, TArgumentState argumentState, SerializationContext context);
}

internal sealed class JsonConstructorParameter<TArgumentState, TParameter> : JsonConstructorParameter<TArgumentState>
{
	private readonly Setter<TArgumentState, TParameter> setter;
	private readonly JsonConverter<TParameter> converter;
	private readonly bool isNonNullableReferenceType;

	internal JsonConstructorParameter(string parameterName, string serializedPropertyName, bool isRequired, Setter<TArgumentState, TParameter> setter, JsonConverter<TParameter> converter, bool isNonNullableReferenceType)
		: base(parameterName, serializedPropertyName, isRequired)
	{
		this.setter = setter;
		this.converter = converter;
		this.isNonNullableReferenceType = isNonNullableReferenceType;
	}

	internal override void Read(ref JsonReader reader, ref TArgumentState argumentState, SerializationContext context)
	{
		TParameter? value = this.converter.Read(ref reader, context);
		if (this.isNonNullableReferenceType
			&& value is null
			&& (context.DeserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties) != DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties)
		{
			throw new FormatException($"Constructor parameter '{this.ParameterName}' does not allow null values.");
		}

		this.setter(ref argumentState, value!);
	}

	internal override async ValueTask<TArgumentState> ReadIntoAsync(JsonAsyncReader reader, TArgumentState argumentState, SerializationContext context)
	{
		TParameter? value = await reader.ReadValueAsync(this.converter, context).ConfigureAwait(false);
		if (this.isNonNullableReferenceType
			&& value is null
			&& (context.DeserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties) != DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties)
		{
			throw new FormatException($"Constructor parameter '{this.ParameterName}' does not allow null values.");
		}

		this.setter(ref argumentState, value!);
		return argumentState;
	}
}

internal sealed class JsonObjectWithConstructorConverter<TDeclaring, TArgumentState> : JsonConverter<TDeclaring>, IJsonUnionObjectConverter
	where TArgumentState : IArgumentState
{
	private readonly JsonExtensionData<TDeclaring>? extensionData;
	private readonly JsonProperty<TDeclaring>[] properties;
	private readonly Func<TArgumentState> argumentStateFactory;
	private readonly Constructor<TArgumentState, TDeclaring> constructor;
	private readonly JsonConstructorParameter<TArgumentState>[] parameters;
	private readonly Dictionary<string, JsonConstructorParameter<TArgumentState>> parametersByName;
	private readonly StringComparer propertyNameComparer;
	private readonly bool supportsInlineObject;

	internal JsonObjectWithConstructorConverter(JsonProperty<TDeclaring>[] properties, Func<TArgumentState> argumentStateFactory, Constructor<TArgumentState, TDeclaring> constructor, JsonConstructorParameter<TArgumentState>[] parameters, Dictionary<string, JsonConstructorParameter<TArgumentState>> parametersByName, StringComparer propertyNameComparer, bool supportsInlineObject, JsonExtensionData<TDeclaring>? extensionData = null)
	{
		this.extensionData = extensionData;
		this.supportsInlineObject = supportsInlineObject;
		this.properties = properties;
		this.argumentStateFactory = argumentStateFactory;
		this.constructor = constructor;
		this.parameters = parameters;
		this.parametersByName = parametersByName;
		this.propertyNameComparer = propertyNameComparer;
	}

	public override bool PreferAsyncSerialization => true;

	bool IJsonUnionObjectConverter.SupportsInlineObject
		=> this.supportsInlineObject;

	public override void Write(ref JsonWriter writer, in TDeclaring? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNull();
			return;
		}

		writer.WriteStartObject();
		this.WriteObjectProperties(ref writer, value, context, first: true);
		writer.WriteEndObject();
	}

	void IJsonUnionObjectConverter.WriteObjectProperties(ref JsonWriter writer, object value, SerializationContext context, bool first)
		=> this.WriteObjectProperties(ref writer, (TDeclaring)value, context, first);

	ValueTask IJsonUnionObjectConverter.WriteObjectPropertiesAsync(JsonAsyncWriter writer, object value, SerializationContext context, bool first)
		=> this.WriteObjectPropertiesAsync(writer, (TDeclaring)value, context, first);

	object? IJsonUnionObjectConverter.ReadObjectWithMetadata(ref JsonReader reader, SerializationContext context)
		=> this.ReadObject(ref reader, context, skipDiscriminator: true);

	object? IJsonUnionObjectConverter.ReadObjectWithMetadataAfterDiscriminator(ref JsonReader reader, SerializationContext context)
		=> this.ReadObject(ref reader, context, skipDiscriminator: true, discriminatorConsumed: true);

	async ValueTask<object?> IJsonUnionObjectConverter.ReadObjectWithMetadataAsync(JsonAsyncReader reader, SerializationContext context)
		=> await this.ReadObjectAsync(reader, context, skipDiscriminator: true).ConfigureAwait(false);

	async ValueTask<object?> IJsonUnionObjectConverter.ReadObjectWithMetadataAfterDiscriminatorAsync(JsonAsyncReader reader, SerializationContext context)
		=> await this.ReadObjectAsync(reader, context, skipDiscriminator: true, discriminatorConsumed: true).ConfigureAwait(false);

	ValueTask<int> IJsonUnionObjectConverter.TryNavigateAfterDiscriminatorAsync(JsonAsyncReader reader, JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
		=> JsonAsyncTargetedNavigator.NavigateMemberAfterDiscriminatorAsync(reader, segment, options, context);

	public override async ValueTask WriteAsync(JsonAsyncWriter writer, TDeclaring? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (value is null)
		{
			await writer.WriteNullAsync(context).ConfigureAwait(false);
			return;
		}

		JsonWriter syncWriter = writer.CreateWriter();
		syncWriter.WriteStartObject();
		writer.ReturnWriter(ref syncWriter);

		await this.WriteObjectPropertiesAsync(writer, value, context, first: true).ConfigureAwait(false);

		syncWriter = writer.CreateWriter();
		syncWriter.WriteEndObject();
		writer.ReturnWriter(ref syncWriter);
		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}

	public override TDeclaring? Read(ref JsonReader reader, SerializationContext context)
		=> this.ReadObject(ref reader, context, skipDiscriminator: false);

	public override async ValueTask<TDeclaring?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
		=> await this.ReadObjectAsync(reader, context, skipDiscriminator: false).ConfigureAwait(false);

	private void WriteObjectProperties(ref JsonWriter writer, TDeclaring value, SerializationContext context, bool first)
	{
		IJsonSerializationCallbacks? callbacks = value as IJsonSerializationCallbacks;
		callbacks?.OnBeforeSerialize();
		context.DepthStep();

		for (int i = 0; i < this.properties.Length; i++)
		{
			JsonProperty<TDeclaring> property = this.properties[i];
			if (!property.CanSerialize)
			{
				continue;
			}

			if (property.Write(ref writer, value, context, first))
			{
				first = false;
			}
		}

		this.extensionData?.Write(ref writer, value, ref first);
		callbacks?.OnAfterSerialize();
	}

	private async ValueTask WriteObjectPropertiesAsync(JsonAsyncWriter writer, TDeclaring value, SerializationContext context, bool first)
	{
		IJsonSerializationCallbacks? callbacks = value as IJsonSerializationCallbacks;
		callbacks?.OnBeforeSerialize();
		context.DepthStep();

		for (int i = 0; i < this.properties.Length; i++)
		{
			JsonProperty<TDeclaring> property = this.properties[i];
			if (!property.CanSerialize)
			{
				continue;
			}

			if (await property.WriteAsync(writer, value, context, first).ConfigureAwait(false))
			{
				first = false;
			}
		}

		JsonWriter syncWriter = writer.CreateWriter();
		this.extensionData?.Write(ref syncWriter, value, ref first);
		writer.ReturnWriter(ref syncWriter);
		callbacks?.OnAfterSerialize();
	}

	private TDeclaring? ReadObject(ref JsonReader reader, SerializationContext context, bool skipDiscriminator, bool discriminatorConsumed = false)
	{
		if (!discriminatorConsumed && !typeof(TDeclaring).IsValueType && reader.TryReadNull())
		{
			return default;
		}

		context.DepthStep();

		TArgumentState argumentState = this.argumentStateFactory();
		try
		{
			HashSet<string> assignedParameters = new(StringComparer.Ordinal);
			UnusedDataPacket.Map? extensionData = null;
			PropertyCollisionDetection collisionDetection = new(this.propertyNameComparer);
			bool foundDiscriminator = discriminatorConsumed;
			if (!discriminatorConsumed)
			{
				reader.ReadStartObject();
			}

			if (!reader.TryReadEndObject())
			{
				if (discriminatorConsumed)
				{
					reader.ReadValueSeparator();
				}

				while (true)
				{
					string propertyName = reader.ReadRequiredString();
					bool isDiscriminator = skipDiscriminator && propertyName == JsonUnionObjectEnvelope.DiscriminatorPropertyName;
					if (!isDiscriminator)
					{
						collisionDetection.MarkAsRead(propertyName);
					}

					reader.ReadNameSeparator();

					if (isDiscriminator)
					{
						if (foundDiscriminator)
						{
							throw new FormatException("A union discriminator object may contain only one '$type' property.");
						}

						foundDiscriminator = true;
						_ = reader.ReadRequiredString();
					}
					else if (this.parametersByName.TryGetValue(propertyName, out JsonConstructorParameter<TArgumentState>? parameter))
					{
						if (!assignedParameters.Add(parameter.SerializedPropertyName))
						{
							throw new JsonSerializationException($"The parameter '{parameter.ParameterName}' has already been assigned a value.")
							{
								Code = JsonSerializationException.ErrorCode.DoublePropertyAssignment,
							};
						}

						parameter.Read(ref reader, ref argumentState, context);
					}
					else if (this.extensionData?.CanRead is true)
					{
						(extensionData ??= new()).Add(propertyName, reader.ReadRawValue(context));
					}
					else
					{
						reader.Skip(context);
					}

					if (reader.TryReadEndObject())
					{
						break;
					}

					reader.ReadValueSeparator();
				}
			}

			this.ThrowIfMissingRequiredParameters(assignedParameters, context);

			TDeclaring result = this.constructor(ref argumentState);
			if (this.extensionData is not null && extensionData is not null)
			{
				this.extensionData.Apply(ref result, extensionData);
			}

			(result as IJsonSerializationCallbacks)?.OnAfterDeserialize();
			return result;
		}
		finally
		{
			argumentState.Return();
		}
	}

	private async ValueTask<TDeclaring?> ReadObjectAsync(JsonAsyncReader reader, SerializationContext context, bool skipDiscriminator, bool discriminatorConsumed = false)
	{
		Requires.NotNull(reader);
		if (!discriminatorConsumed && !typeof(TDeclaring).IsValueType && await reader.TryReadNullAsync(context).ConfigureAwait(false))
		{
			return default;
		}

		context.DepthStep();

		TArgumentState argumentState = this.argumentStateFactory();
		try
		{
			HashSet<string> assignedParameters = new(StringComparer.Ordinal);
			UnusedDataPacket.Map? extensionData = null;
			PropertyCollisionDetection collisionDetection = new(this.propertyNameComparer);
			if (!discriminatorConsumed)
			{
				await reader.ReadStartObjectAsync(context).ConfigureAwait(false);
			}

			if (!await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
			{
				bool foundDiscriminator = discriminatorConsumed;
				if (discriminatorConsumed)
				{
					await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
				}

				while (true)
				{
					string propertyName = await reader.ReadPropertyNameAsync(context).ConfigureAwait(false);
					bool isDiscriminator = skipDiscriminator && propertyName == JsonUnionObjectEnvelope.DiscriminatorPropertyName;
					if (!isDiscriminator)
					{
						collisionDetection.MarkAsRead(propertyName);
					}

					if (isDiscriminator)
					{
						if (foundDiscriminator)
						{
							throw new FormatException("A union discriminator object may contain only one '$type' property.");
						}

						foundDiscriminator = true;
						_ = await reader.ReadStringValueAsync(context).ConfigureAwait(false);
					}
					else if (this.parametersByName.TryGetValue(propertyName, out JsonConstructorParameter<TArgumentState>? parameter))
					{
						if (!assignedParameters.Add(parameter.SerializedPropertyName))
						{
							throw new JsonSerializationException($"The parameter '{parameter.ParameterName}' has already been assigned a value.")
							{
								Code = JsonSerializationException.ErrorCode.DoublePropertyAssignment,
							};
						}

						argumentState = await parameter.ReadIntoAsync(reader, argumentState, context).ConfigureAwait(false);
					}
					else if (this.extensionData?.CanRead is true)
					{
						(extensionData ??= new()).Add(propertyName, await reader.ReadRawValueAsync(context).ConfigureAwait(false));
					}
					else
					{
						await reader.SkipValueAsync(context).ConfigureAwait(false);
					}

					if (await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
					{
						break;
					}

					await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
				}
			}

			this.ThrowIfMissingRequiredParameters(assignedParameters, context);

			TDeclaring result = this.constructor(ref argumentState);
			if (this.extensionData is not null && extensionData is not null)
			{
				this.extensionData.Apply(ref result, extensionData);
			}

			(result as IJsonSerializationCallbacks)?.OnAfterDeserialize();
			return result;
		}
		finally
		{
			argumentState.Return();
		}
	}

	private void ThrowIfMissingRequiredParameters(HashSet<string> assignedParameters, SerializationContext context)
	{
		if (assignedParameters.Count < this.parameters.Length
			&& (context.DeserializeDefaultValues & DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties) != DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties)
		{
			List<string>? missingRequiredParameters = null;
			for (int i = 0; i < this.parameters.Length; i++)
			{
				JsonConstructorParameter<TArgumentState> parameter = this.parameters[i];
				if (parameter.IsRequired && !assignedParameters.Contains(parameter.SerializedPropertyName))
				{
					missingRequiredParameters ??= [];
					missingRequiredParameters.Add(parameter.ParameterName);
				}
			}

			if (missingRequiredParameters is not null)
			{
				throw new JsonSerializationException($"Missing required constructor parameters: {string.Join(", ", missingRequiredParameters)}.")
				{
					Code = JsonSerializationException.ErrorCode.MissingRequiredProperty,
				};
			}
		}
	}
}
