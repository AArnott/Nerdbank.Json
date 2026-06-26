// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

using System;
using System.Globalization;
using System.Text;

namespace Nerdbank.Json;

/// <summary>
/// Reads JSON values from a character buffer.
/// </summary>
public ref struct JsonReader
{
	private ReadOnlySpan<char> json;
	private int position;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonReader"/> struct.
	/// </summary>
	/// <param name="json">The JSON text to read from.</param>
	public JsonReader(ReadOnlySpan<char> json)
	{
		this.json = json;
		this.position = 0;
	}

	/// <summary>
	/// Attempts to read the JSON <see langword="null"/> literal.
	/// </summary>
	/// <returns><see langword="true"/> if <see langword="null"/> was consumed; otherwise, <see langword="false"/>.</returns>
	public bool TryReadNull()
	{
		this.SkipWhiteSpace();
		if (this.json.Length - this.position >= 4 && this.json.Slice(this.position, 4).SequenceEqual("null"))
		{
			this.position += 4;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Reads a JSON boolean literal.
	/// </summary>
	/// <returns>The boolean value.</returns>
	public bool ReadBoolean()
	{
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

	/// <summary>
	/// Reads a JSON string value or <see langword="null"/>.
	/// </summary>
	/// <returns>The string value, or <see langword="null"/>.</returns>
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
	/// <returns>The string value.</returns>
	public string ReadRequiredString()
	{
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
					return this.json.Slice(segmentStart, (this.position - 1) - segmentStart).ToString();
				}

				builder.Append(this.json.Slice(segmentStart, (this.position - 1) - segmentStart).ToString());
				return builder.ToString();
			}

			if (ch == '\\')
			{
				builder ??= new StringBuilder();
				builder.Append(this.json.Slice(segmentStart, (this.position - 1) - segmentStart).ToString());
				builder.Append(this.ReadEscapeSequence());
				segmentStart = this.position;
			}
		}

		throw new FormatException("Unterminated JSON string.");
	}

	/// <summary>
	/// Reads a JSON string that must contain exactly one character.
	/// </summary>
	/// <returns>The character value.</returns>
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
	/// Reads the next JSON number token as text.
	/// </summary>
	/// <returns>The numeric token text.</returns>
	public string ReadNumberToken()
	{
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

		return this.json.Slice(start, this.position - start).ToString();
	}

	/// <summary>
	/// Reads a Base64-encoded JSON string value or <see langword="null"/>.
	/// </summary>
	/// <returns>The decoded bytes, or <see langword="null"/>.</returns>
	public byte[]? ReadBase64Bytes()
	{
		string? value = this.ReadString();
		return value is null ? null : Convert.FromBase64String(value);
	}

	/// <summary>
	/// Reads a required Base64-encoded JSON string value.
	/// </summary>
	/// <returns>The decoded bytes.</returns>
	public byte[] ReadRequiredBase64Bytes() => Convert.FromBase64String(this.ReadRequiredString());

	/// <summary>
	/// Reads the start of a JSON object.
	/// </summary>
	public void ReadStartObject()
	{
		this.SkipWhiteSpace();
		this.RequireCurrent('{');
		this.position++;
	}

	/// <summary>
	/// Attempts to read the end of a JSON object.
	/// </summary>
	/// <returns><see langword="true"/> if the end of the object was consumed; otherwise, <see langword="false"/>.</returns>
	public bool TryReadEndObject()
	{
		this.SkipWhiteSpace();
		if (this.position < this.json.Length && this.json[this.position] == '}')
		{
			this.position++;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Reads the name separator within a JSON object.
	/// </summary>
	public void ReadNameSeparator()
	{
		this.SkipWhiteSpace();
		this.RequireCurrent(':');
		this.position++;
	}

	/// <summary>
	/// Reads the start of a JSON array.
	/// </summary>
	public void ReadStartArray()
	{
		this.SkipWhiteSpace();
		this.RequireCurrent('[');
		this.position++;
	}

	/// <summary>
	/// Attempts to read the end of a JSON array.
	/// </summary>
	/// <returns><see langword="true"/> if the end of the array was consumed; otherwise, <see langword="false"/>.</returns>
	public bool TryReadEndArray()
	{
		this.SkipWhiteSpace();
		if (this.position < this.json.Length && this.json[this.position] == ']')
		{
			this.position++;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Reads the end of a JSON array.
	/// </summary>
	public void ReadEndArray()
	{
		this.SkipWhiteSpace();
		this.RequireCurrent(']');
		this.position++;
	}

	/// <summary>
	/// Reads the value separator between JSON elements.
	/// </summary>
	public void ReadValueSeparator()
	{
		this.SkipWhiteSpace();
		this.RequireCurrent(',');
		this.position++;
	}

	/// <summary>
	/// Verifies that no non-whitespace JSON remains unread.
	/// </summary>
	public void EnsureFullyConsumed()
	{
		this.SkipWhiteSpace();
		if (this.position != this.json.Length)
		{
			throw new FormatException("Unexpected trailing data after the JSON value.");
		}
	}

	/// <summary>
	/// Skips the next JSON value.
	/// </summary>
	public void SkipValue()
	{
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

	internal char PeekValueToken()
	{
		this.SkipWhiteSpace();
		if (this.position >= this.json.Length)
		{
			throw new FormatException("Expected a JSON value.");
		}

		return this.json[this.position];
	}

	private void SkipWhiteSpace()
	{
		while (this.position < this.json.Length && char.IsWhiteSpace(this.json[this.position]))
		{
			this.position++;
		}
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
		switch (ch)
		{
			case '"': return '"';
			case '\\': return '\\';
			case '/': return '/';
			case 'b': return '\b';
			case 'f': return '\f';
			case 'n': return '\n';
			case 'r': return '\r';
			case 't': return '\t';
			case 'u': return (char)this.ReadHexQuad();
			default: throw new FormatException($"Unsupported JSON escape sequence '\\{ch}'.");
		}
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
}
