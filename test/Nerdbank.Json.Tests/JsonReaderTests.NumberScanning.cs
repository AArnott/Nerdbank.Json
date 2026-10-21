// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

public partial class JsonReaderTests
{
	[Test]
	[Arguments(0, false)]
	[Arguments(1, false)]
	[Arguments(3, false)]
	[Arguments(64, false)]
	[Arguments(0, true)]
	[Arguments(1, true)]
	[Arguments(3, true)]
	[Arguments(64, true)]
	public void NumberScanning_ManyValuesPreserveTokensAndPositions(int segmentSize, bool floatingPoint)
	{
		(string Token, double Value)[] spellings = floatingPoint
			? [("12345.625", 12345.625), ("-67890.125", -67890.125), ("0.625", 0.625), ("-0.625", -0.625), ("0.0", 0), ("-0.0", 0), ("0e+2", 0), ("-0E-2", 0), ("12345e+2", 1234500), ("-67890E-2", -678.9)]
			: [("12345", 12345), ("-67890", -67890), ("0", 0), ("-0", 0), ("2147483647", 2147483647), ("-2147483648", -2147483648)];
		const int Repetitions = 32;
		string[] tokens = Enumerable.Range(0, spellings.Length * Repetitions).Select(i => spellings[i % spellings.Length].Token).ToArray();
		byte[] bytes = Encoding.UTF8.GetBytes("[" + string.Join(",", tokens) + "]");
		ReadOnlySequence<byte> sequence = NumberScanningSequence(bytes, segmentSize);
		JsonReader reader = new(sequence);
		reader.ReadStartArray();
		long offset = 1;
		for (int i = 0; i < tokens.Length; i++)
		{
			if (i > 0)
			{
				reader.ReadValueSeparator();
				offset++;
			}

			Assert.Equal(offset, reader.Consumed);
			Assert.Equal(sequence.GetPosition(offset), reader.Position);
			if (i % spellings.Length == 0)
			{
				JsonReader peek = reader.CreatePeekReader();
				Assert.Equal(tokens[i], peek.ReadNumberToken());
				peek.ReadValueSeparator();
				Assert.Equal(spellings[(i + 1) % spellings.Length].Value, peek.ReadDouble());
				Assert.Equal(offset, reader.Consumed);
				Assert.Equal(sequence.GetPosition(offset), reader.Position);
			}

			double expected = spellings[i % spellings.Length].Value;
			Assert.Equal(expected, floatingPoint ? reader.ReadDouble() : reader.ReadInt64());
			offset += tokens[i].Length;
			Assert.Equal(offset, reader.Consumed);
			Assert.Equal(sequence.GetPosition(offset), reader.Position);
			Assert.Equal((byte)(i + 1 == tokens.Length ? ']' : ','), reader.NextCode);
		}

		reader.ReadEndArray();
		Assert.Equal(sequence.Length, reader.Consumed);
		Assert.Equal(sequence.GetPosition(sequence.Length), reader.Position);
		Assert.True(sequence.Slice(reader.Position).IsEmpty);
		Assert.True(reader.End);
	}

	[Test]
	[Arguments("12345")]
	[Arguments("-67890")]
	[Arguments("0")]
	[Arguments("-0")]
	[Arguments("12345.625")]
	[Arguments("-67890.125")]
	[Arguments("0.0")]
	[Arguments("-0.0")]
	[Arguments("0e+2")]
	[Arguments("-0E-2")]
	public void NumberScanning_FinalTokenMatchesPrimitives(string token)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(token);
		Assert.Equal(JsonPrimitives.DecodeResult.Success, JsonPrimitives.TryRead(bytes, out double expected, out int size));
		Assert.Equal(bytes.Length, size);
		foreach (int segmentSize in new[] { 0, 1, 3, 64 })
		{
			ReadOnlySequence<byte> sequence = NumberScanningSequence(bytes, segmentSize);
			JsonReader reader = new(sequence);
			JsonReader peek = reader.CreatePeekReader();
			Assert.Equal(token, peek.ReadNumberToken());
			Assert.True(peek.End);
			double actual = reader.ReadDouble();
#if NET8_0_OR_GREATER
			Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
#else
			Assert.Equal(expected, actual);
#endif
			Assert.Equal(sequence.Length, reader.Consumed);
			Assert.Equal(sequence.GetPosition(sequence.Length), reader.Position);
			Assert.True(reader.End);
		}
	}

	[Test]
	[Arguments("00123,45678]")]
	[Arguments("-00123,45678]")]
	[Arguments("00.625,45678]")]
	[Arguments("-00e+2,45678]")]
	[Arguments("12345e+")]
	[Arguments("-")]
	public void NumberScanning_FailedPeekAndRetryPreserveCursor(string tail)
	{
		const string Prefix = "[12345,67890, \n";
		byte[] bytes = Encoding.UTF8.GetBytes(Prefix + tail);
		foreach (int segmentSize in new[] { 0, 1, 3, 64 })
		{
			ReadOnlySequence<byte> sequence = NumberScanningSequence(bytes, segmentSize);
			JsonReader reader = new(sequence);
			reader.ReadStartArray();
			Assert.Equal(12345, reader.ReadInt32());
			reader.ReadValueSeparator();
			long originalOffset = reader.Consumed;
			JsonReader peek = reader.CreatePeekReader();
			Assert.Equal(67890, peek.ReadInt32());
			peek.ReadValueSeparator();
			AssertNumberScanningFailure(ref peek);
			Assert.Equal(Prefix.Length, peek.Consumed);
			Assert.Equal(sequence.GetPosition(Prefix.Length), peek.Position);
			Assert.Equal(originalOffset, reader.Consumed);
			Assert.Equal(sequence.GetPosition(originalOffset), reader.Position);

			Assert.Equal(67890, reader.ReadInt32());
			reader.ReadValueSeparator();
			for (int attempt = 0; attempt < 2; attempt++)
			{
				AssertNumberScanningFailure(ref reader);
				Assert.Equal(Prefix.Length, reader.Consumed);
				Assert.Equal(sequence.GetPosition(Prefix.Length), reader.Position);
				Assert.Equal(Encoding.UTF8.GetBytes(tail), sequence.Slice(reader.Position).ToArray());
			}
		}
	}

	private static void AssertNumberScanningFailure(ref JsonReader reader)
	{
		FormatException? failure = null;
		try
		{
			reader.ReadDouble();
		}
		catch (FormatException ex)
		{
			failure = ex;
		}

		Assert.Equal("Expected a complete, valid JSON number.", Assert.IsType<FormatException>(failure).Message);
	}

	private static ReadOnlySequence<byte> NumberScanningSequence(byte[] bytes, int segmentSize)
	{
		if (segmentSize == 0)
		{
			return new(bytes);
		}

		Segment first = new(ReadOnlyMemory<byte>.Empty);
		Segment last = first;
		for (int offset = 0; offset < bytes.Length; offset += segmentSize)
		{
			last = last.Append(bytes.AsMemory(offset, Math.Min(segmentSize, bytes.Length - offset))).Append(ReadOnlyMemory<byte>.Empty);
		}

		return new(first, 0, last, 0);
	}
}
