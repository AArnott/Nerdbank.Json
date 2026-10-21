// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers.Text;

namespace Nerdbank.Json;

/// <summary>Provides stateless UTF-8 JSON scalar encoding and decoding.</summary>
/// <remarks>
/// Readers start exactly at a token, without skipping whitespace, comments, or a BOM.
/// Set isFinalBlock only when no more input can arrive. A scalar at the end of a non-final block needs
/// another byte to establish its boundary. Failure outputs are not decoded values.
/// An incomplete token's size is a lower bound, not a completion guarantee: further bytes may extend
/// its lexeme, and a delimiter or final-block indication is still required. Size hints are capped at
/// <see cref="int.MaxValue"/>, the maximum span length.
/// Writers leave the destination unchanged on failure and report the exact required size, or zero for invalid input.
/// Floating-point reads follow the target framework's numeric parsing behavior, including legacy rounding limitations.
/// Legacy floating-point writers use exponential notation with nine significant digits for Single and seventeen for Double
/// rather than the legacy formatter's lossy default precision. Negative zero is preserved.
/// Scalar reads and writes use overloads for boolean values and each numeric type from byte through decimal.
/// Floating-point values must be finite; single-precision values are parsed and formatted without widening to double precision.
/// </remarks>
public static class JsonPrimitives
{
	/// <summary>Identifies the outcome of a scalar decoding operation.</summary>
	public enum DecodeResult
	{
		/// <summary>A complete token was decoded.</summary>
		Success,

		/// <summary>The token is malformed, has the wrong type, or is outside the requested numeric range.</summary>
		TokenMismatch,

		/// <summary>No input byte is available.</summary>
		EmptyBuffer,

		/// <summary>A valid prefix needs additional bytes or a final-block indication.</summary>
		InsufficientBuffer,
	}

	/// <summary>Attempts to decode a JSON null literal.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="tokenSize">The consumed size on success, the minimum required buffer length on incomplete input, or zero on mismatch.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryReadNull(ReadOnlySpan<byte> source, out int tokenSize, bool isFinalBlock = true)
		=> TryReadLiteral(source, "null"u8, out tokenSize, isFinalBlock);

	/// <summary>Attempts to decode a JSON boolean literal.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">The decoded value, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size on success, the minimum required buffer length on incomplete input, or zero on mismatch.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryRead(ReadOnlySpan<byte> source, out bool value, out int tokenSize, bool isFinalBlock = true)
	{
		value = source.IsEmpty || source[0] == (byte)'t';
		DecodeResult result = TryReadLiteral(source, value ? "true"u8 : "false"u8, out tokenSize, isFinalBlock);
		if (result != DecodeResult.Success)
		{
			value = default;
		}

		return result;
	}

	/// <summary>Attempts to frame a JSON number without converting or copying its bytes.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">A borrowed view of the complete token, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size on success, the minimum required buffer length on incomplete input, or zero on mismatch.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryReadNumberToken(ReadOnlySpan<byte> source, out ReadOnlySpan<byte> value, out int tokenSize, bool isFinalBlock = true)
	{
		JsonInput input = new(source);
		DecodeResult result = TryReadNumberToken(in input, 0, isFinalBlock, out long size);
		tokenSize = size <= int.MaxValue ? (int)size : int.MaxValue;
		value = result == DecodeResult.Success ? source[..tokenSize] : default;
		return result;
	}

	/// <summary>Attempts to decode an integer into an unsigned 8-bit value.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">The decoded value, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size, minimum required size, or zero on mismatch or overflow.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryRead(ReadOnlySpan<byte> source, out byte value, out int tokenSize, bool isFinalBlock = true)
	{
		value = default;
		DecodeResult result = TryReadNumberToken(source, out ReadOnlySpan<byte> token, out tokenSize, isFinalBlock);
		if (result == DecodeResult.Success && (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length))
		{
			value = default;
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		return result;
	}

	/// <summary>Attempts to decode an integer into a signed 8-bit value.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">The decoded value, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size, minimum required size, or zero on mismatch or overflow.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryRead(ReadOnlySpan<byte> source, out sbyte value, out int tokenSize, bool isFinalBlock = true)
	{
		value = default;
		DecodeResult result = TryReadNumberToken(source, out ReadOnlySpan<byte> token, out tokenSize, isFinalBlock);
		if (result == DecodeResult.Success && (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length))
		{
			value = default;
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		return result;
	}

	/// <summary>Attempts to decode an integer into a signed 16-bit value.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">The decoded value, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size, minimum required size, or zero on mismatch or overflow.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryRead(ReadOnlySpan<byte> source, out short value, out int tokenSize, bool isFinalBlock = true)
	{
		value = default;
		DecodeResult result = TryReadNumberToken(source, out ReadOnlySpan<byte> token, out tokenSize, isFinalBlock);
		if (result == DecodeResult.Success && (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length))
		{
			value = default;
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		return result;
	}

	/// <summary>Attempts to decode an integer into an unsigned 16-bit value.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">The decoded value, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size, minimum required size, or zero on mismatch or overflow.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryRead(ReadOnlySpan<byte> source, out ushort value, out int tokenSize, bool isFinalBlock = true)
	{
		value = default;
		DecodeResult result = TryReadNumberToken(source, out ReadOnlySpan<byte> token, out tokenSize, isFinalBlock);
		if (result == DecodeResult.Success && (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length))
		{
			value = default;
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		return result;
	}

	/// <summary>Attempts to decode an integer into a signed 32-bit value.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">The decoded value, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size, minimum required size, or zero on mismatch or overflow.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryRead(ReadOnlySpan<byte> source, out int value, out int tokenSize, bool isFinalBlock = true)
	{
		value = default;
		DecodeResult result = TryReadNumberToken(source, out ReadOnlySpan<byte> token, out tokenSize, isFinalBlock);
		if (result == DecodeResult.Success && (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length))
		{
			value = default;
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		return result;
	}

	/// <summary>Attempts to decode an integer into an unsigned 32-bit value.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">The decoded value, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size, minimum required size, or zero on mismatch or overflow.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryRead(ReadOnlySpan<byte> source, out uint value, out int tokenSize, bool isFinalBlock = true)
	{
		value = default;
		DecodeResult result = TryReadNumberToken(source, out ReadOnlySpan<byte> token, out tokenSize, isFinalBlock);
		if (result == DecodeResult.Success && (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length))
		{
			value = default;
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		return result;
	}

	/// <summary>Attempts to decode an integer into a signed 64-bit value.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">The decoded value, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size, minimum required size, or zero on mismatch or overflow.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryRead(ReadOnlySpan<byte> source, out long value, out int tokenSize, bool isFinalBlock = true)
	{
		value = default;
		DecodeResult result = TryReadNumberToken(source, out ReadOnlySpan<byte> token, out tokenSize, isFinalBlock);
		if (result == DecodeResult.Success && (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length))
		{
			value = default;
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		return result;
	}

	/// <summary>Attempts to decode an integer into an unsigned 64-bit value.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">The decoded value, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size, minimum required size, or zero on mismatch or overflow.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryRead(ReadOnlySpan<byte> source, out ulong value, out int tokenSize, bool isFinalBlock = true)
	{
		value = default;
		DecodeResult result = TryReadNumberToken(source, out ReadOnlySpan<byte> token, out tokenSize, isFinalBlock);
		if (result == DecodeResult.Success && (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length))
		{
			value = default;
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		return result;
	}

	/// <summary>Attempts to decode a JSON number into a finite single-precision value.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">The decoded value, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size, minimum required size, or zero on mismatch or overflow.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryRead(ReadOnlySpan<byte> source, out float value, out int tokenSize, bool isFinalBlock = true)
	{
		value = default;
		DecodeResult result = TryReadNumberToken(source, out ReadOnlySpan<byte> token, out tokenSize, isFinalBlock);
		if (result == DecodeResult.Success && (!Utf8Parser.TryParse(token, out value, out int consumed)
			|| consumed != token.Length || float.IsInfinity(value) || float.IsNaN(value)))
		{
			value = default;
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		if (result == DecodeResult.Success && value == 0 && token[0] == (byte)'-')
		{
			value = -0.0f;
		}

		return result;
	}

	/// <summary>Attempts to decode a JSON number into a finite double-precision value.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">The decoded value, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size, minimum required size, or zero on mismatch or overflow.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryRead(ReadOnlySpan<byte> source, out double value, out int tokenSize, bool isFinalBlock = true)
	{
		value = default;
		DecodeResult result = TryReadNumberToken(source, out ReadOnlySpan<byte> token, out tokenSize, isFinalBlock);
		if (result == DecodeResult.Success && (!Utf8Parser.TryParse(token, out value, out int consumed)
			|| consumed != token.Length || double.IsInfinity(value) || double.IsNaN(value)))
		{
			value = default;
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		if (result == DecodeResult.Success && value == 0 && token[0] == (byte)'-')
		{
			value = -0.0d;
		}

		return result;
	}

	/// <summary>Attempts to decode a JSON number into a decimal value.</summary>
	/// <param name="source">Bytes starting at the token.</param>
	/// <param name="value">The decoded value, meaningful only on success.</param>
	/// <param name="tokenSize">The consumed size, minimum required size, or zero on mismatch or overflow.</param>
	/// <param name="isFinalBlock">Whether this is the end of the input.</param>
	/// <returns>The decoding classification.</returns>
	public static DecodeResult TryRead(ReadOnlySpan<byte> source, out decimal value, out int tokenSize, bool isFinalBlock = true)
	{
		value = default;
		DecodeResult result = TryReadNumberToken(source, out ReadOnlySpan<byte> token, out tokenSize, isFinalBlock);
		if (result == DecodeResult.Success && (!Utf8Parser.TryParse(token, out value, out int consumed) || consumed != token.Length))
		{
			value = default;
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		return result;
	}

	/// <summary>Attempts to write a JSON null literal.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="tokenSize">The exact number of bytes written or required.</param>
	/// <returns>Whether the destination was large enough.</returns>
	public static bool TryWriteNull(Span<byte> destination, out int tokenSize) => TryCopy("null"u8, destination, out tokenSize);

	/// <summary>Attempts to write a JSON boolean literal.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">The value to encode.</param>
	/// <param name="tokenSize">The exact number of bytes written or required.</param>
	/// <returns>Whether the destination was large enough.</returns>
	public static bool TryWrite(Span<byte> destination, bool value, out int tokenSize) => TryCopy(value ? "true"u8 : "false"u8, destination, out tokenSize);

	/// <summary>Attempts to write an unsigned 8-bit integer as a JSON number.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">The value to encode.</param>
	/// <param name="tokenSize">The exact number of bytes written or required.</param>
	/// <returns>Whether the destination was large enough.</returns>
	public static bool TryWrite(Span<byte> destination, byte value, out int tokenSize) => TryWrite(destination, (ulong)value, out tokenSize);

	/// <summary>Attempts to write a signed 8-bit integer as a JSON number.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">The value to encode.</param>
	/// <param name="tokenSize">The exact number of bytes written or required.</param>
	/// <returns>Whether the destination was large enough.</returns>
	public static bool TryWrite(Span<byte> destination, sbyte value, out int tokenSize) => TryWrite(destination, (long)value, out tokenSize);

	/// <summary>Attempts to write a signed 16-bit integer as a JSON number.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">The value to encode.</param>
	/// <param name="tokenSize">The exact number of bytes written or required.</param>
	/// <returns>Whether the destination was large enough.</returns>
	public static bool TryWrite(Span<byte> destination, short value, out int tokenSize) => TryWrite(destination, (long)value, out tokenSize);

	/// <summary>Attempts to write an unsigned 16-bit integer as a JSON number.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">The value to encode.</param>
	/// <param name="tokenSize">The exact number of bytes written or required.</param>
	/// <returns>Whether the destination was large enough.</returns>
	public static bool TryWrite(Span<byte> destination, ushort value, out int tokenSize) => TryWrite(destination, (ulong)value, out tokenSize);

	/// <summary>Attempts to write a signed 32-bit integer as a JSON number.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">The value to encode.</param>
	/// <param name="tokenSize">The exact number of bytes written or required.</param>
	/// <returns>Whether the destination was large enough.</returns>
	public static bool TryWrite(Span<byte> destination, int value, out int tokenSize) => TryWrite(destination, (long)value, out tokenSize);

	/// <summary>Attempts to write an unsigned 32-bit integer as a JSON number.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">The value to encode.</param>
	/// <param name="tokenSize">The exact number of bytes written or required.</param>
	/// <returns>Whether the destination was large enough.</returns>
	public static bool TryWrite(Span<byte> destination, uint value, out int tokenSize) => TryWrite(destination, (ulong)value, out tokenSize);

	/// <summary>Attempts to write a signed integer as a JSON number.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">The value to encode.</param>
	/// <param name="tokenSize">The exact number of bytes written or required.</param>
	/// <returns>Whether the destination was large enough.</returns>
	public static bool TryWrite(Span<byte> destination, long value, out int tokenSize)
	{
		Span<byte> scratch = stackalloc byte[20];
		Assumes.True(Utf8Formatter.TryFormat(value, scratch, out int written));
		return TryCopy(scratch[..written], destination, out tokenSize);
	}

	/// <summary>Attempts to write an unsigned integer as a JSON number.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">The value to encode.</param>
	/// <param name="tokenSize">The exact number of bytes written or required.</param>
	/// <returns>Whether the destination was large enough.</returns>
	public static bool TryWrite(Span<byte> destination, ulong value, out int tokenSize)
	{
		Span<byte> scratch = stackalloc byte[20];
		Assumes.True(Utf8Formatter.TryFormat(value, scratch, out int written));
		return TryCopy(scratch[..written], destination, out tokenSize);
	}

	/// <summary>Attempts to write a finite single-precision JSON number without widening it to double precision.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">The finite value to encode.</param>
	/// <param name="tokenSize">The exact number of bytes written or required, or zero for a non-finite value.</param>
	/// <returns>Whether the value is finite and the destination was large enough.</returns>
	public static bool TryWrite(Span<byte> destination, float value, out int tokenSize)
	{
		if (float.IsInfinity(value) || float.IsNaN(value))
		{
			tokenSize = 0;
			return false;
		}

		if (value == 0 && 1 / value < 0)
		{
			return TryCopy("-0"u8, destination, out tokenSize);
		}

		Span<byte> scratch = stackalloc byte[32];
#if NET8_0_OR_GREATER
		Assumes.True(Utf8Formatter.TryFormat(value, scratch, out int written));
#else
		Assumes.True(Utf8Formatter.TryFormat(value, scratch, out int written, new StandardFormat('E', 8)));
#endif
		return TryCopy(scratch[..written], destination, out tokenSize);
	}

	/// <summary>Attempts to write a finite double-precision JSON number.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">The finite value to encode.</param>
	/// <param name="tokenSize">The exact number of bytes written or required, or zero for a non-finite value.</param>
	/// <returns>Whether the value is finite and the destination was large enough.</returns>
	public static bool TryWrite(Span<byte> destination, double value, out int tokenSize)
	{
		if (double.IsInfinity(value) || double.IsNaN(value))
		{
			tokenSize = 0;
			return false;
		}

		if (value == 0 && 1 / value < 0)
		{
			return TryCopy("-0"u8, destination, out tokenSize);
		}

		Span<byte> scratch = stackalloc byte[32];
#if NET8_0_OR_GREATER
		Assumes.True(Utf8Formatter.TryFormat(value, scratch, out int written));
#else
		Assumes.True(Utf8Formatter.TryFormat(value, scratch, out int written, new StandardFormat('E', 16)));
#endif
		return TryCopy(scratch[..written], destination, out tokenSize);
	}

	/// <summary>Attempts to write a decimal JSON number.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">The value to encode.</param>
	/// <param name="tokenSize">The exact number of bytes written or required.</param>
	/// <returns>Whether the destination was large enough.</returns>
	public static bool TryWrite(Span<byte> destination, decimal value, out int tokenSize)
	{
		Span<byte> scratch = stackalloc byte[32];
		Assumes.True(Utf8Formatter.TryFormat(value, scratch, out int written));
		return TryCopy(scratch[..written], destination, out tokenSize);
	}

	/// <summary>Attempts to write exactly one validated JSON number without normalizing its spelling.</summary>
	/// <param name="destination">The output buffer.</param>
	/// <param name="value">An entire number token with no surrounding whitespace.</param>
	/// <param name="tokenSize">The exact required size, or zero for invalid input.</param>
	/// <returns>Whether the token is valid and the destination was large enough.</returns>
	public static bool TryWriteNumberToken(Span<byte> destination, ReadOnlySpan<byte> value, out int tokenSize)
	{
		if (TryReadNumberToken(value, out _, out int consumed) != DecodeResult.Success || consumed != value.Length)
		{
			tokenSize = 0;
			return false;
		}

		return TryCopy(value, destination, out tokenSize);
	}

	/// <summary>Frames a JSON number using the reader's contiguous or segmented input.</summary>
	/// <param name="input">The borrowed input.</param>
	/// <param name="start">The token's absolute offset.</param>
	/// <param name="isFinalBlock">Whether no more input can arrive.</param>
	/// <param name="tokenSize">The consumed or minimum required token length, or zero on mismatch.</param>
	/// <param name="cancellationToken">The active cancellation token.</param>
	/// <returns>The decoding classification.</returns>
	internal static DecodeResult TryReadNumberToken(scoped in JsonInput input, long start, bool isFinalBlock, out long tokenSize, CancellationToken cancellationToken = default)
	{
		tokenSize = 1;
		if (start == input.Length)
		{
			return DecodeResult.EmptyBuffer;
		}

		long index = start;
		byte firstIntegerDigit = input[index];
		if (firstIntegerDigit == (byte)'-')
		{
			index++;
			if (index == input.Length)
			{
				return Incomplete(isFinalBlock, index - start, out tokenSize);
			}

			firstIntegerDigit = input[index];
		}

		long integerStart = index;
		if (!ReadDigits(in input, ref index, cancellationToken)
			|| (index - integerStart > 1 && firstIntegerDigit == (byte)'0'))
		{
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		if (index < input.Length && input[index] == (byte)'.')
		{
			index++;
			if (!ReadDigits(in input, ref index, cancellationToken))
			{
				return index == input.Length ? Incomplete(isFinalBlock, index - start, out tokenSize) : Mismatch(out tokenSize);
			}
		}

		if (index < input.Length && input[index] is (byte)'e' or (byte)'E')
		{
			index++;
			if (index < input.Length && input[index] is (byte)'+' or (byte)'-')
			{
				index++;
			}

			if (!ReadDigits(in input, ref index, cancellationToken))
			{
				return index == input.Length ? Incomplete(isFinalBlock, index - start, out tokenSize) : Mismatch(out tokenSize);
			}
		}

		if (index == input.Length && !isFinalBlock)
		{
			return Incomplete(isFinalBlock, index - start, out tokenSize);
		}

		if (index < input.Length && !JsonStructureScanner.IsScalarTerminator(input[index]))
		{
			return Mismatch(out tokenSize);
		}

		tokenSize = index - start;
		return DecodeResult.Success;
	}

	private static DecodeResult TryReadLiteral(ReadOnlySpan<byte> source, ReadOnlySpan<byte> literal, out int tokenSize, bool isFinalBlock)
	{
		tokenSize = literal.Length;
		if (source.IsEmpty)
		{
			return DecodeResult.EmptyBuffer;
		}

		int commonLength = Math.Min(source.Length, literal.Length);
		if (!source[..commonLength].SequenceEqual(literal[..commonLength]))
		{
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		if (source.Length < literal.Length)
		{
			if (!isFinalBlock)
			{
				return DecodeResult.InsufficientBuffer;
			}

			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		if (source.Length == literal.Length && !isFinalBlock)
		{
			tokenSize++;
			return DecodeResult.InsufficientBuffer;
		}

		if (source.Length > literal.Length && !JsonStructureScanner.IsScalarTerminator(source[literal.Length]))
		{
			tokenSize = 0;
			return DecodeResult.TokenMismatch;
		}

		return DecodeResult.Success;
	}

	private static bool ReadDigits(scoped in JsonInput input, ref long index, CancellationToken cancellationToken)
	{
		long start = index;
		while (index < input.Length && input[index] is >= (byte)'0' and <= (byte)'9')
		{
			if ((index & 1023) == 0)
			{
				cancellationToken.ThrowIfCancellationRequested();
			}

			index++;
		}

		return index != start;
	}

	private static DecodeResult Incomplete(bool isFinalBlock, long available, out long tokenSize)
	{
		tokenSize = isFinalBlock ? 0 : checked(available + 1);
		return isFinalBlock ? DecodeResult.TokenMismatch : DecodeResult.InsufficientBuffer;
	}

	private static DecodeResult Mismatch(out long tokenSize)
	{
		tokenSize = 0;
		return DecodeResult.TokenMismatch;
	}

	private static bool TryCopy(ReadOnlySpan<byte> source, Span<byte> destination, out int tokenSize)
	{
		tokenSize = source.Length;
		return source.TryCopyTo(destination);
	}
}
