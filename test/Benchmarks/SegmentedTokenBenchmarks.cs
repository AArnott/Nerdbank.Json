// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Benchmarks;

/// <summary>Measures token materialization rather than sequence construction.</summary>
[MemoryDiagnoser]
public class SegmentedTokenBenchmarks
{
	private readonly ObjectBenchmarks objects = new();
	private readonly Nerdbank.Json.JsonSerializer serializer = new();
	private ReadOnlySequence<byte> numbers;
	private ReadOnlySequence<byte> names;
	private ReadOnlySequence<byte> text;
	private ReadOnlySequence<byte> escaped;

	[GlobalSetup]
	public void Setup()
	{
		this.numbers = Split(Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat("12345.625", 32)) + "]"), 3);
		this.objects.Setup();
		ArrayBufferWriter<byte> buffer = new();
		this.serializer.Serialize(buffer, this.objects.DeserializeWide_NerdbankJson());
		this.names = Split(buffer.WrittenSpan.ToArray(), 3);
		this.text = Split(Encoding.UTF8.GetBytes("\"" + string.Concat(Enumerable.Repeat("abcé😀", 64)) + "\""), 7);
		this.escaped = Split(Encoding.UTF8.GetBytes("\"" + string.Concat(Enumerable.Repeat("abcé😀\\n", 64)) + "\""), 7);
		if (this.Numbers() != 32 * 12345.625 || this.UnescapedString().Length != 384 || this.EscapedString().Length != 448 || this.PropertyNames()?.PrimaryIdentifier != 42)
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
		for (int i = 0; i < 32; i++)
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

	[Benchmark]
	public ObjectBenchmarks.WideModel? PropertyNames() => this.serializer.Deserialize<ObjectBenchmarks.WideModel>(this.names);

	[Benchmark]
	public string UnescapedString()
	{
		JsonReader reader = new(this.text);
		return reader.ReadRequiredString();
	}

	[Benchmark]
	public string EscapedString()
	{
		JsonReader reader = new(this.escaped);
		return reader.ReadRequiredString();
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
