// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Pipelines;
using System.Text;

public partial class RawJsonTests
{
	[Test]
	public void BorrowedSequenceAndOwnedSpan()
	{
		byte[] bytes = " [1.00e+02,\"\\u0061\"] true"u8.ToArray();
		ReadOnlySequence<byte> sequence = new(bytes);
		JsonReader reader = new(sequence);
		RawJson raw = reader.ReadRaw(new SerializationContext());
		Assert.False(raw.IsOwned);
		Assert.Equal(sequence.GetPosition(1), raw.Json.Start);
		Assert.Equal("""[1.00e+02,"\u0061"]""", Text(raw));
		Assert.True(reader.ReadBoolean());

		JsonReader spanReader = new(bytes.AsSpan());
		RawJson owned = spanReader.ReadRaw(new SerializationContext());
		Assert.True(owned.IsOwned);
		RawJson copy = raw.ToOwned();
		Assert.True(raw.IsOwned);
		Assert.Equal(copy.Json.Start, raw.ToOwned().Json.Start);
		bytes[2] = (byte)'9';
		Assert.Equal("""[1.00e+02,"\u0061"]""", Text(copy));
		Assert.Equal(copy, owned);
	}

	[Test]
	public void ByteEqualityIgnoresSegmentationNotTokenSpelling()
	{
		byte[] bytes = "[1.0]"u8.ToArray();
		Segment first = new(bytes.AsMemory(0, 2));
		Segment last = first.Append(ReadOnlyMemory<byte>.Empty).Append(bytes.AsMemory(2));
		RawJson segmented = new(new(first, 0, last, last.Memory.Length));
		RawJson contiguous = (RawJson)bytes;
		Assert.Equal(contiguous, segmented);
		Assert.Equal(contiguous.GetHashCode(), segmented.GetHashCode());
		Assert.False(contiguous.Equals((RawJson)"[1]"u8.ToArray()));
		Assert.True(default(RawJson).Equals(default));
		Assert.Equal(contiguous, (RawJson)bytes.AsMemory());
		Assert.Equal(contiguous, (RawJson)(ReadOnlyMemory<byte>)bytes);
		Assert.Equal(contiguous, (RawJson)(ReadOnlySequence<byte>)contiguous);
	}

	[Test]
	public void RawCaptureBorrowsAcrossEverySplitAndOwnedCopiesRemainIndependent()
	{
		byte[] bytes = """ [1.2300e+02,"\u0061"] null"""u8.ToArray();
		for (int split = 0; split <= bytes.Length; split++)
		{
			bytes[2] = (byte)'1';
			Segment first = new(bytes.AsMemory(0, split));
			Segment last = first.Append(ReadOnlyMemory<byte>.Empty).Append(bytes.AsMemory(split));
			ReadOnlySequence<byte> sequence = new(first, 0, last, last.Memory.Length);
			JsonReader reader = new(sequence);
			RawJson borrowed = reader.ReadRaw(new SerializationContext());
			Assert.False(borrowed.IsOwned);
			Assert.Equal(sequence.GetPosition(1), borrowed.Json.Start);
			Assert.Equal("""[1.2300e+02,"\u0061"]""", Text(borrowed));
			RawJson copy = borrowed;
			copy = copy.ToOwned();
			bytes[2] = (byte)'9';
			Assert.Equal("""[9.2300e+02,"\u0061"]""", Text(borrowed));
			Assert.Equal("""[1.2300e+02,"\u0061"]""", Text(copy));
			reader.ReadNull();
			Assert.True(reader.End);
		}
	}

	[Test]
	public void ExplicitLengthValidatesBoundsAndOneValue()
	{
		JsonReader reader = new(" 123 true"u8);
		RawJson raw = reader.ReadRaw(5);
		Assert.Equal(" 123 ", Text(raw));
		Assert.True(reader.ReadBoolean());
		Assert.Throws<ArgumentOutOfRangeException>(() =>
		{
			JsonReader invalid = new("1"u8);
			invalid.ReadRaw(-1);
		});
		Assert.Throws<ArgumentOutOfRangeException>(() =>
		{
			JsonReader invalid = new("1"u8);
			invalid.ReadRaw(2);
		});
		Assert.Throws<FormatException>(() =>
		{
			JsonReader invalid = new("1 2"u8);
			invalid.ReadRaw(3);
		});
	}

	[Test]
	public void AutomaticConverterOwnsAndPreservesExactBytes()
	{
		JsonSerializer serializer = new();
		byte[] bytes = """{"payload":{"n":1.2300e+05,"s":"\u0061"},"tag":"ok"}"""u8.ToArray();
		Envelope value = serializer.Deserialize<Envelope>(new ReadOnlySequence<byte>(bytes))!;
		Assert.True(value.Payload.IsOwned);
		Assert.Equal("""{"n":1.2300e+05,"s":"\u0061"}""", Text(value.Payload));
		bytes.AsSpan().Fill((byte)' ');
		Assert.Equal("""{"payload":{"n":1.2300e+05,"s":"\u0061"},"tag":"ok"}""", serializer.Serialize(value));
		RawJson root = serializer.Deserialize<RawJson>("null");
		Assert.True(root.IsOwned);
		Assert.Equal("null", serializer.Serialize(root));
	}

	[Test]
	[Arguments("")]
	[Arguments("true false")]
	[Arguments("[1,]")]
	[Arguments("\"\\q\"")]
	[Arguments("\"\\uD800\"")]
	[Arguments("/*comment*/null")]
	[Arguments("\uFEFFnull")]
	public void InvalidPayloadCannotBeWritten(string json)
	{
		RawJson raw = (RawJson)Encoding.UTF8.GetBytes(json);
		Assert.Throws<JsonSerializationException>(() => new JsonSerializer().Serialize(raw));
		Assert.Throws<FormatException>(() =>
		{
			JsonWriter writer = new(new Pipe().Writer);
			writer.Write(raw);
		});
	}

	[Test]
	public void RawOperationsHonorDepthAndCancellation()
	{
		RawJson raw = (RawJson)"[[]]"u8.ToArray();
		JsonSerializer serializer = new() { StartingContext = new() { MaxDepth = 1 } };
		Assert.Throws<JsonSerializationException>(() => serializer.Serialize(raw));
		Assert.Throws<InvalidOperationException>(() =>
		{
			JsonReader reader = new(raw.Json);
			reader.ReadRaw(new SerializationContext { MaxDepth = 1 });
		});
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		Assert.Throws<OperationCanceledException>(() =>
		{
			JsonReader reader = new(raw.Json);
			reader.ReadRaw(new SerializationContext { CancellationToken = cancellation.Token });
		});
	}

	[Test]
	public void StreamByteOrderMarkIsNotPartOfCapturedValue()
	{
		byte[] bytes = Encoding.UTF8.GetBytes("\uFEFFnull");
		RawJson raw = new JsonSerializer().Deserialize<RawJson>(bytes);
		Assert.Equal("null", Text(raw));
		Assert.Equal("null", new JsonSerializer().Serialize(raw));
		Assert.Throws<FormatException>(() =>
		{
			JsonReader reader = new(bytes);
			reader.ReadRaw(bytes.Length);
		});
	}

	[Test]
	public async Task AsyncConverterOwnsPipeAndStreamResults()
	{
		JsonSerializer serializer = new();
		byte[] bytes = """{"n":12345678901234567890.001e-30}"""u8.ToArray();
		using MemoryStream input = new(bytes);
		RawJson streamed = await serializer.DeserializeAsync<RawJson>(input);
		Assert.True(streamed.IsOwned);
		Pipe pipe = new();
		await pipe.Writer.WriteAsync(bytes);
		await pipe.Writer.CompleteAsync();
		RawJson piped = await serializer.DeserializeAsync<RawJson>(pipe.Reader);
		await pipe.Reader.CompleteAsync();
		bytes.AsSpan().Fill((byte)' ');
		Assert.True(piped.IsOwned);
		Assert.Equal(streamed, piped);
		using MemoryStream output = new();
		await serializer.SerializeAsync(output, piped);
		Assert.Equal(Text(streamed), Encoding.UTF8.GetString(output.ToArray()));
	}

	private static string Text(RawJson raw) => Encoding.UTF8.GetString(raw.Json.ToArray());

	[GenerateShape]
	public partial class Envelope
	{
		public RawJson Payload { get; set; }

		public string? Tag { get; set; }
	}

	private sealed class Segment : ReadOnlySequenceSegment<byte>
	{
		internal Segment(ReadOnlyMemory<byte> memory) => this.Memory = memory;

		internal Segment Append(ReadOnlyMemory<byte> memory)
		{
			Segment next = new(memory) { RunningIndex = this.RunningIndex + this.Memory.Length };
			this.Next = next;
			return next;
		}
	}
}
