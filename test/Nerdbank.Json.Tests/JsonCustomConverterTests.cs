// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Json;

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
}