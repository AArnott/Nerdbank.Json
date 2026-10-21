// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Benchmarks;

/// <summary>Measures numeric array scanning as input length and segmentation vary.</summary>
[MemoryDiagnoser]
public class NumberTokenScalingBenchmarks
{
	private ReadOnlySequence<byte> numbers;

	[Params(32, 256, 1024)]
	public int ValueCount { get; set; }

	[Params(0, 3, 64)]
	public int SegmentSize { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		byte[] bytes = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat("12345.625", this.ValueCount)) + "]");
		this.numbers = this.SegmentSize == 0 ? new(bytes) : Split(bytes, this.SegmentSize);
		JsonReader reader = new(this.numbers);
		reader.ReadStartArray();
		for (int i = 0; i < this.ValueCount; i++)
		{
			if (i > 0)
			{
				reader.ReadValueSeparator();
			}

			if (reader.ReadDouble() != 12345.625)
			{
				throw new InvalidOperationException("Invalid benchmark value.");
			}
		}

		reader.ReadEndArray();
		if (!reader.End || reader.Consumed != bytes.Length || this.Numbers() != this.ValueCount * 12345.625)
		{
			throw new InvalidOperationException("Invalid benchmark input.");
		}
	}

	[Benchmark]
	public double Numbers()
	{
		JsonReader reader = new(this.numbers);
		reader.ReadStartArray();
		double sum = 0;
		for (int i = 0; i < this.ValueCount; i++)
		{
			if (i > 0)
			{
				reader.ReadValueSeparator();
			}

			sum += reader.ReadDouble();
		}

		reader.ReadEndArray();
		return sum;
	}

	private static ReadOnlySequence<byte> Split(byte[] bytes, int size)
	{
		Segment first = new(bytes.AsMemory(0, Math.Min(size, bytes.Length)));
		Segment last = first;
		for (int offset = size; offset < bytes.Length; offset += size)
		{
			last = last.Append(bytes.AsMemory(offset, Math.Min(size, bytes.Length - offset)));
		}

		return new(first, 0, last, last.Memory.Length);
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
