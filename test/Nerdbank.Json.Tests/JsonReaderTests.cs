// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;

public partial class JsonReaderTests
{
	[Test]
	[Arguments("Byte", "255", "255")]
	[Arguments("SByte", "-128", "-128")]
	[Arguments("Int16", "-32768", "-32768")]
	[Arguments("UInt16", "65535", "65535")]
	[Arguments("Int32", "-2147483648", "-2147483648")]
	[Arguments("UInt32", "4294967295", "4294967295")]
	[Arguments("Int64", "-9223372036854775808", "-9223372036854775808")]
	[Arguments("UInt64", "18446744073709551615", "18446744073709551615")]
	[Arguments("Single", "1.25e2", "125")]
	[Arguments("Double", "-1.25e2", "-125")]
	[Arguments("Decimal", "79228162514264337593543950335", "79228162514264337593543950335")]
	public void TypedNumbers_Parse(string type, string token, string expected)
	{
		Assert.Equal(expected, Convert.ToString(ReadNumber(type, token), CultureInfo.InvariantCulture));
	}

	[Test]
	[Arguments("Byte", "256")]
	[Arguments("SByte", "-129")]
	[Arguments("Int16", "32768")]
	[Arguments("UInt16", "-1")]
	[Arguments("Int32", "2147483648")]
	[Arguments("UInt32", "4294967296")]
	[Arguments("Int64", "9223372036854775808")]
	[Arguments("UInt64", "18446744073709551616")]
	[Arguments("Single", "1e100")]
	[Arguments("Double", "-1e999")]
	[Arguments("Decimal", "79228162514264337593543950336")]
	public void TypedNumbers_Overflow(string type, string token)
	{
		Assert.Throws<OverflowException>(() => ReadNumber(type, token));
	}

	[Test]
	[Arguments("Byte")]
	[Arguments("SByte")]
	[Arguments("Int16")]
	[Arguments("UInt16")]
	[Arguments("Int32")]
	[Arguments("UInt32")]
	[Arguments("Int64")]
	[Arguments("UInt64")]
	[Arguments("Single")]
	[Arguments("Double")]
	[Arguments("Decimal")]
	public void TypedNumbers_RejectMalformedJson(string type)
	{
		Assert.Throws<FormatException>(() => ReadNumber(type, "01"));
		Assert.Throws<FormatException>(() => ReadNumber(type, "1e"));
		Assert.Throws<FormatException>(() => ReadNumber(type, "NaN"));
		Assert.Throws<FormatException>(() => ReadNumber(type, "1e+-2"));
		Assert.Throws<FormatException>(() => ReadNumber(type, "1true"));
	}

	[Test]
	public void Sequence_PositionsReferToOriginalInput()
	{
		Segment first = new("[12,"u8.ToArray());
		Segment last = first.Append("34]"u8.ToArray());
		ReadOnlySequence<byte> sequence = new(first, 0, last, last.Memory.Length);
		JsonReader reader = new(sequence);
		Assert.Equal(sequence.Start, reader.Position);
		Assert.False(reader.End);
		reader.ReadStartArray();
		Assert.Equal(12, reader.ReadInt32());
		reader.ReadValueSeparator();
		Assert.Equal(4, reader.Consumed);
		Assert.Equal("34]", Encoding.UTF8.GetString(sequence.Slice(reader.Position).ToArray()));
		Assert.Equal(sequence.Start, reader.Sequence.Start);
		Assert.Equal(34, reader.ReadInt32());
		reader.ReadEndArray();
		Assert.True(reader.End);
		Assert.Equal(sequence.Length, reader.Consumed);
		Assert.True(sequence.Slice(reader.Position).IsEmpty);
	}

	[Test]
	public void Span_SequenceSnapshotIsCached()
	{
		JsonReader reader = new("123"u8);
		ReadOnlySequence<byte> sequence = reader.Sequence;
		Assert.Equal(sequence.Start, reader.Sequence.Start);
		Assert.Equal(123, reader.ReadInt32());
		Assert.True(reader.End);
		Assert.True(sequence.Slice(reader.Position).IsEmpty);
	}

	[Test]
	[Arguments("""["a\uD83D\uDE00\né",true,null,-12.50e+2,{"x":[1,2]}]""")]
	[Arguments("\uFEFF /* lead */ [\"é😀\",false,null,0,] // tail")]
	public void Segmented_AllBoundaries(string json)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		for (int split = 0; split <= bytes.Length; split++)
		{
			Segment first = new(bytes.AsMemory(0, split));
			Segment empty = first.Append(ReadOnlyMemory<byte>.Empty);
			Segment last = empty.Append(bytes.AsMemory(split));
			this.VerifySegmented(new(first, 0, last, last.Memory.Length), json);
		}

		Segment oneByteFirst = new(ReadOnlyMemory<byte>.Empty);
		Segment oneByteLast = oneByteFirst;
		for (int i = 0; i < bytes.Length; i++)
		{
			oneByteLast = oneByteLast.Append(bytes.AsMemory(i, 1)).Append(ReadOnlyMemory<byte>.Empty);
		}

		this.VerifySegmented(new(oneByteFirst, 0, oneByteLast, 0), json);
	}

	[Test]
	public void Segmented_ConstructorBorrowsAndSkipDoesNotMaterializePayload()
	{
		byte[] payload = Encoding.UTF8.GetBytes("\"" + new string('a', 1_000_000) + "\"");
		Segment first = new(payload.AsMemory(0, 500_000));
		Segment last = first.Append(payload.AsMemory(500_000));
		ReadOnlySequence<byte> sequence = new(first, 0, last, last.Memory.Length);
		JsonReader warmup = new(sequence);
		warmup.Skip(new());
#if NET
		long before = GC.GetAllocatedBytesForCurrentThread();
#endif
		JsonReader reader = new(sequence);
		reader.Skip(new());
		Assert.True(reader.End);
#if NET
		Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 1024);
#endif

		JsonReader borrowed = new(sequence);
		payload[1] = (byte)'b';
		Assert.StartsWith("b", borrowed.ReadRequiredString());
	}

	[Test]
	[Arguments("\"\\uD800\"")]
	[Arguments("\"\\uDC00\"")]
	[Arguments("\"\\uD800\\u0041\"")]
	[Arguments("\"\\q\"")]
	[Arguments("\"line\nbreak\"")]
	[Arguments("01")]
	[Arguments("1e+")]
	[Arguments("trueX")]
	public void Segmented_InvalidTokensRejectedByReadAndSkip(string json)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		for (int split = 0; split <= bytes.Length; split++)
		{
			Segment first = new(bytes.AsMemory(0, split));
			Segment last = first.Append(bytes.AsMemory(split));
			ReadOnlySequence<byte> sequence = new(first, 0, last, last.Memory.Length);
			Assert.Throws<FormatException>(() =>
			{
				JsonReader reader = new(sequence);
				reader.Skip(new());
			});
			Assert.Throws<FormatException>(() =>
			{
				JsonReader reader = new(sequence);
				if (json[0] == '"')
				{
					reader.ReadString();
				}
				else if (json[0] == 't')
				{
					reader.ReadBoolean();
				}
				else
				{
					reader.ReadDouble();
				}
			});
		}
	}

	[Test]
	[Arguments(0xC0, 0xAF)]
	[Arguments(0xED, 0xA0)]
	[Arguments(0xF4, 0x90)]
	public void Segmented_InvalidUtf8Rejected(int firstByte, int secondByte)
	{
		byte[] bytes = [(byte)'"', (byte)firstByte, (byte)secondByte, 0x80, 0x80, (byte)'"'];
		Segment first = new(bytes.AsMemory(0, 2));
		Segment last = first.Append(bytes.AsMemory(2));
		ReadOnlySequence<byte> sequence = new(first, 0, last, last.Memory.Length);
		Assert.Throws<FormatException>(() =>
		{
			JsonReader reader = new(sequence);
			reader.ReadString();
		});
		Assert.Throws<FormatException>(() =>
		{
			JsonReader reader = new(sequence);
			reader.Skip(new());
		});
	}

	[Test]
	public void CreatePeekReader_ReturnsIndependentCopyAtCurrentPosition()
	{
		JsonReader reader = new("[1,2]"u8);
		reader.ReadStartArray();
		Assert.Equal("1", reader.ReadNumberToken());
		reader.ReadValueSeparator();

		JsonReader peekReader = reader.CreatePeekReader();

		Assert.Equal("2", peekReader.ReadNumberToken());
		Assert.Equal("2", reader.ReadNumberToken());
		peekReader.ReadEndArray();
		reader.ReadEndArray();
	}

	[Test]
	public void MemoryArrayAndCloneKeepBorrowedStorageAndSyntaxOptions()
	{
		byte[] bytes = "[/*comment*/1,]"u8.ToArray();
		JsonReader reader = new(bytes, allowTrailingCommas: true, JsonCommentHandling.Skip);
		JsonReader memoryReader = new((ReadOnlyMemory<byte>)bytes, allowTrailingCommas: true, JsonCommentHandling.Skip);
		Assert.Equal(reader.Sequence.Start, memoryReader.Sequence.Start);
		reader.ReadStartArray();
		ReadOnlySequence<byte> other = new("/*c*/[2,]"u8.ToArray());
		JsonReader clone = reader.Clone(other);
		Assert.Equal(0, clone.Consumed);
		clone.ReadStartArray();
		Assert.Equal(2, clone.ReadInt32());
		clone.ReadEndArray();
		Assert.True(clone.End);
		Assert.Equal(1, reader.ReadInt32());
		reader.ReadEndArray();
	}

	[Test]
	[Arguments("null", JsonValueKind.Null)]
	[Arguments("true", JsonValueKind.Boolean)]
	[Arguments("-1", JsonValueKind.Number)]
	[Arguments("\"text\"", JsonValueKind.String)]
	[Arguments("[]", JsonValueKind.Array)]
	[Arguments("{}", JsonValueKind.Object)]
	public void TokenInspection(string json, JsonValueKind kind)
	{
		JsonReader reader = new(Encoding.UTF8.GetBytes(" \n" + json));
		Assert.Equal((byte)json[0], reader.NextCode);
		Assert.Equal(kind, reader.NextJsonType);
		Assert.Equal(kind == JsonValueKind.Null, reader.IsNull);
		Assert.Equal(2, reader.Consumed);
		if (reader.IsNull)
		{
			reader.ReadNull();
		}
		else
		{
			reader.Skip(new());
		}

		Assert.True(reader.End);
	}

	[Test]
	[Arguments("\"plain é😀\"", "plain é😀", false)]
	[Arguments("\"a\\n\\uD83D\\uDE00\\\\\\\"\"", "a\n😀\\\"", true)]
	[Arguments("\"\"", "", false)]
	public void Utf8Strings_AllBoundaries(string json, string expected, bool escaped)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		for (int split = 0; split <= bytes.Length; split++)
		{
			Segment first = new(bytes.AsMemory(0, split));
			Segment last = first.Append(bytes.AsMemory(split));
			ReadOnlySequence<byte> sequence = new(first, 0, last, last.Memory.Length);
			JsonReader reader = new(sequence);
			bool contiguous = reader.TryReadStringSpan(out ReadOnlySpan<byte> span);
			Assert.False(escaped && contiguous);
			if (contiguous)
			{
				Assert.Equal(expected, Encoding.UTF8.GetString(span.ToArray()));
			}
			else
			{
				Assert.Equal(0, reader.Consumed);
				ReadOnlySequence<byte> decoded = reader.ReadStringSequence()!.Value;
				Assert.Equal(expected, Encoding.UTF8.GetString(decoded.ToArray()));
			}

			Assert.True(reader.End);
			JsonReader spanReader = new(sequence);
			Assert.Equal(expected, Encoding.UTF8.GetString(spanReader.ReadStringSpan().ToArray()));
			Assert.True(spanReader.End);
		}
	}

	[Test]
	public void Utf8Strings_NullAndBorrowedLifetime()
	{
		JsonReader nullReader = new("null"u8);
		Assert.False(nullReader.TryReadStringSpan(out _));
		Assert.Equal(0, nullReader.Consumed);
		Assert.Null(nullReader.ReadStringSequence());
		Assert.True(nullReader.End);
		byte[] bytes = "\"text\""u8.ToArray();
		JsonReader reader = new(bytes);
		ReadOnlySequence<byte> decoded = reader.ReadStringSequence()!.Value;
		bytes[1] = (byte)'n';
		Assert.Equal("next", Encoding.UTF8.GetString(decoded.ToArray()));
		JsonReader spanReader = new(bytes.AsSpan());
		ReadOnlySequence<byte> owned = spanReader.ReadStringSequence()!.Value;
		bytes[1] = (byte)'t';
		Assert.Equal("next", Encoding.UTF8.GetString(owned.ToArray()));
	}

	private static object ReadNumber(string type, string token)
	{
		JsonReader reader = new(Encoding.UTF8.GetBytes(token));
		return type switch
		{
			"Byte" => (object)reader.ReadByte(),
			"SByte" => reader.ReadSByte(),
			"Int16" => reader.ReadInt16(),
			"UInt16" => reader.ReadUInt16(),
			"Int32" => reader.ReadInt32(),
			"UInt32" => reader.ReadUInt32(),
			"Int64" => reader.ReadInt64(),
			"UInt64" => reader.ReadUInt64(),
			"Single" => reader.ReadSingle(),
			"Double" => reader.ReadDouble(),
			"Decimal" => reader.ReadDecimal(),
			_ => throw new ArgumentOutOfRangeException(nameof(type)),
		};
	}

	private void VerifySegmented(ReadOnlySequence<byte> sequence, string json)
	{
		JsonReader reader = new(sequence, allowTrailingCommas: true, JsonCommentHandling.Skip);
		JsonReader peek = reader.CreatePeekReader();
		peek.Skip(new());
		Assert.Equal(0, reader.Consumed);
		Assert.Equal(sequence.Start, reader.Sequence.Start);
		reader.ReadStartArray();
		Assert.Equal(json.Contains("lead") ? "é😀" : "a😀\né", reader.ReadString());
		reader.ReadValueSeparator();
		Assert.Equal(!json.Contains("lead"), reader.ReadBoolean());
		reader.ReadValueSeparator();
		Assert.True(reader.TryReadNull());
		reader.ReadValueSeparator();
		Assert.Equal(json.Contains("lead") ? 0 : -1250, reader.ReadDouble());
		if (!json.Contains("lead"))
		{
			reader.ReadValueSeparator();
			Assert.Equal("""{"x":[1,2]}""", reader.ReadRawValue());
		}

		reader.ReadEndArray();
		Assert.Equal(peek.Consumed, reader.Consumed);
		reader.EnsureFullyConsumed();
		Assert.Equal(sequence.Length, reader.Consumed);
		Assert.True(reader.End);
		Assert.True(sequence.Slice(reader.Position).IsEmpty);
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
