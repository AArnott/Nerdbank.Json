// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

public partial class JsonObjectSerializerTests
{
	[Test]
	public void Deserialize_ObjectGraph_FromMultiSegmentUtf8Sequence()
	{
		JsonSerializer serializer = new();
		byte[] utf8 = Encoding.UTF8.GetBytes("{\"name\":\"München\",\"age\":37,\"address\":null}");
		int splitIndex = Array.IndexOf(utf8, (byte)0xC3);
		Assert.True(splitIndex > 0);

		ReadOnlySequence<byte> json = CreateSequence(
			utf8.AsMemory(0, splitIndex + 1),
			utf8.AsMemory(splitIndex + 1, 1),
			utf8.AsMemory(splitIndex + 2));

		Person? value = serializer.Deserialize<Person>(json);
		Person expected = new() { Name = "München", Age = 37, Address = null };

		AssertStructuralEqual(expected, value, Encoding.UTF8.GetString(utf8));
	}

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
	public void SerializeDeserialize_ObjectGraph_AsObject()
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
		ITypeShape shape = GetTypeShape<Person>();

		string json = serializer.SerializeObject(value, shape);
		Assert.Equal("{\"name\":\"Ada\",\"age\":37,\"address\":{\"city\":\"Seattle\",\"postalCode\":98101}}", json);

		AssertStructuralEqual(value, Assert.IsType<Person>(serializer.DeserializeObject(json, shape)), json);

		using MemoryStream stream = new();
		serializer.SerializeObject(stream, value, shape);
		stream.Position = 0;
		AssertStructuralEqual(value, Assert.IsType<Person>(serializer.DeserializeObject(stream, shape)), json);

		byte[] utf8 = Encoding.UTF8.GetBytes(json);
		AssertStructuralEqual(value, Assert.IsType<Person>(serializer.DeserializeObject(utf8, shape)), json);

		ReadOnlySequence<byte> sequence = CreateSequence(utf8.AsMemory(0, 10), utf8.AsMemory(10));
		AssertStructuralEqual(value, Assert.IsType<Person>(serializer.DeserializeObject(sequence, shape)), json);
	}

	[Test]
	public void Deserialize_ObjectGraph_IgnoresUnknownProperty()
	{
		JsonSerializer serializer = new();

		Person? value = serializer.Deserialize<Person>("{\"name\":\"Ada\",\"unknown\":true,\"age\":37,\"address\":{\"city\":\"Seattle\",\"postalCode\":98101,\"ignored\":\"x\"}}");
		Assert.NotNull(value);
		Person expected = new()
		{
			Name = "Ada",
			Age = 37,
			Address = new Address { City = "Seattle", PostalCode = 98101 },
		};

		AssertStructuralEqual(expected, value, "{\"name\":\"Ada\",\"unknown\":true,\"age\":37,\"address\":{\"city\":\"Seattle\",\"postalCode\":98101,\"ignored\":\"x\"}}");
	}

	[Test]
	public void Deserialize_ObjectGraph_CanCaptureUnknownPropertiesIntoExtensionData()
	{
		JsonSerializer serializer = new();

		ExtensionDataPerson? value = serializer.Deserialize<ExtensionDataPerson>("{\"name\":\"Ada\",\"unknown\":true,\"extra\":{\"nested\":5}}\n");

		Assert.NotNull(value);
		Assert.Equal("Ada", value.Name);
		Assert.NotNull(value.ExtensionData);
		Assert.Equal("true", value.ExtensionData!["unknown"]);
		Assert.Equal("{\"nested\":5}", value.ExtensionData["extra"]);
	}

	[Test]
	public void Serialize_ObjectGraph_WritesExtensionDataMembers()
	{
		JsonSerializer serializer = new();
		ExtensionDataPerson value = new()
		{
			Name = "Ada",
			ExtensionData = new Dictionary<string, string>
			{
				["unknown"] = "true",
				["extra"] = "{\"nested\":5}",
			},
		};

		string json = serializer.Serialize(value);

		Assert.Equal("{\"name\":\"Ada\",\"unknown\":true,\"extra\":{\"nested\":5}}", json);
	}

	[Test]
	public void Deserialize_ObjectGraph_CaseInsensitivePropertyNames_WhenEnabled()
	{
		JsonSerializer serializer = new() { PropertyNameCaseInsensitive = true };

		Person? value = serializer.Deserialize<Person>("{\"NAME\":\"Ada\",\"AGE\":37,\"ADDRESS\":{\"CITY\":\"Seattle\",\"POSTALCODE\":98101}}");
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

		Person? roundTripped = serializer.Deserialize<Person>(stream);
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
		JsonSerializer serializer = new() { SerializeDefaultValues = SerializeDefaultValuesPolicy.Never };
		Person value = new() { Name = "Ada", Age = 0, Address = null };

		string json = serializer.Serialize(value);

		Assert.Equal("{\"name\":\"Ada\"}", json);
	}

	[Test]
	public void Serialize_ObjectGraph_CanRetainReferenceTypeDefaultsOnly()
	{
		JsonSerializer serializer = new() { SerializeDefaultValues = SerializeDefaultValuesPolicy.ReferenceTypes };
		Person value = new() { Name = "Ada", Age = 0, Address = null };

		string json = serializer.Serialize(value);

		Assert.Equal("{\"name\":\"Ada\",\"address\":null}", json);
	}

	[Test]
	public void Serialize_ObjectGraph_CanRetainValueTypeDefaultsOnly()
	{
		JsonSerializer serializer = new() { SerializeDefaultValues = SerializeDefaultValuesPolicy.ValueTypes };
		Person value = new() { Name = null, Age = 0, Address = null };

		string json = serializer.Serialize(value);

		Assert.Equal("{\"age\":0}", json);
	}

	[Test]
	public void Serialize_ObjectGraph_RequiredPropertiesAreRetainedWhenRequested()
	{
		JsonSerializer serializer = new() { SerializeDefaultValues = SerializeDefaultValuesPolicy.Required };
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
		JsonSerializer serializer = new() { DeserializeDefaultValues = DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties };

		RequiredPropertyContainer? value = serializer.Deserialize<RequiredPropertyContainer>("{}");

		Assert.NotNull(value);
		Assert.Null(value.Name);
	}

	[Test]
	public void Deserialize_ObjectGraph_NullForNonNullableProperty_CanBeAllowed()
	{
		JsonSerializer serializer = new() { DeserializeDefaultValues = DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties };

		NonNullablePropertyContainer? value = serializer.Deserialize<NonNullablePropertyContainer>("{\"name\":null}");

		Assert.NotNull(value);
		Assert.Null(value.Name);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_PreserveReferences()
	{
		JsonSerializer serializer = new() { PreserveReferences = ReferencePreservationMode.RejectCycles };
		SharedLeaf sharedLeaf = new() { Name = "Ada" };
		SharedRoot value = new() { Left = sharedLeaf, Right = sharedLeaf };

		string json = serializer.Serialize(value);
		Assert.Equal("{\"$id\":1,\"$value\":{\"left\":{\"$id\":2,\"$value\":{\"name\":\"Ada\"}},\"right\":{\"$ref\":2}}}", json);

		SharedRoot? roundTripped = serializer.Deserialize<SharedRoot>(json);
		Assert.NotNull(roundTripped?.Left);
		Assert.Same(roundTripped.Left, roundTripped.Right);
		Assert.Equal("Ada", roundTripped.Left.Name);
	}

	[Test]
	public void Serialize_ObjectGraph_RejectsReferenceCycles()
	{
		JsonSerializer serializer = new() { PreserveReferences = ReferencePreservationMode.RejectCycles };
		CyclicNode node = new();
		node.Next = node;

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => serializer.Serialize(node));
		Assert.Contains("Reference cycles", exception.Message);
	}

	private static void AssertRoundtrip<T>(string json, JsonSerializer serializer, T expected)
	{
		ITypeShape<T> shape = GetTypeShape<T>();
		T? actual = serializer.Deserialize(json, shape);
		Assert.True(GetStructuralEqualityComparer<T?>().Equals(expected, actual), $"Round-trip mismatch for serialized JSON: {json}");
	}

	private static void AssertStructuralEqual<T>(T expected, T actual, string json)
	{
		Assert.True(GetStructuralEqualityComparer<T>().Equals(expected, actual), $"Round-trip mismatch for serialized JSON: {json}");
	}

	private static IEqualityComparer<T> GetStructuralEqualityComparer<T>()
	{
		ITypeShape<T> shape = GetTypeShape<T>();
		return Nerdbank.MessagePack.StructuralEqualityComparer.GetDefault(shape);
	}

	private static ITypeShape<T> GetTypeShape<T>()
		=> PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<T>() ?? throw new InvalidOperationException($"No generated type shape found for {typeof(T)}.");

	private static ReadOnlySequence<byte> CreateSequence(params ReadOnlyMemory<byte>[] segments)
	{
		Assert.NotEmpty(segments);
		Segment? first = null;
		Segment? last = null;

		foreach (ReadOnlyMemory<byte> segment in segments)
		{
			Segment current = new(segment);
			if (first is null)
			{
				first = current;
			}
			else
			{
				last!.SetNext(current);
			}

			last = current;
		}

		return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
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
	internal partial class ExtensionDataPerson
	{
		public string? Name { get; set; }

		[JsonExtensionData]
		public Dictionary<string, string>? ExtensionData { get; set; }
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

	private sealed class Segment : ReadOnlySequenceSegment<byte>
	{
		internal Segment(ReadOnlyMemory<byte> memory)
		{
			this.Memory = memory;
		}

		internal void SetNext(Segment next)
		{
			next.RunningIndex = this.RunningIndex + this.Memory.Length;
			this.Next = next;
		}
	}
}
