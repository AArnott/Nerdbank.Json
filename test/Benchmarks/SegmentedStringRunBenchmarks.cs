// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Benchmarks;

/// <summary>Measures ordinary runs following an early escape in borrowed segmented strings.</summary>
[MemoryDiagnoser]
public class SegmentedStringRunBenchmarks
{
	private ReadOnlySequence<byte> input;

	[Params(32, 4096)]
	public int RunLength { get; set; }

	[Params(3, 64, 4096)]
	public int SegmentSize { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		string text = new('a', this.RunLength);
		byte[] bytes = Encoding.UTF8.GetBytes("\"\\n" + text + "\"");
		Segment first = new(bytes.AsMemory(0, Math.Min(this.SegmentSize, bytes.Length)));
		Segment last = first;
		for (int offset = this.SegmentSize; offset < bytes.Length; offset += this.SegmentSize)
		{
			last = last.Append(bytes.AsMemory(offset, Math.Min(this.SegmentSize, bytes.Length - offset)));
		}

		this.input = new(first, 0, last, last.Memory.Length);
		JsonReader reader = new(this.input);
		if (reader.ReadRequiredString() != "\n" + text || reader.Consumed != bytes.Length)
		{
			throw new InvalidOperationException("Invalid benchmark input.");
		}
	}

	[Benchmark]
	public string ReadString()
	{
		JsonReader reader = new(this.input);
		return reader.ReadRequiredString();
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
