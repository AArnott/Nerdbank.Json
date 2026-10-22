// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

namespace Nerdbank.Json;

/// <summary>Represents the exact UTF-8 bytes of a JSON value for deferred deserialization.</summary>
/// <remarks>
/// Construction borrows unvalidated bytes. The caller must keep borrowed storage alive and unchanged.
/// Call <see cref="ToOwned"/> before releasing that storage.
/// The built-in converter always deserializes an owned copy and validates values before writing them.
/// A default instance has an empty payload, not a JSON null, and cannot be serialized.
/// </remarks>
[GenerateShape]
public partial struct RawJson : IEquatable<RawJson>
{
	/// <summary>Initializes a new instance of the <see cref="RawJson"/> struct.</summary>
	/// <param name="json">The borrowed, unvalidated UTF-8 JSON bytes.</param>
	public RawJson(ReadOnlySequence<byte> json) => this.Json = json;

	/// <summary>Gets the exact JSON payload, including its original token spelling.</summary>
	public ReadOnlySequence<byte> Json { get; private set; }

	/// <summary>Gets a value indicating whether this instance has an independent, owned lifetime.</summary>
	public bool IsOwned { get; internal set; }

	/// <summary>Borrows a sequence of JSON bytes.</summary>
	/// <param name="json">The borrowed bytes.</param>
	/// <returns>The raw JSON descriptor.</returns>
	public static explicit operator RawJson(ReadOnlySequence<byte> json) => new(json);

	/// <summary>Borrows a memory region containing JSON bytes.</summary>
	/// <param name="json">The borrowed bytes.</param>
	/// <returns>The raw JSON descriptor.</returns>
	public static explicit operator RawJson(ReadOnlyMemory<byte> json) => new(new(json));

	/// <summary>Borrows a memory region containing JSON bytes.</summary>
	/// <param name="json">The borrowed bytes.</param>
	/// <returns>The raw JSON descriptor.</returns>
	public static explicit operator RawJson(Memory<byte> json) => new(new(json));

	/// <summary>Borrows an array containing JSON bytes.</summary>
	/// <param name="json">The borrowed bytes.</param>
	/// <returns>The raw JSON descriptor.</returns>
	public static explicit operator RawJson(byte[] json) => new(new(json));

	/// <summary>Gets the raw JSON sequence.</summary>
	/// <param name="json">The raw JSON descriptor.</param>
	/// <returns>The borrowed or owned JSON payload.</returns>
	public static implicit operator ReadOnlySequence<byte>(RawJson json) => json.Json;

	/// <inheritdoc/>
	public readonly bool Equals(RawJson other)
	{
		if (this.Json.Length != other.Json.Length)
		{
			return false;
		}

		ReadOnlySequence<byte>.Enumerator left = this.Json.GetEnumerator();
		ReadOnlySequence<byte>.Enumerator right = other.Json.GetEnumerator();
		ReadOnlySpan<byte> leftSpan = default;
		ReadOnlySpan<byte> rightSpan = default;
		while (true)
		{
			while (leftSpan.IsEmpty && left.MoveNext())
			{
				leftSpan = left.Current.Span;
			}

			while (rightSpan.IsEmpty && right.MoveNext())
			{
				rightSpan = right.Current.Span;
			}

			if (leftSpan.IsEmpty || rightSpan.IsEmpty)
			{
				return leftSpan.IsEmpty && rightSpan.IsEmpty;
			}

			int length = Math.Min(leftSpan.Length, rightSpan.Length);
			if (!leftSpan[..length].SequenceEqual(rightSpan[..length]))
			{
				return false;
			}

			leftSpan = leftSpan[length..];
			rightSpan = rightSpan[length..];
		}
	}

	/// <inheritdoc/>
	public override readonly bool Equals(object? obj) => obj is RawJson other && this.Equals(other);

	/// <inheritdoc/>
	public override readonly int GetHashCode()
	{
		int hash = 17;
		foreach (ReadOnlyMemory<byte> segment in this.Json)
		{
			foreach (byte value in segment.Span)
			{
				hash = unchecked((hash * 31) + value);
			}
		}

		return hash;
	}

	/// <summary>Copies borrowed storage and retains the owned copy in this instance.</summary>
	/// <returns>An independently owned payload, or this instance if it already owns its storage.</returns>
	/// <remarks>The copy uses GC-owned storage, never a rented buffer. Repeated calls on a mutable instance reuse the copy.</remarks>
	public RawJson ToOwned()
	{
		if (!this.IsOwned)
		{
			this.Json = new(this.Json.ToArray());
			this.IsOwned = true;
		}

		return this;
	}

	/// <inheritdoc/>
	public override readonly string ToString() => "<raw json>";
}
