// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Pipelines;
using System.Text;

public class JsonWriterBufferTests
{
	[Test]
	[Arguments("null")]
	[Arguments("true")]
	[Arguments("-1.2300e+02")]
	[Arguments("\"é😀\\n\"")]
	[Arguments("[1,{\"x\":false}]")]
	[Arguments("{\"x\":[1,2]}")]
	public void Reservation_WritesOneExactRootValue(string json)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		TestBufferWriter buffer = new(bytes.Length);
		JsonWriter writer = new(buffer);
		Span<byte> span = writer.GetSpan(bytes.Length);
		Assert.Equal(bytes.Length, span.Length);
		bytes.CopyTo(span);
		Assert.Equal(0, writer.UnflushedBytes);
		writer.Advance(bytes.Length);
		Assert.Equal(bytes.Length, writer.UnflushedBytes);
		Assert.Equal(0, buffer.WrittenCount);
		writer.Flush();
		Assert.Equal(0, writer.UnflushedBytes);
		Assert.Equal(bytes, buffer.WrittenSpan.ToArray());
	}

	[Test]
	[Arguments(false, 1)]
	[Arguments(true, 1)]
	[Arguments(false, 128)]
	[Arguments(true, 128)]
	public void Reservation_MixesOpaqueValuesWithStructuredValues(bool indented, int capacity)
	{
		TestBufferWriter buffer = new(capacity);
		JsonWriter writer = new(buffer) { WriteIndented = indented };
		writer.WriteStartArray();
		WriteDirect(ref writer, "{\"x\":[1,2]}"u8);
		writer.WriteValueSeparator();
		writer.WriteStartObject();
		writer.WritePropertyName("raw");
		WriteDirect(ref writer, "[true,null]"u8);
		writer.WriteValueSeparator();
		writer.WritePropertyName("normal");
		writer.Write("ok");
		writer.WriteEndObject();
		writer.WriteValueSeparator();
		writer.WriteRaw("false"u8);
		writer.WriteValueSeparator();
		WriteDirect(ref writer, "123"u8);
		writer.WriteValueSeparator();
		writer.Write(4);
		writer.WriteEndArray();
		writer.Flush();
		string expected = indented
			? "[\n  {\"x\":[1,2]},\n  {\n    \"raw\": [true,null],\n    \"normal\": \"ok\"\n  },\n  false,\n  123,\n  4\n]"
			: "[{\"x\":[1,2]},{\"raw\":[true,null],\"normal\":\"ok\"},false,123,4]";
		Assert.Equal(expected, Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));
	}

	[Test]
	public void Reservation_CanUseMoreThanTheRequestedMinimum()
	{
		TestBufferWriter buffer = new(4);
		JsonWriter writer = new(buffer);
		Span<byte> span = writer.GetSpan(0);
		Assert.Equal(4, span.Length);
		"null"u8.CopyTo(span);
		writer.Advance(4);
		writer.Flush();
		Assert.Equal("null", Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));
	}

	[Test]
	public void Reservation_ExactBufferIncludesIndentationWithoutFlushing()
	{
		TestBufferWriter buffer = new(8);
		JsonWriter writer = new(buffer) { WriteIndented = true };
		writer.WriteStartArray();
		Span<byte> span = writer.GetSpan(4);
		Assert.Equal(4, span.Length);
		"null"u8.CopyTo(span);
		writer.Advance(4);
		Assert.Equal(8, writer.UnflushedBytes);
		Assert.Equal(0, buffer.WrittenCount);
		writer.WriteEndArray();
		writer.Flush();
		Assert.Equal("[\n  null\n]", Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));
	}

	[Test]
	[Arguments(1)]
	[Arguments(12)]
	public void Reservation_DeepIndentationMatchesWriteRaw(int depth)
	{
		TestBufferWriter buffer = new(1);
		TestBufferWriter referenceBuffer = new(1);
		JsonWriter writer = new(buffer) { WriteIndented = true };
		JsonWriter reference = new(referenceBuffer) { WriteIndented = true };
		for (int i = 0; i < depth; i++)
		{
			writer.WriteStartArray();
			reference.WriteStartArray();
		}

		WriteDirect(ref writer, "{\"x\":1}"u8);
		reference.WriteRaw("{\"x\":1}"u8);
		for (int i = 0; i < depth; i++)
		{
			writer.WriteEndArray();
			reference.WriteEndArray();
		}

		writer.Flush();
		reference.Flush();
		Assert.Equal(referenceBuffer.WrittenSpan.ToArray(), buffer.WrittenSpan.ToArray());
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void Reservation_RepeatedAndCancelledReservationsDoNotConsumeValues(bool propertyValue)
	{
		TestBufferWriter buffer = new(1);
		JsonWriter writer = new(buffer) { WriteIndented = true };
		if (propertyValue)
		{
			writer.WriteStartObject();
			writer.WritePropertyName("x");
		}
		else
		{
			writer.WriteStartArray();
		}

		int unflushed = writer.UnflushedBytes;
		writer.GetSpan(0)[0] = (byte)'?';
		writer.GetSpan(1024)[0] = (byte)'!';
		Assert.True(writer.UnflushedBytes <= unflushed);
		writer.Advance(0);
		writer.Advance(0);
		writer.Write(1);
		if (propertyValue)
		{
			writer.WriteEndObject();
		}
		else
		{
			writer.WriteEndArray();
		}

		writer.Flush();
		Assert.Equal(propertyValue ? "{\n  \"x\": 1\n}" : "[\n  1\n]", Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));
	}

	[Test]
	public void Reservation_RepeatedGetSpanUsesLatestCapacity()
	{
		TestBufferWriter buffer = new(1);
		JsonWriter writer = new(buffer) { WriteIndented = true };
		writer.WriteStartArray();
		writer.GetSpan(1)[0] = (byte)'?';
		Span<byte> latest = writer.GetSpan(1024);
		"true"u8.CopyTo(latest);
		AssertInvalidAdvance(ref writer, latest.Length + 1);
		writer.Advance(4);
		writer.WriteEndArray();
		writer.Flush();
		Assert.Equal("[\n  true\n]", Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));
	}

	[Test]
	[Arguments(-1, false)]
	[Arguments(int.MaxValue, false)]
	[Arguments(-1, true)]
	[Arguments(int.MaxValue, true)]
	public void Reservation_InvalidLengthsPreserveBytesAndState(int invalidLength, bool propertyValue)
	{
		TestBufferWriter buffer = new(64);
		JsonWriter writer = new(buffer) { WriteIndented = true };
		if (propertyValue)
		{
			writer.WriteStartObject();
			writer.WritePropertyName("x");
		}
		else
		{
			writer.WriteStartArray();
		}

		"null"u8.CopyTo(writer.GetSpan(4));
		int unflushed = writer.UnflushedBytes;
		try
		{
			writer.GetSpan(invalidLength);
			Assert.Fail("Expected invalid reservation rejection.");
		}
		catch (ArgumentOutOfRangeException)
		{
		}

		AssertInvalidAdvance(ref writer, invalidLength);
		Assert.Equal(unflushed, writer.UnflushedBytes);
		writer.Advance(4);
		writer.WriteValueSeparator();
		if (propertyValue)
		{
			writer.WritePropertyName("y");
		}

		writer.Write(true);
		if (propertyValue)
		{
			writer.WriteEndObject();
		}
		else
		{
			writer.WriteEndArray();
		}

		writer.Flush();
		Assert.Equal(propertyValue ? "{\n  \"x\": null,\n  \"y\": true\n}" : "[\n  null,\n  true\n]", Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));
	}

	[Test]
	[Arguments("none")]
	[Arguments("advance")]
	[Arguments("cancel")]
	[Arguments("flush")]
	[Arguments("scalar")]
	[Arguments("raw")]
	[Arguments("separator")]
	[Arguments("property")]
	[Arguments("start")]
	[Arguments("end")]
	[Arguments("endObject")]
	public void Advance_RequiresAnUnconsumedReservation(string invalidation)
	{
		TestBufferWriter buffer = new(128);
		JsonWriter writer = new(buffer);
		if (invalidation == "endObject")
		{
			writer.WriteStartObject();
		}
		else if (invalidation == "property")
		{
			writer.WriteStartObject();
			writer.WritePropertyName("x");
			writer.Write(1);
			writer.WriteValueSeparator();
		}
		else
		{
			writer.WriteStartArray();
			if (invalidation == "separator")
			{
				writer.Write(1);
			}
		}

		if (invalidation != "none")
		{
			"null"u8.CopyTo(writer.GetSpan(4));
		}

		switch (invalidation)
		{
			case "advance":
				writer.Advance(4);
				break;
			case "cancel":
				writer.Advance(0);
				break;
			case "flush":
				writer.Flush();
				break;
			case "scalar":
				writer.Write(1);
				break;
			case "raw":
				writer.WriteRaw("true"u8);
				break;
			case "separator":
				writer.WriteValueSeparator();
				break;
			case "property":
				writer.WritePropertyName("y");
				break;
			case "start":
				writer.WriteStartArray();
				break;
			case "end":
				writer.WriteEndArray();
				break;
			case "endObject":
				writer.WriteEndObject();
				break;
		}

		int unflushed = writer.UnflushedBytes;
		try
		{
			writer.Advance(1);
			Assert.Fail("Expected missing reservation rejection.");
		}
		catch (InvalidOperationException)
		{
		}

		Assert.Equal(unflushed, writer.UnflushedBytes);
		if (invalidation is not ("end" or "endObject"))
		{
			if (invalidation is "advance" or "scalar" or "raw")
			{
				writer.WriteValueSeparator();
			}

			WriteDirect(ref writer, "false"u8);
			if (invalidation == "property")
			{
				writer.WriteEndObject();
			}
			else
			{
				writer.WriteEndArray();
				if (invalidation == "start")
				{
					writer.WriteEndArray();
				}
			}
		}

		writer.Flush();
		string expected = invalidation switch
		{
			"advance" => "[null,false]",
			"scalar" or "separator" => "[1,false]",
			"raw" => "[true,false]",
			"property" => "{\"x\":1,\"y\":false}",
			"start" => "[[false]]",
			"end" => "[]",
			"endObject" => "{}",
			_ => "[false]",
		};
		Assert.Equal(expected, Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));
	}

	[Test]
	public void Reservation_CloneHasIndependentStateAndDoesNotInvalidateOriginal()
	{
		TestBufferWriter buffer = new(64);
		JsonWriter writer = new(buffer) { WriteIndented = true };
		writer.WriteStartArray();
		"null"u8.CopyTo(writer.GetSpan(4));
		TestBufferWriter cloneBuffer = new();
		JsonWriter clone = writer.Clone(cloneBuffer);
		Assert.True(clone.WriteIndented);
		try
		{
			clone.Advance(1);
			Assert.Fail("A clone must not inherit the reservation.");
		}
		catch (InvalidOperationException)
		{
		}

		WriteDirect(ref clone, "{\"x\":1}"u8);
		clone.Flush();
		writer.Advance(4);
		writer.WriteEndArray();
		writer.Flush();
		Assert.Equal("{\"x\":1}", Encoding.UTF8.GetString(cloneBuffer.WrittenSpan.ToArray()));
		Assert.Equal("[\n  null\n]", Encoding.UTF8.GetString(buffer.WrittenSpan.ToArray()));
	}

	[Test]
	public async Task Reservation_AsyncReturnPreservesOnlyCommittedValueState()
	{
		Pipe pipe = new();
		JsonAsyncWriter asyncWriter = new(pipe.Writer, writeIndented: true);
		JsonWriter writer = asyncWriter.CreateWriter();
		writer.WriteStartObject();
		writer.WritePropertyName("x");
		writer.GetSpan(10)[0] = (byte)'?';
		asyncWriter.ReturnWriter(ref writer);
		writer = asyncWriter.CreateWriter();
		writer.WriteStartArray();
		WriteDirect(ref writer, "{\"a\":1}"u8);
		asyncWriter.ReturnWriter(ref writer);
		writer = asyncWriter.CreateWriter();
		writer.WriteValueSeparator();
		writer.GetSpan(100)[0] = (byte)'!';
		asyncWriter.ReturnWriter(ref writer);
		writer = asyncWriter.CreateWriter();
		WriteDirect(ref writer, "true"u8);
		writer.WriteEndArray();
		writer.WriteEndObject();
		asyncWriter.ReturnWriter(ref writer);
		await asyncWriter.FlushAsync(CancellationToken.None);
		await pipe.Writer.CompleteAsync();
		ReadResult result = await pipe.Reader.ReadAsync();
		Assert.Equal("{\n  \"x\": [\n    {\"a\":1},\n    true\n  ]\n}", Encoding.UTF8.GetString(result.Buffer.ToArray()));
		pipe.Reader.AdvanceTo(result.Buffer.End);
		await pipe.Reader.CompleteAsync();
	}

	private static void WriteDirect(ref JsonWriter writer, ReadOnlySpan<byte> value)
	{
		value.CopyTo(writer.GetSpan(value.Length));
		writer.Advance(value.Length);
	}

	private static void AssertInvalidAdvance(ref JsonWriter writer, int length)
	{
		try
		{
			writer.Advance(length);
			Assert.Fail("Expected invalid advance rejection.");
		}
		catch (ArgumentOutOfRangeException)
		{
		}
	}

	private sealed class TestBufferWriter(int initialCapacity = 256) : IBufferWriter<byte>
	{
		private byte[] buffer = new byte[initialCapacity];

		public int WrittenCount { get; private set; }

		public ReadOnlySpan<byte> WrittenSpan => this.buffer.AsSpan(0, this.WrittenCount);

		public void Advance(int count)
		{
			if ((uint)count > (uint)(this.buffer.Length - this.WrittenCount))
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			this.WrittenCount += count;
		}

		public Memory<byte> GetMemory(int sizeHint = 0)
		{
			if (sizeHint < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(sizeHint));
			}

			sizeHint = Math.Max(sizeHint, 1);
			if (sizeHint > this.buffer.Length - this.WrittenCount)
			{
				Array.Resize(ref this.buffer, Math.Max(this.buffer.Length * 2, checked(this.WrittenCount + sizeHint)));
			}

			return this.buffer.AsMemory(this.WrittenCount);
		}

		public Span<byte> GetSpan(int sizeHint = 0) => this.GetMemory(sizeHint).Span;
	}
}
