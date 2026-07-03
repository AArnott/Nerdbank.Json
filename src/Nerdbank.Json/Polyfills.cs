// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Nerdbank.Json;

/// <summary>
/// Extension methods that polyfill missing APIs in older frameworks.
/// </summary>
internal static class Polyfills
{
#if !NET
	/// <summary>
	/// Gets a string from a span of bytes using the specified encoding.
	/// </summary>
	/// <param name="encoding">The encoding to use.</param>
	/// <param name="bytes">The span of bytes to decode.</param>
	/// <returns>The decoded string.</returns>
	internal static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> bytes)
	{
		if (bytes.IsEmpty)
		{
			return string.Empty;
		}

		fixed (byte* pBytes = bytes)
		{
			return encoding.GetString(pBytes, bytes.Length);
		}
	}

	/// <summary>
	/// Gets a string from a sequence of bytes using the specified encoding.
	/// </summary>
	/// <param name="encoding">The encoding to use.</param>
	/// <param name="bytes">The sequence of bytes to decode.</param>
	/// <returns>The decoded string.</returns>
	internal static unsafe string GetString(this Encoding encoding, ReadOnlySequence<byte> bytes)
	{
		if (bytes.IsEmpty)
		{
			return string.Empty;
		}

		if (bytes.IsSingleSegment)
		{
			return GetString(encoding, bytes.First.Span);
		}

		Decoder decoder = encoding.GetDecoder();
		StringBuilder builder = new(bytes.Length <= int.MaxValue ? (int)bytes.Length : int.MaxValue);
		char[]? charBuffer = null;

		try
		{
			foreach (ReadOnlyMemory<byte> segment in bytes)
			{
				ReadOnlySpan<byte> span = segment.Span;
				if (span.IsEmpty)
				{
					continue;
				}

				int requiredChars = encoding.GetMaxCharCount(span.Length);
				if (charBuffer is null || charBuffer.Length < requiredChars)
				{
					if (charBuffer is not null)
					{
						ArrayPool<char>.Shared.Return(charBuffer);
					}

					charBuffer = ArrayPool<char>.Shared.Rent(requiredChars);
				}

				fixed (byte* pBytes = span)
				{
					fixed (char* pChars = charBuffer)
					{
						decoder.Convert(pBytes, span.Length, pChars, charBuffer.Length, flush: false, out int bytesUsed, out int charsUsed, out bool completed);
						if (bytesUsed != span.Length || !completed)
						{
							throw new InvalidOperationException("Unexpected incomplete character decoding.");
						}

						builder.Append(charBuffer, 0, charsUsed);
					}
				}
			}

			if (charBuffer is null)
			{
				return string.Empty;
			}

			decoder.Convert(Array.Empty<byte>(), 0, 0, charBuffer, 0, charBuffer.Length, flush: true, out _, out int finalCharsUsed, out _);
			builder.Append(charBuffer, 0, finalCharsUsed);
			return builder.ToString();
		}
		finally
		{
			if (charBuffer is not null)
			{
				ArrayPool<char>.Shared.Return(charBuffer);
			}
		}
	}
#endif
}
