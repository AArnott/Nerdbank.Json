// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nerdbank.Json;
using Nerdbank.MessagePack;
using PolyType;

public partial class JsonObjectSerializerTests
{
	[Test]
	public void SerializeDeserialize_ObjectGraph()
	{
		JsonSerializer serializer = new();
		Person value = new()
		{
			Name = "Ada",
			Age = 37,
			Address = new Address
			{
				City = "Seattle",
				PostalCode = 98101,
			},
		};

		string json = serializer.Serialize(value);
		Assert.Equal("{\"name\":\"Ada\",\"age\":37,\"address\":{\"city\":\"Seattle\",\"postalCode\":98101}}", json);

		AssertRoundtrip(json, serializer, value);
	}

	[Test]
	public void Deserialize_ObjectGraph_IgnoresUnknownProperty()
	{
		JsonSerializer serializer = new();

		Person value = serializer.Deserialize<Person>("{\"name\":\"Ada\",\"unknown\":true,\"age\":37,\"address\":{\"city\":\"Seattle\",\"postalCode\":98101,\"ignored\":\"x\"}}");
		Person expected = new()
		{
			Name = "Ada",
			Age = 37,
			Address = new Address { City = "Seattle", PostalCode = 98101 },
		};

		AssertStructuralEqual(expected, value, "{\"name\":\"Ada\",\"unknown\":true,\"age\":37,\"address\":{\"city\":\"Seattle\",\"postalCode\":98101,\"ignored\":\"x\"}}");
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithNullNestedObject()
	{
		JsonSerializer serializer = new();
		Person value = new() { Name = "Ada", Age = 37, Address = null };

		string json = serializer.Serialize(value);
		Assert.Equal("{\"name\":\"Ada\",\"age\":37,\"address\":null}", json);

		AssertRoundtrip(json, serializer, value);
	}

	[Test]
	public void SerializeDeserialize_Stream()
	{
		JsonSerializer serializer = new();
		Person value = new() { Name = "Grace", Age = 41 };

		using MemoryStream stream = new();
		serializer.Serialize(stream, value);
		stream.Position = 0;

		Person roundTripped = serializer.Deserialize<Person>(stream);
		AssertStructuralEqual(value, roundTripped, serializer.Serialize(value));
	}

	[Test]
	public async Task SerializeDeserialize_StreamAsync()
	{
		JsonSerializer serializer = new();
		Person value = new() { Name = "Katherine", Age = 35 };
		CancellationToken cancellationToken = TUnit.Core.TestContext.Current?.Execution.CancellationToken ?? default;

		using MemoryStream stream = new();
		await serializer.SerializeAsync(stream, value, cancellationToken);
		stream.Position = 0;

		Person roundTripped = await serializer.DeserializeAsync<Person>(stream, cancellationToken);
		AssertStructuralEqual(value, roundTripped, serializer.Serialize(value));
	}

	[Test]
	public void Serialize_ObjectGraph_CanDisableNamingPolicy()
	{
		JsonSerializer serializer = new() { PropertyNamingPolicy = null };
		Person value = new() { Name = "Ada", Age = 37 };

		string json = serializer.Serialize(value);

		Assert.Equal("{\"Name\":\"Ada\",\"Age\":37,\"Address\":null}", json);
	}

	[Test]
	public void Serialize_ObjectGraph_ExplicitPropertyNameOverridesNamingPolicy()
	{
		JsonSerializer serializer = new();
		RenamedPropertyContainer value = new() { URLValue = "https://example.com" };

		string json = serializer.Serialize(value);

		Assert.Equal("{\"uri\":\"https://example.com\"}", json);
		AssertRoundtrip(json, serializer, value);
	}

	[Test]
	public void Deserialize_ObjectGraph_MissingRequiredProperty_ThrowsFormatException()
	{
		JsonSerializer serializer = new();

		FormatException exception = Assert.Throws<FormatException>(() => serializer.Deserialize<RequiredPropertyContainer>("{}"));
		Assert.Contains("Name", exception.Message);
	}

	[Test]
	public void Deserialize_ObjectGraph_NullForNonNullableProperty_ThrowsFormatException()
	{
		JsonSerializer serializer = new();

		FormatException exception = Assert.Throws<FormatException>(() => serializer.Deserialize<NonNullablePropertyContainer>("{\"name\":null}"));
		Assert.Contains("Name", exception.Message);
	}

	private static void AssertRoundtrip<T>(string json, JsonSerializer serializer, T expected)
	{
		T actual = serializer.Deserialize<T>(json);
		Assert.True(GetStructuralEqualityComparer<T>().Equals(expected, actual), $"Round-trip mismatch for serialized JSON: {json}");
	}

	private static void AssertStructuralEqual<T>(T expected, T actual, string json)
	{
		Assert.True(GetStructuralEqualityComparer<T>().Equals(expected, actual), $"Round-trip mismatch for serialized JSON: {json}");
	}

	private static IEqualityComparer<T> GetStructuralEqualityComparer<T>()
	{
		ITypeShape<T> shape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<T>() ?? throw new InvalidOperationException($"No generated type shape found for {typeof(T)}.");
		return StructuralEqualityComparer.GetDefault(shape);
	}

	[GenerateShape]
	internal partial class Person
	{
		public string? Name { get; set; }

		public int Age { get; set; }

		public Address? Address { get; set; }
	}

	[GenerateShape]
	internal partial class Address
	{
		public string? City { get; set; }

		public int PostalCode { get; set; }
	}

	[GenerateShape]
	internal partial class RenamedPropertyContainer
	{
		[PropertyShape(Name = "uri")]
		public string? URLValue { get; set; }
	}

	[GenerateShape]
	internal partial class RequiredPropertyContainer
	{
		public required string Name { get; set; }
	}

	[GenerateShape]
	internal partial class NonNullablePropertyContainer
	{
		public string Name { get; set; } = string.Empty;
	}
}
