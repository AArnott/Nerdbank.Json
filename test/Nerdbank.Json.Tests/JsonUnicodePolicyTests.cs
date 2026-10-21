// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Pipelines;
using System.Text;

[GenerateShapeFor<string>]
[GenerateShapeFor<string[]>]
[GenerateShapeFor<RawJson>]
public partial class JsonUnicodePolicyTests
{
	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void InvalidBytes_UseClrFallbackButRetainRawBytes(bool escaped)
	{
		byte[][] payloads =
		[
			[0x80], [0xFF], [0xC0, 0xA2], [0xE0, 0x80, 0xA2], [0xED, 0xA0, 0x80],
			[0xF4, 0x90, 0x80, 0x80], [0xC2], [0xE0, 0xA0], [0xF0, 0x90, 0x80],
			[0xC3, 0xA9, 0xF0, 0x9F, 0x98, 0x80, 0xFF],
		];
		foreach (byte[] payload in payloads)
		{
			byte[] token = [(byte)'"', .. payload, .. escaped ? "\\n"u8.ToArray() : Array.Empty<byte>(), (byte)'"'];
			string expected = Encoding.UTF8.GetString(payload) + (escaped ? "\n" : string.Empty);
			foreach (ReadOnlySequence<byte> sequence in EverySplit(token))
			{
				JsonReader reader = new(sequence);
				Assert.Equal(expected, reader.ReadRequiredString());
				Assert.Equal(token.Length, reader.Consumed);
				reader = new(sequence);
				Assert.Equal(Encoding.UTF8.GetString(token), reader.ReadRawValue());
				reader = new(sequence);
				Assert.Equal(token, reader.ReadRaw(new SerializationContext()).Json.ToArray());
				reader = new(sequence);
				Assert.Equal(payload.Concat(escaped ? new byte[] { 10 } : []).ToArray(), reader.ReadStringSequence()!.Value.ToArray());
				reader = new(sequence);
				if (!escaped && reader.TryReadStringSpan(out ReadOnlySpan<byte> span))
				{
					Assert.True(span.SequenceEqual(payload));
				}

				JsonSerializer serializer = new();
				Assert.Equal(expected, serializer.Deserialize<string, JsonUnicodePolicyTests>(sequence));
				Assert.Equal(expected, Assert.IsType<JsonString>(serializer.DeserializeJsonValue(sequence)).Value);
				RawJson raw = serializer.Deserialize<RawJson, JsonUnicodePolicyTests>(sequence);
				Assert.True(raw.IsOwned);
				TestBufferWriter output = new();
				serializer.Serialize<RawJson, JsonUnicodePolicyTests>(output, raw);
				Assert.Equal(token, output.WrittenArray);
			}
		}
	}

	[Test]
	[Arguments("\"\\uD800\"", "\uD800")]
	[Arguments("\"\\uDC00\"", "\uDC00")]
	[Arguments("\"\\uD800\\u0041\"", "\uD800A")]
	[Arguments("\"\\uD800\\n\\uD800\"", "\uD800\n\uD800")]
	[Arguments("\"\\uD83D\\uDE00\"", "\uD83D\uDE00")]
	public void EscapedCodeUnits_ArePreservedAcrossSegmentsAndWriting(string json, string expected)
	{
		foreach (ReadOnlySequence<byte> sequence in EverySplit(Encoding.UTF8.GetBytes(json)))
		{
			JsonReader reader = new(sequence);
			Assert.Equal(expected, reader.ReadRequiredString());
			reader = new(sequence);
			reader.Skip(new());
			Assert.True(reader.End);
			reader = new(sequence);
			Assert.Equal(Encoding.UTF8.GetBytes(expected), reader.ReadStringSequence()!.Value.ToArray());
		}

		TestBufferWriter output = new();
		JsonWriter writer = new(output);
		writer.WriteStartObject();
		writer.WritePropertyName(expected);
		writer.Write(expected.AsSpan());
		writer.WriteEndObject();
		writer.Flush();
		JsonReader roundtrip = new(output.WrittenArray);
		roundtrip.ReadStartObject();
		Assert.Equal(expected, roundtrip.ReadRequiredString());
		roundtrip.ReadNameSeparator();
		Assert.Equal(expected, roundtrip.ReadRequiredString());
		Assert.True(roundtrip.TryReadEndObject());
	}

	[Test]
	public void Utf8Writing_PreservesInvalidBytesAndEscapesAsciiAtEverySplit()
	{
		byte[] payload = [0xC0, 0xA2, 0xFF, (byte)'"', (byte)'\\', 0, 10, 0xE0, 0xA0];
		byte[] expected = [(byte)'"', 0xC0, 0xA2, 0xFF, .. "\\\"\\\\\\u0000\\n"u8.ToArray(), 0xE0, 0xA0, (byte)'"'];
		foreach (ReadOnlySequence<byte> sequence in EverySplit(payload))
		{
			TestBufferWriter output = new();
			JsonWriter writer = new(output);
			writer.WriteString(sequence);
			writer.Flush();
			Assert.Equal(expected, output.WrittenArray);
		}
	}

	[Test]
	public async Task AsyncFramingAndInterning_UseSameFallbackAndPreserveFirstValue()
	{
		byte[] invalid = [(byte)'"', 0xF0, 0x90, 0x80, (byte)'"'];
		string expected = Encoding.UTF8.GetString(invalid, 1, 3);
		JsonSerializer serializer = new() { InternStrings = true };
		Pipe pipe = new();
		Task<string?> pending = serializer.DeserializeAsync<string, JsonUnicodePolicyTests>(pipe.Reader).AsTask();
		foreach (byte value in invalid)
		{
			await pipe.Writer.WriteAsync(new byte[] { value });
		}

		await pipe.Writer.WriteAsync(" \"\\uD800\""u8.ToArray());
		await pipe.Writer.CompleteAsync();
		Assert.Equal(expected, await pending);
		Assert.Equal("\uD800", await serializer.DeserializeAsync<string, JsonUnicodePolicyTests>(pipe.Reader));
		await pipe.Reader.CompleteAsync();

		byte[] array = [(byte)'[', .. invalid, (byte)',', .. invalid, (byte)']'];
		using MemoryStream stream = new(array);
		string[] values = (await serializer.DeserializeAsync<string[], JsonUnicodePolicyTests>(stream))!;
		Assert.Equal(expected, values[0]);
		Assert.Same(values[0], values[1]);
		using MemoryStream trailing = new([.. invalid, .. " true"u8.ToArray()]);
		await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeAsync<string, JsonUnicodePolicyTests>(trailing));
	}

	[Test]
	public void InvalidPropertyBytes_AreApplicationDataButControlsRemainInvalid()
	{
		byte[] json = [(byte)'{', (byte)'"', 0xC0, 0xAF, (byte)'"', (byte)':', (byte)'"', 0xFF, (byte)'"', (byte)'}'];
		foreach (ReadOnlySequence<byte> sequence in EverySplit(json))
		{
			JsonValue value = new JsonSerializer().DeserializeJsonValue(sequence)!;
			Assert.Equal("\uFFFD", Assert.IsType<JsonString>(Assert.IsType<JsonObject>(value)["\uFFFD\uFFFD"]).Value);
		}
	}

	private static IEnumerable<ReadOnlySequence<byte>> EverySplit(byte[] bytes)
	{
		for (int split = 0; split <= bytes.Length; split++)
		{
			Segment first = new(bytes.AsMemory(0, split));
			Segment last = first.Append(bytes.AsMemory(split));
			yield return new(first, 0, last, last.Memory.Length);
		}
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

	private sealed class TestBufferWriter : IBufferWriter<byte>
	{
		private readonly byte[] buffer = new byte[4096];
		private int written;

		internal byte[] WrittenArray => this.buffer.AsSpan(0, this.written).ToArray();

		public void Advance(int count) => this.written += count;

		public Memory<byte> GetMemory(int sizeHint = 0) => this.buffer.AsMemory(this.written);

		public Span<byte> GetSpan(int sizeHint = 0) => this.buffer.AsSpan(this.written);
	}
}
