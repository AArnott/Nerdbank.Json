// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Internal helper members are intentionally undocumented.
#pragma warning disable SA1204 // Binding helpers read more clearly with instance members before their factories.

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Globalization;
using System.Text;
using JsonSchemaArray = System.Text.Json.Nodes.JsonArray;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;

namespace Nerdbank.Json;

internal interface IUnionDiscriminatorSource
{
	UnionDiscriminatorBinding? Discriminator { get; }
}

internal abstract class UnionDiscriminatorBinding
{
	private readonly byte[] quotedUtf8Name;
	private readonly byte[] encodedName;

	private protected UnionDiscriminatorBinding(string name, IPropertyShape? source, string serializedToken)
	{
		this.Name = name;
		this.Source = source;
		this.SerializedToken = serializedToken;
		this.quotedUtf8Name = Encoding.UTF8.GetBytes("\"" + name + "\"");
		this.encodedName = JsonWriter.EncodePropertyName(name);
	}

	internal string Name { get; }

	internal IPropertyShape? Source { get; }

	internal string SerializedToken { get; }

	internal static UnionDiscriminatorBinding? FromConverter(JsonConverter converter)
		=> converter is IUnionDiscriminatorSource source ? source.Discriminator : null;

	internal static void ValidateUnion(IEnumerable<JsonConverter> converters, StringComparer comparer)
	{
		UnionDiscriminatorBinding? first = null;
		HashSet<string> values = new(StringComparer.Ordinal);
		foreach (JsonConverter converter in converters)
		{
			UnionDiscriminatorBinding? discriminator = FromConverter(converter);
			if (discriminator is null)
			{
				continue;
			}

			if (first is null)
			{
				first = discriminator;
			}
			else if (!comparer.Equals(first.Name, discriminator.Name))
			{
				throw new NotSupportedException($"Union cases declare discriminator names '{first.Name}' and '{discriminator.Name}'. Cases in one union must use the same discriminator name.");
			}

			if (!values.Add(discriminator.SerializedToken))
			{
				throw new NotSupportedException($"The union discriminator '{discriminator.Name}' value {discriminator.SerializedToken} is declared by more than one case.");
			}
		}
	}

	internal static UnionDiscriminatorBinding? Create<T>(IObjectTypeShape<T> shape, ConverterCache owner)
	{
		UnionDiscriminatorAttribute? typeLevel = FirstAttribute(shape.AttributeProvider);
		UnionDiscriminatorAttribute? memberLevel = null;
		IPropertyShape? member = null;
		foreach (IPropertyShape property in shape.Properties)
		{
			UnionDiscriminatorAttribute? attribute = FirstAttribute(property.AttributeProvider);
			if (attribute is null)
			{
				continue;
			}

			if (member is not null)
			{
				throw new NotSupportedException($"Type '{typeof(T).FullName}' declares more than one union discriminator.");
			}

			member = property;
			memberLevel = attribute;
		}

		if (typeLevel is not null && memberLevel is not null)
		{
			throw new NotSupportedException($"Type '{typeof(T).FullName}' declares both a type-level union discriminator and one on '{member!.Name}'.");
		}

		if (member is not null)
		{
			List<string> parameters = [];
			if (shape.Constructor is { } constructor)
			{
				foreach (IParameterShape parameter in constructor.Parameters)
				{
					parameters.Add(parameter.Name);
				}
			}

			return (UnionDiscriminatorBinding)member.Accept(new MemberFactory<T>(owner, memberLevel!, parameters))!;
		}

		if (typeLevel is null)
		{
			return null;
		}

		if (string.IsNullOrEmpty(typeLevel.PropertyName))
		{
			throw new NotSupportedException($"Type '{typeof(T).FullName}' declares a union discriminator without a property name.");
		}

		foreach (IPropertyShape property in shape.Properties)
		{
			string serialized = owner.GetSerializedPropertyName(property.Name, property.AttributeProvider);
			if (owner.PropertyNameComparer.Equals(serialized, typeLevel.PropertyName))
			{
				throw new NotSupportedException($"Type '{typeof(T).FullName}' already has a property serialized as '{typeLevel.PropertyName}'.");
			}
		}

		return CreateTypeLevel(typeof(T), typeLevel, owner);
	}

	internal bool MatchesUtf8(ReadOnlySpan<byte> quotedName) => quotedName.SequenceEqual(this.quotedUtf8Name);

	internal abstract void Write(ref JsonWriter writer, SerializationContext context, bool first);

	internal abstract ValueTask WriteAsync(JsonAsyncWriter writer, SerializationContext context, bool first);

	internal abstract void Read(ref JsonReader reader, SerializationContext context);

	internal abstract ValueTask ReadAsync(JsonAsyncReader reader, SerializationContext context);

	internal abstract JsonSchemaObject CreateSchema(JsonSchemaDialect dialect);

	private protected void WriteName(ref JsonWriter writer, bool first)
	{
		if (!first)
		{
			writer.WriteValueSeparator();
		}

		writer.WritePropertyName(this.encodedName);
	}

	private protected async ValueTask ReadBufferedAsync(JsonAsyncReader reader, SerializationContext context)
	{
		await reader.BufferNextValueAsync(context).ConfigureAwait(false);
		JsonReader sync = reader.CreateBufferedReader();
		try
		{
			this.Read(ref sync, context);
		}
		finally
		{
			reader.ReturnReader(ref sync);
		}
	}

	private protected static JsonSerializationException Mismatch(string name, string actual, string expected)
		=> new($"The union discriminator '{name}' value {actual} does not match {expected}.")
		{
			Code = JsonSerializationException.ErrorCode.DiscriminatorMismatch,
		};

	private protected static JsonSchemaObject ConstantSchema(JsonSchemaDialect dialect, string type, object constValue)
	{
		JsonSchemaObject schema = new JsonSchemaObject().Set("type", type);
		System.Text.Json.Nodes.JsonNode? node = JsonSchemaExtensions.CreatePrimitiveValue(constValue);
		if (dialect == JsonSchemaDialect.Draft4)
		{
			schema["enum"] = new JsonSchemaArray(node);
			return schema;
		}

		schema["const"] = node;
		return schema;
	}

	private static UnionDiscriminatorAttribute? FirstAttribute(IGenericCustomAttributeProvider? provider)
	{
		if (provider is null)
		{
			return null;
		}

		foreach (UnionDiscriminatorAttribute attribute in provider.GetCustomAttributes<UnionDiscriminatorAttribute>(inherit: false))
		{
			return attribute;
		}

		return null;
	}

	private static UnionDiscriminatorBinding CreateTypeLevel(Type declaringType, UnionDiscriminatorAttribute attribute, ConverterCache owner)
	{
		if (!attribute.HasValue || attribute.Value is null)
		{
			throw new NotSupportedException($"Type '{declaringType.FullName}' declares a union discriminator without a constant value.");
		}

		string name = attribute.PropertyName!;
		RejectReservedName(declaringType, name);
		object value = attribute.Value;
		if (value is string text)
		{
			return new ScalarBinding<string>(name, text, Quote(text));
		}

		if (value is bool boolean)
		{
			return new ScalarBinding<bool>(name, boolean, boolean ? "true" : "false");
		}

		if (value.GetType().IsEnum)
		{
			return EnumBinding.Create(declaringType, name, value, owner);
		}

		if (!IsInteger(value.GetType()))
		{
			throw new NotSupportedException($"Type '{declaringType.FullName}' declares a union discriminator of type '{value.GetType().FullName}', which is not a string, boolean, integer, or enum.");
		}

		return new IntegerBinding(name, value);
	}

	private static void RejectReservedName(Type declaringType, string name)
	{
		if (name is JsonUnionObjectEnvelope.DiscriminatorPropertyName or JsonUnionObjectEnvelope.ValuePropertyName)
		{
			throw new NotSupportedException($"Type '{declaringType.FullName}' cannot use '{name}' as a union discriminator name.");
		}
	}

	private static bool IsInteger(Type type)
		=> type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
			|| type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong);

	private static string Quote(string value)
	{
		StringBuilder builder = new(value.Length + 2);
		builder.Append('"');
		foreach (char ch in value)
		{
			switch (ch)
			{
				case '"':
					builder.Append("\\\"");
					break;
				case '\\':
					builder.Append("\\\\");
					break;
				case '\b':
					builder.Append("\\b");
					break;
				case '\f':
					builder.Append("\\f");
					break;
				case '\n':
					builder.Append("\\n");
					break;
				case '\r':
					builder.Append("\\r");
					break;
				case '\t':
					builder.Append("\\t");
					break;
				default:
					if (ch < ' ')
					{
						builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
					}
					else
					{
						builder.Append(ch);
					}

					break;
			}
		}

		builder.Append('"');
		return builder.ToString();
	}

	private static string IntegerToken(object value)
		=> value is ulong unsigned ? unsigned.ToString(CultureInfo.InvariantCulture) : Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

	private sealed class MemberFactory<TDeclaring> : TypeShapeVisitor
	{
		private readonly ConverterCache owner;
		private readonly UnionDiscriminatorAttribute attribute;
		private readonly List<string> constructorParameters;

		internal MemberFactory(ConverterCache owner, UnionDiscriminatorAttribute attribute, List<string> constructorParameters)
		{
			this.owner = owner;
			this.attribute = attribute;
			this.constructorParameters = constructorParameters;
		}

		public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
			=> MemberBinding<TDeclaringType, TPropertyType>.Create(propertyShape, this.attribute, this.owner, this.constructorParameters);

		public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null) => Unsupported();

		public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null) => Unsupported();

		public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null) => Unsupported();

		public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null) => Unsupported();

		public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null) => Unsupported();

		public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state = null) => Unsupported();

		public override object? VisitFunction<TFunction, TArgumentState, TResult>(IFunctionTypeShape<TFunction, TArgumentState, TResult> functionShape, object? state = null) => Unsupported();

		public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null) => Unsupported();

		public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null) => Unsupported();

		public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state = null) => Unsupported();

		public override object? VisitParameter<TArgumentState, TParameterType>(IParameterShape<TArgumentState, TParameterType> parameterShape, object? state = null) => Unsupported();

		private static object Unsupported() => throw new NotSupportedException("A union discriminator must be an object property.");
	}

	private static class MemberBinding<TDeclaring, TProperty>
	{
		internal static UnionDiscriminatorBinding Create(IPropertyShape<TDeclaring, TProperty> property, UnionDiscriminatorAttribute attribute, ConverterCache owner, List<string> constructorParameters)
		{
			if (!IsSupported(typeof(TProperty)))
			{
				throw new NotSupportedException($"Union discriminator '{property.Name}' on '{typeof(TDeclaring).FullName}' has unsupported type '{typeof(TProperty).FullName}'.");
			}

			if (!property.HasGetter)
			{
				throw new NotSupportedException($"Union discriminator '{property.Name}' on '{typeof(TDeclaring).FullName}' must be readable.");
			}

			if (property.HasSetter)
			{
				throw new NotSupportedException($"Union discriminator '{property.Name}' on '{typeof(TDeclaring).FullName}' must not have a setter.");
			}

			foreach (string parameterName in constructorParameters)
			{
				if (string.Equals(parameterName, property.Name, StringComparison.OrdinalIgnoreCase))
				{
					throw new NotSupportedException($"Union discriminator '{property.Name}' on '{typeof(TDeclaring).FullName}' must not be bound to a constructor parameter.");
				}
			}

			string name = owner.GetSerializedPropertyName(property.Name, property.AttributeProvider);
			RejectReservedName(typeof(TDeclaring), name);
			TProperty actual = ReadConstant(property);
			TProperty expected = attribute.HasValue ? ConvertValue(property.Name, attribute.Value) : actual;
			if (!EqualityComparer<TProperty>.Default.Equals(actual, expected))
			{
				throw new NotSupportedException($"Union discriminator '{property.Name}' on '{typeof(TDeclaring).FullName}' returns '{actual}' instead of '{expected}'.");
			}

			JsonConverter<TProperty> converter = owner.GetConverter(property.PropertyType, attributeProvider: null);
			return new ConverterBinding<TProperty>(name, property, expected, converter);
		}

		private static bool IsSupported(Type type)
			=> type == typeof(string) || type == typeof(bool) || type.IsEnum || IsInteger(type);

		private static TProperty ReadConstant(IPropertyShape<TDeclaring, TProperty> property)
		{
			try
			{
				TDeclaring instance = CreateUninitialized();
				return property.GetGetter()(ref instance);
			}
			catch (Exception ex) when (ex is not NotSupportedException)
			{
				throw new NotSupportedException($"Union discriminator '{property.Name}' on '{typeof(TDeclaring).FullName}' must be a constant.", ex);
			}
		}

#if NET
		[UnconditionalSuppressMessage("Trimming", "IL2087", Justification = "The uninitialized instance is only used to read a constant getter.")]
#endif
		private static TDeclaring CreateUninitialized()
		{
			if (typeof(TDeclaring).IsValueType)
			{
				return default!;
			}

#if NET
			return (TDeclaring)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TDeclaring));
#else
			return (TDeclaring)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(TDeclaring));
#endif
		}

		private static TProperty ConvertValue(string propertyName, object? value)
		{
			if (value is null)
			{
				throw new NotSupportedException($"Union discriminator '{propertyName}' on '{typeof(TDeclaring).FullName}' cannot be null.");
			}

			if (value is TProperty typed)
			{
				return typed;
			}

			if (typeof(TProperty).IsEnum && IsInteger(value.GetType()))
			{
				return (TProperty)Enum.ToObject(typeof(TProperty), value);
			}

			if (IsInteger(value.GetType()) && IsInteger(typeof(TProperty)))
			{
				try
				{
					return (TProperty)Convert.ChangeType(value, typeof(TProperty), CultureInfo.InvariantCulture);
				}
				catch (Exception ex) when (ex is OverflowException or InvalidCastException or FormatException)
				{
					throw new NotSupportedException($"Union discriminator '{propertyName}' on '{typeof(TDeclaring).FullName}' value '{value}' does not fit '{typeof(TProperty).FullName}'.", ex);
				}
			}

			throw new NotSupportedException($"Union discriminator '{propertyName}' on '{typeof(TDeclaring).FullName}' value '{value}' is not a '{typeof(TProperty).FullName}'.");
		}
	}

	private sealed class ConverterBinding<T> : UnionDiscriminatorBinding
	{
		private readonly T expected;
		private readonly JsonConverter<T> converter;
		private readonly string schemaType;
		private readonly object schemaValue;

		internal ConverterBinding(string name, IPropertyShape source, T expected, JsonConverter<T> converter)
			: base(name, source, Token(expected, converter))
		{
			this.expected = expected;
			this.converter = converter;
			(this.schemaType, this.schemaValue) = SchemaValue(expected, converter);
		}

		internal override void Write(ref JsonWriter writer, SerializationContext context, bool first)
		{
			this.WriteName(ref writer, first);
			this.converter.Write(ref writer, this.expected, context);
		}

		internal override ValueTask WriteAsync(JsonAsyncWriter writer, SerializationContext context, bool first)
		{
			JsonWriter sync = writer.CreateWriter();
			this.Write(ref sync, context, first);
			writer.ReturnWriter(ref sync);
			return default;
		}

		internal override void Read(ref JsonReader reader, SerializationContext context)
		{
			T actual = this.converter.Read(ref reader, context)!;
			if (!EqualityComparer<T>.Default.Equals(actual, this.expected))
			{
				throw Mismatch(this.Name, Token(actual, this.converter), this.SerializedToken);
			}
		}

		internal override ValueTask ReadAsync(JsonAsyncReader reader, SerializationContext context)
			=> this.ReadBufferedAsync(reader, context);

		internal override JsonSchemaObject CreateSchema(JsonSchemaDialect dialect)
			=> ConstantSchema(dialect, this.schemaType, this.schemaValue);

		private static string Token(T value, JsonConverter<T> converter)
		{
			if (converter is IJsonEnumNameConverter names && value is not null && names.TryGetSerializedName(value, out string? enumName))
			{
				return Quote(enumName!);
			}

			if (value is string text)
			{
				return Quote(text);
			}

			if (value is bool boolean)
			{
				return boolean ? "true" : "false";
			}

			if (value is Enum)
			{
				return IntegerToken(value);
			}

			return Convert.ToString(value, CultureInfo.InvariantCulture)!;
		}

		private static (string Type, object Value) SchemaValue(T value, JsonConverter<T> converter)
		{
			if (converter is IJsonEnumNameConverter names && value is not null && names.TryGetSerializedName(value, out string? enumName))
			{
				return ("string", enumName!);
			}

			if (value is string text)
			{
				return ("string", text);
			}

			if (value is bool boolean)
			{
				return ("boolean", boolean);
			}

			if (value is Enum)
			{
				return ("integer", Convert.ToInt64(value, CultureInfo.InvariantCulture));
			}

			return ("integer", value!);
		}
	}

	private sealed class ScalarBinding<T> : UnionDiscriminatorBinding
		where T : notnull
	{
		private readonly T expected;
		private readonly string schemaType;

		internal ScalarBinding(string name, T expected, string token)
			: base(name, source: null, token)
		{
			this.expected = expected;
			this.schemaType = expected is bool ? "boolean" : "string";
		}

		internal override void Write(ref JsonWriter writer, SerializationContext context, bool first)
		{
			_ = context;
			this.WriteName(ref writer, first);
			if (this.expected is string text)
			{
				writer.Write(text);
			}
			else if (this.expected is bool boolean)
			{
				writer.Write(boolean);
			}
		}

		internal override ValueTask WriteAsync(JsonAsyncWriter writer, SerializationContext context, bool first)
		{
			JsonWriter sync = writer.CreateWriter();
			this.Write(ref sync, context, first);
			writer.ReturnWriter(ref sync);
			return default;
		}

		internal override void Read(ref JsonReader reader, SerializationContext context)
		{
			_ = context;
			object actual = typeof(T) == typeof(string) ? (object)reader.ReadRequiredString() : reader.ReadBoolean();
			if (!Equals(actual, this.expected))
			{
				throw Mismatch(this.Name, actual is string text ? Quote(text) : actual.ToString()!, this.SerializedToken);
			}
		}

		internal override ValueTask ReadAsync(JsonAsyncReader reader, SerializationContext context)
			=> this.ReadBufferedAsync(reader, context);

		internal override JsonSchemaObject CreateSchema(JsonSchemaDialect dialect)
			=> ConstantSchema(dialect, this.schemaType, this.expected);
	}

	private sealed class IntegerBinding : UnionDiscriminatorBinding
	{
		private readonly object expected;
		private readonly bool unsigned;

		internal IntegerBinding(string name, object expected)
			: base(name, source: null, IntegerToken(expected))
		{
			this.expected = expected;
			this.unsigned = expected is ulong;
		}

		internal override void Write(ref JsonWriter writer, SerializationContext context, bool first)
		{
			_ = context;
			this.WriteName(ref writer, first);
			if (this.unsigned)
			{
				writer.Write((ulong)this.expected);
			}
			else
			{
				writer.Write(Convert.ToInt64(this.expected, CultureInfo.InvariantCulture));
			}
		}

		internal override ValueTask WriteAsync(JsonAsyncWriter writer, SerializationContext context, bool first)
		{
			JsonWriter sync = writer.CreateWriter();
			this.Write(ref sync, context, first);
			writer.ReturnWriter(ref sync);
			return default;
		}

		internal override void Read(ref JsonReader reader, SerializationContext context)
		{
			_ = context;
			bool matches = this.unsigned
				? reader.ReadUInt64() == (ulong)this.expected
				: reader.ReadInt64() == Convert.ToInt64(this.expected, CultureInfo.InvariantCulture);
			if (!matches)
			{
				throw Mismatch(this.Name, "a different number", this.SerializedToken);
			}
		}

		internal override ValueTask ReadAsync(JsonAsyncReader reader, SerializationContext context)
			=> this.ReadBufferedAsync(reader, context);

		internal override JsonSchemaObject CreateSchema(JsonSchemaDialect dialect)
			=> ConstantSchema(dialect, "integer", this.unsigned ? this.expected : Convert.ToInt64(this.expected, CultureInfo.InvariantCulture));
	}

	private sealed class EnumBinding : UnionDiscriminatorBinding
	{
		private readonly bool byName;
		private readonly string? expectedName;
		private readonly long expectedNumber;

		private EnumBinding(string name, bool byName, string? expectedName, long expectedNumber)
			: base(name, source: null, byName ? Quote(expectedName!) : expectedNumber.ToString(CultureInfo.InvariantCulture))
		{
			this.byName = byName;
			this.expectedName = expectedName;
			this.expectedNumber = expectedNumber;
		}

		internal static EnumBinding Create(Type declaringType, string name, object value, ConverterCache owner)
		{
			_ = declaringType;
			long number = Convert.ToInt64(value, CultureInfo.InvariantCulture);
			if (!owner.SerializeEnumValuesByName)
			{
				return new EnumBinding(name, byName: false, expectedName: null, number);
			}

			string member = Enum.GetName(value.GetType(), value) ?? number.ToString(CultureInfo.InvariantCulture);
			member = owner.PropertyNamingPolicy?.ConvertName(member) ?? member;
			return new EnumBinding(name, byName: true, member, number);
		}

		internal override void Write(ref JsonWriter writer, SerializationContext context, bool first)
		{
			_ = context;
			this.WriteName(ref writer, first);
			if (this.byName)
			{
				writer.Write(this.expectedName!);
			}
			else
			{
				writer.Write(this.expectedNumber);
			}
		}

		internal override ValueTask WriteAsync(JsonAsyncWriter writer, SerializationContext context, bool first)
		{
			JsonWriter sync = writer.CreateWriter();
			this.Write(ref sync, context, first);
			writer.ReturnWriter(ref sync);
			return default;
		}

		internal override void Read(ref JsonReader reader, SerializationContext context)
		{
			_ = context;
			if (this.byName)
			{
				string actual = reader.ReadRequiredString();
				if (!string.Equals(actual, this.expectedName, StringComparison.Ordinal))
				{
					throw Mismatch(this.Name, Quote(actual), this.SerializedToken);
				}

				return;
			}

			if (reader.ReadInt64() != this.expectedNumber)
			{
				throw Mismatch(this.Name, "a different number", this.SerializedToken);
			}
		}

		internal override ValueTask ReadAsync(JsonAsyncReader reader, SerializationContext context)
			=> this.ReadBufferedAsync(reader, context);

		internal override JsonSchemaObject CreateSchema(JsonSchemaDialect dialect)
			=> this.byName
				? ConstantSchema(dialect, "string", this.expectedName!)
				: ConstantSchema(dialect, "integer", this.expectedNumber);
	}
}
