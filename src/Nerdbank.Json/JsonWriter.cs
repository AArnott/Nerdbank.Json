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
	private const int InlineStackCapacity = 8;

	private static readonly Encoding Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

#if NET8_0_OR_GREATER
	// The common case is shallow nesting, so the first InlineStackCapacity levels are tracked in this
	// inline (non-heap) buffer. `stack` remains null until nesting exceeds that capacity, or until this
	// writer is constructed from previously-exported (necessarily heap-allocated) state.
	private InlineContainerStack inlineStack;
	private ContainerState[]? stack;
#else
	private ContainerState[] stack = new ContainerState[InlineStackCapacity];
#endif

	private BufferWriter writer;
	private int depth;
	private bool pendingPropertyValue;
	private int reservedValueLength;

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
	/// Initializes a new instance of the <see cref="JsonWriter"/> struct, restoring container-tracking state.
	/// </summary>
	/// <param name="writer">The buffer writer to use for output.</param>
	/// <param name="stack">The saved container stack.</param>
	/// <param name="depth">The saved container depth.</param>
	/// <param name="pendingPropertyValue">The saved pending-property-value flag.</param>
	internal JsonWriter(BufferWriter writer, ContainerState[] stack, int depth, bool pendingPropertyValue)
	{
		this.writer = writer;
		this.stack = stack;
		this.depth = depth;
		this.pendingPropertyValue = pendingPropertyValue;
	}

	/// <summary>
	/// Gets a value indicating whether line breaks and indentation should be written.
	/// </summary>
	public bool WriteIndented { get; init; }

	/// <summary>
	/// Gets the number of bytes that have been written but not yet committed <see cref="Flush">flushed</see> to the underlying <see cref="IBufferWriter{T}"/>.
	/// </summary>
	public readonly int UnflushedBytes => this.writer.UncommittedBytes;

	/// <summary>
	/// Gets the underlying buffer writer. Used by <see cref="JsonAsyncWriter"/> to persist state across writers.
	/// </summary>
	internal readonly BufferWriter Writer => this.writer;

	/// <summary>
	/// Ensures everything previously written has been flushed to the underlying <see cref="IBufferWriter{T}"/>.
	/// </summary>
	public void Flush()
	{
		this.reservedValueLength = 0;
		this.writer.Commit();
	}

	/// <summary>
	/// Reserves contiguous space for one complete, trusted UTF-8 JSON value.
	/// </summary>
	/// <param name="length">The minimum number of payload bytes required. Zero requests a nonempty span.</param>
	/// <returns>A borrowed span of at least <paramref name="length"/> bytes, excluding any enclosing indentation.</returns>
	/// <remarks>
	/// Fill the span and call <see cref="Advance"/> once with the complete value's byte length.
	/// As with <see cref="WriteRaw(ReadOnlySpan{byte})"/>, syntax is not validated: fragments, property names,
	/// separators, empty values, and multiple values are not supported.
	/// Reservation does not consume a value or change container state, but may flush previously written bytes.
	/// Each successful call replaces the previous reservation; unadvanced contents need not be preserved.
	/// The span expires on another reservation, any write, <see cref="Advance"/>, <see cref="Flush"/>,
	/// or returning this writer to <see cref="JsonAsyncWriter"/>. Do not copy a writer to share its reservation.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative or too large for a contiguous reservation including indentation.</exception>
	public Span<byte> GetSpan(int length)
	{
		Requires.Range(length >= 0, nameof(length));
		int prefixLength = this.GetValuePrefixLength();
		Requires.Range(Math.Max(length, 1) <= int.MaxValue - prefixLength - this.writer.UncommittedBytes, nameof(length));
		this.reservedValueLength = 0;
		Span<byte> span = this.writer.GetSpan(Math.Max(length, 1) + prefixLength);
		this.reservedValueLength = Math.Min(span.Length - prefixLength, int.MaxValue - this.writer.UncommittedBytes - prefixLength);
		return span.Slice(prefixLength, this.reservedValueLength);
	}

	/// <summary>
	/// Commits one complete, trusted UTF-8 JSON value written into the latest <see cref="GetSpan"/> reservation.
	/// </summary>
	/// <param name="length">The number of payload bytes written, or zero to cancel the reservation without writing a value.</param>
	/// <remarks>
	/// A positive advance consumes the reservation and applies the same enclosing indentation and value bookkeeping
	/// as <see cref="WriteRaw(ReadOnlySpan{byte})"/>. Payload contents are not validated, escaped, or reindented.
	/// Callers still write enclosing separators explicitly. A zero advance is also allowed without a reservation.
	/// Invalid advances leave the reservation, byte counters, and container state unchanged.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative or exceeds the reserved span.</exception>
	/// <exception cref="InvalidOperationException">A positive advance has no active reservation.</exception>
	public void Advance(int length)
	{
		Requires.Range(length >= 0, nameof(length));
		if (length == 0)
		{
			this.reservedValueLength = 0;
			return;
		}

		if (this.reservedValueLength == 0)
		{
			throw new InvalidOperationException("Call GetSpan before advancing a complete raw JSON value.");
		}

		Requires.Range(length <= this.reservedValueLength, nameof(length));
		this.BeforeValueToken();
		this.writer.Advance(length);
	}

	/// <summary>Creates a writer for a new destination using this writer's formatting options.</summary>
	/// <param name="writer">The new destination.</param>
	/// <returns>An independent writer with no inherited container or pending-property state.</returns>
	public readonly JsonWriter Clone(IBufferWriter<byte> writer) => new(writer) { WriteIndented = this.WriteIndented };

	/// <summary>
	/// Writes the JSON <see langword="null"/> literal.
	/// </summary>
	public void WriteNull()
	{
		this.BeforeValueToken();
		this.WriteAscii("null"u8);
	}

	/// <summary>
	/// Writes a JSON boolean literal.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(bool value)
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
		this.reservedValueLength = 0;
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
		this.reservedValueLength = 0;
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
		this.reservedValueLength = 0;
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
		ContainerState state = this.PreparePropertyName();
		this.WriteQuotedString(name.AsSpan());
		this.FinishPropertyName(state);
	}

	/// <summary>
	/// Writes a JSON string value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(string? value)
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
	public void Write(ReadOnlySpan<char> value)
	{
		this.BeforeValueToken();
		this.WriteQuotedString(value);
	}

	/// <summary>Writes decoded UTF-8 content as a JSON string, escaping quotes, backslashes, and control bytes.</summary>
	/// <param name="value">Decoded UTF-8 bytes, not an already-escaped JSON token.</param>
	/// <remarks>Non-ASCII bytes are preserved without Unicode validation or transcoding.</remarks>
	public void WriteString(ReadOnlySpan<byte> value)
	{
		this.BeforeValueToken();
		this.WriteByte((byte)'"');
		this.WriteEscapedUtf8(value);
		this.WriteByte((byte)'"');
	}

	/// <summary>Writes decoded, possibly segmented UTF-8 content as a JSON string.</summary>
	/// <param name="value">Decoded UTF-8 bytes. UTF-8 code points may span segments.</param>
	/// <remarks>Non-ASCII bytes are preserved without Unicode validation or transcoding.</remarks>
	public void WriteString(scoped in ReadOnlySequence<byte> value)
	{
		this.BeforeValueToken();
		this.WriteByte((byte)'"');
		foreach (ReadOnlyMemory<byte> segment in value)
		{
			this.WriteEscapedUtf8(segment.Span);
		}

		this.WriteByte((byte)'"');
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(byte value)
	{
		this.BeforeValueToken();
#if NET8_0_OR_GREATER
		this.WriteUtf8Formattable(value, maxLength: 3);
#else
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
#endif
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(sbyte value)
	{
		this.BeforeValueToken();
#if NET8_0_OR_GREATER
		this.WriteUtf8Formattable(value, maxLength: 4);
#else
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
#endif
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(short value)
	{
		this.BeforeValueToken();
#if NET8_0_OR_GREATER
		this.WriteUtf8Formattable(value, maxLength: 6);
#else
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
#endif
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(ushort value)
	{
		this.BeforeValueToken();
#if NET8_0_OR_GREATER
		this.WriteUtf8Formattable(value, maxLength: 5);
#else
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
#endif
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(int value)
	{
		this.BeforeValueToken();
#if NET8_0_OR_GREATER
		this.WriteUtf8Formattable(value, maxLength: 11);
#else
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
#endif
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(uint value)
	{
		this.BeforeValueToken();
#if NET8_0_OR_GREATER
		this.WriteUtf8Formattable(value, maxLength: 10);
#else
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
#endif
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(long value)
	{
		this.BeforeValueToken();
#if NET8_0_OR_GREATER
		this.WriteUtf8Formattable(value, maxLength: 20);
#else
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
#endif
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(ulong value)
	{
		this.BeforeValueToken();
#if NET8_0_OR_GREATER
		this.WriteUtf8Formattable(value, maxLength: 20);
#else
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
#endif
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(float value)
	{
		if (float.IsNaN(value) || float.IsInfinity(value))
		{
			throw new ArgumentOutOfRangeException(nameof(value), value, "JSON numbers must be finite.");
		}

		this.BeforeValueToken();
#if NET8_0_OR_GREATER
		this.WriteUtf8Formattable(value, maxLength: 32, format: "R");
#else
		this.WriteUtf8(value.ToString("R", CultureInfo.InvariantCulture).AsSpan());
#endif
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(double value)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			throw new ArgumentOutOfRangeException(nameof(value), value, "JSON numbers must be finite.");
		}

		this.BeforeValueToken();
#if NET8_0_OR_GREATER
		this.WriteUtf8Formattable(value, maxLength: 32, format: "R");
#else
		this.WriteUtf8(value.ToString("R", CultureInfo.InvariantCulture).AsSpan());
#endif
	}

	/// <summary>
	/// Writes a JSON number value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void Write(decimal value)
	{
		this.BeforeValueToken();
#if NET8_0_OR_GREATER
		this.WriteUtf8Formattable(value, maxLength: 32);
#else
		this.WriteUtf8(value.ToString(CultureInfo.InvariantCulture).AsSpan());
#endif
	}

	/// <summary>
	/// Writes a JSON Base64 string value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteBase64StringValue(byte[]? value)
	{
		if (value is null)
		{
			this.WriteNull();
			return;
		}

		this.Write(Convert.ToBase64String(value));
	}

	/// <summary>
	/// Writes a JSON Base64 string value.
	/// </summary>
	/// <param name="value">The value to write.</param>
	public void WriteBase64StringValue(ReadOnlySpan<byte> value) => this.Write(Convert.ToBase64String(value.ToArray()));

	/// <summary>
	/// Writes a raw JSON token.
	/// </summary>
	/// <param name="value">The raw JSON text to write.</param>
	public void WriteRaw(string value)
	{
		Requires.NotNull(value);

		this.BeforeValueToken();
		this.WriteUtf8(value.AsSpan());
	}

	/// <summary>Writes raw UTF-8 JSON without validating or escaping it.</summary>
	/// <param name="value">The UTF-8 JSON to write.</param>
	public void WriteRaw(ReadOnlySpan<byte> value)
	{
		this.BeforeValueToken();
		this.WriteBytes(value);
	}

	/// <summary>Writes a raw, possibly segmented UTF-8 JSON value without validating or escaping it.</summary>
	/// <param name="value">The UTF-8 JSON to write.</param>
	public void WriteRaw(scoped in ReadOnlySequence<byte> value)
	{
		this.BeforeValueToken();
		foreach (ReadOnlyMemory<byte> segment in value)
		{
			this.WriteBytes(segment.Span);
		}
	}

	/// <summary>Writes one raw JSON value after validating its syntax and the default depth limit.</summary>
	/// <param name="value">The raw JSON value. An empty or default payload is invalid.</param>
	/// <remarks>Preserves the exact UTF-8 bytes. Raw values must use strict JSON without comments or trailing commas.</remarks>
	public void Write(RawJson value) => RawJsonConverter.Instance.Write(ref this, value, new());

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

	/// <summary>
	/// Exports the container-tracking state so it can be restored on a subsequent writer.
	/// </summary>
	/// <returns>The container stack, depth, and pending-property-value flag.</returns>
	internal (ContainerState[] Stack, int Depth, bool PendingPropertyValue) ExportContainerState()
	{
#if NET8_0_OR_GREATER
		if (this.stack is null)
		{
			// The nesting never exceeded the inline capacity, so no heap array exists yet. The caller
			// requires a heap array it can hold onto as a field across writer instances, so materialize one now.
			ContainerState[] materialized = new ContainerState[InlineStackCapacity];
			for (int i = 0; i < InlineStackCapacity; i++)
			{
				materialized[i] = this.inlineStack[i];
			}

			this.stack = materialized;
		}
#endif

		return (this.stack!, this.depth, this.pendingPropertyValue);
	}

#if NET8_0_OR_GREATER
	/// <summary>
	/// Writes a known-ASCII, escape-free formatted value directly to the UTF-8 output buffer as a JSON string.
	/// </summary>
	/// <typeparam name="T">The type of value to format.</typeparam>
	/// <param name="value">The value to format.</param>
	/// <param name="maxLength">The maximum number of bytes produced by <paramref name="format"/>.</param>
	/// <param name="format">The invariant format that guarantees only ASCII characters requiring no JSON escaping.</param>
	internal void WriteAsciiFormattedString<T>(T value, int maxLength, string format)
		where T : IUtf8SpanFormattable
	{
		this.BeforeValueToken();
		Span<byte> buffer = this.writer.GetSpan(maxLength + 2);
		if (!value.TryFormat(buffer.Slice(1, maxLength), out int bytesWritten, format, CultureInfo.InvariantCulture))
		{
			throw new InvalidOperationException("The buffer was too small to format the value. This is a bug in " + nameof(JsonWriter) + ".");
		}

		buffer[0] = (byte)'"';
		buffer[bytesWritten + 1] = (byte)'"';
		this.writer.Advance(bytesWritten + 2);
	}
#endif

	/// <summary>
	/// Writes an already-escaped UTF-8 JSON property name, including its surrounding quotes.
	/// </summary>
	/// <param name="utf8EncodedName">The UTF-8 property name token to write.</param>
	internal void WritePropertyName(ReadOnlySpan<byte> utf8EncodedName)
	{
		ContainerState state = this.PreparePropertyName();
		this.WriteBytes(utf8EncodedName);
		this.FinishPropertyName(state);
	}

	/// <summary>
	/// Encodes a property name as a quoted, escaped UTF-8 JSON token.
	/// </summary>
	/// <param name="name">The property name to encode.</param>
	/// <returns>The quoted UTF-8 JSON token.</returns>
	internal static byte[] EncodePropertyName(string name)
	{
		Requires.NotNull(name);

		byte[] scratch = new byte[checked((name.Length * 6) + 2)];
		JsonWriter jsonWriter = new(SequencePool<byte>.Shared, scratch);
		jsonWriter.WriteQuotedString(name.AsSpan());
		Assumes.True(jsonWriter.writer.TryGetUncommittedSpan(out ReadOnlySpan<byte> span));
		return span.ToArray();
	}

	private void BeforeValueToken()
	{
		this.reservedValueLength = 0;
		if (this.pendingPropertyValue)
		{
			this.pendingPropertyValue = false;
			return;
		}

		if (this.depth == 0)
		{
			return;
		}

		ContainerState state = this.GetStack(this.depth - 1);
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
		this.SetStack(this.depth - 1, state);
	}

	private readonly int GetValuePrefixLength()
	{
		if (!this.WriteIndented || this.pendingPropertyValue || this.depth == 0)
		{
			return 0;
		}

		ContainerState state = this.GetStack(this.depth - 1);

		// Leave room for BeforeValueToken without moving or overwriting the caller's payload.
		return state.Kind == ContainerKind.Array ? checked((this.depth * 2) + (state.Count == 0 ? 1 : 0)) : 0;
	}

	private void PushContainer(ContainerKind kind)
	{
#if NET8_0_OR_GREATER
		if (this.stack is null)
		{
			if (this.depth < InlineStackCapacity)
			{
				this.inlineStack[this.depth++] = new ContainerState(kind);
				return;
			}

			// The inline buffer is full. Deep nesting is uncommon, so fall back to a heap array from
			// here on, seeded with the elements already tracked in the inline buffer.
			ContainerState[] grown = new ContainerState[InlineStackCapacity * 2];
			for (int i = 0; i < InlineStackCapacity; i++)
			{
				grown[i] = this.inlineStack[i];
			}

			this.stack = grown;
		}
#endif

		if (this.depth == this.stack!.Length)
		{
			Array.Resize(ref this.stack, this.stack.Length * 2);
		}

		this.stack[this.depth++] = new ContainerState(kind);
	}

	private ContainerState PopContainer(ContainerKind expectedKind)
	{
		ContainerState state = this.GetStack(--this.depth);
		if (state.Kind != expectedKind)
		{
			throw new InvalidOperationException("JSON container nesting is inconsistent.");
		}

		return state;
	}

	private readonly ContainerState GetCurrentContainer(ContainerKind expectedKind)
	{
		if (this.depth == 0)
		{
			throw new InvalidOperationException("JSON property names may only appear within objects.");
		}

		ContainerState state = this.GetStack(this.depth - 1);
		if (state.Kind != expectedKind)
		{
			throw new InvalidOperationException("JSON property names may only appear within objects.");
		}

		return state;
	}

	private void SetCurrentContainer(ContainerState state) => this.SetStack(this.depth - 1, state);

	private readonly ContainerState GetStack(int index)
	{
#if NET8_0_OR_GREATER
		return this.stack is null ? this.inlineStack[index] : this.stack[index];
#else
		return this.stack[index];
#endif
	}

	private void SetStack(int index, ContainerState value)
	{
#if NET8_0_OR_GREATER
		if (this.stack is null)
		{
			this.inlineStack[index] = value;
			return;
		}
#endif

		this.stack[index] = value;
	}

	private ContainerState PreparePropertyName()
	{
		this.reservedValueLength = 0;
		ContainerState state = this.GetCurrentContainer(ContainerKind.Object);
		if (this.WriteIndented)
		{
			if (state.Count == 0)
			{
				this.WriteByte(LineFeed);
			}

			this.WriteIndent(this.depth);
		}

		return state;
	}

	private void FinishPropertyName(ContainerState state)
	{
		this.WriteByte((byte)':');
		if (this.WriteIndented)
		{
			this.WriteByte(Space);
		}

		state.Count++;
		this.SetCurrentContainer(state);
		this.pendingPropertyValue = true;
	}

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
				this.WriteUtf8(value[copyStart..i]);
				this.WriteEscapedChar(ch);
				copyStart = i + 1;
			}
		}

		this.WriteUtf8(value[copyStart..]);
		this.WriteByte((byte)'"');
	}

	private void WriteEscapedUtf8(ReadOnlySpan<byte> value)
	{
		int start = 0;
		for (int i = 0; i < value.Length; i++)
		{
			byte ch = value[i];
			if (ch < 0x20 || ch is (byte)'"' or (byte)'\\')
			{
				this.WriteBytes(value[start..i]);
				this.WriteEscapedChar((char)ch);
				start = i + 1;
			}
		}

		this.WriteBytes(value[start..]);
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
#if NET8_0_OR_GREATER
		// Fast path: bulk-copy runs of 7-bit ASCII (the common case for property names and typical
		// string content) using a vectorized narrowing conversion instead of writing one byte at a time.
		// Non-ASCII content falls through to UTF-8 encoding and isolated-surrogate escaping below.
		if (Ascii.IsValid(value))
		{
			Span<byte> buffer = this.writer.GetSpan(value.Length);
			Ascii.FromUtf16(value, buffer, out int bytesWritten);
			this.writer.Advance(bytesWritten);
			return;
		}
#endif

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
				this.WriteUnicodeEscape(ch);
				continue;
			}

			int scalar = char.ConvertToUtf32(ch, value[++i]);
			this.WriteScalar((uint)scalar);
		}
	}

#if NET8_0_OR_GREATER
	/// <summary>
	/// Formats a numeric value directly into the destination UTF-8 buffer, avoiding the intermediate
	/// <see cref="string"/> allocation that <see cref="object.ToString()"/>-based formatting would require.
	/// </summary>
	/// <typeparam name="T">The numeric type to format.</typeparam>
	/// <param name="value">The value to format.</param>
	/// <param name="maxLength">A buffer size, in bytes, guaranteed to be large enough for any value of <typeparamref name="T"/> formatted with <paramref name="format"/>.</param>
	/// <param name="format">The standard numeric format string to use, or <see langword="null"/> for the default format.</param>
	private void WriteUtf8Formattable<T>(T value, int maxLength, string? format = null)
		where T : IUtf8SpanFormattable
	{
		Span<byte> buffer = this.writer.GetSpan(maxLength);
		if (!value.TryFormat(buffer, out int bytesWritten, format, CultureInfo.InvariantCulture))
		{
			throw new InvalidOperationException("The buffer was too small to format the numeric value. This is a bug in " + nameof(JsonWriter) + ".");
		}

		this.writer.Advance(bytesWritten);
	}
#endif

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

	private void WriteAscii(ReadOnlySpan<byte> utf8Text) => this.WriteBytes(utf8Text);

	private void WriteBytes(ReadOnlySpan<byte> value)
	{
		Span<byte> buffer = this.writer.GetSpan(value.Length);
		value.CopyTo(buffer);
		this.writer.Advance(value.Length);
	}

#pragma warning disable SA1600 // Internal writer state types are self-explanatory.
#pragma warning disable SA1602 // Internal writer state enum members are self-explanatory.
	internal enum ContainerKind : byte
	{
		Object,
		Array,
	}

	internal struct ContainerState
	{
		internal ContainerKind Kind;
		internal int Count;

		internal ContainerState(ContainerKind kind)
		{
			this.Kind = kind;
			this.Count = 0;
		}
	}

#if NET8_0_OR_GREATER
	[System.Runtime.CompilerServices.InlineArray(InlineStackCapacity)]
	private struct InlineContainerStack
	{
		private ContainerState element0;
	}
#endif
#pragma warning restore SA1600
#pragma warning restore SA1602
}
