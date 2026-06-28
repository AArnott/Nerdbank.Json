// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1201 // Local writer helper ordering keeps container state adjacent to writer implementation details.
#pragma warning disable SA1204 // Static helper placement is kept close to their call sites in this low-level writer.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Nerdbank.Json;

/// <summary>
/// Writes JSON values to a UTF-8 buffer.
/// </summary>
/// <remarks>
/// This type is allocation-conscious and writes UTF-8 directly to an <see cref="IBufferWriter{T}"/>.
/// </remarks>
public ref struct JsonWriter
{
	private const byte LineFeed = (byte)'\n';
	private const byte Space = (byte)' ';

	private static readonly Encoding Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private ContainerState[] stack = new ContainerState[8];
	private BufferWriter writer;
	private int depth;
	private bool pendingPropertyValue;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonWriter"/> struct.
	/// </summary>
	/// <param name="writer">The destination for UTF-8 JSON bytes.</param>
	public JsonWriter(IBufferWriter<byte> writer) => this.writer = new BufferWriter(Requires.NotNull(writer));

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonWriter"/> struct.
	/// </summary>
	/// <param name="sequencePool">The pool to use for allocating sequences.</param>
	/// <param name="array">The initial buffer to write into.</param>
	internal JsonWriter(SequencePool<byte> sequencePool, byte[] array) => this.writer = new BufferWriter(sequencePool, array);

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonWriter"/> struct.
	/// </summary>
	/// <param name="writer">The buffer writer to use for output.</param>
	internal JsonWriter(BufferWriter writer) => this.writer = writer;

	/// <summary>
	/// Gets a value indicating whether line breaks and indentation should be written.
	/// </summary>
	public bool WriteIndented { get; init; }

	/// <summary>
	/// Gets the number of bytes that have been written but not yet committed <see cref="Flush">flushed</see> to the underlying <see cref="IBufferWriter{T}"/>.
	/// </summary>
	public int UnflushedBytes => this.writer.UncommittedBytes;

	/// <summary>
	/// Ensures everything previously written has been flushed to the underlying <see cref="IBufferWriter{T}"/>.
	/// </summary>
	public void Flush() => this.writer.Commit();

	/// <summary>
	/// Writes the JSON <see langword="null"/> literal.
	/// </summary>
	public void WriteNullValue()
	{
		this.BeforeValueToken();
		this.WriteAscii("null"u8);
	}

	/// <summary>
	/// Writes a JSON boolean literal.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteBooleanValue(bool value)
	{
		this.BeforeValueToken();
		this.WriteAscii(value ? "true"u8 : "false"u8);
	}

	/// <summary>
	/// Writes the start of a JSON object.
	/// </summary>
	public void WriteStartObject()
	{
		this.BeforeValueToken();
		this.WriteByte((byte)'{');
		this.PushContainer(ContainerKind.Object);
	}

	/// <summary>
	/// Writes the end of a JSON object.
	/// </summary>
	public void WriteEndObject()
	{
		ContainerState state = this.PopContainer(ContainerKind.Object);
		if (this.WriteIndented && state.Count > 0)
		{
			this.WriteNewLineAndIndent(this.depth);
		}

		this.WriteByte((byte)'}');
	}

	/// <summary>
	/// Writes the start of a JSON array.
	/// </summary>
	public void WriteStartArray()
	{
		this.BeforeValueToken();
		this.WriteByte((byte)'[');
		this.PushContainer(ContainerKind.Array);
	}

	/// <summary>
	/// Writes the end of a JSON array.
	/// </summary>
	public void WriteEndArray()
	{
		ContainerState state = this.PopContainer(ContainerKind.Array);
		if (this.WriteIndented && state.Count > 0)
		{
			this.WriteNewLineAndIndent(this.depth);
		}

		this.WriteByte((byte)']');
	}

	/// <summary>
	/// Writes a JSON value separator.
	/// </summary>
	public void WriteValueSeparator()
	{
		this.WriteByte((byte)',');
		if (this.WriteIndented)
		{
			this.WriteByte(LineFeed);
		}
	}

	/// <summary>
	/// Writes a JSON property name and name separator.
	/// </summary>
	/// <param name="name">The property name to write.</param>
	public void WritePropertyName(string name)
	{
		ContainerState state = this.GetCurrentContainer(ContainerKind.Object);
		if (this.WriteIndented)
		{
			if (state.Count == 0)
			{
				this.WriteByte(LineFeed);
			}

			this.WriteIndent(this.depth);
		}

		this.WriteQuotedString(name.AsSpan());
		this.WriteByte((byte)':');
		if (this.WriteIndented)
		{
			this.WriteByte(Space);
		}

		state.Count++;
		this.SetCurrentContainer(state);
		this.pendingPropertyValue = true;
	}

	/// <summary>
	/// Writes a JSON string value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteStringValue(string? value)
	{
		this.BeforeValueToken();
		if (value is null)
		{
			this.WriteAscii("null"u8);
			return;
		}

		this.WriteQuotedString(value.AsSpan());
	}

	/// <summary>
	/// Writes a JSON string value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteStringValue(ReadOnlySpan<char> value)
	{
		this.BeforeValueToken();
		this.WriteQuotedString(value);
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(byte value)
	{
		this.BeforeValueToken();
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(sbyte value)
	{
		this.BeforeValueToken();
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(short value)
	{
		this.BeforeValueToken();
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(ushort value)
	{
		this.BeforeValueToken();
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(int value)
	{
		this.BeforeValueToken();
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(uint value)
	{
		this.BeforeValueToken();
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(long value)
	{
		this.BeforeValueToken();
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(ulong value)
	{
		this.BeforeValueToken();
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(float value)
	{
		this.BeforeValueToken();
		this.WriteUtf8(value.ToString("R", CultureInfo.InvariantCulture).AsSpan());
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(double value)
	{
		this.BeforeValueToken();
		this.WriteUtf8(value.ToString("R", CultureInfo.InvariantCulture).AsSpan());
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteNumberValue(decimal value)
	{
		this.BeforeValueToken();
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
	}

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

		this.BeforeValueToken();
		this.WriteUtf8(value.AsSpan());
	}

	/// <summary>
	/// Flushes any uncommitted bytes to the underlying <see cref="IBufferWriter{T}"/> and returns the written bytes as a string.
	/// </summary>
	/// <returns>The written bytes as a string.</returns>
	internal string FlushAndGetString()
	{
		if (this.writer.TryGetUncommittedSpan(out ReadOnlySpan<byte> span))
		{
			return Encoding.GetString(span);
		}
		else
		{
			if (this.writer.SequenceRental.Value is null)
			{
				throw new NotSupportedException("This instance was not initialized to support this operation.");
			}

			this.Flush();
			string result = Encoding.GetString(this.writer.SequenceRental.Value.AsReadOnlySequence);
			this.writer.SequenceRental.Dispose();
			return result;
		}
	}

	private void BeforeValueToken()
	{
		if (this.pendingPropertyValue)
		{
			this.pendingPropertyValue = false;
			return;
		}

		if (this.depth == 0)
		{
			return;
		}

		ContainerState state = this.stack[this.depth - 1];
		if (state.Kind != ContainerKind.Array)
		{
			return;
		}

		if (this.WriteIndented)
		{
			if (state.Count == 0)
			{
				this.WriteByte(LineFeed);
			}

			this.WriteIndent(this.depth);
		}

		state.Count++;
		this.stack[this.depth - 1] = state;
	}

	private void PushContainer(ContainerKind kind)
	{
		if (this.depth == this.stack.Length)
		{
			Array.Resize(ref this.stack, this.stack.Length * 2);
		}

		this.stack[this.depth++] = new ContainerState(kind);
	}

	private ContainerState PopContainer(ContainerKind expectedKind)
	{
		ContainerState state = this.stack[--this.depth];
		if (state.Kind != expectedKind)
		{
			throw new InvalidOperationException("JSON container nesting is inconsistent.");
		}

		return state;
	}

	private ContainerState GetCurrentContainer(ContainerKind expectedKind)
	{
		if (this.depth == 0)
		{
			throw new InvalidOperationException("JSON property names may only appear within objects.");
		}

		ContainerState state = this.stack[this.depth - 1];
		if (state.Kind != expectedKind)
		{
			throw new InvalidOperationException("JSON property names may only appear within objects.");
		}

		return state;
	}

	private void SetCurrentContainer(ContainerState state) => this.stack[this.depth - 1] = state;

	private void WriteNewLineAndIndent(int indentDepth)
	{
		this.WriteByte(LineFeed);
		this.WriteIndent(indentDepth);
	}

	private void WriteIndent(int indentDepth)
	{
		for (int i = 0; i < indentDepth; i++)
		{
			this.WriteByte(Space);
			this.WriteByte(Space);
		}
	}

	private void WriteQuotedString(ReadOnlySpan<char> value)
	{
		this.WriteByte((byte)'"');

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
		this.WriteByte((byte)'"');
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

	private enum ContainerKind : byte
	{
		Object,
		Array,
	}

	private struct ContainerState
	{
		internal ContainerKind Kind;
		internal int Count;

		internal ContainerState(ContainerKind kind)
		{
			this.Kind = kind;
			this.Count = 0;
		}
	}
}
