// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

public partial class JsonReaderTests
{
	[Test]
	[Arguments(0)]
	[Arguments(1)]
	[Arguments(7)]
	[Arguments(8)]
	[Arguments(9)]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(15)]
	[Arguments(16)]
	[Arguments(23)]
	[Arguments(24)]
	[Arguments(25)]
	[Arguments(31)]
	[Arguments(32)]
	[Arguments(63)]
	[Arguments(64)]
	[Arguments(127)]
	[Arguments(128)]
	[Arguments(255)]
	[Arguments(256)]
	[Arguments(17)]
	[Arguments(18)]
	[Arguments(33)]
	[Arguments(34)]
	[Arguments(65)]
	[Arguments(66)]
	public void StringLexicalScan_ControlsAndDelimiterOrdering(int offset)
	{
		string prefix = new('a', offset);
		string tail = new('z', 80);
		const string ControlError = "Unescaped control character in JSON string.";
		for (int control = 0; control < 32; control++)
		{
			string invalid = "\"" + prefix + (char)control;
			VerifyStringLexicalInput(Encoding.UTF8.GetBytes(invalid + "\" true" + tail), null, offset + 2, ControlError);
			VerifyStringLexicalInput(Encoding.UTF8.GetBytes(invalid + "\\n\" true" + tail), null, offset + 2, ControlError);
			VerifyStringLexicalInput(Encoding.UTF8.GetBytes(invalid), null, offset + 2, ControlError);

			// A quote before a control ends the token; a valid escape before it enters the escaped reader.
			VerifyStringLexicalInput(Encoding.UTF8.GetBytes("\"" + prefix + "\"" + (char)control + "\\q" + tail), prefix, offset + 2);
			byte[] escaped = Encoding.UTF8.GetBytes("\"" + prefix + "\\n" + (char)control + "\"");
			VerifyStringLexicalInput(escaped, null, escaped.Length, ControlError);
			byte[] escapedQuote = Encoding.UTF8.GetBytes("\"" + prefix + "\\\"" + (char)control + "\"");
			VerifyStringLexicalInput(escapedQuote, null, escapedQuote.Length, ControlError);
			VerifyStringLexicalInput(
				Encoding.UTF8.GetBytes("\"" + prefix + "\\" + (char)control + "\""),
				null,
				offset + 3,
				$"Unsupported JSON escape sequence '\\{(char)control}'.");
		}
	}

	[Test]
	[Arguments(0)]
	[Arguments(1)]
	[Arguments(7)]
	[Arguments(8)]
	[Arguments(9)]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(15)]
	[Arguments(16)]
	[Arguments(23)]
	[Arguments(24)]
	[Arguments(25)]
	[Arguments(31)]
	[Arguments(32)]
	[Arguments(63)]
	[Arguments(64)]
	[Arguments(127)]
	[Arguments(128)]
	[Arguments(255)]
	[Arguments(256)]
	[Arguments(17)]
	[Arguments(18)]
	[Arguments(33)]
	[Arguments(34)]
	[Arguments(65)]
	[Arguments(66)]
	public void StringLexicalScan_QuotesAndEscapedRuns(int offset)
	{
		string prefix = new('a', offset);
		string tail = new('z', 80);
		VerifyStringLexicalInput(Encoding.UTF8.GetBytes("\"" + prefix + "\"\\q" + tail), prefix, offset + 2);
		VerifyStringLexicalInput(Encoding.UTF8.GetBytes("\"" + prefix + "\\\\\"\\q" + tail), prefix + "\\", offset + 4);

		const string Encoded = "\\\"\\\\\\/\\b\\f\\n\\r\\t\\uD800\\u0041\\uDC00\\uD83D\\uDE00";
		const string Decoded = "\"\\/\b\f\n\r\t\uD800A\uDC00\uD83D\uDE00";
		string middle = new('b', 65);
		byte[] json = Encoding.UTF8.GetBytes("\"" + prefix + Encoded + middle + Encoded + "\" true");
		VerifyStringLexicalInput(json, prefix + Decoded + middle + Decoded, json.Length - 5);
	}

	[Test]
	[Arguments(0)]
	[Arguments(1)]
	[Arguments(7)]
	[Arguments(8)]
	[Arguments(9)]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(15)]
	[Arguments(16)]
	[Arguments(23)]
	[Arguments(24)]
	[Arguments(25)]
	[Arguments(31)]
	[Arguments(32)]
	[Arguments(63)]
	[Arguments(64)]
	[Arguments(127)]
	[Arguments(128)]
	[Arguments(255)]
	[Arguments(256)]
	public void StringLexicalScan_TruncationAndEscapeFailurePositions(int offset)
	{
		string prefix = "\"" + new string('a', offset);
		VerifyStringLexicalInput(Encoding.UTF8.GetBytes(prefix), null, offset + 1, "Unterminated JSON string.");
		foreach (string suffix in new[] { "\\", "\\n", "\\\"", "\\\\", "\\u12" })
		{
			VerifyStringLexicalInput(Encoding.UTF8.GetBytes(prefix + suffix), null, offset + 2, "Unterminated JSON string.");
		}

		VerifyStringLexicalInput(Encoding.UTF8.GetBytes(prefix + "\\q\""), null, offset + 3, "Unsupported JSON escape sequence '\\q'.");
		VerifyStringLexicalInput(Encoding.UTF8.GetBytes(prefix + "\\u12\""), null, offset + 3, "Incomplete JSON unicode escape sequence.");
		VerifyStringLexicalInput(Encoding.UTF8.GetBytes(prefix + "\\u12x4\""), null, offset + 6, "Invalid hex digit in JSON unicode escape sequence.");
	}

	[Test]
	[Arguments(0)]
	[Arguments(1)]
	[Arguments(7)]
	[Arguments(8)]
	[Arguments(9)]
	[Arguments(2)]
	[Arguments(3)]
	[Arguments(15)]
	[Arguments(16)]
	[Arguments(23)]
	[Arguments(24)]
	[Arguments(25)]
	[Arguments(31)]
	[Arguments(32)]
	[Arguments(63)]
	[Arguments(64)]
	[Arguments(127)]
	[Arguments(128)]
	[Arguments(255)]
	[Arguments(256)]
	public void StringLexicalScan_Utf8ReplacementAtBoundaries(int offset)
	{
		string prefix = new('a', offset);
		byte[] prefixBytes = Encoding.UTF8.GetBytes(prefix);
		byte[][] payloads =
		[
			[0x80], [0xFF], [0xC0, 0xAF], [0xE0, 0x80, 0xAF], [0xED, 0xA0, 0x80],
			[0xF4, 0x90, 0x80, 0x80], [0xC2], [0xE0, 0xA0], [0xF0, 0x90, 0x80],
			[0xC3, 0xA9, 0xF0, 0x9F, 0x98, 0x80, 0x7F],
		];
		foreach (byte[] payload in payloads)
		{
			string decoded = Encoding.UTF8.GetString(payload);
			byte[] plain = [(byte)'"', .. prefixBytes, .. payload, (byte)'"'];
			VerifyStringLexicalInput(plain, prefix + decoded, plain.Length);
			byte[] beforeEscape = [(byte)'"', .. prefixBytes, .. payload, .. "\\n\""u8.ToArray()];
			VerifyStringLexicalInput(beforeEscape, prefix + decoded + "\n", beforeEscape.Length);
			byte[] afterEscape = [(byte)'"', .. prefixBytes, .. "\\n"u8.ToArray(), .. payload, (byte)'"'];
			VerifyStringLexicalInput(afterEscape, prefix + "\n" + decoded, afterEscape.Length);
		}
	}

	[Test]
	[Arguments(0)]
	[Arguments(1)]
	[Arguments(7)]
	[Arguments(8)]
	[Arguments(9)]
	[Arguments(15)]
	[Arguments(16)]
	[Arguments(23)]
	[Arguments(24)]
	[Arguments(25)]
	[Arguments(31)]
	[Arguments(32)]
	[Arguments(63)]
	[Arguments(64)]
	[Arguments(128)]
	[Arguments(256)]
	public void StringLexicalScan_InterningIsPreserved(int offset)
	{
		string prefix = new('a', offset);
		foreach ((string suffix, string decoded) in new[] { (string.Empty, string.Empty), ("\\n\\uD800", "\n\uD800"), ("\u007F", "\u007F") })
		{
			string token = "\"" + prefix + suffix + "\"";
			string equivalent = "\"" + prefix + suffix.Replace("\\n", "\\u000A") + "\"";
			byte[] json = Encoding.UTF8.GetBytes("[" + token + "," + equivalent + "," + token + "]");
			string expected = prefix + decoded;
			foreach (ReadOnlySequence<byte> sequence in StringLexicalSequences(json))
			{
				JsonSerializer serializer = new() { InternStrings = true };
				string[] values = serializer.Deserialize<string[], JsonStringInterningTests>(sequence)!;
				Assert.Equal(new[] { expected, expected, expected }, values);
				Assert.Same(values[0], values[1]);
				Assert.Same(values[0], values[2]);
			}
		}
	}

	private static void VerifyStringLexicalInput(byte[] token, string? expected, int consumed, string? error = null)
	{
		// Exercise nonzero absolute offsets without adding bytes to a truncated token's end.
		byte[] json = [.. " \t"u8.ToArray(), .. token];
		VerifyStringLexicalRead(new(json.AsSpan()), json, expected, consumed + 2, error);
		foreach (ReadOnlySequence<byte> sequence in StringLexicalSequences(json))
		{
			VerifyStringLexicalRead(new(sequence), json, expected, consumed + 2, error);
		}
	}

	private static void VerifyStringLexicalRead(JsonReader reader, byte[] json, string? expected, int consumed, string? error)
	{
		if (error is null)
		{
			Assert.Equal(expected, reader.ReadRequiredString());
		}
		else
		{
			FormatException? failure = null;
			try
			{
				reader.ReadRequiredString();
			}
			catch (FormatException ex)
			{
				failure = ex;
			}

			Assert.Equal(error, Assert.IsType<FormatException>(failure).Message);
		}

		Assert.Equal(consumed, reader.Consumed);
		Assert.Equal(consumed == json.Length, reader.End);
		Assert.Equal(reader.Sequence.GetPosition(consumed), reader.Position);
		Assert.Equal(json.AsSpan(consumed).ToArray(), reader.Sequence.Slice(reader.Position).ToArray());
	}

	private static IEnumerable<ReadOnlySequence<byte>> StringLexicalSequences(byte[] bytes)
	{
		yield return new(bytes);
		for (int split = 0; split <= bytes.Length; split++)
		{
			Segment first = new(bytes.AsMemory(0, split));
			Segment last = first.Append(ReadOnlyMemory<byte>.Empty).Append(bytes.AsMemory(split));
			yield return new(first, 0, last, last.Memory.Length);
		}

		Segment oneByteFirst = new(ReadOnlyMemory<byte>.Empty);
		Segment oneByteLast = oneByteFirst;
		for (int i = 0; i < bytes.Length; i++)
		{
			oneByteLast = oneByteLast.Append(bytes.AsMemory(i, 1)).Append(ReadOnlyMemory<byte>.Empty);
		}

		yield return new(oneByteFirst, 0, oneByteLast, 0);
	}
}
