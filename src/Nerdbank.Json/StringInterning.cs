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
/// Modern runtimes use randomized ordinal hashing. Legacy runtimes switch to collision-resistant
/// hashing when a deterministic hash bucket exceeds 100 entries.
/// </remarks>
#if !NET
[GenerateShapeFor<string>]
#endif
internal sealed partial class StringInterning
{
	private const int MaxStackStringByteLength = 4096;
	private readonly Dictionary<uint, List<string>> strings = [];
#if !NET
	private Dictionary<string, string>? secureStrings;
#endif

	/// <summary>
	/// Returns the canonical instance for the supplied characters, allocating a string only on a cache miss.
	/// </summary>
	/// <param name="value">The characters to intern.</param>
	/// <returns>The canonical instance of <paramref name="value"/> for this operation.</returns>
	internal string Intern(ReadOnlySpan<char> value) => this.GetOrAdd(value);

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
			int characterCount = Encoding.UTF8.GetChars(value, characters);
			return this.Intern(characters[..characterCount]);
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
#if NET
		return unchecked((uint)string.GetHashCode(value, StringComparison.Ordinal));
#else
		const uint OffsetBasis = 2166136261;
		const uint Prime = 16777619;
		uint hashCode = OffsetBasis;
		foreach (char character in value)
		{
			hashCode = unchecked((hashCode ^ character) * Prime);
		}

		return hashCode;
#endif
	}

	private string GetOrAdd(ReadOnlySpan<char> value)
	{
#if !NET
		if (this.secureStrings is not null)
		{
			return this.GetOrAddSecure(value);
		}
#endif

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

#if !NET
			if (candidates.Count >= 100)
			{
				this.secureStrings = new(MessagePack.StructuralEqualityComparer.GetHashCollisionResistant(
					PolyType.Abstractions.TypeShapeResolver.ResolveDynamicOrThrow<string, StringInterning>()));
				foreach (List<string> bucket in this.strings.Values)
				{
					foreach (string candidate in bucket)
					{
						this.secureStrings.Add(candidate, candidate);
					}
				}

				this.strings.Clear();
				return this.GetOrAddSecure(value);
			}
#endif
		}
		else
		{
			candidates = [];
			this.strings.Add(hashCode, candidates);
		}

		string interned = value.ToString();
		candidates.Add(interned);
		return interned;
	}

#if !NET
	/// <summary>
	/// Uses collision-resistant hashing after a legacy-runtime cache detects excessive collisions.
	/// </summary>
	/// <param name="value">The characters to intern.</param>
	/// <returns>The canonical string instance.</returns>
	private string GetOrAddSecure(ReadOnlySpan<char> value)
	{
		string candidate = value.ToString();
		if (this.secureStrings!.TryGetValue(candidate, out string? existing))
		{
			return existing;
		}

		this.secureStrings.Add(candidate, candidate);
		return candidate;
	}
#endif
}
