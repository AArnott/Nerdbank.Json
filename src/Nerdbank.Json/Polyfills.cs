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

		// Use GetDecoder() in a loop.
		throw new NotImplementedException();
	}
#endif
}
