// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using System.Text;

[GenerateShapeFor<int>]
public partial class JsonScratchTests
{
	[Test]
	[Arguments(1)]
	[Arguments(7)]
	[Arguments(127)]
	public void LongNumbers_HaveNoScratchSizeLimit(int segmentSize)
	{
		string exponent = "1e" + new string('0', 4096) + "1";
		ReadOnlySequence<byte> sequence = Split(Encoding.UTF8.GetBytes(exponent), segmentSize);
		JsonReader reader = new(sequence);
		Assert.Equal(10d, reader.ReadDouble());
		Assert.True(reader.End);
		reader = new(sequence);
		Assert.Equal(10f, reader.ReadSingle());
		reader = new(sequence);
		Assert.Equal(10m, reader.ReadDecimal());
		reader = new(sequence);
		Assert.Equal(exponent, reader.ReadNumberToken());

		string integer = "1" + new string('0', 4096);
		reader = new(Split(Encoding.UTF8.GetBytes(integer), segmentSize));
		Assert.Equal(BigInteger.Pow(10, 4096), BigInteger.Parse(reader.ReadNumberToken(), System.Globalization.CultureInfo.InvariantCulture));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void LongNumericFailures_PreserveExceptionAndConsumption(bool malformed)
	{
		byte[] bytes = Encoding.UTF8.GetBytes("1" + new string('0', 4096) + (malformed ? "e+" : string.Empty));
		foreach (ReadOnlySequence<byte> input in new[] { new ReadOnlySequence<byte>(bytes), Split(bytes, 7) })
		{
			JsonReader reader = new(input);
			try
			{
				reader.ReadInt32();
				Assert.Fail("Expected numeric rejection.");
			}
			catch (FormatException) when (malformed)
			{
				Assert.Equal(0, reader.Consumed);
			}
			catch (OverflowException) when (!malformed)
			{
				Assert.Equal(bytes.Length, reader.Consumed);
			}
		}
	}

	[Test]
	[Arguments(127)]
	[Arguments(128)]
	[Arguments(129)]
	[Arguments(4096)]
	[Arguments(8192)]
	public void UnicodeFallbackState_SurvivesEveryBoundaryAndEscapeRun(int size)
	{
		byte[] run = [.. Encoding.UTF8.GetBytes(new string('a', size) + "\u00e9\U0001F600"), 0xE0, 0xA0];
		byte[] token = [(byte)'"', .. run, .. "\\n"u8.ToArray(), .. run, (byte)'"'];
		string expected = Encoding.UTF8.GetString(run) + "\n" + Encoding.UTF8.GetString(run);
		foreach (int splitSize in new[] { 1, 3, 7, 128, 255, 256, 257, 4096 })
		{
			JsonReader reader = new(Split(token, splitSize));
			Assert.Equal(expected, reader.ReadRequiredString());
			reader = new(Split(token, splitSize));
			Assert.Equal(Encoding.UTF8.GetString(token), reader.ReadRawValue());
			reader = new(Split(token, splitSize));
			Assert.Equal(run.Concat(new byte[] { 10 }).Concat(run), reader.ReadStringSequence()!.Value.ToArray());
		}
	}

	[Test]
	[Arguments(7)]
	[Arguments(257)]
	public void UnicodeFallbackState_FlushesBeforeShortAndEmptyEscapeRuns(int segmentSize)
	{
		byte[] longRun = [.. Encoding.UTF8.GetBytes(new string('a', 513) + "\U0001F600"), 0xE0, 0xA0];
		byte[] shortRun = [0x80];
		byte[] token = [(byte)'"', .. longRun, .. "\\n"u8.ToArray(), .. shortRun, .. "\\t\\r"u8.ToArray(), .. longRun, (byte)'"'];
		string expected = Encoding.UTF8.GetString(longRun) + "\n" + Encoding.UTF8.GetString(shortRun) + "\t\r" + Encoding.UTF8.GetString(longRun);
		JsonReader reader = new(Split(token, segmentSize));
		Assert.Equal(expected, reader.ReadRequiredString());
		Assert.True(reader.End);
	}

	[Test]
	public void LargePropertyToken_PooledFallbackSupportsNavigationAndFailures()
	{
		string name = new('a', 8192);
		byte[] bytes = Encoding.UTF8.GetBytes("{\"" + name + "\":1}");
		ReadOnlySequence<byte> sequence = Split(bytes, 7);
		JsonSerializer serializer = new();
		JsonPath path = JsonPath.Root.Member(name);
		for (int attempt = 0; attempt < 3; attempt++)
		{
			Assert.Equal(1, serializer.DeserializePath<int, JsonScratchTests>(sequence, path));
			Assert.Throws<JsonSerializationException>(() => serializer.DeserializePath<int, JsonScratchTests>(Split(Encoding.UTF8.GetBytes("{\"" + name + "\":1e+}"), 7), path));
			Assert.Throws<JsonSerializationException>(() => serializer.DeserializePath<int, JsonScratchTests>(Split(Encoding.UTF8.GetBytes("{\"" + name + "\n\":1}"), 7), path));
			Assert.Throws<JsonSerializationException>(() => serializer.DeserializePath<int, JsonScratchTests>(Split(Encoding.UTF8.GetBytes("{\"" + name + "\" 1}"), 7), path));

			Assert.Equal(0, serializer.Deserialize(Split(bytes, 7), PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Item>()!)!.Property);
			Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(Split(Encoding.UTF8.GetBytes("{\"" + name + "\n\":1}"), 7), PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Item>()!));
		}
	}

#if NET
	[Test]
	[NotInParallel]
	public void SplitShortNumbersAndPropertyNames_DoNotAllocateTokenArrays()
	{
		const int Count = 512;
		byte[] numbers = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat("12345.625", Count)) + "]");
		byte[] names = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat("{\"property\":1}", Count)) + "]");
		ReadOnlySequence<byte> numberInput = Split(numbers, 3);
		ReadOnlySequence<byte> nameInput = Split(names, 3);
		ReadOnlySequence<byte> contiguousNames = new(names);
		JsonSerializer serializer = new();
		for (int warmup = 0; warmup < 100; warmup++)
		{
			Consume(numberInput, false, serializer);
			Consume(nameInput, true, serializer);
			Consume(contiguousNames, true, serializer);
		}

		long numberAllocation = Consume(numberInput, false, serializer);
		long nameAllocation = Consume(nameInput, true, serializer);
		long contiguousNameAllocation = Consume(contiguousNames, true, serializer);
		Assert.Equal(0, numberAllocation);
		Assert.Equal(contiguousNameAllocation, nameAllocation);

		static long Consume(ReadOnlySequence<byte> input, bool names, JsonSerializer serializer)
		{
			JsonReader reader = new(input);
			reader.ReadStartArray();

			long before = GC.GetAllocatedBytesForCurrentThread();
			double sum = 0;
			for (int i = 0; i < Count; i++)
			{
				if (i > 0)
				{
					reader.ReadValueSeparator();
				}

				if (names)
				{
					sum += serializer.Deserialize(ref reader, PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<Item>()!)!.Property;
				}
				else
				{
					sum += reader.ReadDouble();
				}
			}

			long allocation = GC.GetAllocatedBytesForCurrentThread() - before;
			if (sum == 0)
			{
				throw new InvalidOperationException("No values read.");
			}

			return allocation;
		}
	}

	[Test]
	[NotInParallel]
	[Arguments(false)]
	[Arguments(true)]
	public void SplitStrings_AllocateOnlyFinalStringsForShortRuns(bool escaped)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(escaped ? "\"hello\\nworld\"" : "\"hello world\"");
		ReadOnlySequence<byte> input = Split(bytes, 3);
		for (int warmup = 0; warmup < 100; warmup++)
		{
			Read(input);
			Read(new(bytes));
		}

		Assert.Equal(Read(new(bytes)), Read(input));

		static long Read(ReadOnlySequence<byte> input)
		{
			JsonReader reader = new(input);
			long before = GC.GetAllocatedBytesForCurrentThread();
			string value = reader.ReadRequiredString();
			long allocation = GC.GetAllocatedBytesForCurrentThread() - before;
			GC.KeepAlive(value);
			return allocation;
		}
	}

	[Test]
	[NotInParallel]
	public void LargeSplitStrings_DoNotAllocateTemporaryByteArrays()
	{
		byte[] bytes = [(byte)'"', .. Encoding.UTF8.GetBytes(new string('a', 8192) + "\u00e9\U0001F600"), 0xE0, 0xA0, (byte)'"'];
		ReadOnlySequence<byte> input = Split(bytes, 7);
		int characterCount = Encoding.UTF8.GetCharCount(bytes, 1, bytes.Length - 2);
		for (int i = 0; i < 100; i++)
		{
			Read(input);
			AllocateString(characterCount);
		}

		long extra = Read(input) - AllocateString(characterCount);
		Assert.InRange(extra, 0, 128);

		static long AllocateString(int length)
		{
			long before = GC.GetAllocatedBytesForCurrentThread();
			string value = new('a', length);
			long allocation = GC.GetAllocatedBytesForCurrentThread() - before;
			GC.KeepAlive(value);
			return allocation;
		}

		static long Read(ReadOnlySequence<byte> input)
		{
			JsonReader reader = new(input);
			long before = GC.GetAllocatedBytesForCurrentThread();
			string value = reader.ReadRequiredString();
			long allocation = GC.GetAllocatedBytesForCurrentThread() - before;
			GC.KeepAlive(value);
			return allocation;
		}
	}

	[Test]
	[NotInParallel]
	public void LargeSplitNumber_ReusesPooledFallback()
	{
		ReadOnlySequence<byte> input = Split(Encoding.UTF8.GetBytes("1e" + new string('0', 4096) + "1"), 7);
		for (int i = 0; i < 100; i++)
		{
			Read(input);
		}

		Assert.Equal(0, Read(input));

		static long Read(ReadOnlySequence<byte> input)
		{
			JsonReader reader = new(input);
			long before = GC.GetAllocatedBytesForCurrentThread();
			double value = reader.ReadDouble();
			long allocation = GC.GetAllocatedBytesForCurrentThread() - before;
			if (value != 10)
			{
				throw new InvalidOperationException("Incorrect large exponent.");
			}

			return allocation;
		}
	}
#endif

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

	[GenerateShape]
	public partial class Item
	{
		public int Property { get; set; }
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
