// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Text;
using Nerdbank.Json;
using Xunit;

public class JsonWriterTests
{
	[Test]
	public void WriteStringValue_EscapesOnlyRfc8259RequiredCharacters()
	{
		TestBufferWriter buffer = new();
		JsonWriter writer = new(buffer);

		writer.WriteStringValue("plain ü <tag> \"quoted\" \\ slash\nnext");

		Assert.Equal("\"plain ü <tag> \\\"quoted\\\" \\\\ slash\\nnext\"", Encoding.UTF8.GetString(buffer.WrittenArray));
	}

	[Test]
	public void WriteStringValue_EscapesControlCharactersAsUnicodeWhenNoShortEscapeExists()
	{
		TestBufferWriter buffer = new();
		JsonWriter writer = new(buffer);

		writer.WriteStringValue("a\u0001b");

		Assert.Equal("\"a\\u0001b\"", Encoding.UTF8.GetString(buffer.WrittenArray));
	}

	[Test]
	public void WriteStringValue_WritesNullLiteralForNullString()
	{
		TestBufferWriter buffer = new();
		JsonWriter writer = new(buffer);

		writer.WriteStringValue((string?)null);

		Assert.Equal("null", Encoding.UTF8.GetString(buffer.WrittenArray));
	}

	[Test]
	public void WriteStringValue_ThrowsOnUnmatchedSurrogate()
	{
		TestBufferWriter buffer = new();
		JsonWriter writer = new(buffer);

		try
		{
			writer.WriteStringValue("\uD800");
			Assert.Fail("Expected an ArgumentException to be thrown.");
		}
		catch (ArgumentException)
		{
		}
	}

	private sealed class TestBufferWriter : IBufferWriter<byte>
	{
		private byte[] buffer = new byte[64];
		private int writtenCount;

		internal byte[] WrittenArray
		{
			get
			{
				byte[] result = new byte[this.writtenCount];
				Array.Copy(this.buffer, result, this.writtenCount);
				return result;
			}
		}

		public void Advance(int count)
		{
			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			this.writtenCount += count;
		}

		public Memory<byte> GetMemory(int sizeHint = 0)
		{
			this.EnsureCapacity(sizeHint);
			return this.buffer.AsMemory(this.writtenCount);
		}

		public Span<byte> GetSpan(int sizeHint = 0)
		{
			this.EnsureCapacity(sizeHint);
			return this.buffer.AsSpan(this.writtenCount);
		}

		private void EnsureCapacity(int sizeHint)
		{
			if (sizeHint < 1)
			{
				sizeHint = 1;
			}

			int available = this.buffer.Length - this.writtenCount;
			if (available >= sizeHint)
			{
				return;
			}

			int newLength = Math.Max(this.buffer.Length * 2, this.writtenCount + sizeHint);
			Array.Resize(ref this.buffer, newLength);
		}
	}
}
