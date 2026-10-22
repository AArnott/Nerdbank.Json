// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Benchmarks;

/// <summary>Measures split floating-point tokens using stack and pooled scratch storage.</summary>
[MemoryDiagnoser]
public class SegmentedNumericScratchBenchmarks
{
	private ReadOnlySequence<byte> input;

	[Params(9, 129)]
	public int TokenLength { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		byte[] bytes = Encoding.UTF8.GetBytes("12345.625" + new string('0', this.TokenLength - 9));
		Segment first = new(bytes.AsMemory(0, 3));
		Segment last = first;
		for (int offset = 3; offset < bytes.Length; offset += 3)
		{
			last = last.Append(bytes.AsMemory(offset, Math.Min(3, bytes.Length - offset)));
		}

		this.input = new(first, 0, last, last.Memory.Length);
		JsonReader reader = new(this.input);
		if (reader.ReadDouble() != 12345.625 || reader.Consumed != bytes.Length || this.ReadSingle() != 12345.625f || this.ReadDecimal() != 12345.625m)
		{
			throw new InvalidOperationException("Invalid benchmark input.");
		}
	}

	[Benchmark]
	public double ReadDouble()
	{
		JsonReader reader = new(this.input);
		return reader.ReadDouble();
	}

	[Benchmark]
	public float ReadSingle()
	{
		JsonReader reader = new(this.input);
		return reader.ReadSingle();
	}

	[Benchmark]
	public decimal ReadDecimal()
	{
		JsonReader reader = new(this.input);
		return reader.ReadDecimal();
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
