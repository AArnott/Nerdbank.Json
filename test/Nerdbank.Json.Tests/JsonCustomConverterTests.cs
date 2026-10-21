// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[GenerateShapeFor<string>]
[GenerateShapeFor<char>]
[GenerateShapeFor<List<char>>]
[GenerateShapeFor<AttributedStringWrapper>]
[GenerateShapeFor<GenericValue<string>>]
[GenerateShapeFor<GenericAttributedValue<int>>]
[GenerateShapeFor<GenericAttributedValue<string>>]
public partial class JsonObjectSerializerTests
{
	[Test]
	public void SerializeDeserialize_CanUseRuntimeRegisteredConverterInstance()
	{
		JsonSerializer serializer = new()
		{
			Converters = ConverterCollection.Create([new UpperCaseStringConverter()]),
		};

		string json = serializer.Serialize<string, JsonObjectSerializerTests>("Ada");
		string? roundTripped = serializer.Deserialize<string, JsonObjectSerializerTests>(json);

		Assert.NotNull(roundTripped);
		Assert.Equal("\"ADA\"", json);
		Assert.Equal("ada", roundTripped);
	}

	[Test]
	public void SerializeDeserialize_CanUseRuntimeRegisteredOpenGenericConverterType()
	{
		JsonSerializer serializer = new()
		{
			ConverterTypes = ConverterTypeCollection.Create([typeof(GenericValueConverter<>)]),
		};

		GenericValue<string> value = new() { Value = "Ada" };

		string json = serializer.Serialize<GenericValue<string>, JsonObjectSerializerTests>(value);
		GenericValue<string>? roundTripped = serializer.Deserialize<GenericValue<string>, JsonObjectSerializerTests>(json);
		Assert.NotNull(roundTripped);

		Assert.Equal("""{"value":"Ada!"}""", json);
		Assert.Equal("Ada", roundTripped.Value);
	}

	[Test]
	public void SerializeDeserialize_CanUseRuntimeRegisteredConverterFactory()
	{
		JsonSerializer serializer = new()
		{
			ConverterFactories = [new CharListWrapperFactory()],
		};

		List<char> value = ['a', 'b', 'c'];

		string json = serializer.Serialize<List<char>, JsonObjectSerializerTests>(value);
		List<char>? roundTripped = serializer.Deserialize<List<char>, JsonObjectSerializerTests>(json);

		Assert.Equal("""[["a","b","c"]]""", json);
		Assert.Equal(value, roundTripped);
	}

	[Test]
	public void SerializeDeserialize_CanUseTypeAttributedConverter_WithoutShape()
	{
		JsonSerializer serializer = new();
		AttributedStringWrapper value = new() { Value = "Ada" };

		string json = serializer.Serialize<AttributedStringWrapper, JsonObjectSerializerTests>(value);
		AttributedStringWrapper? roundTripped = serializer.Deserialize<AttributedStringWrapper, JsonObjectSerializerTests>(json);

		Assert.Equal("""{"value":"ADA"}""", json);
		Assert.Equal("ada", roundTripped?.Value);
	}

	[Test]
	public void AttributedConverters_GenerateConstructorAssociations()
	{
		ITypeShape<AttributedStringWrapper> shape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<AttributedStringWrapper>()!;
		Assert.NotNull(shape.GetAssociatedTypeShape(typeof(AttributedStringWrapperConverter)));
		ITypeShape<string> stringShape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<string>()!;
		Assert.NotNull(stringShape.GetAssociatedTypeShape(typeof(UpperCaseStringConverter)));
	}

	[Test]
	public void GenericAttributedConverters_GenerateClosedAssociationsAndRoundTrip()
	{
		ITypeShape<GenericAttributedValue<int>> intShape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<GenericAttributedValue<int>>()!;
		ITypeShape<GenericAttributedValue<string>> stringShape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<GenericAttributedValue<string>>()!;
		Assert.Equal(typeof(GenericAttributedValueConverter<int>), intShape.GetAssociatedTypeShape(typeof(GenericAttributedValueConverter<>))?.Type);
		Assert.Equal(typeof(GenericAttributedValueConverter<string>), stringShape.GetAssociatedTypeShape(typeof(GenericAttributedValueConverter<>))?.Type);
		JsonSerializer serializer = new();
		Assert.Equal("42", serializer.Serialize(new GenericAttributedValue<int>(41), intShape));
		Assert.Equal(new GenericAttributedValue<int>(41), serializer.Deserialize("42", intShape));
		Assert.Equal("8", serializer.Serialize(new GenericAttributedValue<string>(7), stringShape));
		Assert.Equal(new GenericAttributedValue<string>(7), serializer.Deserialize("8", stringShape));
	}

	[Test]
	public void SerializeDeserialize_CanUsePropertyAttributedConverter()
	{
		JsonSerializer serializer = new();
		PropertyAttributedContainer value = new() { Name = "Ada", Unconverted = "Grace" };

		string json = serializer.Serialize(value);
		PropertyAttributedContainer? roundTripped = serializer.Deserialize<PropertyAttributedContainer>(json);

		Assert.Equal("""{"name":"ADA","unconverted":"Grace"}""", json);
		Assert.NotNull(roundTripped);
		Assert.Equal("ada", roundTripped.Name);
		Assert.Equal("Grace", roundTripped.Unconverted);
	}

	[Test]
	public void Deserialize_CanUseConstructorParameterAttributedConverter()
	{
		JsonSerializer serializer = new();

		ParameterAttributedContainer? roundTripped = serializer.Deserialize<ParameterAttributedContainer>("""{"name":"ADA"}""");

		Assert.NotNull(roundTripped);
		Assert.Equal("ada", roundTripped.Name);
	}

	[GenerateShape]
	internal sealed partial class PropertyAttributedContainer
	{
		[JsonConverter(typeof(UpperCaseStringConverter))]
		public string? Name { get; set; }

		public string? Unconverted { get; set; }
	}

	[GenerateShape]
	internal sealed partial class ParameterAttributedContainer([JsonConverter(typeof(UpperCaseStringConverter))] string name)
	{
		public string Name { get; } = name;
	}

	internal sealed partial class UpperCaseStringConverter : JsonConverter<string>
	{
		public override void Write(ref JsonWriter writer, in string? value, SerializationContext context)
		{
			writer.Write(value?.ToUpperInvariant());
		}

		public override string? Read(ref JsonReader reader, SerializationContext context)
		{
			string? value = reader.ReadString();
			return value?.ToLowerInvariant();
		}
	}

	[JsonConverter(typeof(AttributedStringWrapperConverter))]
	internal sealed partial class AttributedStringWrapper
	{
		public string? Value { get; set; }
	}

	internal sealed partial class AttributedStringWrapperConverter : JsonConverter<AttributedStringWrapper>
	{
		public override void Write(ref JsonWriter writer, in AttributedStringWrapper? value, SerializationContext context)
		{
			if (value is null)
			{
				writer.WriteNull();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName("value");
			writer.Write(value.Value?.ToUpperInvariant());
			writer.WriteEndObject();
		}

		public override AttributedStringWrapper? Read(ref JsonReader reader, SerializationContext context)
		{
			if (reader.TryReadNull())
			{
				return null;
			}

			reader.ReadStartObject();
			string propertyName = reader.ReadRequiredString();
			reader.ReadNameSeparator();
			string? value = reader.ReadString();
			Assert.Equal("value", propertyName);
			Assert.True(reader.TryReadEndObject());

			return new AttributedStringWrapper { Value = value?.ToLowerInvariant() };
		}
	}

	[AssociatedTypeShape(typeof(GenericValueConverter<>))]
	internal sealed partial class GenericValue<T>
	{
		public string? Value { get; set; }
	}

	internal sealed partial class GenericValueConverter<T> : JsonConverter<GenericValue<T>>
	{
		public override void Write(ref JsonWriter writer, in GenericValue<T>? value, SerializationContext context)
		{
			if (value is null)
			{
				writer.WriteNull();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName("value");
			writer.Write(value.Value + "!");
			writer.WriteEndObject();
		}

		public override GenericValue<T>? Read(ref JsonReader reader, SerializationContext context)
		{
			if (reader.TryReadNull())
			{
				return null;
			}

			reader.ReadStartObject();
			string propertyName = reader.ReadRequiredString();
			reader.ReadNameSeparator();
			string? value = reader.ReadString();
			Assert.Equal("value", propertyName);
			Assert.True(reader.TryReadEndObject());

			return new GenericValue<T>
			{
				Value = value?.TrimEnd('!'),
			};
		}
	}

	[JsonConverter(typeof(GenericAttributedValueConverter<>))]
	internal sealed record GenericAttributedValue<T>(int Number);

	internal sealed class GenericAttributedValueConverter<T> : JsonConverter<GenericAttributedValue<T>>
	{
		public override GenericAttributedValue<T> Read(ref JsonReader reader, SerializationContext context) => new(reader.ReadInt32() - 1);

		public override void Write(ref JsonWriter writer, in GenericAttributedValue<T>? value, SerializationContext context) => writer.Write(value!.Number + 1);
	}

	private sealed class CharListWrapperFactory : IJsonConverterFactory
	{
		public JsonConverter? CreateConverter(Type type, ITypeShape? shape, in ConverterContext context)
			=> type == typeof(List<char>) ? new CharListWrapperConverter(context.GetConverter(PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<char>() ?? throw new InvalidOperationException("No generated type shape found for System.Char."))) : null;
	}

	private sealed class CharListWrapperConverter(JsonConverter<char> elementConverter) : JsonConverter<List<char>>
	{
		public override void Write(ref JsonWriter writer, in List<char>? value, SerializationContext context)
		{
			if (value is null)
			{
				writer.WriteNull();
				return;
			}

			writer.WriteStartArray();
			writer.WriteStartArray();
			for (int i = 0; i < value.Count; i++)
			{
				if (i > 0)
				{
					writer.WriteValueSeparator();
				}

				elementConverter.Write(ref writer, value[i], context);
			}

			writer.WriteEndArray();
			writer.WriteEndArray();
		}

		public override List<char>? Read(ref JsonReader reader, SerializationContext context)
		{
			if (reader.TryReadNull())
			{
				return null;
			}

			reader.ReadStartArray();
			reader.ReadStartArray();
			List<char> result = [];
			bool first = true;
			while (!reader.TryReadEndArray())
			{
				if (!first)
				{
					reader.ReadValueSeparator();
				}

				result.Add(elementConverter.Read(ref reader, context));
				first = false;
			}

			reader.ReadEndArray();
			return result;
		}
	}
}
