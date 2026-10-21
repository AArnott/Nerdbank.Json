// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using Microsoft;

public class JsonWriterTests
{
	[Test]
	public void Utf8StringEscapesDecodedContentAndCloneStartsAtRoot()
	{
		TestBufferWriter originalBuffer = new();
		JsonWriter original = new(originalBuffer) { WriteIndented = true };
		original.WriteStartArray();
		original.Write(1);
		TestBufferWriter cloneBuffer = new();
		JsonWriter clone = original.Clone(cloneBuffer);
		Assert.True(clone.WriteIndented);
		clone.WriteString("é😀\n\\\""u8);
		clone.Flush();
		Assert.Equal("\"é😀\\n\\\\\\\"\"", Encoding.UTF8.GetString(cloneBuffer.WrittenArray));
		original.WriteEndArray();
		original.Flush();
		Assert.Equal("[\n  1\n]", Encoding.UTF8.GetString(originalBuffer.WrittenArray));
	}

	[Test]
	public void Utf8StringSequenceAllowsSplitCodePoints()
	{
		byte[] bytes = Encoding.UTF8.GetBytes("é😀\n\\\"");
		for (int split = 0; split <= bytes.Length; split++)
		{
			Utf8Segment first = new(bytes.AsMemory(0, split));
			Utf8Segment last = first.Append(bytes.AsMemory(split));
			ReadOnlySequence<byte> sequence = new(first, 0, last, last.Memory.Length);
			TestBufferWriter buffer = new();
			JsonWriter writer = new(buffer);
			writer.WriteStartObject();
			writer.WritePropertyName("s");
			writer.WriteString(sequence);
			writer.WriteEndObject();
			writer.Flush();
			Assert.Equal("{\"s\":\"é😀\\n\\\\\\\"\"}", Encoding.UTF8.GetString(buffer.WrittenArray));
		}
	}

	[Test]
	public void InvalidUtf8IsPreservedWithoutChangingBoundaries()
	{
		TestBufferWriter buffer = new();
		JsonWriter writer = new(buffer);
		writer.WriteStartArray();
		writer.WriteString(new byte[] { 0xED, 0xA0, 0x80 });
		writer.WriteValueSeparator();
		writer.WriteString("ok"u8);
		writer.WriteEndArray();
		writer.Flush();
		Assert.Equal(new byte[] { (byte)'[', (byte)'"', 0xED, 0xA0, 0x80, (byte)'"', (byte)',', (byte)'"', (byte)'o', (byte)'k', (byte)'"', (byte)']' }, buffer.WrittenArray);
	}

	[Test]
	public void Write_ScalarOverloads()
	{
		TestBufferWriter buffer = new();
		JsonWriter writer = new(buffer);
		writer.WriteStartArray();
		writer.Write((byte)255);
		writer.WriteValueSeparator();
		writer.Write((sbyte)-128);
		writer.WriteValueSeparator();
		writer.Write(short.MinValue);
		writer.WriteValueSeparator();
		writer.Write(ushort.MaxValue);
		writer.WriteValueSeparator();
		writer.Write(int.MinValue);
		writer.WriteValueSeparator();
		writer.Write(uint.MaxValue);
		writer.WriteValueSeparator();
		writer.Write(long.MinValue);
		writer.WriteValueSeparator();
		writer.Write(ulong.MaxValue);
		writer.WriteValueSeparator();
		writer.Write(1.25f);
		writer.WriteValueSeparator();
		writer.Write(1.25d);
		writer.WriteValueSeparator();
		writer.Write(1.25m);
		writer.WriteValueSeparator();
		writer.Write(true);
		writer.WriteValueSeparator();
		writer.Write("hello".AsSpan());
		writer.WriteValueSeparator();
		writer.WriteNull();
		writer.WriteEndArray();
		writer.Flush();
		Assert.Equal("""[255,-128,-32768,65535,-2147483648,4294967295,-9223372036854775808,18446744073709551615,1.25,1.25,1.25,true,"hello",null]""", Encoding.UTF8.GetString(buffer.WrittenArray));
	}

	[Test]
	public void WriteRaw_Utf8AndSequence()
	{
		TestBufferWriter buffer = new();
		JsonWriter writer = new(buffer) { WriteIndented = true };
		writer.WriteStartArray();
		writer.WriteRaw("""{"a":1}"""u8);
		writer.WriteValueSeparator();
		Segment first = new("[2,"u8.ToArray());
		Segment last = first.Append("3]"u8.ToArray());
		ReadOnlySequence<byte> sequence = new(first, 0, last, last.Memory.Length);
		writer.WriteRaw(sequence);
		writer.WriteEndArray();
		writer.Flush();
		Assert.Equal("[\n  {\"a\":1},\n  [2,3]\n]", Encoding.UTF8.GetString(buffer.WrittenArray));
	}

	[Test]
	public void Write_NonfiniteNumbersThrowBeforeWriting()
	{
		TestBufferWriter buffer = new();
		JsonWriter writer = new(buffer);
		try
		{
			writer.Write(double.PositiveInfinity);
			Assert.Fail("Expected non-finite number rejection.");
		}
		catch (ArgumentOutOfRangeException)
		{
		}

		try
		{
			writer.Write(float.NaN);
			Assert.Fail("Expected non-finite number rejection.");
		}
		catch (ArgumentOutOfRangeException)
		{
		}

		writer.Write(1);
		writer.Flush();
		Assert.Equal("1", Encoding.UTF8.GetString(buffer.WrittenArray));
	}

	[Test]
	public void WriteString_EscapesOnlyRfc8259RequiredCharacters()
	{
		TestBufferWriter buffer = new();
		JsonWriter writer = new(buffer);

		writer.Write("plain ü <tag> \"quoted\" \\ slash\nnext");
		writer.Flush();

		Assert.Equal("\"plain ü <tag> \\\"quoted\\\" \\\\ slash\\nnext\"", Encoding.UTF8.GetString(buffer.WrittenArray));
	}

	[Test]
	public void WriteString_EscapesControlCharactersAsUnicodeWhenNoShortEscapeExists()
	{
		TestBufferWriter buffer = new();
		JsonWriter writer = new(buffer);

		writer.Write("a\u0001b");
		writer.Flush();

		Assert.Equal("\"a\\u0001b\"", Encoding.UTF8.GetString(buffer.WrittenArray));
	}

	[Test]
	public void WriteString_WritesNullLiteralForNullString()
	{
		TestBufferWriter buffer = new();
		JsonWriter writer = new(buffer);

		writer.Write((string?)null);
		writer.Flush();

		Assert.Equal("null", Encoding.UTF8.GetString(buffer.WrittenArray));
	}

	[Test]
	public void WriteString_EscapesUnmatchedSurrogate()
	{
		TestBufferWriter buffer = new();
		JsonWriter writer = new(buffer);

		writer.Write("\uD800");
		writer.Flush();
		Assert.Equal("\"\\uD800\"", Encoding.UTF8.GetString(buffer.WrittenArray));
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

	private sealed class Utf8Segment : ReadOnlySequenceSegment<byte>
	{
		internal Utf8Segment(ReadOnlyMemory<byte> memory) => this.Memory = memory;

		internal Utf8Segment Append(ReadOnlyMemory<byte> memory)
		{
			Utf8Segment next = new(memory) { RunningIndex = this.RunningIndex + this.Memory.Length };
			this.Next = next;
			return next;
		}
	}

	private sealed class TestBufferWriter : IBufferWriter<byte>
	{
		private byte[] buffer = new byte[64];
		private int writtenCount;

		internal byte[] WrittenArray
		{
			get
			{
				byte[] result = new byte[this.writtenCount];
				Array.Copy(this.buffer, result, this.writtenCount);
				return result;
			}
		}

		public void Advance(int count)
		{
			Requires.Range(count >= 0, nameof(count), "Count must be non-negative.");

			this.writtenCount += count;
		}

		public Memory<byte> GetMemory(int sizeHint = 0)
		{
			this.EnsureCapacity(sizeHint);
			return this.buffer.AsMemory(this.writtenCount);
		}

		public Span<byte> GetSpan(int sizeHint = 0)
		{
			this.EnsureCapacity(sizeHint);
			return this.buffer.AsSpan(this.writtenCount);
		}

		private void EnsureCapacity(int sizeHint)
		{
			if (sizeHint < 1)
			{
				sizeHint = 1;
			}

			int available = this.buffer.Length - this.writtenCount;
			if (available >= sizeHint)
			{
				return;
			}

			int newLength = Math.Max(this.buffer.Length * 2, this.writtenCount + sizeHint);
			Array.Resize(ref this.buffer, newLength);
		}
	}
}
