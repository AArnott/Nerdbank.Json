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
