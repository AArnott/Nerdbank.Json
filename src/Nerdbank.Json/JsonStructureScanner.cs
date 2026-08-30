// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// A resumable, byte-level state machine that frames a single top-level JSON value within a UTF-8 byte stream,
/// even when tokens are split across arbitrary buffer boundaries.
/// </summary>
/// <remarks>
/// <para>
/// The scanner does not decode values; it only determines where the next complete JSON value ends so that a
/// fully-buffered <see cref="JsonReader"/> can decode it. Because it processes one byte at a time and preserves its
/// state between calls, it correctly resumes across buffer boundaries in the middle of strings, escape sequences,
/// <c>\uXXXX</c> sequences, numbers, exponents, literals, comments, delimiters, and multi-byte UTF-8 sequences.
/// </para>
/// <para>
/// A leading UTF-8 byte-order-mark is skipped. Comments are tolerated for framing purposes regardless of the reader's
/// comment policy; the policy is enforced by <see cref="JsonReader"/> when the buffered value is decoded.
/// </para>
/// </remarks>
internal struct JsonStructureScanner
{
	private Mode mode;
	private Mode commentReturn;
	private int depth;
	private int unicodeRemaining;
	private bool scalarAtRoot;
	private bool sawAnyByte;
	private bool contentStarted;
	private bool treatCommentsAsContent;

	private enum Mode : byte
	{
		Value,
		Bom1,
		Bom2,
		String,
		StringEscape,
		StringUnicode,
		Scalar,
		Slash,
		LineComment,
		BlockComment,
		BlockCommentStar,
	}

	/// <summary>
	/// Gets a value indicating whether, upon reaching the end of the stream, the value currently being scanned can be
	/// considered complete. This is true for a bare scalar at the root, whose terminator is the end of input.
	/// </summary>
	public readonly bool CanCompleteAtEof => this.mode == Mode.Scalar && this.depth == 0 && this.scalarAtRoot;

	/// <summary>
	/// Gets a value indicating whether the scanner has observed the start of a JSON value (as opposed to only
	/// insignificant whitespace, a BOM, or comments).
	/// </summary>
	public readonly bool ContentStarted => this.contentStarted;

	/// <summary>
	/// Resets the scanner to frame a new value.
	/// </summary>
	/// <param name="preserveBomState">When <see langword="true"/>, keeps the "no bytes seen" state so a leading BOM can still be skipped.</param>
	/// <param name="treatCommentsAsContent">When <see langword="true"/>, a <c>/</c> counts as content (used for trailing-data detection when comments are disallowed).</param>
	public void Reset(bool preserveBomState = false, bool treatCommentsAsContent = false)
	{
		this.mode = Mode.Value;
		this.commentReturn = Mode.Value;
		this.depth = 0;
		this.unicodeRemaining = 0;
		this.scalarAtRoot = false;
		this.contentStarted = false;
		this.treatCommentsAsContent = treatCommentsAsContent;
		if (!preserveBomState)
		{
			this.sawAnyByte = true;
		}
	}

	/// <summary>
	/// Processes a chunk of bytes, advancing the framing state.
	/// </summary>
	/// <param name="chunk">The next contiguous bytes to process. Each byte must be processed exactly once across successive calls.</param>
	/// <param name="consumed">
	/// Receives the number of bytes from <paramref name="chunk"/> that belong to the framed value when the return value is
	/// <see langword="true"/>; otherwise the full length of <paramref name="chunk"/>.
	/// </param>
	/// <returns><see langword="true"/> if a complete top-level value boundary was found within this chunk; otherwise <see langword="false"/>.</returns>
	public bool Scan(scoped ReadOnlySpan<byte> chunk, out int consumed)
	{
		for (int i = 0; i < chunk.Length; i++)
		{
			byte b = chunk[i];
		reprocess:
			switch (this.mode)
			{
				case Mode.Value:
					if (!this.sawAnyByte && b == 0xEF)
					{
						this.mode = Mode.Bom1;
						break;
					}

					if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
					{
						break;
					}

					switch (b)
					{
						case (byte)'"':
							this.mode = Mode.String;
							this.contentStarted = true;
							break;
						case (byte)'{':
						case (byte)'[':
							this.depth++;
							this.contentStarted = true;
							break;
						case (byte)'}':
						case (byte)']':
							this.contentStarted = true;
							this.depth--;
							if (this.depth <= 0)
							{
								this.depth = 0;
								consumed = i + 1;
								this.sawAnyByte = true;
								return true;
							}

							break;
						case (byte)',':
						case (byte)':':
							this.contentStarted = true;
							break;
						case (byte)'/':
							this.commentReturn = Mode.Value;
							this.mode = Mode.Slash;
							if (this.treatCommentsAsContent)
							{
								this.contentStarted = true;
							}

							break;
						default:
							this.mode = Mode.Scalar;
							this.scalarAtRoot = this.depth == 0;
							this.contentStarted = true;
							break;
					}

					break;

				case Mode.Bom1:
					this.mode = b == 0xBB ? Mode.Bom2 : Mode.Value;
					break;

				case Mode.Bom2:
					this.mode = Mode.Value;
					break;

				case Mode.String:
					if (b == (byte)'\\')
					{
						this.mode = Mode.StringEscape;
					}
					else if (b == (byte)'"')
					{
						if (this.depth == 0)
						{
							consumed = i + 1;
							this.sawAnyByte = true;
							return true;
						}

						this.mode = Mode.Value;
					}

					break;

				case Mode.StringEscape:
					this.mode = b == (byte)'u' ? SetUnicode(ref this.unicodeRemaining) : Mode.String;
					break;

				case Mode.StringUnicode:
					if (--this.unicodeRemaining == 0)
					{
						this.mode = Mode.String;
					}

					break;

				case Mode.Scalar:
					if (IsScalarTerminator(b))
					{
						if (this.scalarAtRoot)
						{
							consumed = i;
							this.sawAnyByte = true;
							return true;
						}

						this.mode = Mode.Value;
						goto reprocess;
					}

					break;

				case Mode.Slash:
					if (b == (byte)'/')
					{
						this.mode = Mode.LineComment;
					}
					else if (b == (byte)'*')
					{
						this.mode = Mode.BlockComment;
					}
					else
					{
						this.mode = this.commentReturn;
						goto reprocess;
					}

					break;

				case Mode.LineComment:
					if (b is (byte)'\n' or (byte)'\r')
					{
						this.mode = this.commentReturn;
					}

					break;

				case Mode.BlockComment:
					if (b == (byte)'*')
					{
						this.mode = Mode.BlockCommentStar;
					}

					break;

				case Mode.BlockCommentStar:
					if (b == (byte)'/')
					{
						this.mode = this.commentReturn;
					}
					else if (b != (byte)'*')
					{
						this.mode = Mode.BlockComment;
					}

					break;
			}

			this.sawAnyByte = true;
		}

		consumed = chunk.Length;
		return false;

		static Mode SetUnicode(ref int remaining)
		{
			remaining = 4;
			return Mode.StringUnicode;
		}
	}

	private static bool IsScalarTerminator(byte b)
		=> b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'
			or (byte)',' or (byte)':' or (byte)'{' or (byte)'}' or (byte)'[' or (byte)']'
			or (byte)'"' or (byte)'/';
}
