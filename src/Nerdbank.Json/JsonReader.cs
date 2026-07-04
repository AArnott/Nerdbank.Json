// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Nerdbank.Json;

/// <summary>
/// Reads JSON values from a character or UTF-8 byte buffer.
/// </summary>
public ref struct JsonReader
{
	private readonly bool allowTrailingCommas;
	private readonly JsonCommentHandling commentHandling;
	private readonly ReadOnlySpan<char> json;
	private readonly byte[]? utf8Buffer;
	private readonly ReadOnlySpan<byte> utf8Json;
	private readonly bool readingUtf8;
	private int position;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonReader"/> struct.
	/// </summary>
	/// <param name="json">The JSON text to read from.</param>
	/// <param name="allowTrailingCommas">A value indicating whether trailing commas should be accepted while reading arrays and objects.</param>
	/// <param name="commentHandling">The policy for handling comments during deserialization.</param>
	public JsonReader(ReadOnlySpan<char> json, bool allowTrailingCommas = false, JsonCommentHandling commentHandling = JsonCommentHandling.Disallow)
	{
		this.allowTrailingCommas = allowTrailingCommas;
		this.commentHandling = commentHandling;
		this.json = json;
		this.utf8Buffer = null;
		this.utf8Json = default;
		this.readingUtf8 = false;
		this.position = 0;
	}

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
		this.json = default;
		this.utf8Buffer = null;
		this.utf8Json = jsonUtf8;
		this.readingUtf8 = true;
		this.position = 0;
	}

	internal JsonReader(scoped in ReadOnlySequence<byte> jsonUtf8, bool allowTrailingCommas = false, JsonCommentHandling commentHandling = JsonCommentHandling.Disallow)
	{
		this.allowTrailingCommas = allowTrailingCommas;
		this.commentHandling = commentHandling;
		this.json = default;
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

		this.readingUtf8 = true;
		this.position = 0;
	}

	public bool TryReadNull()
	{
		if (this.readingUtf8)
		{
			this.SkipWhiteSpaceUtf8();
			if (this.utf8Json.Length - this.position >= 4 && this.utf8Json.Slice(this.position, 4).SequenceEqual("null"u8))
			{
				this.position += 4;
				return true;
			}

			return false;
		}

		this.SkipWhiteSpace();
		if (this.json.Length - this.position >= 4 && this.json.Slice(this.position, 4).SequenceEqual("null"))
		{
			this.position += 4;
			return true;
		}

		return false;
	}

	public bool ReadBoolean()
	{
		if (this.readingUtf8)
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

		this.SkipWhiteSpace();
		if (this.json.Length - this.position >= 4 && this.json.Slice(this.position, 4).SequenceEqual("true"))
		{
			this.position += 4;
			return true;
		}

		if (this.json.Length - this.position >= 5 && this.json.Slice(this.position, 5).SequenceEqual("false"))
		{
			this.position += 5;
			return false;
		}

		throw new FormatException("Expected a JSON boolean literal.");
	}

	public string? ReadString()
	{
		if (this.TryReadNull())
		{
			return null;
		}

		return this.ReadRequiredString();
	}

	public string ReadRequiredString()
	{
		if (this.readingUtf8)
		{
			return this.ReadRequiredUtf8String();
		}

		this.SkipWhiteSpace();
		this.RequireCurrent('"');
		this.position++;

		int segmentStart = this.position;
		StringBuilder? builder = null;
		while (this.position < this.json.Length)
		{
			char ch = this.json[this.position++];
			if (ch == '"')
			{
				if (builder is null)
				{
					return this.json[segmentStart..(this.position - 1)].ToString();
				}

				builder.Append(this.json[segmentStart..(this.position - 1)].ToString());
				return builder.ToString();
			}

			if (ch == '\\')
			{
				builder ??= new StringBuilder();
				builder.Append(this.json[segmentStart..(this.position - 1)].ToString());
				builder.Append(this.ReadEscapeSequence());
				segmentStart = this.position;
			}
		}

		throw new FormatException("Unterminated JSON string.");
	}

	public char ReadChar()
	{
		string value = this.ReadRequiredString();
		if (value.Length != 1)
		{
			throw new FormatException("Expected a JSON string with exactly one character.");
		}

		return value[0];
	}

	public string ReadNumberToken()
	{
		if (this.readingUtf8)
		{
			return Encoding.UTF8.GetString(this.ReadNumberTokenUtf8Core());
		}

		this.SkipWhiteSpace();
		int start = this.position;

		if (this.TryConsume('-'))
		{
		}

		this.ReadDigits(requireAtLeastOne: true);
		if (this.TryConsume('.'))
		{
			this.ReadDigits(requireAtLeastOne: true);
		}

		if (this.TryConsume('e') || this.TryConsume('E'))
		{
			this.TryConsume('+');
			this.TryConsume('-');
			this.ReadDigits(requireAtLeastOne: true);
		}

		return this.json[start..this.position].ToString();
	}

	public byte[]? ReadBase64Bytes()
	{
		string? value = this.ReadString();
		return value is null ? null : Convert.FromBase64String(value);
	}

	public byte[] ReadRequiredBase64Bytes() => Convert.FromBase64String(this.ReadRequiredString());

	public void ReadStartObject()
	{
		if (this.readingUtf8)
		{
			this.SkipWhiteSpaceUtf8();
			this.RequireCurrent((byte)'{');
			this.position++;
			return;
		}

		this.SkipWhiteSpace();
		this.RequireCurrent('{');
		this.position++;
	}

	public bool TryReadEndObject()
	{
		return this.readingUtf8 ? this.TryReadEndToken((byte)'}') : this.TryReadEndToken('}');
	}

	public void ReadNameSeparator()
	{
		if (this.readingUtf8)
		{
			this.SkipWhiteSpaceUtf8();
			this.RequireCurrent((byte)':');
			this.position++;
			return;
		}

		this.SkipWhiteSpace();
		this.RequireCurrent(':');
		this.position++;
	}

	public void ReadStartArray()
	{
		if (this.readingUtf8)
		{
			this.SkipWhiteSpaceUtf8();
			this.RequireCurrent((byte)'[');
			this.position++;
			return;
		}

		this.SkipWhiteSpace();
		this.RequireCurrent('[');
		this.position++;
	}

	public bool TryReadEndArray()
	{
		return this.readingUtf8 ? this.TryReadEndToken((byte)']') : this.TryReadEndToken(']');
	}

	public void ReadEndArray()
	{
		if (!this.TryReadEndArray())
		{
			throw new FormatException("Expected ']' in JSON input.");
		}
	}

	public void ReadValueSeparator()
	{
		if (this.readingUtf8)
		{
			this.SkipWhiteSpaceUtf8();
			this.RequireCurrent((byte)',');
			this.position++;
			return;
		}

		this.SkipWhiteSpace();
		this.RequireCurrent(',');
		this.position++;
	}

	public void EnsureFullyConsumed()
	{
		if (this.readingUtf8)
		{
			this.SkipWhiteSpaceUtf8();
			if (this.position != this.utf8Json.Length)
			{
				throw new FormatException("Unexpected trailing data after the JSON value.");
			}

			return;
		}

		this.SkipWhiteSpace();
		if (this.position != this.json.Length)
		{
			throw new FormatException("Unexpected trailing data after the JSON value.");
		}
	}

	public void SkipValue()
	{
		if (this.readingUtf8)
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

			return;
		}

		this.SkipWhiteSpace();
		if (this.position >= this.json.Length)
		{
			throw new FormatException("Expected a JSON value.");
		}

		switch (this.json[this.position])
		{
			case '{':
				this.SkipObject();
				break;
			case '[':
				this.SkipArray();
				break;
			case '"':
				this.ReadRequiredString();
				break;
			case 't':
			case 'f':
				this.ReadBoolean();
				break;
			case 'n':
				if (!this.TryReadNull())
				{
					throw new FormatException("Expected a JSON null literal.");
				}

				break;
			default:
				this.ReadNumberToken();
				break;
		}
	}

	public string ReadRawValue()
	{
		if (this.readingUtf8)
		{
			this.SkipWhiteSpaceUtf8();
			int utf8Start = this.position;
			this.SkipValue();
			return Encoding.UTF8.GetString(this.utf8Json[utf8Start..this.position]);
		}

		this.SkipWhiteSpace();
		int start = this.position;
		this.SkipValue();
		return this.json[start..this.position].ToString();
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
		if (this.readingUtf8)
		{
			this.SkipWhiteSpaceUtf8();
			if (this.position >= this.utf8Json.Length)
			{
				throw new FormatException("Expected a JSON value.");
			}

			return (char)this.utf8Json[this.position];
		}

		this.SkipWhiteSpace();
		if (this.position >= this.json.Length)
		{
			throw new FormatException("Expected a JSON value.");
		}

		return this.json[this.position];
	}

	private void SkipWhiteSpace()
	{
		this.position = this.SkipInsignificantCharacters(this.position);
	}

	private int SkipInsignificantCharacters(int index)
	{
		while (index < this.json.Length)
		{
			char ch = this.json[index];
			if (char.IsWhiteSpace(ch))
			{
				index++;
				continue;
			}

			if (this.commentHandling == JsonCommentHandling.Skip && ch == '/' && index + 1 < this.json.Length)
			{
				char next = this.json[index + 1];
				if (next == '/')
				{
					index += 2;
					while (index < this.json.Length && this.json[index] is not '\r' and not '\n')
					{
						index++;
					}

					continue;
				}
				if (next == '*')
				{
					index += 2;
					while (index + 1 < this.json.Length && !(this.json[index] == '*' && this.json[index + 1] == '/'))
					{
						index++;
					}
					if (index + 1 >= this.json.Length)
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

	private bool TryReadEndToken(char endToken)
	{
		int index = this.SkipInsignificantCharacters(this.position);
		if (index < this.json.Length && this.json[index] == endToken)
		{
			this.position = index + 1;
			return true;
		}
		if (this.allowTrailingCommas && index < this.json.Length && this.json[index] == ',')
		{
			int afterComma = this.SkipInsignificantCharacters(index + 1);
			if (afterComma < this.json.Length && this.json[afterComma] == endToken)
			{
				this.position = afterComma + 1;
				return true;
			}
		}
		return false;
	}

	private bool TryConsume(char expected)
	{
		if (this.position < this.json.Length && this.json[this.position] == expected)
		{
			this.position++;
			return true;
		}
		return false;
	}

	private void ReadDigits(bool requireAtLeastOne)
	{
		int start = this.position;
		while (this.position < this.json.Length && char.IsDigit(this.json[this.position]))
		{
			this.position++;
		}
		if (requireAtLeastOne && this.position == start)
		{
			throw new FormatException("Expected one or more digits in the JSON number.");
		}
	}

	private char ReadEscapeSequence()
	{
		if (this.position >= this.json.Length)
		{
			throw new FormatException("Unterminated JSON escape sequence.");
		}
		char ch = this.json[this.position++];
		return ch switch
		{
			'"' => '"',
			'\\' => '\\',
			'/' => '/',
			'b' => '\b',
			'f' => '\f',
			'n' => '\n',
			'r' => '\r',
			't' => '\t',
			'u' => (char)this.ReadHexQuad(),
			_ => throw new FormatException($"Unsupported JSON escape sequence '\\{ch}'."),
		};
	}

	private int ReadHexQuad()
	{
		if (this.position + 4 > this.json.Length)
		{
			throw new FormatException("Incomplete JSON unicode escape sequence.");
		}
		ReadOnlySpan<char> hex = this.json.Slice(this.position, 4);
		this.position += 4;
		return int.Parse(hex.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
	}

	private void SkipArray()
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

	private void SkipObject()
	{
		this.ReadStartObject();
		if (this.TryReadEndObject())
		{
			return;
		}
		while (true)
		{
			this.ReadRequiredString();
			this.ReadNameSeparator();
			this.SkipValue();
			if (this.TryReadEndObject())
			{
				return;
			}
			this.ReadValueSeparator();
		}
	}

	private void RequireCurrent(char expected)
	{
		if (this.position >= this.json.Length || this.json[this.position] != expected)
		{
			throw new FormatException($"Expected '{expected}' in JSON input.");
		}
	}

	private bool TryReadUtf8NumberToken(out ReadOnlySpan<byte> token)
	{
		if (this.readingUtf8)
		{
			token = this.ReadNumberTokenUtf8Core();
			return true;
		}
		token = default;
		return false;
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

	private static int HexToInt(byte value)
		=> value switch
		{
			>= (byte)'0' and <= (byte)'9' => value - (byte)'0',
			>= (byte)'A' and <= (byte)'F' => value - (byte)'A' + 10,
			>= (byte)'a' and <= (byte)'f' => value - (byte)'a' + 10,
			_ => throw new FormatException("Invalid hex digit in JSON unicode escape sequence."),
		};

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
