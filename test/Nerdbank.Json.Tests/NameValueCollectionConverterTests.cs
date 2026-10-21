// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Specialized;

[GenerateShapeFor<NameValueCollection>]
public partial class NameValueCollectionConverterTests
{
	[Test]
	public void NullSingleAndMultipleValuesRoundTrip()
	{
		JsonSerializer serializer = new JsonSerializer().WithNameValueCollectionConverter();
		NameValueCollection value = new()
		{
			{ "null", null },
			{ "single", "a" },
			{ "multiple", "b" },
			{ "multiple", "c" },
		};
		string json = serializer.Serialize<NameValueCollection, NameValueCollectionConverterTests>(value);
		Assert.Equal("""{"null":null,"single":"a","multiple":["b","c"]}""", json);
		NameValueCollection roundTrip = serializer.Deserialize<NameValueCollection, NameValueCollectionConverterTests>(json)!;
		Assert.Equal(value.AllKeys, roundTrip.AllKeys);
		foreach (string key in value.AllKeys!)
		{
			Assert.Equal(value.GetValues(key), roundTrip.GetValues(key));
		}

		Assert.Null(serializer.Deserialize<NameValueCollection, NameValueCollectionConverterTests>("null"));
		Assert.Equal("null", serializer.Serialize<NameValueCollection, NameValueCollectionConverterTests>(null));
		Assert.Empty(serializer.Deserialize<NameValueCollection, NameValueCollectionConverterTests>("{}")!.AllKeys);
		Assert.Null(serializer.Deserialize<NameValueCollection, NameValueCollectionConverterTests>("""{"empty":[]}""")!.GetValues("empty"));
	}

	[Test]
	public void ConfiguredStringConverterIsUsed()
	{
		JsonSerializer serializer = new JsonSerializer
		{
			Converters = [new JsonObjectSerializerTests.UpperCaseStringConverter()],
		}.WithNameValueCollectionConverter();
		Assert.Equal("""{"key":"VALUE"}""", serializer.Serialize<NameValueCollection, NameValueCollectionConverterTests>(new() { { "key", "Value" } }));
		Assert.Equal("value", serializer.Deserialize<NameValueCollection, NameValueCollectionConverterTests>("""{"key":"VALUE"}""")!["key"]);
	}

	[Test]
	public void DuplicateRegistrationAndInvalidInput()
	{
		JsonSerializer serializer = new JsonSerializer().WithNameValueCollectionConverter();
		Assert.Throws<ArgumentException>(() => serializer.WithNameValueCollectionConverter());
		Assert.Throws<JsonSerializationException>(() => serializer.Serialize<NameValueCollection, NameValueCollectionConverterTests>(new() { { null, "value" } }));
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize<NameValueCollection, NameValueCollectionConverterTests>("""{"key":1}"""));
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize<NameValueCollection, NameValueCollectionConverterTests>("""{"key":"one","key":"two"}"""));
	}
}
