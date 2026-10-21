// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using static Nerdbank.Json.JsonPrimitives;

public class JsonPrimitivesTests
{
	[Test]
	[Arguments("null", false)]
	[Arguments("true", true)]
	[Arguments("false", true)]
	public void LiteralsHandleEveryPrefixAndBoundary(string text, bool boolean)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		for (int length = 0; length <= bytes.Length; length++)
		{
			ReadOnlySpan<byte> prefix = bytes.AsSpan(0, length);
			DecodeResult partial = boolean ? TryReadBoolean(prefix, out _, out int size, isFinalBlock: false) : TryReadNull(prefix, out size, isFinalBlock: false);
			Assert.Equal(length == 0 ? DecodeResult.EmptyBuffer : DecodeResult.InsufficientBuffer, partial);
			Assert.Equal(length == bytes.Length ? bytes.Length + 1 : boolean && length == 0 ? 4 : bytes.Length, size);
			DecodeResult final = boolean ? TryReadBoolean(prefix, out _, out size) : TryReadNull(prefix, out size);
			Assert.Equal(length == 0 ? DecodeResult.EmptyBuffer : length == bytes.Length ? DecodeResult.Success : DecodeResult.TokenMismatch, final);
		}

		byte[] terminated = Encoding.UTF8.GetBytes(text + ",");
		DecodeResult complete = boolean ? TryReadBoolean(terminated, out _, out int consumed, isFinalBlock: false) : TryReadNull(terminated, out consumed, isFinalBlock: false);
		Assert.Equal(DecodeResult.Success, complete);
		Assert.Equal(bytes.Length, consumed);
		if (boolean)
		{
			Assert.Equal(DecodeResult.Success, TryReadBoolean(bytes, out bool value, out _));
			Assert.Equal(text == "true", value);
		}
	}

	[Test]
	[Arguments("0")]
	[Arguments("-12.50e+2")]
	[Arguments("1e-25")]
	[Arguments("1234567890123456789012345678901234567890")]
	public void NumberTokenPrefixesNeedFinalityOrDelimiter(string text)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		for (int length = 0; length <= bytes.Length; length++)
		{
			DecodeResult result = TryReadNumberToken(bytes.AsSpan(0, length), out ReadOnlySpan<byte> value, out int required, isFinalBlock: false);
			Assert.Equal(length == 0 ? DecodeResult.EmptyBuffer : DecodeResult.InsufficientBuffer, result);
			Assert.Equal(length + 1, required);
			Assert.True(value.IsEmpty);
		}

		Assert.Equal(DecodeResult.Success, TryReadNumberToken(bytes, out ReadOnlySpan<byte> token, out int consumed));
		Assert.Equal(text, Encoding.UTF8.GetString(token.ToArray()));
		Assert.Equal(bytes.Length, consumed);
		byte[] terminated = Encoding.UTF8.GetBytes(text + " ");
		Assert.Equal(DecodeResult.Success, TryReadNumberToken(terminated, out _, out consumed, isFinalBlock: false));
		Assert.Equal(bytes.Length, consumed);
	}

	[Test]
	[Arguments("+1")]
	[Arguments("01")]
	[Arguments("-")]
	[Arguments("1.")]
	[Arguments("1e")]
	[Arguments("1e+")]
	[Arguments("1e+-2")]
	[Arguments("1true")]
	[Arguments("NaN")]
	[Arguments("Infinity")]
	[Arguments(" 1")]
	public void MalformedNumbersHaveNoSuccessShapedOutput(string text)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		Assert.Equal(DecodeResult.TokenMismatch, TryReadNumberToken(bytes, out ReadOnlySpan<byte> value, out int size));
		Assert.True(value.IsEmpty);
		Assert.Equal(0, size);
		Assert.Equal(DecodeResult.TokenMismatch, TryReadDouble(bytes, out double decoded, out size));
		Assert.Equal(0, decoded);
		Assert.Equal(0, size);
	}

	[Test]
	[Arguments("nullX")]
	[Arguments("trueX")]
	[Arguments("falseX")]
	[Arguments("nulX")]
	[Arguments("True")]
	public void MalformedLiteralsAreNotIncomplete(string text)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		Assert.Equal(DecodeResult.TokenMismatch, TryReadNull(bytes, out int nullSize, isFinalBlock: false));
		Assert.Equal(0, nullSize);
		Assert.Equal(DecodeResult.TokenMismatch, TryReadBoolean(bytes, out bool value, out int booleanSize, isFinalBlock: false));
		Assert.False(value);
		Assert.Equal(0, booleanSize);
	}

	[Test]
	public void NumericRangesAndMismatches()
	{
		Assert.Equal(DecodeResult.Success, TryReadInt64("-9223372036854775808"u8, out long signed, out int consumed));
		Assert.Equal(long.MinValue, signed);
		Assert.Equal(20, consumed);
		Assert.Equal(DecodeResult.Success, TryReadUInt64("18446744073709551615"u8, out ulong unsigned, out consumed));
		Assert.Equal(ulong.MaxValue, unsigned);
		Assert.Equal(20, consumed);
		Assert.Equal(DecodeResult.TokenMismatch, TryReadInt64("9223372036854775808"u8, out signed, out consumed));
		Assert.Equal(0, consumed);
		Assert.Equal(DecodeResult.TokenMismatch, TryReadUInt64("18446744073709551616"u8, out unsigned, out _));
		Assert.Equal(DecodeResult.TokenMismatch, TryReadUInt64("-1"u8, out unsigned, out _));
		Assert.Equal(DecodeResult.TokenMismatch, TryReadInt64("1.0"u8, out signed, out _));
		Assert.Equal(DecodeResult.Success, TryReadDouble("-1.25e2"u8, out double floating, out _));
		Assert.Equal(-125, floating);
		Assert.Equal(DecodeResult.TokenMismatch, TryReadDouble("1e999"u8, out floating, out _));
		Assert.Equal(DecodeResult.Success, TryReadDecimal("79228162514264337593543950335"u8, out decimal precise, out _));
		Assert.Equal(decimal.MaxValue, precise);
		Assert.Equal(DecodeResult.TokenMismatch, TryReadDecimal("79228162514264337593543950336"u8, out precise, out _));
		Assert.Equal(DecodeResult.EmptyBuffer, TryReadInt64([], out _, out consumed));
		Assert.Equal(1, consumed);
		Assert.Equal(DecodeResult.InsufficientBuffer, TryReadDouble("1e+"u8, out _, out consumed, isFinalBlock: false));
		Assert.Equal(4, consumed);
	}

	[Test]
	[Arguments("Null")]
	[Arguments("Boolean")]
	[Arguments("Int64")]
	[Arguments("UInt64")]
	[Arguments("Double")]
	[Arguments("Decimal")]
	[Arguments("Token")]
	public void WritersReportExactSizeWithoutPartialWrites(string kind)
	{
		byte[] large = new byte[128];
		Assert.True(Write(kind, large, out int required));
		byte[] expected = large.AsSpan(0, required).ToArray();
		for (int length = 0; length < required; length++)
		{
			byte[] shortBuffer = Enumerable.Repeat((byte)0xCC, length).ToArray();
			Assert.False(Write(kind, shortBuffer, out int size));
			Assert.Equal(required, size);
			Assert.All(shortBuffer, b => Assert.Equal(0xCC, b));
		}

		byte[] exact = new byte[required];
		Assert.True(Write(kind, exact, out int written));
		Assert.Equal(required, written);
		Assert.Equal(expected, exact);
		JsonReader reader = new(exact);
		reader.Skip(new());
		Assert.True(reader.End);
	}

	[Test]
	public void WritersRejectInvalidValuesWithoutWriting()
	{
		byte[] destination = [0xCC, 0xCC, 0xCC];
		Assert.False(TryWrite(destination, double.NaN, out int size));
		Assert.Equal(0, size);
		Assert.False(TryWrite(destination, double.PositiveInfinity, out size));
		Assert.Equal(0, size);
		Assert.False(TryWriteNumberToken(destination, "01"u8, out size));
		Assert.Equal(0, size);
		Assert.False(TryWriteNumberToken(destination, "1 2"u8, out size));
		Assert.Equal(0, size);
		Assert.All(destination, b => Assert.Equal(0xCC, b));
	}

	private static bool Write(string kind, Span<byte> destination, out int size) => kind switch
	{
		"Null" => TryWriteNull(destination, out size),
		"Boolean" => TryWrite(destination, false, out size),
		"Int64" => TryWrite(destination, long.MinValue, out size),
		"UInt64" => TryWrite(destination, ulong.MaxValue, out size),
		"Double" => TryWrite(destination, -1.25e200, out size),
		"Decimal" => TryWrite(destination, decimal.MaxValue, out size),
		"Token" => TryWriteNumberToken(destination, "1.2300e+02"u8, out size),
		_ => throw new ArgumentOutOfRangeException(nameof(kind)),
	};
}
