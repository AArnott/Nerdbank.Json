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
	public void Deserialize_ObjectGraph_CaseInsensitivePropertyNames_WhenEnabled()
	{
		JsonSerializer serializer = new() { PropertyNameCaseInsensitive = true };

		Person value = serializer.Deserialize<Person>("{\"NAME\":\"Ada\",\"AGE\":37,\"ADDRESS\":{\"CITY\":\"Seattle\",\"POSTALCODE\":98101}}");
		Person expected = new()
		{
			Name = "Ada",
			Age = 37,
			Address = new Address { City = "Seattle", PostalCode = 98101 },
		};

		AssertStructuralEqual(expected, value, "{\"NAME\":\"Ada\",\"AGE\":37,\"ADDRESS\":{\"CITY\":\"Seattle\",\"POSTALCODE\":98101}}");
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
	public void Serialize_ObjectGraph_CanIndentOutput()
	{
		JsonSerializer serializer = new() { WriteIndented = true };
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

		Assert.Equal("{\n  \"name\": \"Ada\",\n  \"age\": 37,\n  \"address\": {\n    \"city\": \"Seattle\",\n    \"postalCode\": 98101\n  }\n}", json);
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
	public void Serialize_ObjectGraph_CanOmitDefaultValues()
	{
		JsonSerializer serializer = new() { SerializeDefaultValues = Nerdbank.Json.SerializeDefaultValuesPolicy.Never };
		Person value = new() { Name = "Ada", Age = 0, Address = null };

		string json = serializer.Serialize(value);

		Assert.Equal("{\"name\":\"Ada\"}", json);
	}

	[Test]
	public void Serialize_ObjectGraph_CanRetainReferenceTypeDefaultsOnly()
	{
		JsonSerializer serializer = new() { SerializeDefaultValues = Nerdbank.Json.SerializeDefaultValuesPolicy.ReferenceTypes };
		Person value = new() { Name = "Ada", Age = 0, Address = null };

		string json = serializer.Serialize(value);

		Assert.Equal("{\"name\":\"Ada\",\"address\":null}", json);
	}

	[Test]
	public void Serialize_ObjectGraph_CanRetainValueTypeDefaultsOnly()
	{
		JsonSerializer serializer = new() { SerializeDefaultValues = Nerdbank.Json.SerializeDefaultValuesPolicy.ValueTypes };
		Person value = new() { Name = null, Age = 0, Address = null };

		string json = serializer.Serialize(value);

		Assert.Equal("{\"age\":0}", json);
	}

	[Test]
	public void Serialize_ObjectGraph_RequiredPropertiesAreRetainedWhenRequested()
	{
		JsonSerializer serializer = new() { SerializeDefaultValues = Nerdbank.Json.SerializeDefaultValuesPolicy.Required };
		RequiredDefaultValueContainer value = new() { Count = 0, Name = null };

		string json = serializer.Serialize(value);

		Assert.Equal("{\"count\":0}", json);
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

	[Test]
	public void Deserialize_ObjectGraph_MissingRequiredProperty_CanBeAllowed()
	{
		JsonSerializer serializer = new() { DeserializeDefaultValues = Nerdbank.Json.DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties };

		RequiredPropertyContainer value = serializer.Deserialize<RequiredPropertyContainer>("{}");

		Assert.Null(value.Name);
	}

	[Test]
	public void Deserialize_ObjectGraph_NullForNonNullableProperty_CanBeAllowed()
	{
		JsonSerializer serializer = new() { DeserializeDefaultValues = Nerdbank.Json.DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties };

		NonNullablePropertyContainer value = serializer.Deserialize<NonNullablePropertyContainer>("{\"name\":null}");

		Assert.Null(value.Name);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_PreserveReferences()
	{
		JsonSerializer serializer = new() { PreserveReferences = Nerdbank.Json.ReferencePreservationMode.RejectCycles };
		SharedLeaf sharedLeaf = new() { Name = "Ada" };
		SharedRoot value = new() { Left = sharedLeaf, Right = sharedLeaf };

		string json = serializer.Serialize(value);
		Assert.Equal("{\"$id\":1,\"$value\":{\"left\":{\"$id\":2,\"$value\":{\"name\":\"Ada\"}},\"right\":{\"$ref\":2}}}", json);

		SharedRoot roundTripped = serializer.Deserialize<SharedRoot>(json);
		Assert.NotNull(roundTripped.Left);
		Assert.Same(roundTripped.Left, roundTripped.Right);
		Assert.Equal("Ada", roundTripped.Left.Name);
	}

	[Test]
	public void Serialize_ObjectGraph_RejectsReferenceCycles()
	{
		JsonSerializer serializer = new() { PreserveReferences = Nerdbank.Json.ReferencePreservationMode.RejectCycles };
		CyclicNode node = new();
		node.Next = node;

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => serializer.Serialize(node));
		Assert.Contains("Reference cycles", exception.Message);
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

	[GenerateShape]
	internal partial class RequiredDefaultValueContainer
	{
		public required int Count { get; set; }

		public string? Name { get; set; }
	}

	[GenerateShape]
	internal partial class SharedRoot
	{
		public SharedLeaf? Left { get; set; }

		public SharedLeaf? Right { get; set; }
	}

	[GenerateShape]
	internal partial class SharedLeaf
	{
		public string? Name { get; set; }
	}

	[GenerateShape]
	internal partial class CyclicNode
	{
		public CyclicNode? Next { get; set; }
	}
}
