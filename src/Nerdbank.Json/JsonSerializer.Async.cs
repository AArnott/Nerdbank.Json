// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace Nerdbank.Json;

public partial record JsonSerializer
{
	private static readonly StreamPipeReaderOptions StreamPipeReaderOptions = new(leaveOpen: true);

	private static readonly StreamPipeWriterOptions StreamPipeWriterLeaveOpen = new(leaveOpen: true);

	/// <summary>
	/// Serializes a value as UTF-8 JSON to a <see cref="PipeWriter"/> incrementally, flushing periodically so large
	/// graphs are written with bounded memory and backpressure.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="writer">The pipe to write to. It is flushed but never completed by this method.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task tracking the asynchronous serialization.</returns>
	public ValueTask SerializeAsync<T>(PipeWriter writer, T? value, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(writer);
		Requires.NotNull(shape);

		CancellationToken effectiveCancellationToken = cancellationToken.CanBeCanceled ? cancellationToken : this.StartingContext.CancellationToken;
		return this.SerializeAsyncCore(writer, value, shape, this.StartingContext, effectiveCancellationToken);
	}

	/// <summary>
	/// Serializes a value as UTF-8 JSON to a <see cref="PipeWriter"/> incrementally using a caller-supplied starting context,
	/// flushing periodically so large graphs are written with bounded memory and backpressure.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="writer">The pipe to write to. It is flushed but never completed by this method.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="startingContext">
	/// The context to start this operation with, used instead of <see cref="StartingContext"/>.
	/// Its <see cref="SerializationContext.CancellationToken"/> cancels the operation.
	/// </param>
	/// <returns>A task tracking the asynchronous serialization.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="startingContext"/> belongs to an operation already in progress.</exception>
	/// <remarks>
	/// Use this overload to supply per-call state to converters via <see cref="SerializationContext.this[object]"/>.
	/// </remarks>
	[OverloadResolutionPriority(-1)]
	public ValueTask SerializeAsync<T>(PipeWriter writer, T? value, ITypeShape<T> shape, SerializationContext startingContext)
	{
		Requires.NotNull(writer);
		Requires.NotNull(shape);
		ThrowIfOperationInProgress(startingContext);

		return this.SerializeAsyncCore(writer, value, shape, startingContext, startingContext.CancellationToken);
	}

	/// <summary>
	/// Serializes a value as UTF-8 JSON to a <see cref="Stream"/> incrementally.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="stream">The destination stream. It is flushed but not disposed or closed by this method.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task tracking the asynchronous serialization.</returns>
	public async ValueTask SerializeAsync<T>(Stream stream, T? value, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		Requires.NotNull(shape);

		PipeWriter writer = PipeWriter.Create(stream, StreamPipeWriterLeaveOpen);
		CancellationToken effectiveCancellationToken = cancellationToken.CanBeCanceled ? cancellationToken : this.StartingContext.CancellationToken;
		await CompleteStreamSerializationAsync(writer, this.SerializeAsync(writer, value, shape, cancellationToken), effectiveCancellationToken).ConfigureAwait(false);
	}

	/// <summary>Serializes a value asynchronously to a stream with per-call state.</summary>
	/// <typeparam name="T">The type of value.</typeparam>
	/// <param name="stream">The destination stream, which is left open.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The shape describing the value.</param>
	/// <param name="startingContext">The inactive starting context.</param>
	/// <returns>A task tracking the write.</returns>
	[OverloadResolutionPriority(-1)]
	public async ValueTask SerializeAsync<T>(Stream stream, T? value, ITypeShape<T> shape, SerializationContext startingContext)
	{
		Requires.NotNull(stream);
		Requires.NotNull(shape);
		ThrowIfOperationInProgress(startingContext);
		PipeWriter writer = PipeWriter.Create(stream, StreamPipeWriterLeaveOpen);
		await CompleteStreamSerializationAsync(writer, this.SerializeAsync(writer, value, shape, startingContext), startingContext.CancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Deserializes the first value from UTF-8 JSON read incrementally from a <see cref="PipeReader"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize.</typeparam>
	/// <param name="reader">The pipe to read from. It is advanced but never completed by this method.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task whose result is the deserialized value.</returns>
	/// <exception cref="JsonSerializationException">Thrown if deserialization fails.</exception>
	/// <remarks>Unread bytes remain in the pipe. A scalar requires a token delimiter or end of input before it can be returned.</remarks>
	public ValueTask<T?> DeserializeAsync<T>(PipeReader reader, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(reader);
		Requires.NotNull(shape);

		CancellationToken effectiveCancellationToken = cancellationToken.CanBeCanceled ? cancellationToken : this.StartingContext.CancellationToken;
		return this.DeserializeAsyncCore(reader, shape, this.StartingContext, effectiveCancellationToken);
	}

	/// <summary>
	/// Deserializes a value from UTF-8 JSON read incrementally from a <see cref="PipeReader"/> using a caller-supplied starting context.
	/// </summary>
	/// <typeparam name="T">The type to deserialize.</typeparam>
	/// <param name="reader">The pipe to read from. It is advanced but never completed by this method.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="startingContext">
	/// The context to start this operation with, used instead of <see cref="StartingContext"/>.
	/// Its <see cref="SerializationContext.CancellationToken"/> cancels the operation.
	/// </param>
	/// <returns>A task whose result is the deserialized value.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="startingContext"/> belongs to an operation already in progress.</exception>
	/// <exception cref="JsonSerializationException">Thrown if deserialization fails.</exception>
	/// <remarks>
	/// Use this overload to supply per-call state to converters via <see cref="SerializationContext.this[object]"/>.
	/// </remarks>
	[OverloadResolutionPriority(-1)]
	public ValueTask<T?> DeserializeAsync<T>(PipeReader reader, ITypeShape<T> shape, SerializationContext startingContext)
	{
		Requires.NotNull(reader);
		Requires.NotNull(shape);
		ThrowIfOperationInProgress(startingContext);

		return this.DeserializeAsyncCore(reader, shape, startingContext, startingContext.CancellationToken);
	}

	/// <summary>
	/// Deserializes a value from UTF-8 JSON read incrementally from a <see cref="Stream"/>.
	/// </summary>
	/// <typeparam name="T">The type to deserialize.</typeparam>
	/// <param name="stream">The source stream. It is read but not disposed or closed by this method.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task whose result is the deserialized value.</returns>
	/// <remarks>
	/// Requires a complete JSON document and validates trailing trivia through the end of the stream.
	/// To read successive values without losing prefetched bytes, create and retain a caller-owned <see cref="PipeReader"/>
	/// and use the pipe overloads for all subsequent reads instead of reading the underlying stream directly.
	/// </remarks>
	public async ValueTask<T?> DeserializeAsync<T>(Stream stream, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		Requires.NotNull(shape);

		PipeReader reader = PipeReader.Create(stream, StreamPipeReaderOptions);
		try
		{
			CancellationToken effectiveCancellationToken = cancellationToken.CanBeCanceled ? cancellationToken : this.StartingContext.CancellationToken;
			return await this.DeserializeAsyncCore(reader, shape, this.StartingContext, effectiveCancellationToken, requireFullyConsumed: true).ConfigureAwait(false);
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

#if NET
	/// <inheritdoc cref="SerializeAsync{T}(PipeWriter, T, ITypeShape{T}, CancellationToken)"/>
	public ValueTask SerializeAsync<T>(PipeWriter writer, T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeAsync(writer, value, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="SerializeAsync{T}(PipeWriter, T, ITypeShape{T}, CancellationToken)"/>
	public ValueTask SerializeAsync<T, TProvider>(PipeWriter writer, T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.SerializeAsync(writer, value, TProvider.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="SerializeAsync{T}(Stream, T, ITypeShape{T}, CancellationToken)"/>
	public ValueTask SerializeAsync<T>(Stream stream, T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeAsync(stream, value, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="SerializeAsync{T}(Stream, T, ITypeShape{T}, CancellationToken)"/>
	public ValueTask SerializeAsync<T, TProvider>(Stream stream, T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.SerializeAsync(stream, value, TProvider.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="DeserializeAsync{T}(PipeReader, ITypeShape{T}, CancellationToken)"/>
	public ValueTask<T?> DeserializeAsync<T>(PipeReader reader, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAsync(reader, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="DeserializeAsync{T}(PipeReader, ITypeShape{T}, CancellationToken)"/>
	public ValueTask<T?> DeserializeAsync<T, TProvider>(PipeReader reader, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.DeserializeAsync(reader, TProvider.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="DeserializeAsync{T}(Stream, ITypeShape{T}, CancellationToken)"/>
	public ValueTask<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAsync(stream, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="DeserializeAsync{T}(Stream, ITypeShape{T}, CancellationToken)"/>
	public ValueTask<T?> DeserializeAsync<T, TProvider>(Stream stream, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.DeserializeAsync(stream, TProvider.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="SerializeAsync{T}(PipeWriter, T, ITypeShape{T}, SerializationContext)"/>
	[OverloadResolutionPriority(-1)]
	public ValueTask SerializeAsync<T>(PipeWriter writer, T? value, SerializationContext startingContext)
		where T : IShapeable<T> => this.SerializeAsync(writer, value, T.GetTypeShape(), startingContext);

	/// <inheritdoc cref="SerializeAsync{T}(PipeWriter, T, ITypeShape{T}, SerializationContext)"/>
	[OverloadResolutionPriority(-1)]
	public ValueTask SerializeAsync<T, TProvider>(PipeWriter writer, T? value, SerializationContext startingContext)
		where TProvider : IShapeable<T> => this.SerializeAsync(writer, value, TProvider.GetTypeShape(), startingContext);

	/// <inheritdoc cref="SerializeAsync{T}(Stream, T, ITypeShape{T}, SerializationContext)"/>
	[OverloadResolutionPriority(-1)]
	public ValueTask SerializeAsync<T>(Stream stream, T? value, SerializationContext startingContext)
		where T : IShapeable<T> => this.SerializeAsync(stream, value, T.GetTypeShape(), startingContext);

	/// <inheritdoc cref="SerializeAsync{T}(Stream, T, ITypeShape{T}, SerializationContext)"/>
	[OverloadResolutionPriority(-1)]
	public ValueTask SerializeAsync<T, TProvider>(Stream stream, T? value, SerializationContext startingContext)
		where TProvider : IShapeable<T> => this.SerializeAsync(stream, value, TProvider.GetTypeShape(), startingContext);

	/// <inheritdoc cref="DeserializeAsync{T}(PipeReader, ITypeShape{T}, SerializationContext)"/>
	[OverloadResolutionPriority(-1)]
	public ValueTask<T?> DeserializeAsync<T>(PipeReader reader, SerializationContext startingContext)
		where T : IShapeable<T> => this.DeserializeAsync(reader, T.GetTypeShape(), startingContext);

	/// <inheritdoc cref="DeserializeAsync{T}(PipeReader, ITypeShape{T}, SerializationContext)"/>
	[OverloadResolutionPriority(-1)]
	public ValueTask<T?> DeserializeAsync<T, TProvider>(PipeReader reader, SerializationContext startingContext)
		where TProvider : IShapeable<T> => this.DeserializeAsync(reader, TProvider.GetTypeShape(), startingContext);

	/// <inheritdoc cref="DeserializeAsync{T}(Stream, ITypeShape{T}, SerializationContext)"/>
	[OverloadResolutionPriority(-1)]
	public ValueTask<T?> DeserializeAsync<T>(Stream stream, SerializationContext startingContext)
		where T : IShapeable<T> => this.DeserializeAsync(stream, T.GetTypeShape(), startingContext);

	/// <inheritdoc cref="DeserializeAsync{T}(Stream, ITypeShape{T}, SerializationContext)"/>
	[OverloadResolutionPriority(-1)]
	public ValueTask<T?> DeserializeAsync<T, TProvider>(Stream stream, SerializationContext startingContext)
		where TProvider : IShapeable<T> => this.DeserializeAsync(stream, TProvider.GetTypeShape(), startingContext);
#endif

	/// <summary>Deserializes a value asynchronously from a stream with per-call state.</summary>
	/// <typeparam name="T">The type of value.</typeparam>
	/// <param name="stream">The source stream, which is left open.</param>
	/// <param name="shape">The shape describing the value.</param>
	/// <param name="startingContext">The inactive starting context.</param>
	/// <returns>The deserialized value.</returns>
	/// <remarks>
	/// Requires a complete JSON document and validates trailing trivia through the end of the stream.
	/// Use a retained caller-owned <see cref="PipeReader"/> for successive first-value reads.
	/// </remarks>
	[OverloadResolutionPriority(-1)]
	public async ValueTask<T?> DeserializeAsync<T>(Stream stream, ITypeShape<T> shape, SerializationContext startingContext)
	{
		Requires.NotNull(stream);
		Requires.NotNull(shape);
		ThrowIfOperationInProgress(startingContext);
		PipeReader reader = PipeReader.Create(stream, StreamPipeReaderOptions);
		try
		{
			return await this.DeserializeAsyncCore(reader, shape, startingContext, startingContext.CancellationToken, requireFullyConsumed: true).ConfigureAwait(false);
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	private static async ValueTask CompleteStreamSerializationAsync(PipeWriter writer, ValueTask operation, CancellationToken cancellationToken)
	{
		try
		{
			try
			{
				await operation.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// Completing with the failure releases buffers without retrying writes or flushing partial JSON.
				await writer.CompleteAsync(ex).ConfigureAwait(false);
				throw;
			}

			await writer.CompleteAsync().ConfigureAwait(false);
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, cancellationToken))
		{
			throw new JsonSerializationException("An error occurred during serialization.", ex);
		}
	}

	private async ValueTask SerializeAsyncCore<T>(PipeWriter writer, T? value, ITypeShape<T> shape, SerializationContext startingContext, CancellationToken cancellationToken)
	{
		SerializationContext context = startingContext.Start(this, this.ConverterCache, cancellationToken) with { TypeShapeProvider = shape.Provider };
		try
		{
			JsonAsyncWriter asyncWriter = new(writer, this.WriteIndented);
			JsonConverter<T> converter = this.ConverterCache.GetOrAddConverter(shape);
			await converter.WriteAsync(asyncWriter, value, context).ConfigureAwait(false);
			await asyncWriter.FlushAsync(context.CancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException("An error occurred during serialization.", ex);
		}
	}

	private async ValueTask<T?> DeserializeAsyncCore<T>(PipeReader reader, ITypeShape<T> shape, SerializationContext startingContext, CancellationToken cancellationToken, bool requireFullyConsumed = false)
	{
		SerializationContext context = startingContext.Start(this, this.ConverterCache, cancellationToken) with { TypeShapeProvider = shape.Provider };
		try
		{
			AsyncBufferPlan plan = await this.TryBufferWithinMaxAsyncBufferAsync(reader, context, requireFullyConsumed).ConfigureAwait(false);
			if (plan.Synchronous is { } buffered && this.PreserveReferences != ReferencePreservationMode.AllowCycles)
			{
				try
				{
					return this.ReadBuffered(reader, buffered.Buffer, shape, context, requireFullyConsumed);
				}
				catch (NotSupportedException)
				{
					// A converter supports only the incremental reader. Drop any partial reference or interning state
					// before that reader runs, so the retry is not rejected as a duplicate reference.
					context = context.Start(this, this.ConverterCache, context.CancellationToken);
				}
				finally
				{
					if (plan.DrainSpeculativeCancellation)
					{
						await this.ReleaseSpeculativeCancellationAsync(reader, context).ConfigureAwait(false);
					}
				}
			}
			else
			{
				await this.ReleaseUnusedBufferedReadAsync(reader, plan, context).ConfigureAwait(false);
			}

			using JsonAsyncReader asyncReader = new(plan.Reader ?? reader, this.AllowTrailingCommas, this.ReadCommentHandling)
			{
				CancellationToken = context.CancellationToken,
			};
			JsonConverter<T> converter = this.ConverterCache.GetOrAddConverter(shape);
			T? result = await converter.ReadAsync(asyncReader, context).ConfigureAwait(false);
			if (requireFullyConsumed)
			{
				await asyncReader.EnsureFullyConsumedAsync(context).ConfigureAwait(false);
			}

			return result;
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException("An error occurred during deserialization.", ex);
		}
	}

	/// <summary>
	/// Buffers a complete JSON value for synchronous deserialization when one is already available within <see cref="MaxAsyncBuffer"/>.
	/// </summary>
	/// <param name="reader">The pipe to read.</param>
	/// <param name="context">The active serialization context.</param>
	/// <param name="requireFullyConsumed">
	/// <see langword="true"/> when the caller will reject trailing data, so a complete value is not enough until the reader is exhausted.
	/// </param>
	/// <returns>
	/// A plan whose <see cref="AsyncBufferPlan.Synchronous"/> read is outstanding when the caller should deserialize synchronously.
	/// <see cref="AsyncBufferPlan.Reader"/> is set when incremental deserialization must observe a prefix that is still buffered in <paramref name="reader"/>.
	/// </returns>
	private async ValueTask<AsyncBufferPlan> TryBufferWithinMaxAsyncBufferAsync(PipeReader reader, SerializationContext context, bool requireFullyConsumed)
	{
		if (this.MaxAsyncBuffer <= 0)
		{
			return default;
		}

		ReadResult readResult = await reader.ReadAsync(context.CancellationToken).ConfigureAwait(false);
		bool outstanding = true;
		try
		{
			JsonStructureScanner scanner = default;
			scanner.Reset(preserveBomState: true);
			long scanned = 0;

			// Framing counts JSON containers, including reference envelopes that do not consume converter graph depth.
			// Leave MaxDepth enforcement to deserialization.
			SerializationContext framing = context with { MaxDepth = int.MaxValue };
			while (true)
			{
				if (readResult.IsCanceled)
				{
					context.CancellationToken.ThrowIfCancellationRequested();
					throw new OperationCanceledException(context.CancellationToken);
				}

				if (IsSynchronousCandidate(readResult))
				{
					outstanding = false;
					return new AsyncBufferPlan(readResult);
				}

				if (readResult.Buffer.Length >= this.MaxAsyncBuffer)
				{
					// This read is still outstanding, so releasing it at the start leaves the buffered prefix visible.
					reader.AdvanceTo(readResult.Buffer.Start);
					outstanding = false;
					return default;
				}

				long bufferedLength = readResult.Buffer.Length;
				reader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.End);
				outstanding = false;

				ValueTask<ReadResult> pending = reader.ReadAsync(context.CancellationToken);
				if (!pending.IsCompleted)
				{
					// Waiting here would stall a caller that writes the rest of the value only after parsing starts.
					// CancelPendingRead on a stream reader can also mark the following read canceled.
					reader.CancelPendingRead();
					readResult = await pending.ConfigureAwait(false);
					outstanding = true;
					context.CancellationToken.ThrowIfCancellationRequested();
					if (!readResult.IsCanceled)
					{
						if (IsSynchronousCandidate(readResult))
						{
							// The in-flight read won and already holds a usable value. Return it before another read,
							// and let the caller drain the one-shot cancellation without waiting for more bytes.
							outstanding = false;
							return new AsyncBufferPlan(readResult, drainSpeculativeCancellation: true);
						}

						// The value is still incomplete. Drain the one-shot cancellation, then keep the bytes
						// that are already buffered. This matches the previous stream-reader recovery path.
						reader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.End);
						outstanding = false;
						ReadResult followUp = await reader.ReadAsync(context.CancellationToken).ConfigureAwait(false);
						outstanding = true;
						if (followUp.IsCanceled)
						{
							reader.AdvanceTo(followUp.Buffer.Start, followUp.Buffer.End);
							outstanding = false;
							readResult = await reader.ReadAsync(context.CancellationToken).ConfigureAwait(false);
							outstanding = true;
						}
						else
						{
							readResult = followUp;
						}

						continue;
					}

					if (IsSynchronousCandidate(readResult))
					{
						outstanding = false;
						return new AsyncBufferPlan(readResult);
					}

					if (readResult.Buffer.Length == 0)
					{
						return default;
					}

					byte[] prefix = Copy(readResult.Buffer);
					reader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.End);
					outstanding = false;
					return new AsyncBufferPlan(new ReplayPipeReader(prefix, reader));
				}

				readResult = await pending.ConfigureAwait(false);
				outstanding = true;
				if (!readResult.IsCompleted && !readResult.IsCanceled && readResult.Buffer.Length <= bufferedLength)
				{
					reader.AdvanceTo(readResult.Buffer.Start);
					outstanding = false;
					return default;
				}
			}

			bool IsSynchronousCandidate(ReadResult result)
			{
				if (result.IsCompleted)
				{
					return true;
				}

				if (requireFullyConsumed)
				{
					return false;
				}

				return ScanAdditional(result.Buffer, ref scanner, ref scanned, framing) && scanned <= this.MaxAsyncBuffer;
			}
		}
		finally
		{
			if (outstanding)
			{
				reader.AdvanceTo(readResult.Buffer.Start);
			}
		}

		static bool ScanAdditional(ReadOnlySequence<byte> buffer, ref JsonStructureScanner scanner, ref long scanned, SerializationContext context)
		{
			if (scanned > buffer.Length)
			{
				scanner = default;
				scanner.Reset(preserveBomState: true);
				scanned = 0;
			}

			ReadOnlySequence<byte> tail = buffer.Slice(scanned);
			long offset = scanned;
			foreach (ReadOnlyMemory<byte> segment in tail)
			{
				if (scanner.Scan(segment.Span, out int consumed, context))
				{
					scanned = offset + consumed;
					return true;
				}

				offset += segment.Length;
			}

			scanned = buffer.Length;
			return false;
		}

		static byte[] Copy(ReadOnlySequence<byte> buffer)
		{
			byte[] copy = new byte[checked((int)buffer.Length)];
			buffer.CopyTo(copy);
			return copy;
		}
	}

	/// <summary>
	/// Releases a buffered read that will not be consumed synchronously, including a one-shot cancellation from <see cref="PipeReader.CancelPendingRead"/>.
	/// </summary>
	/// <param name="reader">The pipe that owns the buffered read.</param>
	/// <param name="plan">The buffering plan.</param>
	/// <param name="context">The active serialization context.</param>
	/// <returns>A task that completes when the unused read has been released.</returns>
	private async ValueTask ReleaseUnusedBufferedReadAsync(PipeReader reader, AsyncBufferPlan plan, SerializationContext context)
	{
		if (plan.Synchronous is { } buffered)
		{
			reader.AdvanceTo(buffered.Buffer.Start);
		}

		if (plan.DrainSpeculativeCancellation)
		{
			await this.ReleaseSpeculativeCancellationAsync(reader, context).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Observes the read that follows a speculative <see cref="PipeReader.CancelPendingRead"/> without consuming its bytes.
	/// </summary>
	/// <param name="reader">The pipe whose next read may be canceled.</param>
	/// <param name="context">The active serialization context.</param>
	/// <returns>A task that completes when that read has been released.</returns>
	private async ValueTask ReleaseSpeculativeCancellationAsync(PipeReader reader, SerializationContext context)
	{
		ValueTask<ReadResult> pending = reader.ReadAsync(context.CancellationToken);
		if (!pending.IsCompleted)
		{
			reader.CancelPendingRead();
		}

		ReadResult followUp = await pending.ConfigureAwait(false);
		context.CancellationToken.ThrowIfCancellationRequested();
		reader.AdvanceTo(followUp.Buffer.Start);
	}

	private T? ReadBuffered<T>(PipeReader reader, ReadOnlySequence<byte> utf8Json, ITypeShape<T> shape, SerializationContext context, bool requireFullyConsumed)
	{
		SequencePosition consumed = utf8Json.Start;
		try
		{
			JsonReader jsonReader = new(utf8Json, this.AllowTrailingCommas, this.ReadCommentHandling);
			T? result = this.ConverterCache.GetOrAddConverter(shape).Read(ref jsonReader, context);
			consumed = jsonReader.Position;
			if (requireFullyConsumed)
			{
				jsonReader.EnsureFullyConsumed();
				consumed = jsonReader.Position;
			}

			return result;
		}
		finally
		{
			reader.AdvanceTo(consumed);
		}
	}

	private object? ReadBufferedObject(PipeReader reader, ReadOnlySequence<byte> utf8Json, ITypeShape shape, SerializationContext context, bool requireFullyConsumed)
	{
		SequencePosition consumed = utf8Json.Start;
		try
		{
			JsonReader jsonReader = new(utf8Json, this.AllowTrailingCommas, this.ReadCommentHandling);
			object? result = this.ConverterCache.GetOrAddConverter(shape).ReadObject(ref jsonReader, context);
			consumed = jsonReader.Position;
			if (requireFullyConsumed)
			{
				jsonReader.EnsureFullyConsumed();
				consumed = jsonReader.Position;
			}

			return result;
		}
		finally
		{
			reader.AdvanceTo(consumed);
		}
	}

	private JsonValue? ReadBufferedJsonValue(PipeReader reader, ReadOnlySequence<byte> utf8Json, SerializationContext context, bool requireFullyConsumed)
	{
		SequencePosition consumed = utf8Json.Start;
		try
		{
			JsonReader jsonReader = new(utf8Json, this.AllowTrailingCommas, this.ReadCommentHandling);
			JsonValue? result = JsonValueConverter.Instance.Read(ref jsonReader, context);
			consumed = jsonReader.Position;
			if (requireFullyConsumed)
			{
				jsonReader.EnsureFullyConsumed();
				consumed = jsonReader.Position;
			}

			return result;
		}
		finally
		{
			reader.AdvanceTo(consumed);
		}
	}

	private readonly struct AsyncBufferPlan
	{
		internal AsyncBufferPlan(ReadResult synchronous, bool drainSpeculativeCancellation = false)
		{
			this.Synchronous = synchronous;
			this.DrainSpeculativeCancellation = drainSpeculativeCancellation;
		}

		internal AsyncBufferPlan(PipeReader reader) => this.Reader = reader;

		internal ReadResult? Synchronous { get; }

		internal PipeReader? Reader { get; }

		internal bool DrainSpeculativeCancellation { get; }
	}

	/// <summary>
	/// Replays bytes already consumed from an inner reader, then continues with that reader.
	/// </summary>
	/// <remarks>
	/// A pipe does not make examined bytes readable again after <see cref="PipeReader.CancelPendingRead"/>.
	/// This reader keeps that prefix available to <see cref="JsonAsyncReader"/> and forwards later reads.
	/// </remarks>
	private sealed class ReplayPipeReader : PipeReader
	{
		private readonly byte[] prefix;
		private readonly PipeReader inner;
		private int prefixConsumed;
		private bool pullInner;
		private bool prefixOutstanding;
		private bool innerOutstanding;
		private bool skippedPrefix;

		internal ReplayPipeReader(byte[] prefix, PipeReader inner)
		{
			this.prefix = prefix;
			this.inner = inner;
		}

		public override void AdvanceTo(SequencePosition consumed) => this.AdvanceTo(consumed, consumed);

		public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
		{
			if (this.prefixOutstanding)
			{
				this.prefixOutstanding = false;
				this.prefixConsumed = this.AbsolutePrefixIndex(consumed);
				this.pullInner = this.AbsolutePrefixIndex(examined) >= this.prefix.Length;
				return;
			}

			if (!this.innerOutstanding)
			{
				throw new InvalidOperationException("AdvanceTo was called before ReadAsync.");
			}

			this.innerOutstanding = false;
			this.pullInner = true;
			this.inner.AdvanceTo(consumed, examined);
		}

		public override void CancelPendingRead() => this.inner.CancelPendingRead();

		public override void Complete(Exception? exception = null) => this.inner.Complete(exception);

		public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
		{
			if (this.prefixOutstanding || this.innerOutstanding)
			{
				throw new InvalidOperationException("AdvanceTo was not called.");
			}

			cancellationToken.ThrowIfCancellationRequested();
			if (!this.pullInner && this.prefixConsumed < this.prefix.Length)
			{
				return new ValueTask<ReadResult>(this.ReadPrefix());
			}

			return this.ReadInnerAsync(cancellationToken);
		}

		public override bool TryRead(out ReadResult result) => throw new NotSupportedException();

		private async ValueTask<ReadResult> ReadInnerAsync(CancellationToken cancellationToken)
		{
			ReadResult innerResult = await this.inner.ReadAsync(cancellationToken).ConfigureAwait(false);
			this.innerOutstanding = true;
			if (this.skippedPrefix)
			{
				return innerResult;
			}

			this.skippedPrefix = true;
			long skip = Math.Min(this.prefixConsumed, innerResult.Buffer.Length);
			return new ReadResult(innerResult.Buffer.Slice(skip), innerResult.IsCanceled, innerResult.IsCompleted);
		}

		private ReadResult ReadPrefix()
		{
			Segment segment = new(this.prefix.AsMemory(this.prefixConsumed), 0)
			{
				PrefixBase = this.prefixConsumed,
			};
			this.prefixOutstanding = true;
			ReadOnlySequence<byte> sequence = new(segment, 0, segment, segment.Memory.Length);
			return new ReadResult(sequence, isCanceled: false, isCompleted: false);
		}

		private int AbsolutePrefixIndex(SequencePosition position)
		{
			if (position.GetObject() is not Segment segment)
			{
				return 0;
			}

			return segment.PrefixBase + position.GetInteger();
		}

		private sealed class Segment : ReadOnlySequenceSegment<byte>
		{
			internal Segment(ReadOnlyMemory<byte> memory, long runningIndex)
			{
				this.Memory = memory;
				this.RunningIndex = runningIndex;
			}

			internal int PrefixBase { get; init; }
		}
	}
}
