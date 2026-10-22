// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

public partial class JsonObjectSerializerTests
{
	[Test]
	[Arguments(1)]
	[Arguments(64)]
	[Arguments(4096)]
	public void SegmentedEscapedPropertyFallbackAndStringLookaheadKeepForwardReaderState(int segmentSize)
	{
		const int Count = 256;
		byte[] bytes = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat("""{"\u006eame":"é\uD83D\uDE00","age":37,"address":null}""", Count)) + "]");
		ReadOnlyMemory<byte>[] segments = Enumerable.Range(0, (bytes.Length + segmentSize - 1) / segmentSize)
			.Select(i => bytes.AsMemory(i * segmentSize, Math.Min(segmentSize, bytes.Length - (i * segmentSize))))
			.Select(memory => (ReadOnlyMemory<byte>)memory)
			.ToArray();
		ReadOnlySequence<byte> sequence = CreateSequence(segments);
		ITypeShape<Person> shape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Person>()!;
		JsonReader reader = new(sequence);
		reader.ReadStartArray();
		for (int i = 0; i < Count; i++)
		{
			JsonReader peek = reader.CreatePeekReader();
			peek.Skip(new());
			Person person = this.Serializer.Deserialize(ref reader, shape)!;
			Assert.Equal("é😀", person.Name);
			Assert.Equal(37, person.Age);
			Assert.Equal(peek.Consumed, reader.Consumed);
			if (i < Count - 1)
			{
				reader.ReadValueSeparator();
			}
		}

		reader.ReadEndArray();
		Assert.True(reader.End);
		Assert.Equal(sequence.End, reader.Position);
	}

	[Test]
	public void SegmentedConstructorUnionAndUnknownRetention_AllBoundaries()
	{
		byte[] constructor = """{"name":"é\uD83D\uDE00","age":37}"""u8.ToArray();
		byte[] union = """["Cat",{"lives":9,"name":"é\uD83D\uDE00"}]"""u8.ToArray();
		const string Retained = """{"name":"é\uD83D\uDE00","future":{"n":1.2300e+02,"s":"\u0061"}}""";
		byte[] retained = Encoding.UTF8.GetBytes(Retained);
		foreach (ReadOnlySequence<byte> sequence in EverySplit(constructor))
		{
			Assert.Equal(new ParameterizedRecord("é😀", 37), this.Serializer.Deserialize<ParameterizedRecord>(sequence));
		}

		foreach (ReadOnlySequence<byte> sequence in EverySplit(union))
		{
			Cat cat = Assert.IsType<Cat>(this.Serializer.Deserialize<Animal>(sequence));
			Assert.Equal("é😀", cat.Name);
			Assert.Equal(9, cat.Lives);
		}

		foreach (ReadOnlySequence<byte> sequence in EverySplit(retained))
		{
			MixedConstructorTypeWithExtensionData model = this.Serializer.Deserialize<MixedConstructorTypeWithExtensionData>(sequence)!;
			Assert.Equal("é😀", model.Name);
			Assert.NotNull(model.ExtensionData);
			Assert.Equal("""{"name":"é😀","future":{"n":1.2300e+02,"s":"\u0061"}}""", this.Serializer.Serialize(model));
		}
	}

	[Test]
	public void SegmentedReferenceMetadata_AllBoundaries()
	{
		JsonSerializer serializer = new() { PreserveReferences = ReferencePreservationMode.RejectCycles };
		byte[] bytes = """{"$id":1,"$value":{"left":{"$id":2,"$value":{"name":"é\uD83D\uDE00"}},"right":{"$ref":2}}}"""u8.ToArray();
		foreach (ReadOnlySequence<byte> sequence in EverySplit(bytes))
		{
			SharedRoot model = serializer.Deserialize<SharedRoot>(sequence)!;
			Assert.NotNull(model.Left);
			Assert.Same(model.Left, model.Right);
			Assert.Equal("é😀", model.Left.Name);
		}
	}

	[Test]
	[Arguments("""{"name":"Ada","name":"Grace","age":37}""", JsonSerializationException.ErrorCode.DoublePropertyAssignment)]
	[Arguments("""{"age":37}""", JsonSerializationException.ErrorCode.MissingRequiredProperty)]
	public void SegmentedConstructorErrors_AllBoundaries(string json, JsonSerializationException.ErrorCode expected)
	{
		foreach (ReadOnlySequence<byte> sequence in EverySplit(Encoding.UTF8.GetBytes(json)))
		{
			JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<ParameterizedRecord>(sequence));
			Assert.Equal(expected, error.Code);
		}
	}

	[Test]
	public void SegmentedPropertyControlsCannotBypassLookupValidation()
	{
		foreach (byte[] bytes in new[]
		{
			"{\"bad\nname\":1}"u8.ToArray(),
		})
		{
			foreach (ReadOnlySequence<byte> sequence in EverySplit(bytes))
			{
				Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize<Person>(sequence));
			}
		}
	}

	private static IEnumerable<ReadOnlySequence<byte>> EverySplit(byte[] bytes)
	{
		for (int split = 0; split <= bytes.Length; split++)
		{
			yield return CreateSequence(bytes.AsMemory(0, split), ReadOnlyMemory<byte>.Empty, bytes.AsMemory(split));
		}

		yield return CreateSequence(bytes.Select(b => (ReadOnlyMemory<byte>)new byte[] { b }).ToArray());
	}
}
