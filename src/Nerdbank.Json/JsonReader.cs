// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

using System;
using System.Globalization;
using System.Text;

namespace Nerdbank.Json;

internal ref struct JsonReader
{
	private ReadOnlySpan<char> json;
	private int position;

	internal JsonReader(ReadOnlySpan<char> json)
	{
		this.json = json;
		this.position = 0;
	}

	internal bool TryReadNull()
	{
		this.SkipWhiteSpace();
		if (this.json.Length - this.position >= 4 && this.json.Slice(this.position, 4).SequenceEqual("null"))
		{
			this.position += 4;
			return true;
		}

		return false;
	}

	internal bool ReadBoolean()
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

	internal string? ReadString()
	{
		if (this.TryReadNull())
		{
			return null;
		}

		return this.ReadRequiredString();
	}

	internal string ReadRequiredString()
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

	internal char ReadChar()
	{
		string value = this.ReadRequiredString();
		if (value.Length != 1)
		{
			throw new FormatException("Expected a JSON string with exactly one character.");
		}

		return value[0];
	}

	internal string ReadNumberToken()
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

	internal byte[]? ReadBase64Bytes()
	{
		string? value = this.ReadString();
		return value is null ? null : Convert.FromBase64String(value);
	}

	internal byte[] ReadRequiredBase64Bytes() => Convert.FromBase64String(this.ReadRequiredString());

	internal void ReadStartObject()
	{
		this.SkipWhiteSpace();
		this.RequireCurrent('{');
		this.position++;
	}

	internal bool TryReadEndObject()
	{
		this.SkipWhiteSpace();
		if (this.position < this.json.Length && this.json[this.position] == '}')
		{
			this.position++;
			return true;
		}

		return false;
	}

	internal void ReadNameSeparator()
	{
		this.SkipWhiteSpace();
		this.RequireCurrent(':');
		this.position++;
	}

	internal void ReadStartArray()
	{
		this.SkipWhiteSpace();
		this.RequireCurrent('[');
		this.position++;
	}

	internal bool TryReadEndArray()
	{
		this.SkipWhiteSpace();
		if (this.position < this.json.Length && this.json[this.position] == ']')
		{
			this.position++;
			return true;
		}

		return false;
	}

	internal void ReadEndArray()
	{
		this.SkipWhiteSpace();
		this.RequireCurrent(']');
		this.position++;
	}

	internal void ReadValueSeparator()
	{
		this.SkipWhiteSpace();
		this.RequireCurrent(',');
		this.position++;
	}

	internal void EnsureFullyConsumed()
	{
		this.SkipWhiteSpace();
		if (this.position != this.json.Length)
		{
			throw new FormatException("Unexpected trailing data after the JSON value.");
		}
	}

	internal void SkipValue()
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
