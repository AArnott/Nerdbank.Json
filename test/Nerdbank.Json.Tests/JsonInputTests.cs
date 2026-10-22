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
	public void IndexOfDelimiter_AllPositionsAndStartsMatchBytewiseScan(int layout)
	{
		byte[] ordinary = [0, 1, 9, 10, 13, 31, 32, 127, 128, 192, 255, 226, 130, 172, (byte)'a', (byte)'z'];
		foreach (byte delimiter in new byte[] { (byte)'"', (byte)'\\' })
		{
			for (int position = -1; position < ordinary.Length; position++)
			{
				byte[] bytes = (byte[])ordinary.Clone();
				if (position >= 0)
				{
					bytes[position] = delimiter;
					if (position + 3 < bytes.Length)
					{
						bytes[position + 3] = delimiter == (byte)'"' ? (byte)'\\' : (byte)'"';
					}
				}

				for (int split = 0; split <= bytes.Length; split++)
				{
					ReadOnlySequence<byte> sequence = CreateDelimiterInput(bytes, split, layout);
					JsonInput input = new(sequence);
					JsonInput reference = new(sequence);
					for (long start = 0; start <= bytes.Length + 1; start++)
					{
						if (start <= bytes.Length || !sequence.IsSingleSegment)
						{
							AssertDelimiterSearch(input, reference, start);
						}
					}

					for (long start = bytes.Length; start >= 0; start--)
					{
						AssertDelimiterSearch(input, reference, start);
					}

					if (!sequence.IsSingleSegment)
					{
						AssertDelimiterSearch(input, reference, long.MaxValue);
					}
				}
			}
		}
	}

	[Test]
	public void IndexOfDelimiter_AllOtherByteValuesAreIgnored()
	{
		byte[] bytes = new byte[254];
		int length = 0;
		for (int value = 0; value <= byte.MaxValue; value++)
		{
			if (value is not '"' and not '\\')
			{
				bytes[length++] = (byte)value;
			}
		}

		ReadOnlySequence<byte> sequence = CreateDelimiterInput(bytes, 127, 2);
		JsonInput input = new(sequence);
		JsonInput reference = new(sequence);
		for (long start = 0; start <= bytes.Length + 1; start++)
		{
			AssertDelimiterSearch(input, reference, start);
		}
	}

	[Test]
	public void IndexOfDelimiter_EmptyAndTruncatedSequencesMatchBytewiseScan()
	{
		ReadOnlySequence<byte>[] sequences =
		[
			CreateSequence(ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty),
			CreateSequence("\"abc"u8.ToArray(), ReadOnlyMemory<byte>.Empty, "def\\"u8.ToArray()).Slice(1, 0),
			CreateSequence("\"abc"u8.ToArray(), ReadOnlyMemory<byte>.Empty, "def\\"u8.ToArray()).Slice(1, 6),
			CreateSequence("abc"u8.ToArray(), ReadOnlyMemory<byte>.Empty, "\\"u8.ToArray(), ReadOnlyMemory<byte>.Empty),
		];
		foreach (ReadOnlySequence<byte> sequence in sequences)
		{
			JsonInput input = new(sequence);
			JsonInput reference = new(sequence);
			for (long start = 0; start <= sequence.Length; start++)
			{
				AssertDelimiterSearch(input, reference, start);
			}

			if (!sequence.IsSingleSegment)
			{
				AssertDelimiterSearch(input, reference, sequence.Length + 1);
				AssertDelimiterSearch(input, reference, long.MaxValue);
			}
		}
	}

	[Test]
	public void IndexOfDelimiter_RestorePeekAndCopiesKeepCursorsIndependent()
	{
		byte[] first = "abc"u8.ToArray();
		ReadOnlySequence<byte> sequence = CreateSequence(first, ReadOnlyMemory<byte>.Empty, "de\\"u8.ToArray(), "fg\""u8.ToArray(), ReadOnlyMemory<byte>.Empty);
		JsonInput input = new(sequence);
		JsonInput reference = new(sequence);
		Assert.Equal(reference[1], input[1]);
		Assert.True(input.TryGetSpan(0, 3, out ReadOnlySpan<byte> borrowed));
		(ReadOnlyMemory<byte> Memory, SequencePosition Next, long Start) saved = input.SaveCursor();
		JsonInput peek = input.CreatePeekInput();
		Assert.Equal(8, peek.IndexOfDelimiter(6));
		Assert.Equal(-1, peek.IndexOfDelimiter(9));
		Assert.Equal(saved, input.SaveCursor());
		JsonInput copy = input;
		Assert.Equal(8, copy.IndexOfDelimiter(6));
		Assert.Equal(copy.SaveCursor(), input.SaveCursor());
		input.RestoreCursor(saved);
		Assert.Equal(5, input.IndexOfDelimiter(0));
		input.RestoreCursor(saved);
		Assert.Equal(saved, input.SaveCursor());
		AssertDelimiterSearch(input, reference, 0);
		(ReadOnlyMemory<byte> Memory, SequencePosition Next, long Start) afterScan = input.SaveCursor();

		Span<byte> scratch = stackalloc byte[9];
		using (JsonInput.Token token = input.CopyToken(1, 7, scratch))
		{
			Assert.True(token.Span.SequenceEqual("bcde\\fg"u8));
		}

		Assert.True(input.SliceSequence(0, 3)!.Value.First.Span == borrowed);
		Assert.Equal(afterScan, input.SaveCursor());
		Assert.Equal(-1, peek.IndexOfDelimiter(9));
		Assert.Equal(afterScan, input.SaveCursor());
		input.RestoreCursor(saved);
		reference.RestoreCursor(saved);
		first[1] = (byte)'\\';
		Assert.Equal((byte)'\\', borrowed[1]);
		AssertDelimiterSearch(input, reference, 0);
		Assert.Equal(1, input.IndexOfDelimiter(0));
	}

	[Test]
	[Arguments(32, false)]
	[Arguments(32, true)]
	[Arguments(4096, false)]
	[Arguments(4096, true)]
	[Arguments(65536, false)]
	[Arguments(65536, true)]
	public void IndexOfDelimiter_OrdinaryRunsBorrowMemoryPerSegment(int blockLength, bool hasDelimiter)
	{
		byte[] ordinary = new byte[blockLength];
		ordinary.AsSpan().Fill((byte)'a');
		byte[] lastBytes = new byte[blockLength];
		ordinary.CopyTo(lastBytes, 0);
		if (hasDelimiter)
		{
			lastBytes[^1] = (byte)'"';
		}

		using CountingMemoryManager first = new(ordinary);
		using CountingMemoryManager middle = new(ordinary);
		using CountingMemoryManager last = new(lastBytes);
		using CountingMemoryManager empty = new([]);
		ReadOnlyMemory<byte> emptyMemory = empty.Memory;
		ReadOnlySequence<byte> sequence = CreateSequence(emptyMemory, first.Memory, emptyMemory, middle.Memory, emptyMemory, last.Memory, emptyMemory);
		JsonInput input = new(sequence);
		int firstCalls = first.GetSpanCalls;
		int middleCalls = middle.GetSpanCalls;
		int lastCalls = last.GetSpanCalls;
		int emptyCalls = empty.GetSpanCalls;

		Assert.Equal(hasDelimiter ? sequence.Length - 1 : -1, input.IndexOfDelimiter(7));
		Assert.InRange(first.GetSpanCalls - firstCalls, 1, 2);
		Assert.InRange(middle.GetSpanCalls - middleCalls, 1, 2);
		Assert.InRange(last.GetSpanCalls - lastCalls, 1, 2);
		Assert.Equal(emptyCalls, empty.GetSpanCalls);
		Assert.Equal(2L * blockLength, input.SaveCursor().Start);
		(ReadOnlyMemory<byte> Memory, SequencePosition Next, long Start) saved = input.SaveCursor();
		Assert.Equal(-1, input.IndexOfDelimiter(sequence.Length));
		Assert.Equal(-1, input.IndexOfDelimiter(long.MaxValue));
		Assert.Equal(saved, input.SaveCursor());
		Assert.InRange(last.GetSpanCalls - lastCalls, 1, 2);
	}

	[Test]
	public void IndexOfDelimiter_LongAbsoluteOffsetsAreNotNarrowed()
	{
		byte[] block = new byte[1024 * 1024];
		Segment first = new(block);
		Segment last = first;
		for (int i = 1; i < 2048; i++)
		{
			last = last.Append(block);
		}

		last = last.Append(ReadOnlyMemory<byte>.Empty).Append("\"a\\z"u8.ToArray()).Append(ReadOnlyMemory<byte>.Empty);
		ReadOnlySequence<byte> sequence = new(first, 0, last, 0);
		JsonInput input = new(sequence);
		JsonInput reference = new(sequence);
		long offset = (long)int.MaxValue + 1;
		long[] starts = [offset - 2, offset, offset + 1, offset + 2, offset + 3, offset + 4, offset + 5, long.MaxValue, offset];
		foreach (long start in starts)
		{
			AssertDelimiterSearch(input, reference, start);
		}

		Assert.Equal(offset, input.IndexOfDelimiter(offset - 2));
		Assert.Equal(offset + 2, input.IndexOfDelimiter(offset + 1));
	}

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

	private static void AssertDelimiterSearch(JsonInput input, JsonInput reference, long start)
	{
		long expected = -1;
		for (long index = start; index < reference.Length; index++)
		{
			if (reference[index] is (byte)'"' or (byte)'\\')
			{
				expected = index;
				break;
			}
		}

		Assert.Equal(expected, input.IndexOfDelimiter(start));
		Assert.Equal(reference.SaveCursor(), input.SaveCursor());
	}

	private static ReadOnlySequence<byte> CreateDelimiterInput(byte[] bytes, int split, int layout)
	{
		switch (layout)
		{
			case 0:
				return CreateSequence(bytes.AsMemory(0, split), bytes.AsMemory(split));
			case 1:
				return CreateSequence(ReadOnlyMemory<byte>.Empty, bytes.AsMemory(0, split), ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, bytes.AsMemory(split), ReadOnlyMemory<byte>.Empty);
			case 2:
				byte[] first = new byte[split + 1];
				first[0] = (byte)'"';
				bytes.AsSpan(0, split).CopyTo(first.AsSpan(1));
				byte[] last = new byte[bytes.Length - split + 1];
				bytes.AsSpan(split).CopyTo(last);
				last[^1] = (byte)'\\';
				return CreateSequence(first, ReadOnlyMemory<byte>.Empty, last).Slice(1, bytes.Length);
			case 3:
				return new(bytes);
			default:
				throw new ArgumentOutOfRangeException(nameof(layout));
		}
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
