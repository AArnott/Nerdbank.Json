// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Nerdbank.Json;

/// <summary>
/// Writes JSON values to a UTF-8 buffer.
/// </summary>
/// <remarks>
/// This type is allocation-conscious and writes UTF-8 directly to an <see cref="IBufferWriter{T}"/>.
/// </remarks>
public ref struct JsonWriter
{
	private IBufferWriter<byte> writer;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonWriter"/> struct.
	/// </summary>
	/// <param name="writer">The destination for UTF-8 JSON bytes.</param>
	public JsonWriter(IBufferWriter<byte> writer)
	{
		if (writer is null)
		{
			throw new ArgumentNullException(nameof(writer));
		}

		this.writer = writer;
	}

	/// <summary>
	/// Writes the JSON <see langword="null"/> literal.
	/// </summary>
	public void WriteNullValue() => this.WriteAscii("null"u8);

	/// <summary>
	/// Writes a JSON boolean literal.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteBooleanValue(bool value) => this.WriteAscii(value ? "true"u8 : "false"u8);

	/// <summary>
	/// Writes the start of a JSON object.
	/// </summary>
	public void WriteStartObject() => this.WriteByte((byte)'{');

	/// <summary>
	/// Writes the end of a JSON object.
	/// </summary>
	public void WriteEndObject() => this.WriteByte((byte)'}');

	/// <summary>
	/// Writes the start of a JSON array.
	/// </summary>
	public void WriteStartArray() => this.WriteByte((byte)'[');

	/// <summary>
	/// Writes the end of a JSON array.
	/// </summary>
	public void WriteEndArray() => this.WriteByte((byte)']');

	/// <summary>
	/// Writes a JSON value separator.
	/// </summary>
	public void WriteValueSeparator() => this.WriteByte((byte)',');

	/// <summary>
	/// Writes a JSON property name and name separator.
	/// </summary>
	/// <param name="name">The property name to write.</param>
	public void WritePropertyName(string name)
	{
		this.WriteStringValue(name);
		this.WriteByte((byte)':');
	}

	/// <summary>
	/// Writes a JSON string value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteStringValue(string? value)
	{
		if (value is null)
		{
			this.WriteNullValue();
			return;
		}

		this.WriteStringValue(value.AsSpan());
	}

	/// <summary>
	/// Writes a JSON string value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteStringValue(ReadOnlySpan<char> value)
	{
		this.WriteByte((byte)'\"');

		int copyStart = 0;
		for (int i = 0; i < value.Length; i++)
		{
			char ch = value[i];
			if (NeedsEscaping(ch))
			{
				this.WriteUtf8(value.Slice(copyStart, i - copyStart));
				this.WriteEscapedChar(ch);
				copyStart = i + 1;
			}
		}

		this.WriteUtf8(value.Slice(copyStart));
		this.WriteByte((byte)'\"');
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(byte value) => this.WriteRawValue(value.ToString(CultureInfo.InvariantCulture));

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(sbyte value) => this.WriteRawValue(value.ToString(CultureInfo.InvariantCulture));

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(short value) => this.WriteRawValue(value.ToString(CultureInfo.InvariantCulture));

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(ushort value) => this.WriteRawValue(value.ToString(CultureInfo.InvariantCulture));

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(int value) => this.WriteRawValue(value.ToString(CultureInfo.InvariantCulture));

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(uint value) => this.WriteRawValue(value.ToString(CultureInfo.InvariantCulture));

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(long value) => this.WriteRawValue(value.ToString(CultureInfo.InvariantCulture));

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(ulong value) => this.WriteRawValue(value.ToString(CultureInfo.InvariantCulture));

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(float value) => this.WriteRawValue(value.ToString("R", CultureInfo.InvariantCulture));

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(double value) => this.WriteRawValue(value.ToString("R", CultureInfo.InvariantCulture));

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(decimal value) => this.WriteRawValue(value.ToString(CultureInfo.InvariantCulture));

	/// <summary>
	/// Writes a JSON Base64 string value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteBase64StringValue(byte[]? value)
	{
		if (value is null)
		{
			this.WriteNullValue();
			return;
		}

		this.WriteStringValue(Convert.ToBase64String(value));
	}

	/// <summary>
	/// Writes a JSON Base64 string value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteBase64StringValue(ReadOnlySpan<byte> value) => this.WriteStringValue(Convert.ToBase64String(value.ToArray()));

	/// <summary>
	/// Writes a raw JSON token.
	/// </summary>
	/// <param name="value">The raw JSON text to write.</param>
	public void WriteRawValue(string value)
	{
		if (value is null)
		{
			throw new ArgumentNullException(nameof(value));
		}

		this.WriteUtf8(value.AsSpan());
	}

	private static bool NeedsEscaping(char ch) => ch < 0x20 || ch is '"' or '\\';

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static byte ToHex(uint value)
	{
		value &= 0xF;
		return (byte)(value < 10 ? '0' + value : 'A' + (value - 10));
	}

	private void WriteEscapedChar(char ch)
	{
		switch (ch)
		{
			case '"':
				this.WriteAscii("\\\""u8);
				break;
			case '\\':
				this.WriteAscii("\\\\"u8);
				break;
			case '\b':
				this.WriteAscii("\\b"u8);
				break;
			case '\f':
				this.WriteAscii("\\f"u8);
				break;
			case '\n':
				this.WriteAscii("\\n"u8);
				break;
			case '\r':
				this.WriteAscii("\\r"u8);
				break;
			case '\t':
				this.WriteAscii("\\t"u8);
				break;
			default:
				this.WriteUnicodeEscape(ch);
				break;
		}
	}

	private void WriteUtf8(ReadOnlySpan<char> value)
	{
		for (int i = 0; i < value.Length; i++)
		{
			char ch = value[i];
			if (!char.IsSurrogate(ch))
			{
				this.WriteScalar(ch);
				continue;
			}

			if (!char.IsHighSurrogate(ch) || i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
			{
				throw new ArgumentException("The value contains an unmatched surrogate code point.", nameof(value));
			}

			int scalar = char.ConvertToUtf32(ch, value[++i]);
			this.WriteScalar((uint)scalar);
		}
	}

	private void WriteScalar(uint scalar)
	{
		if (scalar <= 0x7F)
		{
			this.WriteByte((byte)scalar);
		}
		else if (scalar <= 0x7FF)
		{
			Span<byte> buffer = this.writer.GetSpan(2);
			buffer[0] = (byte)(0xC0 | (scalar >> 6));
			buffer[1] = (byte)(0x80 | (scalar & 0x3F));
			this.writer.Advance(2);
		}
		else if (scalar <= 0xFFFF)
		{
			Span<byte> buffer = this.writer.GetSpan(3);
			buffer[0] = (byte)(0xE0 | (scalar >> 12));
			buffer[1] = (byte)(0x80 | ((scalar >> 6) & 0x3F));
			buffer[2] = (byte)(0x80 | (scalar & 0x3F));
			this.writer.Advance(3);
		}
		else
		{
			Span<byte> buffer = this.writer.GetSpan(4);
			buffer[0] = (byte)(0xF0 | (scalar >> 18));
			buffer[1] = (byte)(0x80 | ((scalar >> 12) & 0x3F));
			buffer[2] = (byte)(0x80 | ((scalar >> 6) & 0x3F));
			buffer[3] = (byte)(0x80 | (scalar & 0x3F));
			this.writer.Advance(4);
		}
	}

	private void WriteUnicodeEscape(char ch)
	{
		Span<byte> buffer = this.writer.GetSpan(6);
		buffer[0] = (byte)'\\';
		buffer[1] = (byte)'u';
		buffer[2] = ToHex((uint)ch >> 12);
		buffer[3] = ToHex((uint)ch >> 8);
		buffer[4] = ToHex((uint)ch >> 4);
		buffer[5] = ToHex(ch);
		this.writer.Advance(6);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void WriteByte(byte value)
	{
		Span<byte> buffer = this.writer.GetSpan(1);
		buffer[0] = value;
		this.writer.Advance(1);
	}

	private void WriteAscii(ReadOnlySpan<byte> utf8Text) => this.Write(utf8Text);

	private void Write(ReadOnlySpan<byte> value)
	{
		Span<byte> buffer = this.writer.GetSpan(value.Length);
		value.CopyTo(buffer);
		this.writer.Advance(value.Length);
	}
}
