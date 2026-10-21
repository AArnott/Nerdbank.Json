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
public ref struct JsonReader
{
	private readonly bool allowTrailingCommas;
	private readonly JsonCommentHandling commentHandling;
	private readonly bool sequenceInput;
	private readonly JsonInput utf8Json;
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

	/// <summary>Gets the net number of containers entered through structural reader methods.</summary>
	internal readonly int NavigationDepth => this.navigationDepth;

	/// <summary>
	/// Creates an independent copy of this reader at the current position.
	/// </summary>
	/// <returns>A copy of this reader positioned at the same location.</returns>
	/// <remarks>Advancing the returned reader does not affect this instance.</remarks>
	public readonly JsonReader CreatePeekReader() => this;

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
		if (this.utf8Json.Length - this.position >= 4 && this.utf8Json.Slice(this.position, 4).SequenceEqual("null"u8))
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
		if (this.utf8Json.Length - this.position >= 4 && this.utf8Json.Slice(this.position, 4).SequenceEqual("true"u8))
		{
			this.position += 4;
			this.ValidateScalarTerminator();
			return true;
		}

		if (this.utf8Json.Length - this.position >= 5 && this.utf8Json.Slice(this.position, 5).SequenceEqual("false"u8))
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
	/// <returns>Whether the next string is unescaped and contiguous. Null, escaped, or segmented strings return false without advancing.</returns>
	/// <remarks>A successful span borrows the reader's input lifetime. Invalid JSON throws rather than returning false.</remarks>
	public bool TryReadStringSpan(out ReadOnlySpan<byte> span)
	{
		span = default;
		JsonReader peek = this;
		if (peek.TryReadNull())
		{
			return false;
		}

		bool escaped = peek.ReadStringTokenUtf8(default, out long start, out long length);
		if (!escaped && this.utf8Json.TryGetSpan(start, length, out span))
		{
			this.position = peek.position;
			return true;
		}

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

		JsonReader peek = this;
		bool escaped = peek.ReadStringTokenUtf8(default, out long start, out long length);
		if (escaped)
		{
			return new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(this.ReadRequiredString()));
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

	/// <summary>Validates decoded UTF-8 string content before writing it.</summary>
	/// <param name="bytes">The decoded string bytes, which may include control characters.</param>
	internal static void ValidateUtf8String(ReadOnlySpan<byte> bytes) => ValidateStringBytes(bytes, allowControlCharacters: true);

	/// <summary>Validates decoded, possibly segmented UTF-8 string content before writing it.</summary>
	/// <param name="bytes">The decoded string bytes, which may include control characters.</param>
	internal static void ValidateUtf8String(scoped in ReadOnlySequence<byte> bytes)
	{
		JsonReader validator = new(bytes);
		while (!validator.End)
		{
			validator.ValidateStringByte(validator.utf8Json[validator.position++], allowControlCharacters: true);
		}
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
		return this.utf8Json.Length - this.position >= 4 && this.utf8Json.Slice(this.position, 4).SequenceEqual("null"u8);
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

	private static void ValidateStringBytes(ReadOnlySpan<byte> bytes, bool allowControlCharacters = false)
	{
		JsonInput input = new(bytes);
		long index = 0;
		while (index < input.Length)
		{
			ValidateStringByte(in input, ref index, input[index++], allowControlCharacters);
		}
	}

	private static void ValidateStringByte(scoped in JsonInput input, ref long position, byte first, bool allowControlCharacters)
	{
		if (first < 0x20 && !allowControlCharacters)
		{
			throw new FormatException("Unescaped control character in JSON string.");
		}

		if (first < 0x80)
		{
			return;
		}

		int continuation = first switch
		{
			>= 0xC2 and <= 0xDF => 1,
			>= 0xE0 and <= 0xEF => 2,
			>= 0xF0 and <= 0xF4 => 3,
			_ => throw new FormatException("Invalid UTF-8 in JSON string."),
		};
		for (int i = 0; i < continuation; i++)
		{
			if (position >= input.Length)
			{
				throw new FormatException("Incomplete UTF-8 in JSON string.");
			}

			byte next = input[position++];
			if (next is < 0x80 or > 0xBF
				|| (i == 0 && ((first == 0xE0 && next < 0xA0) || (first == 0xED && next >= 0xA0)
					|| (first == 0xF0 && next < 0x90) || (first == 0xF4 && next >= 0x90))))
			{
				throw new FormatException("Invalid UTF-8 in JSON string.");
			}
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

			this.ValidateStringByte(ch);
		}

		throw new FormatException("Unterminated JSON string.");
	}

	private string ReadEscapedUtf8String(long segmentStart, StringInterning? stringInterning)
	{
		const int MaxStackStringByteLength = 4096;

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

				int scalar = this.ReadEscapeSequenceUtf8();
				if (scalar <= char.MaxValue)
				{
					characters[written++] = (char)scalar;
				}
				else
				{
					scalar -= 0x10000;
					characters[written++] = (char)(0xD800 + (scalar >> 10));
					characters[written++] = (char)(0xDC00 + (scalar & 0x3FF));
				}

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

	private bool TryConsume(byte expected)
	{
		if (this.position < this.utf8Json.Length && this.utf8Json[this.position] == expected)
		{
			this.position++;
			return true;
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
			(byte)'u' => this.ReadUnicodeScalarUtf8(),
			_ => throw new FormatException($"Unsupported JSON escape sequence '\\{(char)ch}'."),
		};
	}

	private int ReadUnicodeScalarUtf8()
	{
		int value = this.ReadHexQuadUtf8();
		if (value is >= 0xD800 and <= 0xDBFF)
		{
			if (!this.TryConsume((byte)'\\') || !this.TryConsume((byte)'u'))
			{
				throw new FormatException("A high surrogate must be followed by a low surrogate escape.");
			}

			int low = this.ReadHexQuadUtf8();
			if (low is < 0xDC00 or > 0xDFFF)
			{
				throw new FormatException("Invalid low surrogate escape.");
			}

			return 0x10000 + ((value - 0xD800) << 10) + low - 0xDC00;
		}

		return value is >= 0xDC00 and <= 0xDFFF ? throw new FormatException("Unpaired low surrogate escape.") : value;
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
			else
			{
				this.ValidateStringByte(ch);
			}
		}

		throw new FormatException("Unterminated JSON string.");
	}

	private void ValidateStringByte(byte first, bool allowControlCharacters = false)
		=> ValidateStringByte(in this.utf8Json, ref this.position, first, allowControlCharacters);

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
			? new(this.sequence!.Value.Slice(start, length))
			: new(new ReadOnlySequence<byte>(this.utf8Json.Slice(start, length).ToArray())) { IsOwned = true };
	}
}
