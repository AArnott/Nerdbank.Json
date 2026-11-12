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

	private static SequencePosition PositionAt(ReadOnlySequence<byte> buffer, long offset)
	{
		if (offset <= 0)
		{
			return buffer.Start;
		}

		if (offset >= buffer.Length)
		{
			return buffer.End;
		}

		return buffer.GetPosition(offset);
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
			JsonConverter<T> converter = this.ConverterCache.GetOrAddConverter(shape);
			AsyncBufferPlan plan = await this.TryBufferWithinMaxAsyncBufferAsync(reader, context, requireFullyConsumed).ConfigureAwait(false);
			if (plan.Synchronous is { } buffered
				&& this.PreserveReferences != ReferencePreservationMode.AllowCycles)
			{
				bool readFailed = true;
				long unconsumedExamined = 0;
				try
				{
					T? bufferedResult = this.ReadBuffered(reader, buffered.Buffer, shape, context, requireFullyConsumed, plan.ExaminedFloor, out unconsumedExamined);
					readFailed = false;
					return bufferedResult;
				}
				finally
				{
					if (plan.DrainSpeculativeCancellation)
					{
						await this.ReleaseSpeculativeCancellationAsync(reader, context, readFailed ? unconsumedExamined : 0).ConfigureAwait(false);
					}
				}
			}

			PipeReader incremental = await this.OpenIncrementalReaderAsync(reader, plan, context).ConfigureAwait(false);
			try
			{
				using JsonAsyncReader asyncReader = new(incremental, this.AllowTrailingCommas, this.ReadCommentHandling)
				{
					CancellationToken = context.CancellationToken,
				};
				T? result = await converter.ReadAsync(asyncReader, context).ConfigureAwait(false);
				if (requireFullyConsumed)
				{
					await asyncReader.EnsureFullyConsumedAsync(context).ConfigureAwait(false);
				}

				return result;
			}
			finally
			{
				if (incremental is ForwardingPipeReader forwarding)
				{
					await forwarding.DrainSpeculativeCancellationAsync().ConfigureAwait(false);
				}
			}
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
		long examinedFloor = 0;
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
					return new AsyncBufferPlan(readResult, examinedFloor: examinedFloor);
				}

				if (readResult.Buffer.Length >= this.MaxAsyncBuffer)
				{
					// Keep this read outstanding. AdvanceTo(Start) would move examined backward after a previous
					// AdvanceTo(start, end) and a standard pipe would throw.
					return Handoff(readResult);
				}

				long bufferedLength = readResult.Buffer.Length;
				reader.AdvanceTo(readResult.Buffer.Start, readResult.Buffer.End);
				examinedFloor = readResult.Buffer.Length;
				outstanding = false;

				ValueTask<ReadResult> pending = reader.ReadAsync(context.CancellationToken);
				if (!pending.IsCompleted && requireFullyConsumed)
				{
					// A stream overload must observe the whole payload, including trailing comments.
					// Canceling this read reports failure even though the stream still has those bytes.
					readResult = await pending.ConfigureAwait(false);
					outstanding = true;
					continue;
				}

				if (!pending.IsCompleted)
				{
					// Waiting here would stall a caller that writes the rest of the value only after parsing starts.
					// CancelPendingRead on a stream reader can also mark the following read canceled.
					reader.CancelPendingRead();
					readResult = await pending.ConfigureAwait(false);
					outstanding = true;
					context.CancellationToken.ThrowIfCancellationRequested();
					if (!readResult.IsCanceled && IsSynchronousCandidate(readResult))
					{
						// The in-flight read won and already holds a usable value. Return it before another read,
						// and let the caller drain the one-shot cancellation without waiting for more bytes.
						outstanding = false;
						return new AsyncBufferPlan(readResult, drainSpeculativeCancellation: true, examinedFloor: examinedFloor);
					}

					if (!readResult.IsCanceled)
					{
						// The raced buffer may hold a complete value larger than the cap, plus a later value.
						// Release the read at the prefix already examined so those later bytes stay readable.
						reader.AdvanceTo(readResult.Buffer.Start, PositionAt(readResult.Buffer, examinedFloor));
						outstanding = false;

						ValueTask<ReadResult> followUp = reader.ReadAsync(context.CancellationToken);
						if (!followUp.IsCompleted)
						{
							// This reader did not surface the cancellation immediately. Cancel once so the bytes
							// already buffered can be handed off, and let the wrapper absorb the one poisoned read.
							reader.CancelPendingRead();
							readResult = await followUp.ConfigureAwait(false);
							outstanding = true;
							context.CancellationToken.ThrowIfCancellationRequested();
							return Handoff(readResult, drainCanceledFollowUp: true);
						}

						readResult = await followUp.ConfigureAwait(false);
						outstanding = true;
						context.CancellationToken.ThrowIfCancellationRequested();
						return Handoff(readResult);
					}

					if (IsSynchronousCandidate(readResult))
					{
						outstanding = false;
						return new AsyncBufferPlan(readResult, examinedFloor: examinedFloor);
					}

					if (readResult.Buffer.IsEmpty)
					{
						return default;
					}

					return Handoff(readResult);
				}

				readResult = await pending.ConfigureAwait(false);
				outstanding = true;
				if (!readResult.IsCompleted && !readResult.IsCanceled && readResult.Buffer.Length <= bufferedLength)
				{
					return Handoff(readResult);
				}
			}

			AsyncBufferPlan Handoff(ReadResult result, bool drainCanceledFollowUp = false)
			{
				outstanding = false;
				ReadResult visible = new(result.Buffer, isCanceled: false, result.IsCompleted);
				return new AsyncBufferPlan(new ForwardingPipeReader(visible, reader, examinedFloor, drainCanceledFollowUp));
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

				// Cap plus one byte so a scalar terminator can be seen without scanning an entire open buffer.
				return ScanAdditional(result.Buffer, ref scanner, ref scanned, framing, this.MaxAsyncBuffer + 1L)
					&& scanned <= this.MaxAsyncBuffer;
			}
		}
		finally
		{
			if (outstanding)
			{
				// Examined cannot move backward. When a previous iteration already examined a prefix, release
				// this read at the end of the buffer, which is at or past that prefix.
				SequencePosition examined = readResult.Buffer.End;
				reader.AdvanceTo(readResult.Buffer.Start, examinedFloor > 0 ? examined : readResult.Buffer.Start);
			}
		}

		static bool ScanAdditional(ReadOnlySequence<byte> buffer, ref JsonStructureScanner scanner, ref long scanned, SerializationContext context, long scanLimit)
		{
			if (scanned > buffer.Length)
			{
				scanner = default;
				scanner.Reset(preserveBomState: true);
				scanned = 0;
			}

			if (scanned >= scanLimit)
			{
				return false;
			}

			ReadOnlySequence<byte> tail = buffer.Slice(scanned, Math.Min(buffer.Length, scanLimit) - scanned);
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

			scanned = offset;
			return false;
		}
	}

	/// <summary>
	/// Returns the reader incremental deserialization should use, without moving an examined cursor backward.
	/// </summary>
	/// <param name="reader">The pipe that owns the buffered read.</param>
	/// <param name="plan">The buffering plan.</param>
	/// <param name="context">The active serialization context.</param>
	/// <returns>The reader that observes the buffered prefix.</returns>
	private async ValueTask<PipeReader> OpenIncrementalReaderAsync(PipeReader reader, AsyncBufferPlan plan, SerializationContext context)
	{
		if (plan.Reader is not null)
		{
			return plan.Reader;
		}

		if (plan.Synchronous is { } buffered)
		{
			return new ForwardingPipeReader(buffered, reader, plan.ExaminedFloor, plan.DrainSpeculativeCancellation);
		}

		if (plan.DrainSpeculativeCancellation)
		{
			await this.ReleaseSpeculativeCancellationAsync(reader, context, examinedOffset: 0).ConfigureAwait(false);
		}

		return reader;
	}

	/// <summary>
	/// Observes the read that follows a speculative <see cref="PipeReader.CancelPendingRead"/> without consuming its bytes.
	/// </summary>
	/// <param name="reader">The pipe whose next read may be canceled.</param>
	/// <param name="context">The active serialization context.</param>
	/// <param name="examinedOffset">
	/// The number of bytes at the start of the follow-up that were already examined. Bytes beyond this offset stay
	/// unexamined so a later read can observe them without waiting for more input.
	/// </param>
	/// <returns>A task that completes when that read has been released.</returns>
	private async ValueTask ReleaseSpeculativeCancellationAsync(PipeReader reader, SerializationContext context, long examinedOffset)
	{
		ValueTask<ReadResult> pending = reader.ReadAsync(context.CancellationToken);
		if (!pending.IsCompleted)
		{
			reader.CancelPendingRead();
		}

		ReadResult followUp = await pending.ConfigureAwait(false);
		context.CancellationToken.ThrowIfCancellationRequested();
		reader.AdvanceTo(followUp.Buffer.Start, PositionAt(followUp.Buffer, examinedOffset));
	}

	private T? ReadBuffered<T>(PipeReader reader, ReadOnlySequence<byte> utf8Json, ITypeShape<T> shape, SerializationContext context, bool requireFullyConsumed, long examinedFloor, out long unconsumedExamined)
	{
		SequencePosition consumed = utf8Json.Start;
		unconsumedExamined = 0;
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
			unconsumedExamined = this.AdvanceBufferedRead(reader, utf8Json, consumed, examinedFloor);
		}
	}

	private object? ReadBufferedObject(PipeReader reader, ReadOnlySequence<byte> utf8Json, ITypeShape shape, SerializationContext context, bool requireFullyConsumed, long examinedFloor, out long unconsumedExamined)
	{
		SequencePosition consumed = utf8Json.Start;
		unconsumedExamined = 0;
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
			unconsumedExamined = this.AdvanceBufferedRead(reader, utf8Json, consumed, examinedFloor);
		}
	}

	private JsonValue? ReadBufferedJsonValue(PipeReader reader, ReadOnlySequence<byte> utf8Json, SerializationContext context, bool requireFullyConsumed, long examinedFloor, out long unconsumedExamined)
	{
		SequencePosition consumed = utf8Json.Start;
		unconsumedExamined = 0;
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
			unconsumedExamined = this.AdvanceBufferedRead(reader, utf8Json, consumed, examinedFloor);
		}
	}

	private long AdvanceBufferedRead(PipeReader reader, ReadOnlySequence<byte> utf8Json, SequencePosition consumed, long examinedFloor)
	{
		SequencePosition examined = consumed;
		long consumedLength = utf8Json.Slice(0, consumed).Length;
		long unconsumedExamined = 0;
		if (consumedLength < examinedFloor && examinedFloor <= utf8Json.Length)
		{
			examined = utf8Json.GetPosition(examinedFloor);
			unconsumedExamined = examinedFloor - consumedLength;
		}

		reader.AdvanceTo(consumed, examined);
		return unconsumedExamined;
	}

	private readonly struct AsyncBufferPlan
	{
		internal AsyncBufferPlan(ReadResult synchronous, bool drainSpeculativeCancellation = false, long examinedFloor = 0)
		{
			this.Synchronous = synchronous;
			this.DrainSpeculativeCancellation = drainSpeculativeCancellation;
			this.ExaminedFloor = examinedFloor;
		}

		internal AsyncBufferPlan(PipeReader reader) => this.Reader = reader;

		internal ReadResult? Synchronous { get; }

		internal PipeReader? Reader { get; }

		internal bool DrainSpeculativeCancellation { get; }

		internal long ExaminedFloor { get; }
	}

	/// <summary>
	/// Serves one outstanding read, then continues on the inner reader.
	/// </summary>
	/// <remarks>
	/// Positions belong to the inner reader, so <see cref="AdvanceTo(SequencePosition, SequencePosition)"/> releases
	/// consumed bytes there. The examined cursor is never moved earlier than <see cref="examinedFloor"/>.
	/// </remarks>
	private sealed class ForwardingPipeReader : PipeReader
	{
		private readonly PipeReader inner;
		private readonly long examinedFloor;
		private readonly bool drainCanceledFollowUp;
		private ReadOnlySequence<byte> pendingBuffer;
		private ReadOnlySequence<byte> activeBuffer;
		private ReadResult? pending;
		private long activeOrigin;
		private long consumedLength;
		private long examinedLength;
		private bool servingPending;
		private bool drainedCancellation;
		private bool innerOutstanding;

		internal ForwardingPipeReader(ReadResult pending, PipeReader inner, long examinedFloor, bool drainCanceledFollowUp)
		{
			// A speculative CancelPendingRead can mark a usable buffer canceled. The caller did not cancel.
			this.pending = new ReadResult(pending.Buffer, isCanceled: false, pending.IsCompleted);
			this.pendingBuffer = pending.Buffer;
			this.inner = inner;
			this.examinedFloor = examinedFloor;
			this.examinedLength = examinedFloor;
			this.drainCanceledFollowUp = drainCanceledFollowUp;
		}

		public override void AdvanceTo(SequencePosition consumed) => this.AdvanceTo(consumed, consumed);

		public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
		{
			if (this.servingPending)
			{
				this.servingPending = false;
				examined = this.ClampExamined(this.pendingBuffer, examined);
				this.Remember(this.pendingBuffer, this.activeOrigin, consumed, examined);
				this.inner.AdvanceTo(consumed, examined);
				return;
			}

			if (!this.innerOutstanding)
			{
				throw new InvalidOperationException("AdvanceTo was called before ReadAsync.");
			}

			this.innerOutstanding = false;
			this.Remember(this.activeBuffer, this.activeOrigin, consumed, examined);
			this.inner.AdvanceTo(consumed, examined);
		}

		public override void CancelPendingRead() => this.inner.CancelPendingRead();

		public override void Complete(Exception? exception = null) => this.inner.Complete(exception);

		public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
		{
			if (this.servingPending || this.innerOutstanding)
			{
				throw new InvalidOperationException("AdvanceTo was not called.");
			}

			cancellationToken.ThrowIfCancellationRequested();
			if (this.pending is { } buffered)
			{
				this.pending = null;
				this.servingPending = true;
				this.activeBuffer = buffered.Buffer;
				this.activeOrigin = 0;
				return new ValueTask<ReadResult>(buffered);
			}

			return this.ReadInnerAsync(cancellationToken);
		}

		public override bool TryRead(out ReadResult result) => throw new NotSupportedException();

		internal async ValueTask DrainSpeculativeCancellationAsync()
		{
			// A converter can fail before it reads. Release that outstanding buffer before any other read,
			// so the failure is not replaced by "a read is already in progress".
			if (this.pending is { } buffered)
			{
				this.pending = null;
				SequencePosition examined = JsonSerializer.PositionAt(buffered.Buffer, this.examinedLength);
				this.inner.AdvanceTo(buffered.Buffer.Start, examined);
				this.consumedLength = 0;
			}

			if (!this.drainCanceledFollowUp || this.drainedCancellation || this.servingPending || this.innerOutstanding)
			{
				return;
			}

			this.drainedCancellation = true;
			ValueTask<ReadResult> pendingRead = this.inner.ReadAsync();
			if (!pendingRead.IsCompleted)
			{
				this.inner.CancelPendingRead();
			}

			ReadResult followUp = await pendingRead.ConfigureAwait(false);
			long alreadyExamined = this.examinedLength - this.consumedLength;
			if (alreadyExamined < 0)
			{
				alreadyExamined = 0;
			}

			// The follow-up starts at the consumed cursor. Examining only the prefix the parser already
			// saw leaves a later value in this buffer readable by the caller.
			this.inner.AdvanceTo(followUp.Buffer.Start, JsonSerializer.PositionAt(followUp.Buffer, alreadyExamined));
		}

		private void Remember(ReadOnlySequence<byte> buffer, long origin, SequencePosition consumed, SequencePosition examined)
		{
			this.consumedLength = origin + buffer.Slice(0, consumed).Length;
			this.examinedLength = origin + buffer.Slice(0, examined).Length;
		}

		private SequencePosition ClampExamined(ReadOnlySequence<byte> buffer, SequencePosition examined)
		{
			if (this.examinedFloor <= 0 || this.examinedFloor > buffer.Length)
			{
				return examined;
			}

			if (buffer.Slice(0, examined).Length >= this.examinedFloor)
			{
				return examined;
			}

			return buffer.GetPosition(this.examinedFloor);
		}

		private async ValueTask<ReadResult> ReadInnerAsync(CancellationToken cancellationToken)
		{
			ReadResult result = await this.inner.ReadAsync(cancellationToken).ConfigureAwait(false);
			this.innerOutstanding = true;
			if (this.drainCanceledFollowUp && !this.drainedCancellation && result.IsCanceled)
			{
				this.drainedCancellation = true;
				if (cancellationToken.IsCancellationRequested)
				{
					this.ReleaseOutstanding(result.Buffer);
					cancellationToken.ThrowIfCancellationRequested();
				}

				// The canceled read can be the one that delivered the rest of the value. Keep those bytes.
				result = new ReadResult(result.Buffer, isCanceled: false, result.IsCompleted);
			}

			this.activeBuffer = result.Buffer;
			this.activeOrigin = this.consumedLength;

			if (result.IsCanceled)
			{
				this.ReleaseOutstanding(result.Buffer);
				cancellationToken.ThrowIfCancellationRequested();
				throw new OperationCanceledException(cancellationToken);
			}

			return result;
		}

		private void ReleaseOutstanding(ReadOnlySequence<byte> buffer)
		{
			long alreadyExamined = this.examinedLength - this.consumedLength;
			if (alreadyExamined < 0)
			{
				alreadyExamined = 0;
			}

			this.inner.AdvanceTo(buffer.Start, JsonSerializer.PositionAt(buffer, alreadyExamined));
			this.innerOutstanding = false;
		}
	}
}
