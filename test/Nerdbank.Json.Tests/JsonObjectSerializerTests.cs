// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using PolyType.Abstractions;

public partial class JsonObjectSerializerTests : TestBase
{
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
	public void Deserialize_ObjectGraph_CanCaptureUnknownPropertiesIntoExtensionData()
	{
		ExtensionDataPerson? value = this.Serializer.Deserialize<ExtensionDataPerson>("""{"name":"Ada","unknown":true,"extra":{"nested":5}}""" + "\n");

		Assert.NotNull(value);
		Assert.Equal("Ada", value.Name);
		Assert.NotNull(value.ExtensionData);
		Assert.Equal("true", value.ExtensionData!["unknown"]);
		Assert.Equal("""{"nested":5}""", value.ExtensionData["extra"]);
	}

	[Test]
	public void Serialize_ObjectGraph_WritesExtensionDataMembers()
	{
		ExtensionDataPerson value = new()
		{
			Name = "Ada",
			ExtensionData = new Dictionary<string, string>
			{
				["unknown"] = "true",
				["extra"] = """{"nested":5}""",
			},
		};

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
	public void Deserialize_ObjectGraph_MissingRequiredProperty_ThrowsFormatException()
	{
		FormatException exception = Assert.Throws<FormatException>(() => this.Serializer.Deserialize<RequiredPropertyContainer>("""{}"""));
		Assert.Contains("Name", exception.Message);
	}

	[Test]
	public void Deserialize_ObjectGraph_NullForNonNullableProperty_ThrowsFormatException()
	{
		FormatException exception = Assert.Throws<FormatException>(() => this.Serializer.Deserialize<NonNullablePropertyContainer>("""{"name":null}"""));
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

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => this.Serializer.Serialize(node));
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

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => this.Serializer.Serialize(value));
		Assert.Contains("Exceeded maximum depth", exception.Message);
	}

	[Test]
	public void Deserialize_ObjectGraph_ExceedingMaxDepth_Throws()
	{
		this.Serializer = this.Serializer with
		{
			StartingContext = this.Serializer.StartingContext with { MaxDepth = 2 },
		};

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => this.Serializer.Deserialize<DeepNode>("""{"next":{"next":{"next":null}}}"""));
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
