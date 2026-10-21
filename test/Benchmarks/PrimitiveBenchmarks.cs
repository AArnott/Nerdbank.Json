// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Benchmarks;

/// <summary>Compares scalar reader/writer APIs with stateless primitives and direct output buffers.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class PrimitiveBenchmarks
{
	private const int IntegerValue = -123456789;
	private const float SingleValue = 1.2345678f;
	private readonly ArrayBufferWriter<byte> buffer = new(256);
	private readonly byte[] integer = "-123456789"u8.ToArray();
	private readonly byte[] single = "1.2345678"u8.ToArray();

	[GlobalSetup]
	public void Setup()
	{
		if (this.ReadInt32() != IntegerValue || this.ReadInt32Primitive() != IntegerValue || this.ReadSingle() != SingleValue || this.ReadSinglePrimitive() != SingleValue)
		{
			throw new InvalidOperationException("Invalid scalar benchmark input.");
		}

		JsonWriter writer = new(this.buffer);
		writer.Write(SingleValue);
		writer.Flush();
		byte[] expected = this.buffer.WrittenSpan.ToArray();
		this.buffer.Clear();
		writer = new(this.buffer);
		if (!JsonPrimitives.TryWrite(writer.GetSpan(32), SingleValue, out int written))
		{
			throw new InvalidOperationException("The scalar output buffer was insufficient.");
		}

		writer.Advance(written);
		writer.Flush();
		if (!this.buffer.WrittenSpan.SequenceEqual(expected))
		{
			throw new InvalidOperationException("Direct and structured scalar output differ: " + Encoding.UTF8.GetString(this.buffer.WrittenSpan));
		}

		this.buffer.Clear();
	}

	[Benchmark]
	[BenchmarkCategory("ReadInt32")]
	public int ReadInt32()
	{
		JsonReader reader = new(this.integer);
		return reader.ReadInt32();
	}

	[Benchmark]
	[BenchmarkCategory("ReadInt32")]
	public int ReadInt32Primitive()
	{
		JsonPrimitives.TryRead(this.integer, out int value, out _);
		return value;
	}

	[Benchmark]
	[BenchmarkCategory("ReadSingle")]
	public float ReadSingle()
	{
		JsonReader reader = new(this.single);
		return reader.ReadSingle();
	}

	[Benchmark]
	[BenchmarkCategory("ReadSingle")]
	public float ReadSinglePrimitive()
	{
		JsonPrimitives.TryRead(this.single, out float value, out _);
		return value;
	}

	[Benchmark]
	[BenchmarkCategory("WriteSingle")]
	public int WriteSingle()
	{
		JsonWriter writer = new(this.buffer);
		writer.Write(SingleValue);
		writer.Flush();
		int length = this.buffer.WrittenCount;
		this.buffer.Clear();
		return length;
	}

	[Benchmark]
	[BenchmarkCategory("WriteSingle")]
	public int WriteSinglePrimitive()
	{
		JsonWriter writer = new(this.buffer);
		if (!JsonPrimitives.TryWrite(writer.GetSpan(32), SingleValue, out int written))
		{
			throw new InvalidOperationException("The scalar output buffer was insufficient.");
		}

		writer.Advance(written);
		writer.Flush();
		int length = this.buffer.WrittenCount;
		this.buffer.Clear();
		return length;
	}
}
