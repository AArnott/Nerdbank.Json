// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using PolyType.Abstractions;

public partial class JsonObjectSerializerTests : TestBase
{
#if NET
	[Test]
	[NotInParallel]
	public void Serialize_CachedObjectGraph_DoesNotAllocate()
	{
		Person value = new()
		{
			Name = "Ada",
			Age = 37,
			Address = new Address { City = "Seattle", PostalCode = 98101 },
		};
		ITypeShape<Person> shape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Person>()!;
		ArrayBufferWriter<byte> buffer = new();
		const string ExpectedJson = """{"name":"Ada","age":37,"address":{"city":"Seattle","postalCode":98101}}""";
		for (int i = 0; i < 1000; i++)
		{
			buffer.Clear();
			this.Serializer.Serialize(buffer, value, shape);
		}

		Assert.Equal(ExpectedJson, Encoding.UTF8.GetString(buffer.WrittenSpan));
		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
		{
			buffer.Clear();
			this.Serializer.Serialize(buffer, value, shape);
		}

		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.Equal(0L, allocated);
		Assert.Equal(ExpectedJson, Encoding.UTF8.GetString(buffer.WrittenSpan));
	}
#endif

	[Test]
	public void Deserialize_ObjectGraph_FromMultiSegmentUtf8Sequence()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"name":"München","age":37,"address":null}""");
		int splitIndex = Array.IndexOf(utf8, (byte)0xC3);
		Assert.True(splitIndex > 0);

		ReadOnlySequence<byte> json = CreateSequence(
			utf8.AsMemory(0, splitIndex + 1),
			utf8.AsMemory(splitIndex + 1, 1),
			utf8.AsMemory(splitIndex + 2));

		Person value = this.Serializer.Deserialize<Person>(json)!;
		Person expected = new() { Name = "München", Age = 37, Address = null };

		AssertStructuralEqual<Person>(expected, value);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph()
	{
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

		this.AssertRoundtrip(value, """{"name":"Ada","age":37,"address":{"city":"Seattle","postalCode":98101}}""");
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_AsObject()
	{
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
#if NET
		ITypeShape<Person> shape = TypeShapeResolver.Resolve<Person>();
#else
		ITypeShape<Person> shape = TypeShapeResolver.ResolveDynamicOrThrow<Person>();
#endif

		string json = this.Serializer.SerializeObject(value, shape);
		Assert.Equal("""{"name":"Ada","age":37,"address":{"city":"Seattle","postalCode":98101}}""", json);

		AssertStructuralEqual(value, Assert.IsType<Person>(this.Serializer.DeserializeObject(json, shape)), shape);

		using MemoryStream stream = new();
		this.Serializer.SerializeObject(stream, value, shape);
		stream.Position = 0;
		AssertStructuralEqual(value, Assert.IsType<Person>(this.Serializer.DeserializeObject(stream, shape)), shape);

		byte[] utf8 = Encoding.UTF8.GetBytes(json);
		AssertStructuralEqual(value, Assert.IsType<Person>(this.Serializer.DeserializeObject(utf8, shape)), shape);

		ReadOnlySequence<byte> sequence = CreateSequence(utf8.AsMemory(0, 10), utf8.AsMemory(10));
		AssertStructuralEqual(value, Assert.IsType<Person>(this.Serializer.DeserializeObject(sequence, shape)), shape);
	}

	[Test]
	public void Deserialize_ObjectGraph_IgnoresUnknownProperty()
	{
		Person? value = this.Serializer.Deserialize<Person>("""{"name":"Ada","unknown":true,"age":37,"address":{"city":"Seattle","postalCode":98101,"ignored":"x"}}""");
		Assert.NotNull(value);
		Person expected = new()
		{
			Name = "Ada",
			Age = 37,
			Address = new Address { City = "Seattle", PostalCode = 98101 },
		};

		AssertStructuralEqual(expected, value);
	}

	[Test]
	[Arguments("{}")]
	[Arguments("""{"unknown":1}""")]
	public void Deserialize_EmptyObjectGraph_IgnoresUnknownProperty(string json)
	{
		EmptyContainer? value = this.Serializer.Deserialize<EmptyContainer>(json);

		Assert.NotNull(value);
	}

	[Test]
	[Arguments("""{"Aa":1,"BB":2,"Ua":3,"café":4}""")]
	[Arguments("""{"café":4,"C#":{"nested":[1,2]},"Ua":3,"BB":2,"Aa":1}""")]
	[Arguments("""{"caf\u00e9":4,"Ua":3,"B\u0042":2,"\u0041a":1,"C#":false}""")]
	public void Deserialize_ObjectGraph_PowerOfTwoPropertyCount_ResolvesCollisions(string json)
	{
		this.Serializer = new() { PropertyNamingPolicy = null };
		PowerOfTwoPropertyContainer? value = this.Serializer.Deserialize<PowerOfTwoPropertyContainer>(Encoding.UTF8.GetBytes(json));

		Assert.NotNull(value);
		Assert.Equal(1, value.First);
		Assert.Equal(2, value.Second);
		Assert.Equal(3, value.Third);
		Assert.Equal(4, value.Unicode);
	}

	[Test]
	[Arguments("""{"Aa":1,"BB":2,"Ua":3,"café":4,"abcde":5}""")]
	[Arguments("""{"abcde":5,"café":4,"C#":{"nested":[1,2]},"Ua":3,"BB":2,"Aa":1}""")]
	[Arguments("""{"abcde":5,"caf\u00e9":4,"Ua":3,"B\u0042":2,"\u0041a":1,"C#":false}""")]
	public void Deserialize_ObjectGraph_NonPowerOfTwoPropertyCount_ResolvesCollisions(string json)
	{
		this.Serializer = new() { PropertyNamingPolicy = null };
		NonPowerOfTwoPropertyContainer? value = this.Serializer.Deserialize<NonPowerOfTwoPropertyContainer>(Encoding.UTF8.GetBytes(json));

		Assert.NotNull(value);
		Assert.Equal(1, value.First);
		Assert.Equal(2, value.Second);
		Assert.Equal(3, value.Third);
		Assert.Equal(4, value.Unicode);
		Assert.Equal(5, value.NegativeHash);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Deserialize_ObjectGraph_CollidingPropertyNames_CaseInsensitiveFallback(bool escapedNames)
	{
		this.Serializer = new() { PropertyNamingPolicy = null, PropertyNameCaseInsensitive = true };
		string json = escapedNames
			? """{"ABC\u0044E":5,"CAF\u00e9":4,"uA":3,"b\u0062":2,"\u0061A":1,"C#":false}"""
			: """{"ABCDE":5,"CAFé":4,"uA":3,"bb":2,"aA":1,"C#":false}""";
		byte[] utf8 = Encoding.UTF8.GetBytes(json);

		PowerOfTwoPropertyContainer? powerOfTwo = this.Serializer.Deserialize<PowerOfTwoPropertyContainer>(utf8);
		NonPowerOfTwoPropertyContainer? nonPowerOfTwo = this.Serializer.Deserialize<NonPowerOfTwoPropertyContainer>(utf8);

		Assert.NotNull(powerOfTwo);
		Assert.Equal(1, powerOfTwo.First);
		Assert.Equal(2, powerOfTwo.Second);
		Assert.Equal(3, powerOfTwo.Third);
		Assert.Equal(4, powerOfTwo.Unicode);
		Assert.NotNull(nonPowerOfTwo);
		Assert.Equal(1, nonPowerOfTwo.First);
		Assert.Equal(2, nonPowerOfTwo.Second);
		Assert.Equal(3, nonPowerOfTwo.Third);
		Assert.Equal(4, nonPowerOfTwo.Unicode);
		Assert.Equal(5, nonPowerOfTwo.NegativeHash);
	}

	[Test]
	[Arguments("""{"unknown":false,"uri":"value"}""")]
	[Arguments("""{"\u0075ri":"value","unknown":false}""")]
	public void Deserialize_ObjectGraph_SingleProperty_ResolvesName(string json)
	{
		RenamedPropertyContainer? value = this.Serializer.Deserialize<RenamedPropertyContainer>(Encoding.UTF8.GetBytes(json));

		Assert.NotNull(value);
		Assert.Equal("value", value.URLValue);
	}

	[Test]
	public void Deserialize_ObjectGraph_FromUtf8WithBom()
	{
		byte[] utf8Json = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("""{"name":"Ada","age":37,"address":null}""")];

		Person? value = this.Serializer.Deserialize<Person>(utf8Json);

		Assert.NotNull(value);
		Assert.Equal("Ada", value.Name);
		Assert.Equal(37, value.Age);
		Assert.Null(value.Address);
	}

	[Test]
	public void Deserialize_ObjectGraph_CanCaptureUnknownPropertiesIntoExtensionData()
	{
		ExtensionDataPerson? value = this.Serializer.Deserialize<ExtensionDataPerson>("""{"name":"Ada","unknown":true,"extra":{"nested":5}}""" + "\n");

		Assert.NotNull(value);
		Assert.Equal("Ada", value.Name);
		Assert.NotNull(value.ExtensionData);
		Assert.Equal("""{"name":"Ada","unknown":true,"extra":{"nested":5}}""", this.Serializer.Serialize(value));
	}

	[Test]
	public void Deserialize_ObjectGraph_DuplicateProperty_ThrowsJsonSerializationException()
	{
		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<Person>("""{"name":"Ada","name":"Grace","age":37,"address":null}"""));
		Assert.Equal(JsonSerializationException.ErrorCode.DoublePropertyAssignment, exception.Code);
		Assert.Contains("name", exception.Message, System.StringComparison.OrdinalIgnoreCase);
	}

	[Test]
	public void Deserialize_ObjectGraph_DuplicateUnknownProperty_ThrowsJsonSerializationException()
	{
		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<Person>("""{"name":"Ada","unknown":true,"unknown":false,"age":37,"address":null}"""));
		Assert.Equal(JsonSerializationException.ErrorCode.DoublePropertyAssignment, exception.Code);
		Assert.Contains("unknown", exception.Message, System.StringComparison.OrdinalIgnoreCase);
	}

	[Test]
	public void Deserialize_ObjectGraph_DuplicateProperty_CaseInsensitive_ThrowsJsonSerializationException()
	{
		this.Serializer = new() { PropertyNameCaseInsensitive = true };

		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<Person>("""{"Name":"Ada","name":"Grace","age":37,"address":null}"""));
		Assert.Equal(JsonSerializationException.ErrorCode.DoublePropertyAssignment, exception.Code);
	}

	[Test]
	public void Deserialize_ObjectGraph_DuplicateExtensionDataProperty_ThrowsJsonSerializationException()
	{
		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<ExtensionDataPerson>("""{"name":"Ada","unknown":true,"unknown":false}"""));
		Assert.Equal(JsonSerializationException.ErrorCode.DoublePropertyAssignment, exception.Code);
	}

	[Test]
	public void Serialize_ObjectGraph_WritesExtensionDataMembers()
	{
		ExtensionDataPerson value = this.Serializer.Deserialize<ExtensionDataPerson>("""{"name":"Ada","unknown":true,"extra":{"nested":5}}""")!;

		string json = this.Serializer.Serialize(value);

		Assert.Equal("""{"name":"Ada","unknown":true,"extra":{"nested":5}}""", json);
	}

	[Test]
	public void Deserialize_ObjectGraph_CaseInsensitivePropertyNames_WhenEnabled()
	{
		this.Serializer = new() { PropertyNameCaseInsensitive = true };

		Person value = this.Serializer.Deserialize<Person>("""{"NAME":"Ada","AGE":37,"ADDRESS":{"CITY":"Seattle","POSTALCODE":98101}}""")!;
		Person expected = new()
		{
			Name = "Ada",
			Age = 37,
			Address = new Address { City = "Seattle", PostalCode = 98101 },
		};

		AssertStructuralEqual(expected, value);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithNullNestedObject()
	{
		Person value = new() { Name = "Ada", Age = 37, Address = null };

		this.AssertRoundtrip(value, """{"name":"Ada","age":37,"address":null}""");
	}

	[Test]
	public void Serialize_ObjectGraph_CanIndentOutput()
	{
		this.Serializer = new() { WriteIndented = true };
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
		string expectedJson = "{\n  \"name\": \"Ada\",\n  \"age\": 37,\n  \"address\": {\n    \"city\": \"Seattle\",\n    \"postalCode\": 98101\n  }\n}";

		this.AssertRoundtrip(value, expectedJson);
	}

	[Test]
	public void SerializeDeserialize_Stream()
	{
		Person value = new() { Name = "Grace", Age = 41 };

		using MemoryStream stream = new();
		this.Serializer.Serialize(stream, value);
		stream.Position = 0;

		Person roundTripped = this.Serializer.Deserialize<Person>(stream)!;
		AssertStructuralEqual(value, roundTripped);
	}

	[Test]
	public void Serialize_ObjectGraph_CanDisableNamingPolicy()
	{
		this.Serializer = new() { PropertyNamingPolicy = null };
		Person value = new() { Name = "Ada", Age = 37 };

		string json = this.Serializer.Serialize(value);

		Assert.Equal("""{"Name":"Ada","Age":37,"Address":null}""", json);
	}

	[Test]
	public void Serialize_ObjectGraph_ExplicitPropertyNameOverridesNamingPolicy()
	{
		RenamedPropertyContainer value = new() { URLValue = "https://example.com" };

		this.AssertRoundtrip(value, """{"uri":"https://example.com"}""");
	}

	[Test]
	public void Serialize_ObjectGraph_ExplicitPropertyNameEscapesSpecialCharacters()
	{
		EscapedPropertyNameContainer value = new() { Value = 3 };

		this.AssertRoundtrip(value, """{"say\"hi":3}""");
	}

	[Test]
	public void Serialize_ObjectGraph_CanOmitDefaultValues()
	{
		this.Serializer = new() { SerializeDefaultValues = SerializeDefaultValuesPolicy.Never };
		Person value = new() { Name = "Ada", Age = 0, Address = null };

		string json = this.Serializer.Serialize(value);

		Assert.Equal("""{"name":"Ada"}""", json);
	}

	[Test]
	public void Serialize_ObjectGraph_CanRetainReferenceTypeDefaultsOnly()
	{
		this.Serializer = new() { SerializeDefaultValues = SerializeDefaultValuesPolicy.ReferenceTypes };
		Person value = new() { Name = "Ada", Age = 0, Address = null };

		string json = this.Serializer.Serialize(value);

		Assert.Equal("""{"name":"Ada","address":null}""", json);
	}

	[Test]
	public void Serialize_ObjectGraph_CanRetainValueTypeDefaultsOnly()
	{
		this.Serializer = new() { SerializeDefaultValues = SerializeDefaultValuesPolicy.ValueTypes };
		Person value = new() { Name = null, Age = 0, Address = null };

		string json = this.Serializer.Serialize(value);

		Assert.Equal("""{"age":0}""", json);
	}

	[Test]
	public void Serialize_ObjectGraph_RequiredPropertiesAreRetainedWhenRequested()
	{
		this.Serializer = new() { SerializeDefaultValues = SerializeDefaultValuesPolicy.Required };
		RequiredDefaultValueContainer value = new() { Count = 0, Name = null };

		string json = this.Serializer.Serialize(value);

		Assert.Equal("""{"count":0}""", json);
	}

	[Test]
	public void Deserialize_ObjectGraph_MissingRequiredProperty_ThrowsJsonSerializationException()
	{
		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<RequiredPropertyContainer>("""{}"""));
		Assert.Contains("Name", exception.Message);
	}

	[Test]
	public void Deserialize_ObjectGraph_NullForNonNullableProperty_ThrowsJsonSerializationException()
	{
		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<NonNullablePropertyContainer>("""{"name":null}"""));
		Assert.Contains("Name", exception.Message);
	}

	[Test]
	public void Deserialize_ObjectGraph_MissingRequiredProperty_CanBeAllowed()
	{
		this.Serializer = new() { DeserializeDefaultValues = DeserializeDefaultValuesPolicy.AllowMissingValuesForRequiredProperties };

		RequiredPropertyContainer? value = this.Serializer.Deserialize<RequiredPropertyContainer>("{}");

		Assert.NotNull(value);
		Assert.Null(value.Name);
	}

	[Test]
	public void Deserialize_ObjectGraph_NullForNonNullableProperty_CanBeAllowed()
	{
		this.Serializer = new() { DeserializeDefaultValues = DeserializeDefaultValuesPolicy.AllowNullValuesForNonNullableProperties };

		NonNullablePropertyContainer? value = this.Serializer.Deserialize<NonNullablePropertyContainer>("""{"name":null}""");

		Assert.NotNull(value);
		Assert.Null(value.Name);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_PreserveReferences()
	{
		this.Serializer = new() { PreserveReferences = ReferencePreservationMode.RejectCycles };
		SharedLeaf sharedLeaf = new() { Name = "Ada" };
		SharedRoot value = new() { Left = sharedLeaf, Right = sharedLeaf };

		string json = this.Serializer.Serialize(value);
		Assert.Equal("""{"$id":1,"$value":{"left":{"$id":2,"$value":{"name":"Ada"}},"right":{"$ref":2}}}""", json);

		SharedRoot? roundTripped = this.Serializer.Deserialize<SharedRoot>(json);
		Assert.NotNull(roundTripped?.Left);
		Assert.Same(roundTripped.Left, roundTripped.Right);
		Assert.Equal("Ada", roundTripped.Left.Name);
	}

	[Test]
	public void Serialize_ObjectGraph_RejectsReferenceCycles()
	{
		this.Serializer = new() { PreserveReferences = ReferencePreservationMode.RejectCycles };
		CyclicNode node = new();
		node.Next = node;

		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(node));
		Assert.Contains("Reference cycles", exception.Message);
	}

	[Test]
	public void Serialize_ObjectGraph_ExceedingMaxDepth_Throws()
	{
		this.Serializer = this.Serializer with
		{
			StartingContext = this.Serializer.StartingContext with { MaxDepth = 2 },
		};

		DeepNode value = CreateDeepNode(3);

		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(value));
		Assert.Contains("Exceeded maximum depth", exception.Message);
	}

	[Test]
	public void Deserialize_ObjectGraph_ExceedingMaxDepth_Throws()
	{
		this.Serializer = this.Serializer with
		{
			StartingContext = this.Serializer.StartingContext with { MaxDepth = 2 },
		};

		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<DeepNode>("""{"next":{"next":{"next":null}}}"""));
		Assert.Contains("Exceeded maximum depth", exception.Message);
	}

	private static DeepNode CreateDeepNode(int depth)
	{
		DeepNode current = new();
		for (int i = 1; i < depth; i++)
		{
			current = new() { Next = current };
		}

		return current;
	}

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
	internal partial class EmptyContainer
	{
	}

	// Including quotes, Aa and BB have equal hashes; Ua shares their bucket for both 4 and 5 buckets.
	// The unknown name C# has the same hash as Aa and BB.
	[GenerateShape]
	internal partial class PowerOfTwoPropertyContainer
	{
		[PropertyShape(Name = "Aa")]
		public int First { get; set; }

		[PropertyShape(Name = "BB")]
		public int Second { get; set; }

		[PropertyShape(Name = "Ua")]
		public int Third { get; set; }

		[PropertyShape(Name = "café")]
		public int Unicode { get; set; }
	}

	[GenerateShape]
	internal partial class NonPowerOfTwoPropertyContainer
	{
		[PropertyShape(Name = "Aa")]
		public int First { get; set; }

		[PropertyShape(Name = "BB")]
		public int Second { get; set; }

		[PropertyShape(Name = "Ua")]
		public int Third { get; set; }

		[PropertyShape(Name = "café")]
		public int Unicode { get; set; }

		// The quoted UTF-8 name has a negative hash, exercising positive-hash modulus.
		[PropertyShape(Name = "abcde")]
		public int NegativeHash { get; set; }
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

		public UnusedDataPacket? ExtensionData { get; set; }
	}

	[GenerateShape]
	internal partial class RenamedPropertyContainer
	{
		[PropertyShape(Name = "uri")]
		public string? URLValue { get; set; }
	}

	[GenerateShape]
	internal partial class EscapedPropertyNameContainer
	{
		[PropertyShape(Name = "say\"hi")]
		public int Value { get; set; }
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

	[GenerateShape]
	internal partial class DeepNode
	{
		public DeepNode? Next { get; set; }
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
