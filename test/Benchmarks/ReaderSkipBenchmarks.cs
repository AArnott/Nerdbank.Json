// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using Nerdbank.Json;

namespace Benchmarks;

/// <summary>
/// Measures context-aware skipping of nested containers, long strings, and long numeric tokens.
/// </summary>
[MemoryDiagnoser]
public class ReaderSkipBenchmarks
{
	private byte[] json = null!;
	private ReadOnlySequence<byte> sequence;
	private CancellationTokenSource? cancellation;
	private SerializationContext context;

	/// <summary>Gets or sets the skipped token distribution.</summary>
	[Params("Containers", "String", "Unicode", "Number")]
	public string Scenario { get; set; } = "Containers";

	/// <summary>Gets or sets the number of repeated entries or token characters.</summary>
	[Params(16, 4096)]
	public int Size { get; set; }

	/// <summary>Gets or sets the segment size, or zero for the contiguous span path.</summary>
	[Params(0, 1, 64, 4096)]
	public int SegmentSize { get; set; }

	/// <summary>Gets or sets a value indicating whether the active context supports cancellation.</summary>
	[Params(false, true)]
	public bool Cancellable { get; set; }

	/// <summary>Prepares and validates the input outside the measured operation.</summary>
	[GlobalSetup]
	public void Setup()
	{
		string text = this.Scenario switch
		{
			"Containers" => "[" + string.Join(",", Enumerable.Repeat("""{"a":[1,true,null,{}]}""", this.Size)) + "]",
			"String" => "\"" + string.Concat(Enumerable.Repeat("text\\\"more", this.Size)) + "\"",
			"Unicode" => "\"" + string.Concat(Enumerable.Repeat("é😀\\n", this.Size)) + "\"",
			"Number" => "0." + new string('1', this.Size),
			_ => throw new InvalidOperationException("Unknown skip scenario."),
		};
		this.json = Encoding.UTF8.GetBytes(text);
		if (this.SegmentSize > 0)
		{
			Segment first = new(ReadOnlyMemory<byte>.Empty);
			Segment last = first;
			for (int offset = 0; offset < this.json.Length; offset += this.SegmentSize)
			{
				last = last.Append(this.json.AsMemory(offset, Math.Min(this.SegmentSize, this.json.Length - offset)));
			}

			this.sequence = new(first, 0, last, last.Memory.Length);
		}

		this.cancellation = this.Cancellable ? new CancellationTokenSource() : null;
		this.context = new SerializationContext { CancellationToken = this.cancellation?.Token ?? default };
		if (this.Skip() != this.json.Length)
		{
			throw new InvalidOperationException("Skip did not consume exactly one complete value.");
		}
	}

	/// <summary>Releases the optional cancellation source.</summary>
	[GlobalCleanup]
	public void Cleanup() => this.cancellation?.Dispose();

	/// <summary>Skips one complete value without materializing strings or object graphs.</summary>
	/// <returns>The number of bytes consumed.</returns>
	[Benchmark]
	public long Skip()
	{
		JsonReader reader = this.SegmentSize == 0 ? new(this.json.AsSpan()) : new(this.sequence);
		reader.Skip(this.context);
		return reader.Consumed;
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
