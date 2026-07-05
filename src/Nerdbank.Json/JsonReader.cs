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
	private readonly byte[]? utf8Buffer;
	private readonly ReadOnlySpan<byte> utf8Json;
	private int position;

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
		this.utf8Buffer = null;
		this.utf8Json = jsonUtf8;
		this.position = 0;
	}

	internal JsonReader(scoped in ReadOnlySequence<byte> jsonUtf8, bool allowTrailingCommas = false, JsonCommentHandling commentHandling = JsonCommentHandling.Disallow)
	{
		this.allowTrailingCommas = allowTrailingCommas;
		this.commentHandling = commentHandling;
		if (jsonUtf8.IsSingleSegment)
		{
			this.utf8Buffer = null;
			this.utf8Json = jsonUtf8.First.Span;
		}
		else
		{
			this.utf8Buffer = jsonUtf8.ToArray();
			this.utf8Json = this.utf8Buffer;
		}

		this.position = 0;
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
			return true;
		}

		if (this.utf8Json.Length - this.position >= 5 && this.utf8Json.Slice(this.position, 5).SequenceEqual("false"u8))
		{
			this.position += 5;
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
		if (this.TryReadNull())
		{
			return null;
		}

		return this.ReadRequiredString();
	}

	/// <summary>
	/// Reads a required JSON string value.
	/// </summary>
	/// <returns>The decoded string value.</returns>
	public string ReadRequiredString() => this.ReadRequiredUtf8String();

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
	public void SkipValue()
	{
		this.SkipWhiteSpaceUtf8();
		if (this.position >= this.utf8Json.Length)
		{
			throw new FormatException("Expected a JSON value.");
		}

		switch (this.utf8Json[this.position])
		{
			case (byte)'{':
				this.SkipObjectUtf8();
				break;
			case (byte)'[':
				this.SkipArrayUtf8();
				break;
			case (byte)'"':
				this.SkipStringUtf8();
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
				this.ReadNumberTokenUtf8Core();
				break;
		}
	}

	/// <summary>
	/// Reads the next JSON value and returns its raw JSON text.
	/// </summary>
	/// <returns>The raw JSON representation of the next value.</returns>
	public string ReadRawValue()
	{
		this.SkipWhiteSpaceUtf8();
		int utf8Start = this.position;
		this.SkipValue();
		return Encoding.UTF8.GetString(this.utf8Json[utf8Start..this.position]);
	}

	internal byte ReadByteValue()
		=> this.TryReadUtf8NumberToken(out ReadOnlySpan<byte> utf8Token)
			&& Utf8Parser.TryParse(utf8Token, out byte utf8Value, out int bytesConsumed)
			&& bytesConsumed == utf8Token.Length
			? utf8Value
			: byte.Parse(this.ReadNumberToken(), NumberStyles.Integer, CultureInfo.InvariantCulture);

	internal sbyte ReadSByteValue()
		=> this.TryReadUtf8NumberToken(out ReadOnlySpan<byte> utf8Token)
			&& Utf8Parser.TryParse(utf8Token, out sbyte utf8Value, out int bytesConsumed)
			&& bytesConsumed == utf8Token.Length
			? utf8Value
			: sbyte.Parse(this.ReadNumberToken(), NumberStyles.Integer, CultureInfo.InvariantCulture);

	internal short ReadInt16Value()
		=> this.TryReadUtf8NumberToken(out ReadOnlySpan<byte> utf8Token)
			&& Utf8Parser.TryParse(utf8Token, out short utf8Value, out int bytesConsumed)
			&& bytesConsumed == utf8Token.Length
			? utf8Value
			: short.Parse(this.ReadNumberToken(), NumberStyles.Integer, CultureInfo.InvariantCulture);

	internal ushort ReadUInt16Value()
		=> this.TryReadUtf8NumberToken(out ReadOnlySpan<byte> utf8Token)
			&& Utf8Parser.TryParse(utf8Token, out ushort utf8Value, out int bytesConsumed)
			&& bytesConsumed == utf8Token.Length
			? utf8Value
			: ushort.Parse(this.ReadNumberToken(), NumberStyles.Integer, CultureInfo.InvariantCulture);

	internal int ReadInt32Value()
		=> this.TryReadUtf8NumberToken(out ReadOnlySpan<byte> utf8Token)
			&& Utf8Parser.TryParse(utf8Token, out int utf8Value, out int bytesConsumed)
			&& bytesConsumed == utf8Token.Length
			? utf8Value
			: int.Parse(this.ReadNumberToken(), NumberStyles.Integer, CultureInfo.InvariantCulture);

	internal uint ReadUInt32Value()
		=> this.TryReadUtf8NumberToken(out ReadOnlySpan<byte> utf8Token)
			&& Utf8Parser.TryParse(utf8Token, out uint utf8Value, out int bytesConsumed)
			&& bytesConsumed == utf8Token.Length
			? utf8Value
			: uint.Parse(this.ReadNumberToken(), NumberStyles.Integer, CultureInfo.InvariantCulture);

	internal long ReadInt64Value()
		=> this.TryReadUtf8NumberToken(out ReadOnlySpan<byte> utf8Token)
			&& Utf8Parser.TryParse(utf8Token, out long utf8Value, out int bytesConsumed)
			&& bytesConsumed == utf8Token.Length
			? utf8Value
			: long.Parse(this.ReadNumberToken(), NumberStyles.Integer, CultureInfo.InvariantCulture);

	internal ulong ReadUInt64Value()
		=> this.TryReadUtf8NumberToken(out ReadOnlySpan<byte> utf8Token)
			&& Utf8Parser.TryParse(utf8Token, out ulong utf8Value, out int bytesConsumed)
			&& bytesConsumed == utf8Token.Length
			? utf8Value
			: ulong.Parse(this.ReadNumberToken(), NumberStyles.Integer, CultureInfo.InvariantCulture);

	internal float ReadSingleValue()
		=> this.TryReadUtf8NumberToken(out ReadOnlySpan<byte> utf8Token)
			&& Utf8Parser.TryParse(utf8Token, out float utf8Value, out int bytesConsumed)
			&& bytesConsumed == utf8Token.Length
			? utf8Value
			: float.Parse(this.ReadNumberToken(), NumberStyles.Float, CultureInfo.InvariantCulture);

	internal double ReadDoubleValue()
		=> this.TryReadUtf8NumberToken(out ReadOnlySpan<byte> utf8Token)
			&& Utf8Parser.TryParse(utf8Token, out double utf8Value, out int bytesConsumed)
			&& bytesConsumed == utf8Token.Length
			? utf8Value
			: double.Parse(this.ReadNumberToken(), NumberStyles.Float, CultureInfo.InvariantCulture);

	internal decimal ReadDecimalValue()
		=> this.TryReadUtf8NumberToken(out ReadOnlySpan<byte> utf8Token)
			&& Utf8Parser.TryParse(utf8Token, out decimal utf8Value, out int bytesConsumed)
			&& bytesConsumed == utf8Token.Length
			? utf8Value
			: decimal.Parse(this.ReadNumberToken(), NumberStyles.Float, CultureInfo.InvariantCulture);

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
		int index = this.SkipInsignificantCharactersUtf8(this.position);
		if (index >= this.utf8Json.Length || this.utf8Json[index] != (byte)'"')
		{
			throw new FormatException("Expected a JSON string.");
		}

		int start = index;
		ReadOnlySpan<byte> remaining = this.utf8Json[(index + 1)..];
		int relativeEnd = remaining.IndexOfAny((byte)'"', (byte)'\\');
		if (relativeEnd >= 0)
		{
			index += relativeEnd + 1;
			if (this.utf8Json[index] == (byte)'"')
			{
				token = this.utf8Json[start..(index + 1)];
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

	private bool TryReadUtf8NumberToken(out ReadOnlySpan<byte> token)
	{
		token = this.ReadNumberTokenUtf8Core();
		return true;
	}

	private string ReadRequiredUtf8String()
	{
		this.SkipWhiteSpaceUtf8();
		this.RequireCurrent((byte)'"');
		this.position++;
		int segmentStart = this.position;
		StringBuilder? builder = null;
		while (this.position < this.utf8Json.Length)
		{
			byte ch = this.utf8Json[this.position++];
			if (ch == (byte)'"')
			{
				if (builder is null)
				{
					return Encoding.UTF8.GetString(this.utf8Json[segmentStart..(this.position - 1)]);
				}

				builder.Append(Encoding.UTF8.GetString(this.utf8Json[segmentStart..(this.position - 1)]));
				return builder.ToString();
			}

			if (ch == (byte)'\\')
			{
				builder ??= new StringBuilder();
				builder.Append(Encoding.UTF8.GetString(this.utf8Json[segmentStart..(this.position - 1)]));
				builder.Append(this.ReadEscapeSequenceUtf8());
				segmentStart = this.position;
			}
		}

		throw new FormatException("Unterminated JSON string.");
	}

	private ReadOnlySpan<byte> ReadNumberTokenUtf8Core()
	{
		this.SkipWhiteSpaceUtf8();
		int start = this.position;
		if (this.TryConsume((byte)'-'))
		{
		}

		this.ReadDigitsUtf8(requireAtLeastOne: true);
		if (this.TryConsume((byte)'.'))
		{
			this.ReadDigitsUtf8(requireAtLeastOne: true);
		}

		if (this.TryConsume((byte)'e') || this.TryConsume((byte)'E'))
		{
			this.TryConsume((byte)'+');
			this.TryConsume((byte)'-');
			this.ReadDigitsUtf8(requireAtLeastOne: true);
		}

		return this.utf8Json[start..this.position];
	}

	private void SkipWhiteSpaceUtf8()
	{
		this.position = this.SkipInsignificantCharactersUtf8(this.position);
	}

	private int SkipInsignificantCharactersUtf8(int index)
	{
		while (index < this.utf8Json.Length)
		{
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
						index++;
					}

					continue;
				}

				if (next == (byte)'*')
				{
					index += 2;
					while (index + 1 < this.utf8Json.Length && !(this.utf8Json[index] == (byte)'*' && this.utf8Json[index + 1] == (byte)'/'))
					{
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

	private bool TryReadEndToken(byte endToken)
	{
		int index = this.SkipInsignificantCharactersUtf8(this.position);
		if (index < this.utf8Json.Length && this.utf8Json[index] == endToken)
		{
			this.position = index + 1;
			return true;
		}

		if (this.allowTrailingCommas && index < this.utf8Json.Length && this.utf8Json[index] == (byte)',')
		{
			int afterComma = this.SkipInsignificantCharactersUtf8(index + 1);
			if (afterComma < this.utf8Json.Length && this.utf8Json[afterComma] == endToken)
			{
				this.position = afterComma + 1;
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

	private void ReadDigitsUtf8(bool requireAtLeastOne)
	{
		int start = this.position;
		while (this.position < this.utf8Json.Length && this.utf8Json[this.position] is >= (byte)'0' and <= (byte)'9')
		{
			this.position++;
		}

		if (requireAtLeastOne && this.position == start)
		{
			throw new FormatException("Expected one or more digits in the JSON number.");
		}
	}

	private char ReadEscapeSequenceUtf8()
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
			(byte)'u' => (char)this.ReadHexQuadUtf8(),
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

	private void SkipArrayUtf8()
	{
		this.ReadStartArray();
		if (this.TryReadEndArray())
		{
			return;
		}

		while (true)
		{
			this.SkipValue();
			if (this.TryReadEndArray())
			{
				return;
			}

			this.ReadValueSeparator();
		}
	}

	private void SkipObjectUtf8()
	{
		this.ReadStartObject();
		if (this.TryReadEndObject())
		{
			return;
		}

		while (true)
		{
			this.SkipStringUtf8();
			this.ReadNameSeparator();
			this.SkipValue();
			if (this.TryReadEndObject())
			{
				return;
			}

			this.ReadValueSeparator();
		}
	}

	private void SkipStringUtf8()
	{
		this.SkipWhiteSpaceUtf8();
		this.RequireCurrent((byte)'"');
		this.position++;
		while (this.position < this.utf8Json.Length)
		{
			byte ch = this.utf8Json[this.position++];
			if (ch == (byte)'"')
			{
				return;
			}

			if (ch == (byte)'\\')
			{
				if (this.position >= this.utf8Json.Length)
				{
					throw new FormatException("Unterminated JSON escape sequence.");
				}

				this.position++;
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
}
