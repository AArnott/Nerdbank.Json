// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Nerdbank.Json;

/// <summary>
/// Reads JSON values from a UTF-8 byte buffer.
/// </summary>
/// <remarks>
/// String boundaries, escapes, and unescaped control bytes are checked, but Unicode well-formedness is not.
/// CLR strings use UTF-8 replacement fallback and preserve each escaped UTF-16 code unit.
/// Unescaped byte views and raw JSON preserve the original bytes, including invalid UTF-8.
/// </remarks>
public ref struct JsonReader
{
	private readonly bool allowTrailingCommas;
	private readonly JsonCommentHandling commentHandling;
	private readonly bool sequenceInput;
	private JsonInput utf8Json;
	private ReadOnlySequence<byte>? sequence;
	private long position;
	private int navigationDepth;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonReader"/> struct.
	/// </summary>
	/// <param name="jsonUtf8">The UTF-8 JSON bytes to read from.</param>
	/// <param name="allowTrailingCommas">A value indicating whether trailing commas should be accepted while reading arrays and objects.</param>
	/// <param name="commentHandling">The policy for handling comments during deserialization.</param>
	public JsonReader(ReadOnlySpan<byte> jsonUtf8, bool allowTrailingCommas = false, JsonCommentHandling commentHandling = JsonCommentHandling.Disallow)
	{
		this.allowTrailingCommas = allowTrailingCommas;
		this.commentHandling = commentHandling;
		this.utf8Json = new(jsonUtf8);
		this.sequence = null;
		this.position = 0;
		this.navigationDepth = 0;
	}

	/// <summary>Initializes a new instance of the <see cref="JsonReader"/> struct over borrowed memory.</summary>
	/// <param name="jsonUtf8">The borrowed UTF-8 document.</param>
	/// <param name="allowTrailingCommas">Whether trailing commas are accepted.</param>
	/// <param name="commentHandling">The policy for JSON comments.</param>
	public JsonReader(ReadOnlyMemory<byte> jsonUtf8, bool allowTrailingCommas = false, JsonCommentHandling commentHandling = JsonCommentHandling.Disallow)
		: this(new ReadOnlySequence<byte>(jsonUtf8), allowTrailingCommas, commentHandling)
	{
	}

	/// <summary>Initializes a new instance of the <see cref="JsonReader"/> struct over a borrowed array.</summary>
	/// <param name="jsonUtf8">The borrowed UTF-8 document.</param>
	/// <param name="allowTrailingCommas">Whether trailing commas are accepted.</param>
	/// <param name="commentHandling">The policy for JSON comments.</param>
	public JsonReader(byte[] jsonUtf8, bool allowTrailingCommas = false, JsonCommentHandling commentHandling = JsonCommentHandling.Disallow)
		: this((ReadOnlyMemory<byte>)Requires.NotNull(jsonUtf8), allowTrailingCommas, commentHandling)
	{
	}

	/// <summary>Initializes a new instance of the <see cref="JsonReader"/> struct over a possibly segmented UTF-8 document.</summary>
	/// <param name="jsonUtf8">The UTF-8 document.</param>
	/// <param name="allowTrailingCommas">Whether trailing commas are accepted.</param>
	/// <param name="commentHandling">The policy for JSON comments.</param>
	/// <remarks>The input is borrowed for the lifetime of the reader. Only individual tokens that span segments may be copied.</remarks>
	public JsonReader(scoped in ReadOnlySequence<byte> jsonUtf8, bool allowTrailingCommas = false, JsonCommentHandling commentHandling = JsonCommentHandling.Disallow)
	{
		this.allowTrailingCommas = allowTrailingCommas;
		this.commentHandling = commentHandling;
		this.sequence = jsonUtf8;
		this.sequenceInput = true;
		this.utf8Json = new(jsonUtf8);

		this.position = 0;
		this.navigationDepth = 0;
	}

	/// <summary>Gets the original input sequence.</summary>
	/// <remarks>For span-based readers, the first access copies the input into a sequence, which is cached for later accesses.</remarks>
	public ReadOnlySequence<byte> Sequence
	{
		get
		{
			if (this.sequence is not { } result)
			{
				result = new(this.utf8Json.Slice(0, this.utf8Json.Length).ToArray());
				this.sequence = result;
			}

			return result;
		}
	}

	/// <summary>Gets the current position within <see cref="Sequence"/>.</summary>
	public SequencePosition Position => this.Sequence.GetPosition(this.position);

	/// <summary>Gets the number of bytes consumed.</summary>
	public readonly long Consumed => this.position;

	/// <summary>Gets a value indicating whether all input bytes have been consumed.</summary>
	public readonly bool End => this.position == this.utf8Json.Length;

	/// <summary>Gets the first byte of the next significant token, skipping whitespace and permitted comments.</summary>
	/// <exception cref="FormatException">There is no next token.</exception>
	public byte NextCode => (byte)this.PeekValueToken();

	/// <summary>Gets the kind indicated by the next value's first significant byte.</summary>
	/// <remarks>Inspection does not validate the whole value. Container terminators and separators are not values.</remarks>
	public JsonValueKind NextJsonType => this.NextCode switch
	{
		(byte)'n' => JsonValueKind.Null,
		(byte)'t' or (byte)'f' => JsonValueKind.Boolean,
		(byte)'"' => JsonValueKind.String,
		(byte)'[' => JsonValueKind.Array,
		(byte)'{' => JsonValueKind.Object,
		(byte)'-' or >= (byte)'0' and <= (byte)'9' => JsonValueKind.Number,
		_ => throw new FormatException("Expected a JSON value."),
	};

	/// <summary>Gets a value indicating whether the next significant byte starts a null literal.</summary>
	public bool IsNull => this.NextCode == (byte)'n';

	/// <summary>Gets the number of bytes consumed from the input buffer.</summary>
	internal readonly long BytesConsumed => this.position;

	/// <summary>Gets a value indicating whether the original input starts with a stream byte-order mark.</summary>
	internal readonly bool HasByteOrderMark => this.utf8Json.Matches(0, "\uFEFF"u8);

	/// <summary>Gets the net number of containers entered through structural reader methods.</summary>
	internal readonly int NavigationDepth => this.navigationDepth;

	/// <summary>
	/// Creates an independent copy of this reader at the current position.
	/// </summary>
	/// <returns>A copy of this reader positioned at the same location.</returns>
	/// <remarks>Advancing the returned reader does not affect this instance. Segmented readers copy only their small navigation state, never the input payload.</remarks>
	public readonly JsonReader CreatePeekReader()
	{
		JsonReader result = this;
		result.utf8Json = this.utf8Json.CreatePeekInput();
		return result;
	}

	/// <summary>Creates a reader over different input using this reader's syntax options.</summary>
	/// <param name="jsonUtf8">The new borrowed input.</param>
	/// <returns>A reader at the beginning of the new input, with no inherited navigation state.</returns>
	public readonly JsonReader Clone(scoped in ReadOnlySequence<byte> jsonUtf8) => new(jsonUtf8, this.allowTrailingCommas, this.commentHandling);

	/// <summary>Reads a required JSON null literal.</summary>
	/// <exception cref="FormatException">The next value is not null.</exception>
	public void ReadNull()
	{
		if (!this.TryReadNull())
		{
			throw new FormatException("Expected a JSON null literal.");
		}
	}

	/// <summary>
	/// Attempts to read a JSON <see langword="null"/> literal.
	/// </summary>
	/// <returns><see langword="true"/> if a <see langword="null"/> literal was consumed; otherwise, <see langword="false"/>.</returns>
	public bool TryReadNull()
	{
		this.SkipWhiteSpaceUtf8();
		if (this.utf8Json.Matches(this.position, "null"u8))
		{
			this.position += 4;
			this.ValidateScalarTerminator();
			return true;
		}

		return false;
	}

	/// <summary>
	/// Reads a required JSON boolean literal.
	/// </summary>
	/// <returns>The boolean value that was read.</returns>
	public bool ReadBoolean()
	{
		this.SkipWhiteSpaceUtf8();
		if (this.utf8Json.Matches(this.position, "true"u8))
		{
			this.position += 4;
			this.ValidateScalarTerminator();
			return true;
		}

		if (this.utf8Json.Matches(this.position, "false"u8))
		{
			this.position += 5;
			this.ValidateScalarTerminator();
			return false;
		}

		throw new FormatException("Expected a JSON boolean literal.");
	}

	/// <summary>
	/// Reads a JSON string value that may be <see langword="null"/>.
	/// </summary>
	/// <returns>The decoded string value, or <see langword="null"/> when the next token is a JSON <see langword="null"/> literal.</returns>
	public string? ReadString()
	{
		return this.ReadString(stringInterning: null);
	}

	/// <summary>
	/// Reads a required JSON string value.
	/// </summary>
	/// <returns>The decoded string value.</returns>
	public string ReadRequiredString() => this.ReadRequiredString(stringInterning: null);

	/// <summary>Attempts to read decoded UTF-8 string bytes without allocating.</summary>
	/// <param name="span">Receives a borrowed view of the string's content, without quotes.</param>
	/// <returns>Whether the next string's content is unescaped and contiguous. Null, escaped content, or content spanning segments returns false without advancing.</returns>
	/// <remarks>A successful span borrows the reader's input lifetime. Invalid JSON throws rather than returning false.</remarks>
	public bool TryReadStringSpan(out ReadOnlySpan<byte> span)
	{
		span = default;
		(ReadOnlyMemory<byte> Memory, SequencePosition Next, long Start) cursor = this.utf8Json.SaveCursor();
		JsonReader peek = this;
		if (peek.TryReadNull())
		{
			this.utf8Json.RestoreCursor(cursor);
			return false;
		}

		bool escaped = peek.ReadStringTokenUtf8(default, out long start, out long length);
		if (!escaped && this.utf8Json.TryGetSpan(start, length, out span))
		{
			this.position = peek.position;
			return true;
		}

		this.utf8Json.RestoreCursor(cursor);
		return false;
	}

	/// <summary>Reads decoded UTF-8 string bytes, without surrounding quotes.</summary>
	/// <returns>A borrowed span for contiguous unescaped input, otherwise a GC-owned token copy.</returns>
	/// <exception cref="FormatException">The next token is not a non-null JSON string.</exception>
	public ReadOnlySpan<byte> ReadStringSpan()
	{
		if (this.TryReadStringSpan(out ReadOnlySpan<byte> span))
		{
			return span;
		}

		ReadOnlySequence<byte> value = this.ReadStringSequence() ?? throw new FormatException("Expected a non-null JSON string.");
		return value.IsSingleSegment ? value.First.Span : value.ToArray();
	}

	/// <summary>Reads a nullable JSON string as decoded UTF-8 bytes, without quotes or JSON escapes.</summary>
	/// <returns>Null for a null literal, a borrowed slice for unescaped sequence input, or GC-owned decoded storage.</returns>
	/// <remarks>Escaped input is decoded, never returned as escaped text. Borrowed results must not outlive the input buffers.</remarks>
	public ReadOnlySequence<byte>? ReadStringSequence()
	{
		if (this.TryReadNull())
		{
			return null;
		}

		(ReadOnlyMemory<byte> Memory, SequencePosition Next, long Start) cursor = this.utf8Json.SaveCursor();
		JsonReader peek = this;
		bool escaped = peek.ReadStringTokenUtf8(default, out long start, out long length);
		if (escaped)
		{
			this.utf8Json.RestoreCursor(cursor);
			return this.ReadEscapedStringBytes(start, length);
		}

		this.position = peek.position;
		return this.CaptureRaw(start, length).Json;
	}

	/// <summary>
	/// Reads a JSON string that must contain exactly one character.
	/// </summary>
	/// <returns>The character encoded in the JSON string.</returns>
	public char ReadChar()
	{
		string value = this.ReadRequiredString();
		if (value.Length != 1)
		{
			throw new FormatException("Expected a JSON string with exactly one character.");
		}

		return value[0];
	}

	/// <summary>
	/// Reads the next JSON number token without converting it to a numeric type.
	/// </summary>
	/// <returns>The raw number token as text.</returns>
	public string ReadNumberToken() => Encoding.UTF8.GetString(this.ReadNumberTokenUtf8Core());

	/// <summary>
	/// Reads a base64-encoded JSON string value that may be <see langword="null"/>.
	/// </summary>
	/// <returns>The decoded bytes, or <see langword="null"/> when the next token is a JSON <see langword="null"/> literal.</returns>
	public byte[]? ReadBase64Bytes()
	{
		string? value = this.ReadString();
		return value is null ? null : Convert.FromBase64String(value);
	}

	/// <summary>
	/// Reads a required base64-encoded JSON string value.
	/// </summary>
	/// <returns>The decoded bytes.</returns>
	public byte[] ReadRequiredBase64Bytes() => Convert.FromBase64String(this.ReadRequiredString());

	/// <summary>
	/// Reads the opening token for a JSON object.
	/// </summary>
	public void ReadStartObject()
	{
		this.SkipWhiteSpaceUtf8();
		this.RequireCurrent((byte)'{');
		this.position++;
		this.navigationDepth++;
	}

	/// <summary>
	/// Attempts to read the closing token for a JSON object.
	/// </summary>
	/// <returns><see langword="true"/> if the object terminator was consumed; otherwise, <see langword="false"/>.</returns>
	public bool TryReadEndObject() => this.TryReadEndToken((byte)'}');

	/// <summary>
	/// Reads the name/value separator between a JSON property name and value.
	/// </summary>
	public void ReadNameSeparator()
	{
		this.SkipWhiteSpaceUtf8();
		this.RequireCurrent((byte)':');
		this.position++;
	}

	/// <summary>
	/// Reads the opening token for a JSON array.
	/// </summary>
	public void ReadStartArray()
	{
		this.SkipWhiteSpaceUtf8();
		this.RequireCurrent((byte)'[');
		this.position++;
		this.navigationDepth++;
	}

	/// <summary>
	/// Attempts to read the closing token for a JSON array.
	/// </summary>
	/// <returns><see langword="true"/> if the array terminator was consumed; otherwise, <see langword="false"/>.</returns>
	public bool TryReadEndArray() => this.TryReadEndToken((byte)']');

	/// <summary>
	/// Reads the closing token for a JSON array.
	/// </summary>
	public void ReadEndArray()
	{
		if (!this.TryReadEndArray())
		{
			throw new FormatException("Expected ']' in JSON input.");
		}
	}

	/// <summary>
	/// Reads the separator between consecutive JSON values.
	/// </summary>
	public void ReadValueSeparator()
	{
		this.SkipWhiteSpaceUtf8();
		this.RequireCurrent((byte)',');
		this.position++;
	}

	/// <summary>
	/// Verifies that the entire input has been consumed except for insignificant whitespace.
	/// </summary>
	public void EnsureFullyConsumed()
	{
		this.SkipWhiteSpaceUtf8();
		if (this.position != this.utf8Json.Length)
		{
			throw new FormatException("Unexpected trailing data after the JSON value.");
		}
	}

	/// <summary>
	/// Skips over the next JSON value.
	/// </summary>
	/// <param name="context">The active serialization context, including the remaining depth and cancellation token.</param>
	/// <remarks>Each array or object, including an empty one, consumes one depth step. The caller's context is not modified, so siblings retain the same depth budget.</remarks>
	/// <exception cref="InvalidOperationException">The value exceeds the remaining depth limit.</exception>
	/// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
	public void Skip(SerializationContext context)
	{
		context.CancellationToken.ThrowIfCancellationRequested();
		this.SkipWhiteSpaceUtf8(context.CancellationToken);
		if (this.position >= this.utf8Json.Length)
		{
			throw new FormatException("Expected a JSON value.");
		}

		switch (this.utf8Json[this.position])
		{
			case (byte)'{':
				context.DepthStep();
				this.SkipObjectUtf8(context);
				break;
			case (byte)'[':
				context.DepthStep();
				this.SkipArrayUtf8(context);
				break;
			case (byte)'"':
				this.SkipStringUtf8(context.CancellationToken);
				break;
			case (byte)'t':
			case (byte)'f':
				this.ReadBoolean();
				break;
			case (byte)'n':
				if (!this.TryReadNull())
				{
					throw new FormatException("Expected a JSON null literal.");
				}

				break;
			default:
				this.ReadNumberTokenUtf8Core(context.CancellationToken, materialize: false);
				break;
		}
	}

	/// <summary>
	/// Reads the next JSON value and returns its raw JSON text.
	/// </summary>
	/// <returns>The raw JSON representation of the next value.</returns>
	/// <remarks>Uses the default depth limit of 64. Converters should use <see cref="ReadRawValue(SerializationContext)"/> to preserve the active operation's limits.</remarks>
	public string ReadRawValue() => this.ReadRawValue(new SerializationContext());

	/// <summary>
	/// Reads the next JSON value and returns its raw JSON text using the active operation's limits.
	/// </summary>
	/// <param name="context">The active serialization context.</param>
	/// <returns>The raw JSON representation of the next value.</returns>
	public string ReadRawValue(SerializationContext context)
	{
		context.CancellationToken.ThrowIfCancellationRequested();
		this.SkipWhiteSpaceUtf8(context.CancellationToken);
		long utf8Start = this.position;
		this.Skip(context);
		return Encoding.UTF8.GetString(this.utf8Json.Slice(utf8Start, this.position - utf8Start));
	}

	/// <summary>Reads the next JSON value as exact UTF-8 bytes using the active operation's limits.</summary>
	/// <param name="context">The active depth and cancellation limits.</param>
	/// <returns>A borrowed sequence slice, or an owned copy when this reader was constructed from a span.</returns>
	/// <remarks>Borrowed results must not outlive the input buffers. Use <see cref="RawJson.ToOwned"/> to retain them.</remarks>
	public RawJson ReadRaw(SerializationContext context)
	{
		context.CancellationToken.ThrowIfCancellationRequested();
		this.SkipWhiteSpaceUtf8(context.CancellationToken);
		long start = this.position;
		this.Skip(context);
		return this.CaptureRaw(start, this.position - start);
	}

	/// <summary>Reads exactly the specified number of bytes as one complete JSON value.</summary>
	/// <param name="length">The byte length, including any surrounding whitespace.</param>
	/// <returns>The exact bytes, borrowed for sequence input and owned for span input.</returns>
	/// <remarks>Validates with the default depth limit. Use <see cref="ReadRaw(SerializationContext)"/> to preserve active operation limits.</remarks>
	/// <exception cref="ArgumentOutOfRangeException">The length is negative or exceeds the remaining input.</exception>
	/// <exception cref="FormatException">The slice does not contain exactly one JSON value.</exception>
	public RawJson ReadRaw(long length)
	{
		if (length < 0 || length > this.utf8Json.Length - this.position)
		{
			throw new ArgumentOutOfRangeException(nameof(length));
		}

		RawJson raw = this.CaptureRaw(this.position, length);
		JsonReader validation = new(raw.Json, this.allowTrailingCommas, this.commentHandling);
		if (validation.HasByteOrderMark)
		{
			throw new FormatException("A raw JSON value cannot include a stream byte-order mark.");
		}

		validation.Skip(new());
		validation.EnsureFullyConsumed();
		this.position += length;
		return raw;
	}

	/// <summary>Reads a JSON number as an unsigned 8-bit integer.</summary>
	/// <returns>The parsed value.</returns>
	public byte ReadByte()
	{
		ReadOnlySpan<byte> utf8Token = this.ReadNumberTokenUtf8Core();
		if (Utf8Parser.TryParse(utf8Token, out byte utf8Value, out int bytesConsumed) && bytesConsumed == utf8Token.Length)
		{
			return utf8Value;
		}

		return byte.Parse(Encoding.UTF8.GetString(utf8Token), NumberStyles.Integer, CultureInfo.InvariantCulture);
	}

	/// <summary>Reads a JSON number as a signed 8-bit integer.</summary>
	/// <returns>The parsed value.</returns>
	public sbyte ReadSByte()
	{
		ReadOnlySpan<byte> utf8Token = this.ReadNumberTokenUtf8Core();
		if (Utf8Parser.TryParse(utf8Token, out sbyte utf8Value, out int bytesConsumed) && bytesConsumed == utf8Token.Length)
		{
			return utf8Value;
		}

		return sbyte.Parse(Encoding.UTF8.GetString(utf8Token), NumberStyles.Integer, CultureInfo.InvariantCulture);
	}

	/// <summary>Reads a JSON number as a signed 16-bit integer.</summary>
	/// <returns>The parsed value.</returns>
	public short ReadInt16()
	{
		ReadOnlySpan<byte> utf8Token = this.ReadNumberTokenUtf8Core();
		if (Utf8Parser.TryParse(utf8Token, out short utf8Value, out int bytesConsumed) && bytesConsumed == utf8Token.Length)
		{
			return utf8Value;
		}

		return short.Parse(Encoding.UTF8.GetString(utf8Token), NumberStyles.Integer, CultureInfo.InvariantCulture);
	}

	/// <summary>Reads a JSON number as an unsigned 16-bit integer.</summary>
	/// <returns>The parsed value.</returns>
	public ushort ReadUInt16()
	{
		ReadOnlySpan<byte> utf8Token = this.ReadNumberTokenUtf8Core();
		if (Utf8Parser.TryParse(utf8Token, out ushort utf8Value, out int bytesConsumed) && bytesConsumed == utf8Token.Length)
		{
			return utf8Value;
		}

		return ushort.Parse(Encoding.UTF8.GetString(utf8Token), NumberStyles.Integer, CultureInfo.InvariantCulture);
	}

	/// <summary>Reads a JSON number as a signed 32-bit integer.</summary>
	/// <returns>The parsed value.</returns>
	public int ReadInt32()
	{
		ReadOnlySpan<byte> utf8Token = this.ReadNumberTokenUtf8Core();
		if (Utf8Parser.TryParse(utf8Token, out int utf8Value, out int bytesConsumed) && bytesConsumed == utf8Token.Length)
		{
			return utf8Value;
		}

		return int.Parse(Encoding.UTF8.GetString(utf8Token), NumberStyles.Integer, CultureInfo.InvariantCulture);
	}

	/// <summary>Reads a JSON number as an unsigned 32-bit integer.</summary>
	/// <returns>The parsed value.</returns>
	public uint ReadUInt32()
	{
		ReadOnlySpan<byte> utf8Token = this.ReadNumberTokenUtf8Core();
		if (Utf8Parser.TryParse(utf8Token, out uint utf8Value, out int bytesConsumed) && bytesConsumed == utf8Token.Length)
		{
			return utf8Value;
		}

		return uint.Parse(Encoding.UTF8.GetString(utf8Token), NumberStyles.Integer, CultureInfo.InvariantCulture);
	}

	/// <summary>Reads a JSON number as a signed 64-bit integer.</summary>
	/// <returns>The parsed value.</returns>
	public long ReadInt64()
	{
		ReadOnlySpan<byte> utf8Token = this.ReadNumberTokenUtf8Core();
		if (Utf8Parser.TryParse(utf8Token, out long utf8Value, out int bytesConsumed) && bytesConsumed == utf8Token.Length)
		{
			return utf8Value;
		}

		return long.Parse(Encoding.UTF8.GetString(utf8Token), NumberStyles.Integer, CultureInfo.InvariantCulture);
	}

	/// <summary>Reads a JSON number as an unsigned 64-bit integer.</summary>
	/// <returns>The parsed value.</returns>
	public ulong ReadUInt64()
	{
		ReadOnlySpan<byte> utf8Token = this.ReadNumberTokenUtf8Core();
		if (Utf8Parser.TryParse(utf8Token, out ulong utf8Value, out int bytesConsumed) && bytesConsumed == utf8Token.Length)
		{
			return utf8Value;
		}

		return ulong.Parse(Encoding.UTF8.GetString(utf8Token), NumberStyles.Integer, CultureInfo.InvariantCulture);
	}

	/// <summary>Reads a JSON number as a finite single-precision floating-point value.</summary>
	/// <returns>The parsed value.</returns>
	public float ReadSingle()
	{
		ReadOnlySpan<byte> utf8Token = this.ReadNumberTokenUtf8Core();
		if (Utf8Parser.TryParse(utf8Token, out float utf8Value, out int bytesConsumed) && bytesConsumed == utf8Token.Length)
		{
			return !float.IsInfinity(utf8Value) ? utf8Value : throw new OverflowException("The JSON number exceeds the range of Single.");
		}

		return float.Parse(Encoding.UTF8.GetString(utf8Token), NumberStyles.Float, CultureInfo.InvariantCulture);
	}

	/// <summary>Reads a JSON number as a finite double-precision floating-point value.</summary>
	/// <returns>The parsed value.</returns>
	public double ReadDouble()
	{
		ReadOnlySpan<byte> utf8Token = this.ReadNumberTokenUtf8Core();
		if (Utf8Parser.TryParse(utf8Token, out double utf8Value, out int bytesConsumed) && bytesConsumed == utf8Token.Length)
		{
			return !double.IsInfinity(utf8Value) ? utf8Value : throw new OverflowException("The JSON number exceeds the range of Double.");
		}

		return double.Parse(Encoding.UTF8.GetString(utf8Token), NumberStyles.Float, CultureInfo.InvariantCulture);
	}

	/// <summary>Reads a JSON number as a decimal value.</summary>
	/// <returns>The parsed value.</returns>
	public decimal ReadDecimal()
	{
		ReadOnlySpan<byte> utf8Token = this.ReadNumberTokenUtf8Core();
		if (Utf8Parser.TryParse(utf8Token, out decimal utf8Value, out int bytesConsumed) && bytesConsumed == utf8Token.Length)
		{
			return utf8Value;
		}

		return decimal.Parse(Encoding.UTF8.GetString(utf8Token), NumberStyles.Float, CultureInfo.InvariantCulture);
	}

	internal string? ReadString(StringInterning? stringInterning)
	{
		if (this.TryReadNull())
		{
			return null;
		}

		return this.ReadRequiredString(stringInterning);
	}

	internal string ReadRequiredString(StringInterning? stringInterning) => this.ReadRequiredUtf8String(stringInterning);

	/// <summary>Determines whether the next significant token opens a JSON array.</summary>
	/// <returns>Whether the next token is an array start token.</returns>
	internal bool IsNextTokenStartArray()
	{
		this.SkipWhiteSpaceUtf8();
		return this.position < this.utf8Json.Length && this.utf8Json[this.position] == (byte)'[';
	}

	/// <summary>Determines whether the next significant token is a JSON null literal.</summary>
	/// <returns>Whether the next token is null.</returns>
	internal bool IsNextTokenNull()
	{
		this.SkipWhiteSpaceUtf8();
		return this.utf8Json.Matches(this.position, "null"u8);
	}

	internal char PeekValueToken()
	{
		this.SkipWhiteSpaceUtf8();
		if (this.position >= this.utf8Json.Length)
		{
			throw new FormatException("Expected a JSON value.");
		}

		return (char)this.utf8Json[this.position];
	}

	internal bool TryReadUnescapedUtf8StringToken(out ReadOnlySpan<byte> token)
	{
		token = default;
		(ReadOnlyMemory<byte> Memory, SequencePosition Next, long Start) cursor = this.utf8Json.SaveCursor();
		long index = this.SkipInsignificantCharactersUtf8(this.position);
		if (index >= this.utf8Json.Length || this.utf8Json[index] != (byte)'"')
		{
			throw new FormatException("Expected a JSON string.");
		}

		long start = index;
		index = this.utf8Json.IndexOfDelimiter(index + 1);
		if (index >= 0)
		{
			if (this.utf8Json[index] == (byte)'"')
			{
				token = this.utf8Json.Slice(start, index + 1 - start);
				ValidateStringBytes(token[1..^1]);
				this.position = index + 1;
				return true;
			}

			this.utf8Json.RestoreCursor(cursor);
			return false;
		}

		throw new FormatException("Unterminated JSON string.");
	}

	private static int HexToInt(byte value)
		=> value switch
		{
			>= (byte)'0' and <= (byte)'9' => value - (byte)'0',
			>= (byte)'A' and <= (byte)'F' => value - (byte)'A' + 10,
			>= (byte)'a' and <= (byte)'f' => value - (byte)'a' + 10,
			_ => throw new FormatException("Invalid hex digit in JSON unicode escape sequence."),
		};

	private static void ValidateStringBytes(ReadOnlySpan<byte> bytes)
	{
#if NET8_0_OR_GREATER
		if (bytes.IndexOfAnyInRange((byte)0, (byte)0x1F) >= 0)
		{
			throw new FormatException("Unescaped control character in JSON string.");
		}
#else
		for (int index = 0; index < bytes.Length; index++)
		{
			if (bytes[index] < 0x20)
			{
				throw new FormatException("Unescaped control character in JSON string.");
			}
		}
#endif
	}

	private ReadOnlySequence<byte> ReadEscapedStringBytes(long start, long length)
	{
		byte[] decoded = new byte[checked((int)length)];
		int written = 0;
		Span<char> escape = stackalloc char[2];
		this.position = start;
		while (true)
		{
			long delimiter = this.utf8Json.IndexOfDelimiter(this.position);
			ReadOnlySpan<byte> run = this.utf8Json.Slice(this.position, delimiter - this.position);
			run.CopyTo(decoded.AsSpan(written));
			written += run.Length;
			this.position = delimiter + 1;
			if (this.utf8Json[delimiter] == (byte)'"')
			{
				return new(decoded.AsMemory(0, written));
			}

			escape[0] = (char)this.ReadEscapeSequenceUtf8();
			int count = 1;
			if (char.IsHighSurrogate(escape[0]) && this.utf8Json.Matches(this.position, "\\u"u8))
			{
				JsonReader peek = this;
				peek.position += 2;
				char low = (char)peek.ReadHexQuadUtf8();
				if (char.IsLowSurrogate(low))
				{
					escape[count++] = low;
					this.position = peek.position;
				}
			}

			written += Encoding.UTF8.GetBytes(escape[..count], decoded.AsSpan(written));
		}
	}

	private string ReadRequiredUtf8String(StringInterning? stringInterning)
	{
		this.SkipWhiteSpaceUtf8();
		this.RequireCurrent((byte)'"');
		this.position++;
		long segmentStart = this.position;
		while (this.position < this.utf8Json.Length)
		{
			byte ch = this.utf8Json[this.position++];
			if (ch == (byte)'"')
			{
				ReadOnlySpan<byte> rawValue = this.utf8Json.Slice(segmentStart, this.position - 1 - segmentStart);
				return stringInterning?.GetOrAddUtf8(rawValue) ?? Encoding.UTF8.GetString(rawValue);
			}

			if (ch == (byte)'\\')
			{
				return this.ReadEscapedUtf8String(segmentStart, stringInterning);
			}

			if (ch < 0x20)
			{
				throw new FormatException("Unescaped control character in JSON string.");
			}
		}

		throw new FormatException("Unterminated JSON string.");
	}

	private string ReadEscapedUtf8String(long segmentStart, StringInterning? stringInterning)
	{
		const int MaxStackStringByteLength = 4096;
		(ReadOnlyMemory<byte> Memory, SequencePosition Next, long Start) cursor = this.utf8Json.SaveCursor();

		// The token's UTF-8 byte length bounds its decoded UTF-16 length. Do not size the buffer to the
		// remainder of the document, which may be much larger than this string.
		long tokenEnd = this.position + 1;
		while (tokenEnd < this.utf8Json.Length)
		{
			long delimiter = this.utf8Json.IndexOfDelimiter(tokenEnd);
			if (delimiter < 0)
			{
				throw new FormatException("Unterminated JSON string.");
			}

			tokenEnd = delimiter;
			if (this.utf8Json[tokenEnd] == (byte)'"')
			{
				break;
			}

			tokenEnd += 2;
		}

		if (tokenEnd >= this.utf8Json.Length)
		{
			throw new FormatException("Unterminated JSON string.");
		}

		this.utf8Json.RestoreCursor(cursor);
		int byteLength = checked((int)(tokenEnd - segmentStart));
		char[]? rented = byteLength > MaxStackStringByteLength ? ArrayPool<char>.Shared.Rent(byteLength) : null;
		try
		{
			Span<char> characters = rented ?? stackalloc char[byteLength];
			int written = 0;
			while (true)
			{
				ReadOnlySpan<byte> bytes = this.utf8Json.Slice(segmentStart, this.position - 1 - segmentStart);
				ValidateStringBytes(bytes);
				written += Encoding.UTF8.GetChars(bytes, characters[written..]);
				if (this.utf8Json[this.position - 1] == (byte)'"')
				{
					ReadOnlySpan<char> value = characters[..written];
					return stringInterning?.Intern(value) ?? value.ToString();
				}

				characters[written++] = (char)this.ReadEscapeSequenceUtf8();

				segmentStart = this.position;
				long delimiter = this.utf8Json.IndexOfDelimiter(segmentStart);
				if (delimiter < 0)
				{
					throw new FormatException("Unterminated JSON string.");
				}

				this.position = delimiter + 1;
			}
		}
		finally
		{
			if (rented is not null)
			{
				ArrayPool<char>.Shared.Return(rented);
			}
		}
	}

	private ReadOnlySpan<byte> ReadNumberTokenUtf8Core(CancellationToken cancellationToken = default, bool materialize = true)
	{
		this.SkipWhiteSpaceUtf8(cancellationToken);
		long start = this.position;
		if (JsonPrimitives.TryReadNumberToken(in this.utf8Json, start, isFinalBlock: true, out long length, cancellationToken) != JsonPrimitives.DecodeResult.Success)
		{
			throw new FormatException("Expected a complete, valid JSON number.");
		}

		this.position += length;
		return materialize ? this.utf8Json.Slice(start, length) : default;
	}

	private readonly void ValidateScalarTerminator()
	{
		if (this.position < this.utf8Json.Length && !JsonStructureScanner.IsScalarTerminator(this.utf8Json[this.position]))
		{
			throw new FormatException("Invalid character after a JSON scalar value.");
		}
	}

	private void SkipWhiteSpaceUtf8(CancellationToken cancellationToken = default)
	{
		this.position = this.SkipInsignificantCharactersUtf8(this.position, cancellationToken);
	}

	private long SkipInsignificantCharactersUtf8(long index, CancellationToken cancellationToken = default)
	{
		while (index < this.utf8Json.Length)
		{
			if ((index & 1023) == 0)
			{
				cancellationToken.ThrowIfCancellationRequested();
			}

			if (index == 0
				&& this.utf8Json.Length >= 3
				&& this.utf8Json[0] == 0xEF
				&& this.utf8Json[1] == 0xBB
				&& this.utf8Json[2] == 0xBF)
			{
				index = 3;
				continue;
			}

			byte ch = this.utf8Json[index];
			if (ch is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
			{
				index++;
				continue;
			}

			if (this.commentHandling == JsonCommentHandling.Skip && ch == (byte)'/' && index + 1 < this.utf8Json.Length)
			{
				byte next = this.utf8Json[index + 1];
				if (next == (byte)'/')
				{
					index += 2;
					while (index < this.utf8Json.Length && this.utf8Json[index] is not (byte)'\r' and not (byte)'\n')
					{
						if ((index & 1023) == 0)
						{
							cancellationToken.ThrowIfCancellationRequested();
						}

						index++;
					}

					continue;
				}

				if (next == (byte)'*')
				{
					index += 2;
					while (index + 1 < this.utf8Json.Length && !(this.utf8Json[index] == (byte)'*' && this.utf8Json[index + 1] == (byte)'/'))
					{
						if ((index & 1023) == 0)
						{
							cancellationToken.ThrowIfCancellationRequested();
						}

						index++;
					}

					if (index + 1 >= this.utf8Json.Length)
					{
						throw new FormatException("Unterminated JSON block comment.");
					}

					index += 2;
					continue;
				}
			}

			break;
		}

		return index;
	}

	private bool TryReadEndToken(byte endToken, CancellationToken cancellationToken = default)
	{
		long index = this.SkipInsignificantCharactersUtf8(this.position, cancellationToken);
		if (index < this.utf8Json.Length && this.utf8Json[index] == endToken)
		{
			this.position = index + 1;
			this.navigationDepth--;
			return true;
		}

		if (this.allowTrailingCommas && index < this.utf8Json.Length && this.utf8Json[index] == (byte)',')
		{
			long afterComma = this.SkipInsignificantCharactersUtf8(index + 1, cancellationToken);
			if (afterComma < this.utf8Json.Length && this.utf8Json[afterComma] == endToken)
			{
				this.position = afterComma + 1;
				this.navigationDepth--;
				return true;
			}
		}

		return false;
	}

	private int ReadEscapeSequenceUtf8()
	{
		if (this.position >= this.utf8Json.Length)
		{
			throw new FormatException("Unterminated JSON escape sequence.");
		}

		byte ch = this.utf8Json[this.position++];
		return ch switch
		{
			(byte)'"' => '"',
			(byte)'\\' => '\\',
			(byte)'/' => '/',
			(byte)'b' => '\b',
			(byte)'f' => '\f',
			(byte)'n' => '\n',
			(byte)'r' => '\r',
			(byte)'t' => '\t',
			(byte)'u' => this.ReadHexQuadUtf8(),
			_ => throw new FormatException($"Unsupported JSON escape sequence '\\{(char)ch}'."),
		};
	}

	private int ReadHexQuadUtf8()
	{
		if (this.position + 4 > this.utf8Json.Length)
		{
			throw new FormatException("Incomplete JSON unicode escape sequence.");
		}

		int value = 0;
		for (int i = 0; i < 4; i++)
		{
			value = (value << 4) | HexToInt(this.utf8Json[this.position++]);
		}

		return value;
	}

	private void SkipArrayUtf8(SerializationContext context)
	{
		this.ReadStartArray();
		if (this.TryReadEndToken((byte)']', context.CancellationToken))
		{
			return;
		}

		while (true)
		{
			this.Skip(context);
			if (this.TryReadEndToken((byte)']', context.CancellationToken))
			{
				return;
			}

			this.SkipWhiteSpaceUtf8(context.CancellationToken);
			this.ReadValueSeparator();
		}
	}

	private void SkipObjectUtf8(SerializationContext context)
	{
		this.ReadStartObject();
		if (this.TryReadEndToken((byte)'}', context.CancellationToken))
		{
			return;
		}

		while (true)
		{
			this.SkipStringUtf8(context.CancellationToken);
			this.SkipWhiteSpaceUtf8(context.CancellationToken);
			this.ReadNameSeparator();
			this.Skip(context);
			if (this.TryReadEndToken((byte)'}', context.CancellationToken))
			{
				return;
			}

			this.SkipWhiteSpaceUtf8(context.CancellationToken);
			this.ReadValueSeparator();
		}
	}

	private void SkipStringUtf8(CancellationToken cancellationToken)
		=> this.ReadStringTokenUtf8(cancellationToken, out _, out _);

	private bool ReadStringTokenUtf8(CancellationToken cancellationToken, out long start, out long length)
	{
		this.SkipWhiteSpaceUtf8(cancellationToken);
		this.RequireCurrent((byte)'"');
		this.position++;
		start = this.position;
		bool escaped = false;
		while (this.position < this.utf8Json.Length)
		{
			if ((this.position & 1023) <= 1)
			{
				cancellationToken.ThrowIfCancellationRequested();
			}

			byte ch = this.utf8Json[this.position++];
			if (ch == (byte)'"')
			{
				length = this.position - 1 - start;
				return escaped;
			}

			if (ch == (byte)'\\')
			{
				escaped = true;
				this.ReadEscapeSequenceUtf8();
			}
			else if (ch < 0x20)
			{
				throw new FormatException("Unescaped control character in JSON string.");
			}
		}

		throw new FormatException("Unterminated JSON string.");
	}

	private void RequireCurrent(byte expected)
	{
		if (this.position >= this.utf8Json.Length || this.utf8Json[this.position] != expected)
		{
			throw new FormatException($"Expected '{(char)expected}' in JSON input.");
		}
	}

	private readonly RawJson CaptureRaw(long start, long length)
	{
		return this.sequenceInput
			? new(this.utf8Json.SliceSequence(start, length) ?? this.sequence!.Value.Slice(start, length))
			: new(new ReadOnlySequence<byte>(this.utf8Json.Slice(start, length).ToArray())) { IsOwned = true };
	}
}
