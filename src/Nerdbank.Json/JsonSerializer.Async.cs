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
			using JsonAsyncReader asyncReader = new(reader, this.AllowTrailingCommas, this.ReadCommentHandling)
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
}
