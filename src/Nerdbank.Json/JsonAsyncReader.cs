// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;

namespace Nerdbank.Json;

/// <summary>
/// An incremental UTF-8 JSON reader that decodes from a <see cref="PipeReader"/> without buffering the entire document.
/// </summary>
/// <remarks>
/// <para>
/// This reader fetches bytes from the pipe on demand and frames one JSON value (or one structural token) at a time using
/// <see cref="JsonStructureScanner"/>, so tokens that straddle buffer boundaries are handled correctly and memory use is
/// bounded by the size of the largest single value being decoded rather than the whole document.
/// </para>
/// <para>
/// It is the asynchronous counterpart to <see cref="JsonReader"/> and is used when implementing the asynchronous virtual
/// methods on <see cref="JsonConverter{T}"/>. The underlying <see cref="PipeReader"/> is never completed by this type; it
/// is left positioned after the last consumed byte.
/// </para>
/// </remarks>
public sealed class JsonAsyncReader : IDisposable
{
	private readonly PipeReader pipeReader;
	private readonly bool allowTrailingCommas;
	private readonly JsonCommentHandling commentHandling;

	private JsonStructureScanner scanner;
	private ReadOnlySequence<byte> buffer;
	private bool hasBufferedRead;
	private bool isCompleted;
	private bool readerReturned = true;
	private long valueEnd;
	private SkipState skipState;
	private bool bomChecked;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonAsyncReader"/> class.
	/// </summary>
	/// <param name="pipeReader">The pipe to read UTF-8 JSON from. It is not completed by this reader.</param>
	/// <param name="allowTrailingCommas">Whether trailing commas are tolerated in arrays and objects.</param>
	/// <param name="commentHandling">The comment handling policy.</param>
	public JsonAsyncReader(PipeReader pipeReader, bool allowTrailingCommas = false, JsonCommentHandling commentHandling = JsonCommentHandling.Disallow)
	{
		this.pipeReader = Requires.NotNull(pipeReader);
		this.allowTrailingCommas = allowTrailingCommas;
		this.commentHandling = commentHandling;
	}

	private enum SkipState : byte
	{
		None,
		Slash,
		LineComment,
		BlockComment,
		BlockCommentStar,
	}

	/// <summary>
	/// Gets a cancellation token that applies to all reads from this reader.
	/// </summary>
	public required CancellationToken CancellationToken { get; init; }

	/// <summary>
	/// Buffers the next complete JSON value from the pipe, fetching more bytes as needed.
	/// </summary>
	/// <param name="context">The serialization context.</param>
	/// <returns>A task that completes when a full value is buffered.</returns>
	public async ValueTask BufferNextValueAsync(SerializationContext context)
	{
		this.ThrowIfReaderNotReturned();
		context.CancellationToken.ThrowIfCancellationRequested();

		if (!this.bomChecked)
		{
			await this.SkipByteOrderMarkAsync().ConfigureAwait(false);
		}

		this.scanner.Reset();
		long scanned = 0;
		while (true)
		{
			if (this.hasBufferedRead && this.TryScan(ref scanned, out long end))
			{
				this.valueEnd = end;
				return;
			}

			if (this.isCompleted)
			{
				this.valueEnd = this.buffer.Length;
				return;
			}

			await this.ReadMoreAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Creates a synchronous reader over the value most recently buffered by <see cref="BufferNextValueAsync"/>.
	/// </summary>
	/// <returns>A synchronous reader.</returns>
	public JsonReader CreateBufferedReader()
	{
		this.ThrowIfReaderNotReturned();
		this.readerReturned = false;
		return new JsonReader(this.buffer.Slice(0, this.valueEnd), this.allowTrailingCommas, this.commentHandling);
	}

	/// <summary>
	/// Returns a synchronous reader previously obtained from this object, advancing this reader past the consumed bytes.
	/// </summary>
	/// <param name="reader">The reader to return. It is reset to prevent reuse.</param>
	public void ReturnReader(ref JsonReader reader)
	{
		this.buffer = this.buffer.Slice(reader.BytesConsumed);
		reader = default;
		this.readerReturned = true;
	}

	/// <summary>
	/// Reads a value using the specified converter, streaming when the converter prefers asynchronous serialization.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="converter">The converter for the value.</param>
	/// <param name="context">The serialization context.</param>
	/// <returns>The deserialized value.</returns>
	public async ValueTask<T?> ReadValueAsync<T>(JsonConverter<T> converter, SerializationContext context)
	{
		Requires.NotNull(converter);
		if (converter.PreferAsyncSerialization)
		{
			return await converter.ReadAsync(this, context).ConfigureAwait(false);
		}

		await this.BufferNextValueAsync(context).ConfigureAwait(false);
		JsonReader sync = this.CreateBufferedReader();
		T? result = converter.Read(ref sync, context);
		this.ReturnReader(ref sync);
		return result;
	}

	/// <summary>
	/// Peeks at the next significant (non-whitespace, non-comment) byte without consuming it.
	/// </summary>
	/// <returns>The next significant byte, or <c>-1</c> if the end of the stream is reached first.</returns>
	public async ValueTask<int> PeekNextByteAsync()
	{
		this.ThrowIfReaderNotReturned();
		while (true)
		{
			if (this.hasBufferedRead && this.TrySkipInsignificant(out byte significant))
			{
				return significant;
			}

			if (this.isCompleted)
			{
				return -1;
			}

			await this.ReadMoreAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Reads and discards a leading <see langword="null"/> literal if present.
	/// </summary>
	/// <param name="context">The serialization context.</param>
	/// <returns><see langword="true"/> if a <see langword="null"/> literal was consumed.</returns>
	public async ValueTask<bool> TryReadNullAsync(SerializationContext context)
	{
		int next = await this.PeekNextByteAsync().ConfigureAwait(false);
		if (next != 'n')
		{
			return false;
		}

		await this.BufferNextValueAsync(context).ConfigureAwait(false);
		JsonReader sync = this.CreateBufferedReader();
		bool result = sync.TryReadNull();
		this.ReturnReader(ref sync);
		return result;
	}

	/// <summary>
	/// Reads the start-of-array token.
	/// </summary>
	/// <param name="context">The serialization context.</param>
	/// <returns>A task representing the read.</returns>
	public ValueTask ReadStartArrayAsync(SerializationContext context) => this.ReadStructuralAsync((byte)'[', context);

	/// <summary>
	/// Reads the start-of-object token.
	/// </summary>
	/// <param name="context">The serialization context.</param>
	/// <returns>A task representing the read.</returns>
	public ValueTask ReadStartObjectAsync(SerializationContext context) => this.ReadStructuralAsync((byte)'{', context);

	/// <summary>
	/// Reads a value separator (comma).
	/// </summary>
	/// <param name="context">The serialization context.</param>
	/// <returns>A task representing the read.</returns>
	public ValueTask ReadValueSeparatorAsync(SerializationContext context) => this.ReadStructuralAsync((byte)',', context);

	/// <summary>
	/// Reads the name separator (colon).
	/// </summary>
	/// <param name="context">The serialization context.</param>
	/// <returns>A task representing the read.</returns>
	public ValueTask ReadNameSeparatorAsync(SerializationContext context) => this.ReadStructuralAsync((byte)':', context);

	/// <summary>
	/// Reads the end-of-array token if present.
	/// </summary>
	/// <param name="context">The serialization context.</param>
	/// <returns>A task whose result is <see langword="true"/> if the token was consumed.</returns>
	public ValueTask<bool> TryReadEndArrayAsync(SerializationContext context) => this.TryReadStructuralAsync((byte)']', context);

	/// <summary>
	/// Reads the end-of-object token if present.
	/// </summary>
	/// <param name="context">The serialization context.</param>
	/// <returns>A task whose result is <see langword="true"/> if the token was consumed.</returns>
	public ValueTask<bool> TryReadEndObjectAsync(SerializationContext context) => this.TryReadStructuralAsync((byte)'}', context);

	/// <summary>
	/// Reads a JSON property name (a string) and the following name separator.
	/// </summary>
	/// <param name="context">The serialization context.</param>
	/// <returns>A task whose result is the decoded property name.</returns>
	public async ValueTask<string> ReadPropertyNameAsync(SerializationContext context)
	{
		await this.BufferNextValueAsync(context).ConfigureAwait(false);
		JsonReader sync = this.CreateBufferedReader();
		string name = sync.ReadRequiredString();
		this.ReturnReader(ref sync);
		await this.ReadNameSeparatorAsync(context).ConfigureAwait(false);
		return name;
	}

	/// <summary>
	/// Verifies that no significant data (other than whitespace and, when allowed, comments) follows the value already read.
	/// </summary>
	/// <param name="context">The serialization context.</param>
	/// <returns>A task that completes when the check passes.</returns>
	/// <exception cref="FormatException">Thrown if trailing data is present.</exception>
	public async ValueTask EnsureFullyConsumedAsync(SerializationContext context)
	{
		int next = await this.PeekNextByteAsync().ConfigureAwait(false);
		if (next != -1)
		{
			throw new FormatException("Unexpected trailing data after the JSON value.");
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		if (!this.readerReturned)
		{
			throw new InvalidOperationException("A synchronous reader was not returned before disposing this object.");
		}

		if (this.hasBufferedRead)
		{
			this.pipeReader.AdvanceTo(this.buffer.Start, this.buffer.End);
			this.hasBufferedRead = false;
		}
	}

	private async ValueTask ReadStructuralAsync(byte expected, SerializationContext context)
	{
		int next = await this.PeekNextByteAsync().ConfigureAwait(false);
		if (next != expected)
		{
			throw new FormatException($"Expected '{(char)expected}' but found {(next < 0 ? "end of stream" : $"'{(char)next}'")}.");
		}

		this.buffer = this.buffer.Slice(1);
	}

	private async ValueTask<bool> TryReadStructuralAsync(byte expected, SerializationContext context)
	{
		int next = await this.PeekNextByteAsync().ConfigureAwait(false);
		if (next != expected)
		{
			return false;
		}

		this.buffer = this.buffer.Slice(1);
		return true;
	}

	private async ValueTask SkipByteOrderMarkAsync()
	{
		this.bomChecked = true;
		while (this.buffer.Length < 3 && !this.isCompleted)
		{
			if (!await this.ReadMoreAsync().ConfigureAwait(false))
			{
				break;
			}
		}

		if (this.buffer.Length >= 3)
		{
			Span<byte> first3 = stackalloc byte[3];
			this.buffer.Slice(0, 3).CopyTo(first3);
			if (first3[0] == 0xEF && first3[1] == 0xBB && first3[2] == 0xBF)
			{
				this.buffer = this.buffer.Slice(3);
			}
		}
	}

	private bool TryScan(ref long scanned, out long endOffset)
	{
		ReadOnlySequence<byte> tail = this.buffer.Slice(scanned);
		long baseOffset = scanned;
		foreach (ReadOnlyMemory<byte> segment in tail)
		{
			if (this.scanner.Scan(segment.Span, out int consumed))
			{
				endOffset = baseOffset + consumed;
				scanned = endOffset;
				return true;
			}

			baseOffset += segment.Length;
		}

		scanned = this.buffer.Length;
		endOffset = 0;
		return false;
	}

	private bool TrySkipInsignificant(out byte significant)
	{
		bool skipComments = this.commentHandling == JsonCommentHandling.Skip;
		long offset = 0;
		long slashPos = -1;
		foreach (ReadOnlyMemory<byte> memory in this.buffer)
		{
			ReadOnlySpan<byte> span = memory.Span;
			for (int i = 0; i < span.Length; i++)
			{
				byte b = span[i];
				long pos = offset + i;
				switch (this.skipState)
				{
					case SkipState.None:
						if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
						{
							continue;
						}

						if (skipComments && b == (byte)'/')
						{
							this.skipState = SkipState.Slash;
							slashPos = pos;
							continue;
						}

						this.buffer = this.buffer.Slice(pos);
						significant = b;
						return true;

					case SkipState.Slash:
						if (b == (byte)'/')
						{
							this.skipState = SkipState.LineComment;
						}
						else if (b == (byte)'*')
						{
							this.skipState = SkipState.BlockComment;
						}
						else
						{
							this.buffer = this.buffer.Slice(slashPos);
							this.skipState = SkipState.None;
							significant = (byte)'/';
							return true;
						}

						continue;

					case SkipState.LineComment:
						if (b is (byte)'\n' or (byte)'\r')
						{
							this.skipState = SkipState.None;
						}

						continue;

					case SkipState.BlockComment:
						if (b == (byte)'*')
						{
							this.skipState = SkipState.BlockCommentStar;
						}

						continue;

					case SkipState.BlockCommentStar:
						if (b == (byte)'/')
						{
							this.skipState = SkipState.None;
						}
						else if (b != (byte)'*')
						{
							this.skipState = SkipState.BlockComment;
						}

						continue;
				}
			}

			offset += span.Length;
		}

		if (this.skipState == SkipState.Slash)
		{
			this.buffer = this.buffer.Slice(slashPos);
			this.skipState = SkipState.None;
		}
		else
		{
			this.buffer = this.buffer.Slice(this.buffer.Length);
		}

		significant = 0;
		return false;
	}

	private async ValueTask<bool> ReadMoreAsync()
	{
		if (this.hasBufferedRead)
		{
			this.pipeReader.AdvanceTo(this.buffer.Start, this.buffer.End);
			this.hasBufferedRead = false;
		}

		if (this.isCompleted)
		{
			return false;
		}

		ReadResult result = await this.pipeReader.ReadAsync(this.CancellationToken).ConfigureAwait(false);
		if (result.IsCanceled)
		{
			throw new OperationCanceledException(this.CancellationToken);
		}

		this.buffer = result.Buffer;
		this.isCompleted = result.IsCompleted;
		this.hasBufferedRead = true;
		return true;
	}

	private void ThrowIfReaderNotReturned()
	{
		if (!this.readerReturned)
		{
			throw new InvalidOperationException("The previous synchronous reader must be returned before using this reader again.");
		}
	}
}
