// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Nerdbank.Json;

/// <summary>
/// Maintains the canonical string instances for one deserialization operation.
/// </summary>
/// <remarks>
/// The cache intentionally holds strong references only for the lifetime of the operation, avoiding the
/// application-wide string retention associated with <see cref="string.Intern(string)"/>.
/// </remarks>
internal sealed class StringInterning
{
	private const int MaxStackStringByteLength = 4096;
	private readonly Dictionary<uint, List<string>> strings = [];

	/// <summary>
	/// Returns the canonical instance for the supplied string value.
/// </summary>
	/// <param name="value">The string to intern.</param>
	/// <returns>The canonical instance of <paramref name="value"/> for this operation.</returns>
	internal string Intern(string value)
	{
		return this.GetOrAdd(value.AsSpan(), value);
	}

	/// <summary>
	/// Returns an interned string for the given UTF-8 encoded bytes without first materializing a string.
	/// </summary>
	/// <param name="value">The UTF-8 encoded string bytes.</param>
	/// <returns>The canonical string instance for this operation.</returns>
	internal string GetOrAddUtf8(ReadOnlySpan<byte> value)
	{
		if (value.IsEmpty)
		{
			return string.Empty;
		}

		char[]? rented = value.Length > MaxStackStringByteLength ? ArrayPool<char>.Shared.Rent(value.Length) : null;
		try
		{
			Span<char> characters = rented ?? stackalloc char[value.Length];
			int characterCount = GetUtf8CharacterCount(value, characters);
			return this.GetOrAdd(characters[..characterCount], candidateValue: null);
		}
		finally
		{
			if (rented is not null)
			{
				ArrayPool<char>.Shared.Return(rented);
			}
		}
	}

	private static uint CalculateHashCode(ReadOnlySpan<char> value)
	{
		const uint OffsetBasis = 2166136261;
		const uint Prime = 16777619;
		uint hashCode = OffsetBasis;
		foreach (char character in value)
		{
			hashCode = unchecked((hashCode ^ character) * Prime);
		}

		return hashCode;
	}

	private static unsafe int GetUtf8CharacterCount(ReadOnlySpan<byte> bytes, Span<char> characters)
	{
#if NET
		return Encoding.UTF8.GetChars(bytes, characters);
#else
		fixed (byte* pBytes = bytes)
		{
			fixed (char* pCharacters = characters)
			{
				return Encoding.UTF8.GetChars(pBytes, bytes.Length, pCharacters, characters.Length);
			}
		}
#endif
	}

	private string GetOrAdd(ReadOnlySpan<char> value, string? candidateValue)
	{
		uint hashCode = CalculateHashCode(value);
		if (this.strings.TryGetValue(hashCode, out List<string>? candidates))
		{
			foreach (string candidate in candidates)
			{
				if (candidate.AsSpan().SequenceEqual(value))
				{
					return candidate;
				}
			}
		}
		else
		{
			candidates = [];
			this.strings.Add(hashCode, candidates);
		}

		string interned = candidateValue ?? value.ToString();
		candidates.Add(interned);
		return interned;
	}
}
