// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET8_0_OR_GREATER
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Nerdbank.Json;

/// <summary>Locates lexical string boundaries without enlarging the scalar reader's loop.</summary>
internal static class JsonStringSearch
{
	private static readonly SearchValues<byte> SpecialBytes = SearchValues.Create(
		"\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\b\t\n\u000B\f\r\u000E\u000F\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F\"\\"u8);

	/// <summary>Finds the first quote, backslash, or unescaped control byte.</summary>
	/// <param name="input">The contiguous UTF-8 input.</param>
	/// <param name="start">The offset where the ordinary string run continues.</param>
	/// <returns>The special byte's offset, or the input length when no terminator exists.</returns>
	[MethodImpl(MethodImplOptions.NoInlining)]
	internal static int FindSpecialByte(ReadOnlySpan<byte> input, int start)
	{
		int offset = input[start..].IndexOfAny(SpecialBytes);
		return offset < 0 ? input.Length : start + offset;
	}
}
#endif
