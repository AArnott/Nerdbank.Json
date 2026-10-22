// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Nerdbank.Json;

/// <summary>Provides contiguous and segmented access to borrowed JSON input.</summary>
internal readonly ref struct JsonInput
{
	private readonly ReadOnlySpan<byte> span;
	private readonly Segments? segments;

	/// <summary>Initializes a new instance of the <see cref="JsonInput"/> struct.</summary>
	/// <param name="span">The borrowed input.</param>
	internal JsonInput(ReadOnlySpan<byte> span)
	{
		this.span = span;
		this.Length = span.Length;
	}

	/// <summary>Initializes a new instance of the <see cref="JsonInput"/> struct.</summary>
	/// <param name="sequence">The borrowed input.</param>
	internal JsonInput(scoped in ReadOnlySequence<byte> sequence)
	{
		this.span = sequence.IsSingleSegment ? sequence.First.Span : default;
		this.segments = sequence.IsSingleSegment ? null : new(sequence);
		this.Length = sequence.Length;
	}

	/// <summary>Initializes a new instance of the <see cref="JsonInput"/> struct with an independent segment cursor.</summary>
	/// <param name="segments">The copied navigation state.</param>
	private JsonInput(Segments segments)
	{
		this.segments = segments;
		this.Length = segments.Sequence.Length;
	}

	/// <summary>Gets the input length without narrowing sequence offsets.</summary>
	internal long Length { get; }

	/// <summary>Gets the borrowed contiguous input, or an empty span for segmented input.</summary>
	internal ReadOnlySpan<byte> ContiguousSpan => this.span;

	/// <summary>Gets a byte at an absolute input offset.</summary>
	/// <param name="index">The absolute offset.</param>
	internal byte this[long index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.segments is null ? this.span[checked((int)index)] : this.segments.GetByte(index);
	}

	/// <summary>Copies a range into independently owned storage.</summary>
	/// <param name="start">The token offset.</param>
	/// <param name="length">The token length.</param>
	/// <returns>A GC-owned copy for an ownership-bearing API, never pooled storage.</returns>
	internal byte[] ToArray(long start, long length)
	{
		byte[] copy = new byte[checked((int)length)];
		this.CopyTo(start, copy);
		return copy;
	}

	/// <summary>Compares a short literal without copying segmented input or disturbing the token-slice cursor.</summary>
	/// <param name="start">The absolute literal offset.</param>
	/// <param name="literal">The expected bytes.</param>
	/// <returns>Whether the bytes match.</returns>
	internal bool Matches(long start, ReadOnlySpan<byte> literal)
	{
		if (start < 0 || this.Length - start < literal.Length)
		{
			return false;
		}

		if (this.segments is null)
		{
			return this.span.Slice(checked((int)start), literal.Length).SequenceEqual(literal);
		}

		for (int i = 0; i < literal.Length; i++)
		{
			if (this.segments.GetByte(start + i) != literal[i])
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>Gets a borrowed span only when the requested range is contiguous.</summary>
	/// <param name="start">The range offset.</param>
	/// <param name="length">The range length.</param>
	/// <param name="value">Receives the borrowed span.</param>
	/// <returns>Whether no copy is required.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal bool TryGetSpan(long start, long length, out ReadOnlySpan<byte> value)
	{
		if (this.segments is null)
		{
			value = this.span.Slice(checked((int)start), checked((int)length));
			return true;
		}

		return this.segments.TryGetSpan(start, length, out value);
	}

	/// <summary>Copies a range into caller-owned storage without materializing a token array.</summary>
	/// <param name="start">The range offset.</param>
	/// <param name="destination">The destination, whose length specifies the range length.</param>
	internal void CopyTo(long start, Span<byte> destination)
	{
		if (this.segments is null)
		{
			this.span.Slice(checked((int)start), destination.Length).CopyTo(destination);
		}
		else
		{
			this.segments.Slice(start, destination.Length).CopyTo(destination);
		}
	}

	/// <summary>Copies a known split token into caller scratch or a rented buffer.</summary>
	/// <param name="start">The token offset.</param>
	/// <param name="length">The token length.</param>
	/// <param name="scratch">Caller-owned storage for short tokens.</param>
	/// <returns>A token that must be disposed by the consuming caller before its scratch goes out of scope.</returns>
	internal Token CopyToken(long start, long length, Span<byte> scratch)
	{
		int count = checked((int)length);
		if (count > scratch.Length)
		{
			return this.CopyLargeToken(start, count);
		}

		Span<byte> destination = scratch[..count];
		this.CopyTo(start, destination);
		return new(destination, null);
	}

	/// <summary>Slices segmented input using its cached sequence cursor.</summary>
	/// <param name="start">The absolute range offset.</param>
	/// <param name="length">The range length.</param>
	/// <returns>The borrowed slice, or null for contiguous input.</returns>
	internal ReadOnlySequence<byte>? SliceSequence(long start, long length) => this.segments?.Slice(start, length);

	/// <summary>Copies navigation state for an independent peek without copying input storage.</summary>
	/// <returns>The same contiguous view, or a segmented view with an independent cursor.</returns>
	internal JsonInput CreatePeekInput() => this.segments is null ? this : new(this.segments.CreatePeek());

	/// <summary>Saves the byte cursor for an allocation-free, tentative token read.</summary>
	/// <returns>The current navigation-only checkpoint.</returns>
	internal (ReadOnlyMemory<byte> Memory, SequencePosition Next, long Start) SaveCursor() => this.segments?.SaveCursor() ?? default;

	/// <summary>Restores a byte cursor after a tentative read did not consume a token.</summary>
	/// <param name="cursor">A checkpoint from this input.</param>
	internal void RestoreCursor((ReadOnlyMemory<byte> Memory, SequencePosition Next, long Start) cursor) => this.segments?.RestoreCursor(cursor);

	/// <summary>Finds the next quote or backslash without materializing input.</summary>
	/// <param name="start">The offset at which to start searching.</param>
	/// <returns>The absolute delimiter offset, or -1.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal long IndexOfDelimiter(long start)
	{
		if (this.segments is null)
		{
			int offset = this.span[checked((int)start)..].IndexOfAny((byte)'"', (byte)'\\');
			return offset < 0 ? -1 : start + offset;
		}

		return this.segments.IndexOfSegmentedDelimiter(start);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private Token CopyLargeToken(long start, int length)
	{
		byte[] rented = ArrayPool<byte>.Shared.Rent(length);
		bool copied = false;
		try
		{
			Span<byte> destination = rented.AsSpan(0, length);
			this.CopyTo(start, destination);
			copied = true;
			return new(destination, rented);
		}
		finally
		{
			if (!copied)
			{
				ArrayPool<byte>.Shared.Return(rented);
			}
		}
	}

	/// <summary>A borrowed or temporarily rented token, consumed within its caller's scratch lifetime.</summary>
	internal readonly ref struct Token
	{
		private readonly byte[]? rented;

		/// <summary>Initializes a new instance of the <see cref="Token"/> struct.</summary>
		/// <param name="span">The token bytes.</param>
		/// <param name="rented">Storage to return, or null for borrowed input or scratch.</param>
		internal Token(ReadOnlySpan<byte> span, byte[]? rented)
		{
			this.Span = span;
			this.rented = rented;
		}

		/// <summary>Gets the token bytes until disposal.</summary>
		internal ReadOnlySpan<byte> Span { get; }

		/// <summary>Returns any rented storage after the caller finishes consuming the token.</summary>
		public void Dispose()
		{
			if (this.rented is not null)
			{
				ArrayPool<byte>.Shared.Return(this.rented);
			}
		}
	}

	/// <summary>Caches forward segment and token-slice cursors without owning or flattening input storage.</summary>
	private sealed class Segments
	{
		private ReadOnlyMemory<byte> memory;
		private SequencePosition next;
		private long start;
		private SequencePosition slicePosition;
		private long sliceOffset;

		/// <summary>Initializes a new instance of the <see cref="Segments"/> class.</summary>
		/// <param name="sequence">The borrowed sequence.</param>
		internal Segments(ReadOnlySequence<byte> sequence)
		{
			this.Sequence = sequence;
			this.next = sequence.Start;
			this.slicePosition = sequence.Start;
		}

		private Segments(Segments original)
		{
			this.Sequence = original.Sequence;
			this.memory = original.memory;
			this.next = original.next;
			this.start = original.start;
			this.slicePosition = original.slicePosition;
			this.sliceOffset = original.sliceOffset;
		}

		/// <summary>Gets the original sequence.</summary>
		internal ReadOnlySequence<byte> Sequence { get; }

		/// <summary>Creates independent cursor state so peeking does not force the original reader to rescan earlier segments.</summary>
		/// <returns>A cursor over the same borrowed storage.</returns>
		internal Segments CreatePeek() => new(this);

		/// <summary>Saves the current byte cursor independently of token slicing.</summary>
		/// <returns>The navigation checkpoint.</returns>
		internal (ReadOnlyMemory<byte> Memory, SequencePosition Next, long Start) SaveCursor() => (this.memory, this.next, this.start);

		/// <summary>Restores a byte cursor after a tentative token read.</summary>
		/// <param name="cursor">The saved navigation checkpoint.</param>
		internal void RestoreCursor((ReadOnlyMemory<byte> Memory, SequencePosition Next, long Start) cursor)
		{
			this.memory = cursor.Memory;
			this.next = cursor.Next;
			this.start = cursor.Start;
		}

		/// <summary>Slices tokens with a separate forward cursor so token materialization remains linear.</summary>
		/// <param name="offset">The absolute token offset.</param>
		/// <param name="length">The token length.</param>
		/// <returns>The borrowed token.</returns>
		internal ReadOnlySequence<byte> Slice(long offset, long length) => this.Sequence.Slice(this.GetSlicePosition(offset), length);

		/// <summary>Checks contiguous availability without traversing to the end of the range.</summary>
		/// <param name="offset">The absolute range offset.</param>
		/// <param name="length">The range length.</param>
		/// <param name="value">Receives a span borrowing the original input, or an empty span for a split range.</param>
		/// <returns>Whether the range fits in the first memory block at its cached start position.</returns>
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal bool TryGetSpan(long offset, long length, out ReadOnlySpan<byte> value)
		{
			SequencePosition position = this.GetSlicePosition(offset);
			if (length < 0 || length > this.Sequence.Length - offset)
			{
				throw new ArgumentOutOfRangeException(nameof(length));
			}

			this.Sequence.TryGet(ref position, out ReadOnlyMemory<byte> first, advance: false);
			if (length <= first.Length)
			{
				value = first.Span[..(int)length];
				return true;
			}

			value = default;
			return false;
		}

		/// <summary>Gets a byte, advancing or resetting the cached segment as necessary.</summary>
		/// <param name="index">The absolute input offset.</param>
		/// <returns>The input byte.</returns>
		internal byte GetByte(long index)
		{
			if (index < this.start)
			{
				this.start = 0;
				this.memory = default;
				this.next = this.Sequence.Start;
			}

			while (index - this.start >= this.memory.Length)
			{
				this.start += this.memory.Length;
				if (!this.Sequence.TryGet(ref this.next, out this.memory))
				{
					throw new ArgumentOutOfRangeException(nameof(index));
				}
			}

			return this.memory.Span[checked((int)(index - this.start))];
		}

		/// <summary>Finds the next quote or backslash using only the byte cursor.</summary>
		/// <param name="start">The absolute offset at which to start searching.</param>
		/// <returns>The absolute delimiter offset, or -1.</returns>
		[MethodImpl(MethodImplOptions.NoInlining)]
		internal long IndexOfSegmentedDelimiter(long start)
		{
			while (start < this.Sequence.Length)
			{
				this.GetByte(start);
				ReadOnlySpan<byte> remaining = this.memory.Span[checked((int)(start - this.start))..];
				int offset = remaining.IndexOfAny((byte)'"', (byte)'\\');
				if (offset >= 0)
				{
					return start + offset;
				}

				start += remaining.Length;
			}

			return -1;
		}

		private SequencePosition GetSlicePosition(long offset)
		{
			if (offset < this.sliceOffset)
			{
				this.sliceOffset = 0;
				this.slicePosition = this.Sequence.Start;
			}

			this.slicePosition = this.Sequence.GetPosition(offset - this.sliceOffset, this.slicePosition);
			this.sliceOffset = offset;
			return this.slicePosition;
		}
	}
}
