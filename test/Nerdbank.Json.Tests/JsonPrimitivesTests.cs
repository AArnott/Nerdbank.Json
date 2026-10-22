// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using static Nerdbank.Json.JsonPrimitives;

public class JsonPrimitivesTests
{
	private const string ScalarDelimiters = " \t\r\n,:{}[]\"/";

	private delegate DecodeResult ScalarReader<T>(ReadOnlySpan<byte> source, out T value, out int tokenSize, bool isFinalBlock);

	private delegate bool ScalarWriter<T>(Span<byte> destination, T value, out int tokenSize);

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
			DecodeResult partial = boolean ? TryRead(prefix, out bool _, out int size, isFinalBlock: false) : TryReadNull(prefix, out size, isFinalBlock: false);
			Assert.Equal(length == 0 ? DecodeResult.EmptyBuffer : DecodeResult.InsufficientBuffer, partial);
			Assert.Equal(length == bytes.Length ? bytes.Length + 1 : boolean && length == 0 ? 4 : bytes.Length, size);
			DecodeResult final = boolean ? TryRead(prefix, out bool _, out size) : TryReadNull(prefix, out size);
			Assert.Equal(length == 0 ? DecodeResult.EmptyBuffer : length == bytes.Length ? DecodeResult.Success : DecodeResult.TokenMismatch, final);
		}

		byte[] terminated = Encoding.UTF8.GetBytes(text + ",");
		DecodeResult complete = boolean ? TryRead(terminated, out bool _, out int consumed, isFinalBlock: false) : TryReadNull(terminated, out consumed, isFinalBlock: false);
		Assert.Equal(DecodeResult.Success, complete);
		Assert.Equal(bytes.Length, consumed);
		if (boolean)
		{
			Assert.Equal(DecodeResult.Success, TryRead(bytes, out bool value, out _));
			Assert.Equal(text == "true", value);
		}
	}

	[Test]
	[Arguments("0")]
	[Arguments("-0")]
	[Arguments("12345")]
	[Arguments("-12345")]
	[Arguments("12345.625")]
	[Arguments("-12345.625")]
	[Arguments("0.625")]
	[Arguments("-0.625")]
	[Arguments("0e+2")]
	[Arguments("-0E-2")]
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
	[Arguments("00", false)]
	[Arguments("012345", false)]
	[Arguments("-012345", false)]
	[Arguments("00.625", false)]
	[Arguments("-00e+2", false)]
	[Arguments("-", true)]
	[Arguments("12345.", true)]
	[Arguments("12345e", true)]
	[Arguments("12345e+", true)]
	[Arguments("-0.", true)]
	public void NumberTokenInvalidOrIncompleteOutputs(string text, bool incomplete)
	{
		foreach (bool final in new[] { false, true })
		{
			foreach (string delimiter in new[] { string.Empty, "," })
			{
				byte[] bytes = Encoding.UTF8.GetBytes(text + delimiter);
				bool needsMore = incomplete && !final && delimiter.Length == 0;
				DecodeResult expected = needsMore ? DecodeResult.InsufficientBuffer : DecodeResult.TokenMismatch;
				int expectedSize = needsMore ? bytes.Length + 1 : 0;
				Assert.Equal(expected, TryReadNumberToken(bytes, out ReadOnlySpan<byte> token, out int size, final));
				Assert.True(token.IsEmpty);
				Assert.Equal(expectedSize, size);
				Assert.Equal(expected, TryRead(bytes, out double value, out size, final));
				Assert.Equal(0, value);
				Assert.Equal(expectedSize, size);
			}
		}
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
		Assert.Equal(DecodeResult.TokenMismatch, TryRead(bytes, out double decoded, out size));
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
		Assert.Equal(DecodeResult.TokenMismatch, TryRead(bytes, out bool value, out int booleanSize, isFinalBlock: false));
		Assert.False(value);
		Assert.Equal(0, booleanSize);
	}

	[Test]
	public void NumericRangesAndMismatches()
	{
		Assert.Equal(DecodeResult.Success, TryRead("-9223372036854775808"u8, out long signed, out int consumed));
		Assert.Equal(long.MinValue, signed);
		Assert.Equal(20, consumed);
		Assert.Equal(DecodeResult.Success, TryRead("18446744073709551615"u8, out ulong unsigned, out consumed));
		Assert.Equal(ulong.MaxValue, unsigned);
		Assert.Equal(20, consumed);
		Assert.Equal(DecodeResult.TokenMismatch, TryRead("9223372036854775808"u8, out signed, out consumed));
		Assert.Equal(0, consumed);
		Assert.Equal(DecodeResult.TokenMismatch, TryRead("18446744073709551616"u8, out unsigned, out _));
		Assert.Equal(DecodeResult.TokenMismatch, TryRead("-1"u8, out unsigned, out _));
		Assert.Equal(DecodeResult.TokenMismatch, TryRead("1.0"u8, out signed, out _));
		Assert.Equal(DecodeResult.Success, TryRead("-1.25e2"u8, out double floating, out _));
		Assert.Equal(-125, floating);
		Assert.Equal(DecodeResult.TokenMismatch, TryRead("1e999"u8, out floating, out _));
		Assert.Equal(DecodeResult.Success, TryRead("79228162514264337593543950335"u8, out decimal precise, out _));
		Assert.Equal(decimal.MaxValue, precise);
		Assert.Equal(DecodeResult.TokenMismatch, TryRead("79228162514264337593543950336"u8, out precise, out _));
		Assert.Equal(DecodeResult.EmptyBuffer, TryRead([], out long _, out consumed));
		Assert.Equal(1, consumed);
		Assert.Equal(DecodeResult.InsufficientBuffer, TryRead("1e+"u8, out double _, out consumed, isFinalBlock: false));
		Assert.Equal(4, consumed);
	}

	[Test]
	public void IntegerOverloadsCoverRangesAndTransactionalWrites()
	{
		VerifyInteger<byte>(byte.MinValue, byte.MaxValue, "-1", "256", TryRead, TryWrite);
		VerifyInteger<sbyte>(sbyte.MinValue, sbyte.MaxValue, "-129", "128", TryRead, TryWrite);
		VerifyInteger<short>(short.MinValue, short.MaxValue, "-32769", "32768", TryRead, TryWrite);
		VerifyInteger<ushort>(ushort.MinValue, ushort.MaxValue, "-1", "65536", TryRead, TryWrite);
		VerifyInteger<int>(int.MinValue, int.MaxValue, "-2147483649", "2147483648", TryRead, TryWrite);
		VerifyInteger<uint>(uint.MinValue, uint.MaxValue, "-1", "4294967296", TryRead, TryWrite);
		VerifyInteger<long>(long.MinValue, long.MaxValue, "-9223372036854775809", "9223372036854775808", TryRead, TryWrite);
		VerifyInteger<ulong>(ulong.MinValue, ulong.MaxValue, "-1", "18446744073709551616", TryRead, TryWrite);
	}

	[Test]
	public void FloatingPointAndDecimalOverloadsCoverRangesAndTransactionalWrites()
	{
		VerifyNumeric<float>(float.MinValue, TryRead, TryWrite);
		VerifyNumeric<float>(float.MaxValue, TryRead, TryWrite);
		VerifyNumeric<float>(float.Epsilon, TryRead, TryWrite);
		VerifyNumeric<float>(-float.Epsilon, TryRead, TryWrite);
		VerifyNumeric<float>(0, TryRead, TryWrite);
		VerifyNumeric<double>(double.MinValue, TryRead, TryWrite);
		VerifyNumeric<double>(double.MaxValue, TryRead, TryWrite);
		VerifyNumeric<double>(double.Epsilon, TryRead, TryWrite);
		VerifyNumeric<double>(-double.Epsilon, TryRead, TryWrite);
		VerifyNumeric<double>(0, TryRead, TryWrite);
		VerifyNumeric<decimal>(decimal.MinValue, TryRead, TryWrite);
		VerifyNumeric<decimal>(decimal.MaxValue, TryRead, TryWrite);
		VerifyNumeric<decimal>(0.0000000000000000000000000001m, TryRead, TryWrite);
		VerifyNumeric<decimal>(0, TryRead, TryWrite);
		VerifyMismatch<float>("3.4028236e38", TryRead);
		VerifyMismatch<float>("-3.4028236e38", TryRead);
		VerifyMismatch<double>("1e309", TryRead);
		VerifyMismatch<double>("-1e309", TryRead);
		VerifyMismatch<decimal>("79228162514264337593543950336", TryRead);
		VerifyMismatch<decimal>("-79228162514264337593543950336", TryRead);
	}

	[Test]
	[Arguments(0.1f)]
	[Arguments(1.2345678f)]
	[Arguments(16777216f)]
	public void SinglePrecisionUsesItsOwnFormattingAndRoundTrips(float value)
	{
		byte[] destination = new byte[32];
		Assert.True(TryWrite(destination, value, out int size));
#if NET8_0_OR_GREATER
		Assert.Equal(value.ToString("R", CultureInfo.InvariantCulture), Encoding.UTF8.GetString(destination, 0, size));
#else
		Assert.Equal(value.ToString("E8", CultureInfo.InvariantCulture), Encoding.UTF8.GetString(destination, 0, size));
#endif
		Assert.Equal(DecodeResult.Success, TryRead(destination.AsSpan(0, size), out float decoded, out int consumed));
		Assert.Equal(value, decoded);
		Assert.Equal(size, consumed);
	}

	[Test]
	public void SinglePrecisionParsingMatchesReader()
	{
		ReadOnlySpan<byte> source = "1.0000000596046448"u8;
		Assert.Equal(DecodeResult.Success, TryRead(source, out float value, out int size));
		JsonReader reader = new(source);
		Assert.Equal(reader.ReadSingle(), value);
#if NET8_0_OR_GREATER
		Assert.Equal(1.00000011920928955078125f, value);
#endif
		Assert.Equal(18, size);
	}

	[Test]
	public void FloatingPointNegativeZeroIsPreserved()
	{
		byte[] buffer = new byte[32];
		Assert.Equal(DecodeResult.Success, TryRead("-0"u8, out float single, out _));
		Assert.Equal(BitConverter.GetBytes(-0.0f), BitConverter.GetBytes(single));
		Assert.True(TryWrite(buffer, single, out int size));
		Assert.Equal("-0", Encoding.UTF8.GetString(buffer, 0, size));
		Assert.Equal(DecodeResult.Success, TryRead(buffer.AsSpan(0, size), out single, out _));
		Assert.Equal(BitConverter.GetBytes(-0.0f), BitConverter.GetBytes(single));
		Assert.Equal(DecodeResult.Success, TryRead("-0"u8, out double precision, out _));
		Assert.Equal(BitConverter.GetBytes(-0.0d), BitConverter.GetBytes(precision));
		Assert.True(TryWrite(buffer, precision, out size));
		Assert.Equal("-0", Encoding.UTF8.GetString(buffer, 0, size));
	}

	[Test]
	[Arguments("true")]
	[Arguments("false")]
	public void BooleanOverloadZerosFailuresAndHonorsAllDelimiters(string text)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(text);
		for (int length = 0; length <= bytes.Length; length++)
		{
			Assert.Equal(length == 0 ? DecodeResult.EmptyBuffer : DecodeResult.InsufficientBuffer, TryRead(bytes.AsSpan(0, length), out bool value, out _, isFinalBlock: false));
			Assert.False(value);
			if (length < bytes.Length)
			{
				Assert.Equal(length == 0 ? DecodeResult.EmptyBuffer : DecodeResult.TokenMismatch, TryRead(bytes.AsSpan(0, length), out value, out int size));
				Assert.False(value);
				Assert.Equal(length == 0 ? 4 : 0, size);
			}
		}

		foreach (char delimiter in ScalarDelimiters)
		{
			foreach (bool final in new[] { false, true })
			{
				Assert.Equal(DecodeResult.Success, TryRead(Encoding.UTF8.GetBytes(text + delimiter), out bool value, out int size, final));
				Assert.Equal(text == "true", value);
				Assert.Equal(bytes.Length, size);
			}
		}
	}

	[Test]
	[Arguments("Null")]
	[Arguments("Boolean")]
	[Arguments("True")]
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
		switch (kind)
		{
			case "Null":
				reader.ReadNull();
				break;
			case "Boolean":
				Assert.False(reader.ReadBoolean());
				break;
			case "True":
				Assert.True(reader.ReadBoolean());
				break;
			case "Int64":
				Assert.Equal(long.MinValue, reader.ReadInt64());
				break;
			case "UInt64":
				Assert.Equal(ulong.MaxValue, reader.ReadUInt64());
				break;
			case "Double":
				Assert.Equal(-1.25e200, reader.ReadDouble());
				break;
			case "Decimal":
				Assert.Equal(decimal.MaxValue, reader.ReadDecimal());
				break;
			case "Token":
				Assert.Equal("1.2300e+02", reader.ReadNumberToken());
				break;
			default:
				Assert.Fail("Unknown primitive kind.");
				break;
		}

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
		Assert.False(TryWrite(destination, double.NegativeInfinity, out size));
		Assert.Equal(0, size);
		Assert.False(TryWrite(destination, float.NaN, out size));
		Assert.Equal(0, size);
		Assert.False(TryWrite(destination, float.PositiveInfinity, out size));
		Assert.Equal(0, size);
		Assert.False(TryWrite(destination, float.NegativeInfinity, out size));
		Assert.Equal(0, size);
		Assert.False(TryWriteNumberToken(destination, "01"u8, out size));
		Assert.Equal(0, size);
		Assert.False(TryWriteNumberToken(destination, "1 2"u8, out size));
		Assert.Equal(0, size);
		Assert.All(destination, b => Assert.Equal(0xCC, b));
	}

	private static void VerifyInteger<T>(T minimum, T maximum, string belowMinimum, string aboveMaximum, ScalarReader<T> read, ScalarWriter<T> write)
		where T : struct, IFormattable
	{
		foreach (T value in new[] { minimum, maximum, default })
		{
			VerifyNumeric(value, read, write);
			byte[] destination = new byte[32];
			Assert.True(write(destination, value, out int size));
			Assert.Equal(value.ToString(null, CultureInfo.InvariantCulture), Encoding.UTF8.GetString(destination, 0, size));
		}

		foreach (string text in new[] { belowMinimum, aboveMaximum, "1.0", "1e0" })
		{
			VerifyMismatch(text, read);
		}
	}

	private static void VerifyNumeric<T>(T expected, ScalarReader<T> read, ScalarWriter<T> write)
		where T : struct
	{
		byte[] buffer = Enumerable.Repeat((byte)0xCC, 128).ToArray();
		Assert.True(write(buffer, expected, out int required));
		Assert.All(buffer.Skip(required), b => Assert.Equal(0xCC, b));
		byte[] token = buffer.AsSpan(0, required).ToArray();
		for (int length = 0; length < required; length++)
		{
			byte[] destination = Enumerable.Repeat((byte)0xCC, length).ToArray();
			Assert.False(write(destination, expected, out int size));
			Assert.Equal(required, size);
			Assert.All(destination, b => Assert.Equal(0xCC, b));
		}

		byte[] exact = new byte[required];
		Assert.True(write(exact, expected, out int written));
		Assert.Equal(required, written);
		Assert.Equal(token, exact);
		Assert.Equal(DecodeResult.Success, read(token, out T decoded, out int consumed, true));
		Assert.Equal(expected, decoded);
		Assert.Equal(required, consumed);
		for (int length = 0; length <= required; length++)
		{
			ReadOnlySpan<byte> prefix = token.AsSpan(0, length);
			Assert.Equal(length == 0 ? DecodeResult.EmptyBuffer : DecodeResult.InsufficientBuffer, read(prefix, out decoded, out consumed, false));
			Assert.Equal(default(T), decoded);
			Assert.Equal(length + 1, consumed);
			DecodeResult framed = TryReadNumberToken(prefix, out _, out int framedSize);
			Assert.Equal(framed, read(prefix, out decoded, out consumed, true));
			Assert.Equal(framedSize, consumed);
			if (framed != DecodeResult.Success)
			{
				Assert.Equal(default(T), decoded);
			}
		}

		foreach (char delimiter in ScalarDelimiters)
		{
			byte[] terminated = token.Concat(new[] { (byte)delimiter }).ToArray();
			foreach (bool final in new[] { false, true })
			{
				Assert.Equal(DecodeResult.Success, read(terminated, out decoded, out consumed, final));
				Assert.Equal(expected, decoded);
				Assert.Equal(required, consumed);
			}
		}

		foreach (string text in new[] { "null", "true", "NaN", "Infinity", "-Infinity", "01", "1x", " 1", "1.", "1e+" })
		{
			VerifyMismatch(text, read);
		}
	}

	private static void VerifyMismatch<T>(string text, ScalarReader<T> read)
		where T : struct
	{
		foreach (string suffix in new[] { string.Empty }.Concat(ScalarDelimiters.Select(c => c.ToString())))
		{
			byte[] source = Encoding.UTF8.GetBytes(text + suffix);
			foreach (bool final in new[] { false, true })
			{
				DecodeResult framed = TryReadNumberToken(source, out _, out int framedSize, final);
				DecodeResult result = read(source, out T value, out int size, final);
				Assert.Equal(framed == DecodeResult.InsufficientBuffer ? framed : DecodeResult.TokenMismatch, result);
				Assert.Equal(default(T), value);
				Assert.Equal(framed == DecodeResult.InsufficientBuffer ? framedSize : 0, size);
			}
		}
	}

	private static bool Write(string kind, Span<byte> destination, out int size) => kind switch
	{
		"Null" => TryWriteNull(destination, out size),
		"Boolean" => TryWrite(destination, false, out size),
		"True" => TryWrite(destination, true, out size),
		"Int64" => TryWrite(destination, long.MinValue, out size),
		"UInt64" => TryWrite(destination, ulong.MaxValue, out size),
		"Double" => TryWrite(destination, -1.25e200, out size),
		"Decimal" => TryWrite(destination, decimal.MaxValue, out size),
		"Token" => TryWriteNumberToken(destination, "1.2300e+02"u8, out size),
		_ => throw new ArgumentOutOfRangeException(nameof(kind)),
	};
}
