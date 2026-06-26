// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Nerdbank.Json;
using PolyType;

public partial class JsonObjectSerializerTests
{
	[Test]
	public void SerializeDeserialize_CanUseRuntimeRegisteredConverterInstance()
	{
		JsonSerializer serializer = new()
		{
			Converters = new JsonConverterCollection(new JsonConverter[] { new UpperCaseStringConverter() }),
		};

		string json = serializer.Serialize("Ada");
		string roundTripped = serializer.Deserialize<string>(json);

		Assert.Equal("\"ADA\"", json);
		Assert.Equal("ada", roundTripped);
	}

	[Test]
	public void SerializeDeserialize_CanUseRuntimeRegisteredOpenGenericConverterType()
	{
		JsonSerializer serializer = new()
		{
			ConverterTypes = new JsonConverterTypeCollection(new[] { typeof(GenericValueConverter<>) }),
		};

		GenericValue<string> value = new() { Value = "Ada" };

		string json = serializer.Serialize(value);
		GenericValue<string> roundTripped = serializer.Deserialize<GenericValue<string>>(json);

		Assert.Equal("{\"value\":\"Ada!\"}", json);
		Assert.Equal("Ada", roundTripped.Value);
	}

	[Test]
	public void SerializeDeserialize_CanUseRuntimeRegisteredConverterFactory()
	{
		JsonSerializer serializer = new()
		{
			ConverterFactories = new IJsonConverterFactory[] { new CharListWrapperFactory() },
		};

		List<char> value = ['a', 'b', 'c'];

		string json = serializer.Serialize(value);
		List<char> roundTripped = serializer.Deserialize<List<char>>(json);

		Assert.Equal("[[\"a\",\"b\",\"c\"]]", json);
		Assert.Equal(value, roundTripped);
	}

	private sealed class UpperCaseStringConverter : JsonConverter<string>
	{
		public override void Write(ref JsonWriter writer, string? value, JsonSerializer serializer)
		{
			writer.WriteStringValue(value?.ToUpperInvariant());
		}

		public override string? Read(ref JsonReader reader, JsonSerializer serializer)
		{
			string? value = reader.ReadString();
			return value?.ToLowerInvariant();
		}
	}

	private sealed class GenericValue<T>
	{
		public string? Value { get; set; }
	}

	private sealed class GenericValueConverter<T> : JsonConverter<GenericValue<T>>
	{
		public override void Write(ref JsonWriter writer, GenericValue<T>? value, JsonSerializer serializer)
		{
			if (value is null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName("value");
			writer.WriteStringValue(value.Value + "!");
			writer.WriteEndObject();
		}

		public override GenericValue<T>? Read(ref JsonReader reader, JsonSerializer serializer)
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

	private sealed class CharListWrapperFactory : IJsonConverterFactory
	{
		public JsonConverter? CreateConverter(Type type, ITypeShape? shape, in JsonConverterFactoryContext context)
			=> type == typeof(List<char>) ? new CharListWrapperConverter(context.GetConverter<char>()) : null;
	}

	private sealed class CharListWrapperConverter(JsonConverter<char> elementConverter) : JsonConverter<List<char>>
	{
		public override void Write(ref JsonWriter writer, List<char>? value, JsonSerializer serializer)
		{
			if (value is null)
			{
				writer.WriteNullValue();
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

				elementConverter.Write(ref writer, value[i], serializer);
			}

			writer.WriteEndArray();
			writer.WriteEndArray();
		}

		public override List<char>? Read(ref JsonReader reader, JsonSerializer serializer)
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

				result.Add(elementConverter.Read(ref reader, serializer));
				first = false;
			}

			reader.ReadEndArray();
			return result;
		}
	}
}
