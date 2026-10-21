// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0
[GenerateShapeFor<Envelope<Inner>>]
public partial class CustomConverterSamplesTests
{
	[Test]
	public void TypeAttribute_UsesConverterForTheDeclaredType()
	{
		JsonSerializer serializer = new();
		Assert.Equal("""{"name":"ADA"}""", serializer.Serialize(new Foo { Name = "Ada" }));
		Assert.Equal("ada", serializer.Deserialize<Foo>("""{"name":"ADA"}""")!.Name);
		Assert.Equal("null", serializer.Serialize<Foo>(null));
		Assert.Null(serializer.Deserialize<Foo>("null"));
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => serializer.Deserialize<Foo>("""{"wrong":"ADA"}"""));
		Assert.Equal(JsonSerializationException.ErrorCode.UnexpectedToken, error.Code);
	}

	[Test]
	public void MemberAndParameterAttributes_UseTheSampleConverter()
	{
		JsonSerializer serializer = new();
		MemberAttributedContainer member = new() { Value = new Foo { Name = "Ada" } };
		Assert.Equal("""{"value":{"name":"ADA"}}""", serializer.Serialize(member));
		Assert.Equal("ada", serializer.Deserialize<MemberAttributedContainer>("""{"value":{"name":"ADA"}}""")!.Value!.Name);
		Assert.Equal("ada", serializer.Deserialize<ParameterAttributedContainer>("""{"value":{"name":"ADA"}}""")!.Value!.Name);
	}

	[Test]
	public async Task RuntimeRegistrationAndDelegation_UseCurrentApis()
	{
		JsonSerializer serializer = new() { Converters = [new WrapperConverter()] };
		Wrapper value = new() { Value = new Inner { Name = "Ada" } };
		const string Json = """{"value":{"name":"Ada"}}""";
		Assert.Equal(Json, serializer.Serialize(value));
		Assert.Equal("Ada", serializer.Deserialize<Wrapper>(Json)!.Value!.Name);
		using MemoryStream stream = new();
		await serializer.SerializeAsync(stream, value);
		stream.Position = 0;
		Assert.Equal("Ada", (await serializer.DeserializeAsync<Wrapper>(stream))!.Value!.Name);
	}

	[Test]
	public void OpenGenericAndFactoryRegistrations_UseTheSampleConverters()
	{
		Envelope<Inner> value = new() { Value = new Inner { Name = "Ada" } };
		JsonSerializer generic = new() { ConverterTypes = [typeof(EnvelopeConverter<>)] };
		JsonSerializer factory = new() { ConverterFactories = [new EnvelopeConverterFactory()] };
		foreach (JsonSerializer serializer in new[] { generic, factory })
		{
			string json = serializer.Serialize<Envelope<Inner>, CustomConverterSamplesTests>(value);
			Assert.Equal("""{"value":{"name":"Ada"}}""", json);
			Assert.Equal("Ada", serializer.Deserialize<Envelope<Inner>, CustomConverterSamplesTests>(json)!.Value!.Name);
		}
	}
}
#endif
