// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

public class JsonInputTests
{
	[Test]
	[Arguments(0)]
	[Arguments(1)]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(4)]
	[Arguments(5)]
	public void TryGetSpan_AllRangesMatchCachedPositionSlice(int layout)
	{
		ReadOnlySequence<byte> sequence = CreateInput(layout);
		JsonInput input = new(sequence);
		for (long start = 0; start <= sequence.Length; start++)
		{
			for (long length = sequence.Length - start; length >= 0; length--)
			{
				AssertQuery(input, sequence, start, length);
			}
		}

		for (long start = sequence.Length; start >= 0; start--)
		{
			for (long length = 0; length <= sequence.Length - start; length++)
			{
				AssertQuery(input, sequence, start, length);
			}
		}
	}

	[Test]
	public void TryGetSpan_WholeSegmentPreservesPositionSliceBoundarySemantics()
	{
		ReadOnlySequence<byte> sequence = CreateSequence("abc"u8.ToArray(), ReadOnlyMemory<byte>.Empty, "def"u8.ToArray());
		JsonInput input = new(sequence);

		// Offset-based Slice moves an exact segment-end boundary into the next segment.
		Assert.False(sequence.Slice(0, 3).IsSingleSegment);
		Assert.True(sequence.Slice(sequence.GetPosition(0, sequence.Start), 3).IsSingleSegment);
		Assert.True(input.TryGetSpan(0, 3, out ReadOnlySpan<byte> value));
		Assert.True(value.SequenceEqual("abc"u8));
		Assert.True(value == sequence.First.Span);
		AssertQuery(input, sequence, 3, 3);
		AssertQuery(input, sequence, 6, 0);
	}

	[Test]
	[Arguments(0)]
	[Arguments(1)]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(5)]
	public void TryGetSpan_InvalidRangesPreserveExceptionsAndCursorRecovery(int layout)
	{
		ReadOnlySequence<byte> sequence = CreateInput(layout);
		JsonInput input = new(sequence);
		(long Start, long Length)[] ranges =
		[
			(-1, 0), (-1, -1), (0, -1), (sequence.Length + 1, 0),
			(sequence.Length, 1), (sequence.Length + 1, -1),
			(1, long.MaxValue), (long.MaxValue, 0), (long.MinValue, 0),
			(0, long.MinValue), (0, long.MaxValue), (long.MaxValue, long.MaxValue),
		];
		foreach ((long start, long length) in ranges)
		{
			AssertQuery(input, sequence, sequence.Length, 0);
			ArgumentOutOfRangeException expected = Assert.IsType<ArgumentOutOfRangeException>(GetSliceException(sequence, start, length));
			ArgumentOutOfRangeException actual = Assert.IsType<ArgumentOutOfRangeException>(GetQueryException(input, start, length));
			Assert.Equal(expected.ParamName, actual.ParamName);
			Assert.Throws<ArgumentOutOfRangeException>(() => sequence.Slice(start, length));
			AssertQuery(input, sequence, 0, sequence.Length);
		}
	}

	[Test]
	public void TryGetSpan_InterleavedCopiesAndPeeksPreserveBorrowedStorageAndCursors()
	{
		byte[] first = "abc"u8.ToArray();
		byte[] last = "efgh"u8.ToArray();
		ReadOnlySequence<byte> sequence = CreateSequence(first, ReadOnlyMemory<byte>.Empty, "d"u8.ToArray(), last);
		JsonInput input = new(sequence);
		Assert.Equal((byte)'b', input[1]);
		AssertQuery(input, sequence, 1, 1);
		JsonInput peek = input.CreatePeekInput();
		(ReadOnlyMemory<byte> Memory, SequencePosition Next, long Start) cursor = input.SaveCursor();
		Assert.Equal((byte)'h', peek[7]);
		AssertQuery(peek, sequence, 7, 1);
		Assert.Equal(cursor, input.SaveCursor());

		(long Start, long Length)[] ranges = [(5, 2), (0, 3), (3, 1), (1, 6), (8, 0), (4, 4), (0, 0)];
		Span<byte> scratch = stackalloc byte[8];
		foreach ((long start, long length) in ranges)
		{
			AssertQuery(input, sequence, start, length);
			using JsonInput.Token token = input.CopyToken(start, length, scratch);
			byte[] expected = sequence.Slice(start, length).ToArray();
			Assert.True(token.Span.SequenceEqual(expected));
			byte[] copy = new byte[(int)length];
			input.CopyTo(start, copy);
			Assert.Equal(expected, copy);
			ReadOnlySequence<byte>? slice = input.SliceSequence(start, length);
			Assert.True(slice.HasValue);
			Assert.Equal(expected, slice!.Value.ToArray());
			AssertQuery(peek, sequence, sequence.Length - start - length, length);
			AssertQuery(input, sequence, start, length);
		}

		Assert.True(input.TryGetSpan(0, 3, out ReadOnlySpan<byte> borrowedFirst));
		Assert.True(peek.TryGetSpan(4, 4, out ReadOnlySpan<byte> borrowedLast));
		first[0] = (byte)'A';
		last[0] = (byte)'E';
		Assert.Equal((byte)'A', borrowedFirst[0]);
		Assert.Equal((byte)'E', borrowedLast[0]);
	}

	[Test]
	public void TryGetSpan_MemoryManagerBorrowingDoesNotReadSplitPayloads()
	{
		byte[] bytes = "abc"u8.ToArray();
		using CountingMemoryManager manager = new(bytes);
		ReadOnlySequence<byte> sequence = CreateSequence(manager.Memory, "def"u8.ToArray());
		JsonInput input = new(sequence);
		int calls = manager.GetSpanCalls;
		Assert.False(input.TryGetSpan(0, 4, out ReadOnlySpan<byte> split));
		Assert.True(split.IsEmpty);
		Assert.Equal(calls, manager.GetSpanCalls);
		Assert.True(input.TryGetSpan(0, 3, out ReadOnlySpan<byte> borrowed));
		Assert.Equal(calls + 1, manager.GetSpanCalls);
		Assert.True(borrowed.SequenceEqual("abc"u8));
		bytes[0] = (byte)'A';
		Assert.Equal((byte)'A', borrowed[0]);
		JsonInput peek = input.CreatePeekInput();
		AssertQuery(peek, sequence, 1, 2);
		AssertQuery(input, sequence, 0, 3);
	}

	[Test]
	public void TryGetSpan_LongOffsetsAndLengthsAreNotNarrowed()
	{
		byte[] block = new byte[1024 * 1024];
		Segment first = new(block);
		Segment last = first;
		for (int i = 1; i < 2048; i++)
		{
			last = last.Append(block);
		}

		last = last.Append("xyz"u8.ToArray());
		ReadOnlySequence<byte> sequence = new(first, 0, last, last.Memory.Length);
		JsonInput input = new(sequence);
		long offset = (long)int.MaxValue + 1;
		AssertQuery(input, sequence, 0, offset);
		AssertQuery(input, sequence, offset, 3);
		AssertQuery(input, sequence, offset + 1, 1);
		AssertQuery(input, sequence, sequence.Length, 0);
		Assert.IsType<ArgumentOutOfRangeException>(GetQueryException(input, offset, long.MaxValue));
		AssertQuery(input, sequence, 1, 1);
		AssertQuery(input, sequence, offset, 3);
	}

	private static void AssertQuery(JsonInput input, ReadOnlySequence<byte> sequence, long start, long length)
	{
		ReadOnlySequence<byte> expected = sequence.Slice(sequence.GetPosition(start, sequence.Start), length);
		bool contiguous = input.TryGetSpan(start, length, out ReadOnlySpan<byte> actual);
		Assert.Equal(expected.IsSingleSegment, contiguous);
		if (contiguous)
		{
			Assert.True(expected.First.Span.SequenceEqual(actual));
			Assert.True(expected.First.Span == actual);
		}
		else
		{
			Assert.True(actual.IsEmpty);
		}

		ReadOnlySequence<byte> offsetSlice = sequence.Slice(start, length);
		if (offsetSlice.IsSingleSegment)
		{
			Assert.True(contiguous);
			Assert.True(offsetSlice.First.Span.SequenceEqual(actual));
		}
		else if (contiguous)
		{
			// The accepted position-based contract includes a whole first memory block.
			Assert.Equal(length, expected.First.Length);
		}
	}

	private static ArgumentOutOfRangeException? GetSliceException(ReadOnlySequence<byte> sequence, long start, long length)
	{
		try
		{
			sequence.Slice(sequence.GetPosition(start, sequence.Start), length);
			return null;
		}
		catch (ArgumentOutOfRangeException ex)
		{
			return ex;
		}
	}

	private static ArgumentOutOfRangeException? GetQueryException(JsonInput input, long start, long length)
	{
		try
		{
			input.TryGetSpan(start, length, out _);
			return null;
		}
		catch (ArgumentOutOfRangeException ex)
		{
			return ex;
		}
	}

	private static ReadOnlySequence<byte> CreateInput(int layout) => layout switch
	{
		0 => CreateSequence("abc"u8.ToArray(), "d"u8.ToArray(), "efgh"u8.ToArray()),
		1 => CreateSequence(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, "abc"u8.ToArray(), ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, "d"u8.ToArray(), ReadOnlyMemory<byte>.Empty, "efgh"u8.ToArray(), ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty),
		2 => CreateSequence("xabc"u8.ToArray(), "d"u8.ToArray(), "efghy"u8.ToArray()).Slice(1, 8),
		3 => CreateSequence(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty),
		4 => new("abcdefgh"u8.ToArray()),
		5 => CreateSequence("abc"u8.ToArray(), "d"u8.ToArray(), "efgh"u8.ToArray()).Slice(3, 1),
		_ => throw new ArgumentOutOfRangeException(nameof(layout)),
	};

	private static ReadOnlySequence<byte> CreateSequence(params ReadOnlyMemory<byte>[] memories)
	{
		Segment first = new(memories[0]);
		Segment last = first;
		for (int i = 1; i < memories.Length; i++)
		{
			last = last.Append(memories[i]);
		}

		return new(first, 0, last, last.Memory.Length);
	}

	private sealed class CountingMemoryManager(byte[] bytes) : MemoryManager<byte>
	{
		private bool disposed;

		internal int GetSpanCalls { get; private set; }

		public override Span<byte> GetSpan()
		{
			if (this.disposed)
			{
				throw new ObjectDisposedException(nameof(CountingMemoryManager));
			}

			this.GetSpanCalls++;
			return bytes;
		}

		public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

		public override void Unpin()
		{
		}

		protected override void Dispose(bool disposing) => this.disposed = true;
	}

	private sealed class Segment : ReadOnlySequenceSegment<byte>
	{
		internal Segment(ReadOnlyMemory<byte> memory) => this.Memory = memory;

		internal Segment Append(ReadOnlyMemory<byte> memory)
		{
			Segment next = new(memory) { RunningIndex = this.RunningIndex + this.Memory.Length };
			this.Next = next;
			return next;
		}
	}
}
