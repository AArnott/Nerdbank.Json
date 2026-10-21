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
	internal JsonInput(ReadOnlySpan<byte> span) => this.span = span;

	/// <summary>Initializes a new instance of the <see cref="JsonInput"/> struct.</summary>
	/// <param name="sequence">The borrowed input.</param>
	internal JsonInput(scoped in ReadOnlySequence<byte> sequence)
	{
		this.span = sequence.IsSingleSegment ? sequence.First.Span : default;
		this.segments = sequence.IsSingleSegment ? null : new(sequence);
	}

	/// <summary>Gets the input length without narrowing sequence offsets.</summary>
	internal long Length => this.segments is null ? this.span.Length : this.segments.Sequence.Length;

	/// <summary>Gets a byte at an absolute input offset.</summary>
	/// <param name="index">The absolute offset.</param>
	internal byte this[long index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => this.segments is null ? this.span[checked((int)index)] : this.segments.GetByte(index);
	}

	/// <summary>Gets contiguous token storage, allocating only when the token spans segments.</summary>
	/// <param name="start">The token offset.</param>
	/// <param name="length">The token length.</param>
	/// <returns>Borrowed storage or a GC-owned token copy, never pooled storage.</returns>
	internal ReadOnlySpan<byte> Slice(long start, long length)
	{
		if (this.segments is null)
		{
			return this.span.Slice(checked((int)start), checked((int)length));
		}

		ReadOnlySequence<byte> token = this.segments.Slice(start, length);
		return token.IsSingleSegment ? token.First.Span : token.ToArray();
	}

	/// <summary>Gets a borrowed span only when the requested range is contiguous.</summary>
	/// <param name="start">The range offset.</param>
	/// <param name="length">The range length.</param>
	/// <param name="value">Receives the borrowed span.</param>
	/// <returns>Whether no copy is required.</returns>
	internal bool TryGetSpan(long start, long length, out ReadOnlySpan<byte> value)
	{
		if (this.segments is null)
		{
			value = this.span.Slice(checked((int)start), checked((int)length));
			return true;
		}

		ReadOnlySequence<byte> slice = this.segments.Slice(start, length);
		value = slice.IsSingleSegment ? slice.First.Span : default;
		return slice.IsSingleSegment;
	}

	/// <summary>Finds the next quote or backslash without materializing input.</summary>
	/// <param name="start">The offset at which to start searching.</param>
	/// <returns>The absolute delimiter offset, or -1.</returns>
	internal long IndexOfDelimiter(long start)
	{
		if (this.segments is null)
		{
			int offset = this.span[checked((int)start)..].IndexOfAny((byte)'"', (byte)'\\');
			return offset < 0 ? -1 : start + offset;
		}

		for (long index = start; index < this.Length; index++)
		{
			if (this[index] is (byte)'"' or (byte)'\\')
			{
				return index;
			}
		}

		return -1;
	}

	/// <summary>Caches a segment cursor; reader copies may safely share this navigation-only cache.</summary>
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

		/// <summary>Gets the original sequence.</summary>
		internal ReadOnlySequence<byte> Sequence { get; }

		/// <summary>Slices tokens with a separate forward cursor so token materialization remains linear.</summary>
		/// <param name="offset">The absolute token offset.</param>
		/// <param name="length">The token length.</param>
		/// <returns>The borrowed token.</returns>
		internal ReadOnlySequence<byte> Slice(long offset, long length)
		{
			if (offset < this.sliceOffset)
			{
				this.sliceOffset = 0;
				this.slicePosition = this.Sequence.Start;
			}

			this.slicePosition = this.Sequence.GetPosition(offset - this.sliceOffset, this.slicePosition);
			this.sliceOffset = offset;
			return this.Sequence.Slice(this.slicePosition, length);
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
	}
}
