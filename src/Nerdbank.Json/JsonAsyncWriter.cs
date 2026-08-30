// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;

namespace Nerdbank.Json;

/// <summary>
/// An incremental UTF-8 JSON writer that writes to a <see cref="PipeWriter"/> and flushes periodically so large object
/// graphs can be serialized with bounded memory and cooperative backpressure.
/// </summary>
/// <remarks>
/// This is the asynchronous counterpart to <see cref="JsonWriter"/> and is used when implementing the asynchronous
/// virtual methods on <see cref="JsonConverter{T}"/>. The underlying <see cref="PipeWriter"/> is never completed by this
/// type.
/// </remarks>
public sealed class JsonAsyncWriter
{
	private readonly PipeWriter pipeWriter;
	private readonly bool writeIndented;
	private BufferMemoryWriter bufferWriter;
	private JsonWriter.ContainerState[] savedStack = new JsonWriter.ContainerState[8];
	private int savedDepth;
	private bool savedPendingPropertyValue;
	private bool writerReturned = true;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonAsyncWriter"/> class.
	/// </summary>
	/// <param name="pipeWriter">The pipe to write UTF-8 JSON to. It is not completed by this writer.</param>
	/// <param name="writeIndented">Whether written JSON should be indented.</param>
	public JsonAsyncWriter(PipeWriter pipeWriter, bool writeIndented = false)
	{
		this.pipeWriter = Requires.NotNull(pipeWriter);
		this.writeIndented = writeIndented;
		this.bufferWriter = new BufferMemoryWriter(pipeWriter);
	}

	/// <summary>
	/// Creates a synchronous <see cref="JsonWriter"/> that writes into this writer's buffer.
	/// </summary>
	/// <returns>The synchronous writer, which must be returned via <see cref="ReturnWriter(ref JsonWriter)"/>.</returns>
	public JsonWriter CreateWriter()
	{
		this.ThrowIfWriterNotReturned();
		this.writerReturned = false;
		return new JsonWriter(new BufferWriter(ref this.bufferWriter), this.savedStack, this.savedDepth, this.savedPendingPropertyValue)
		{
			WriteIndented = this.writeIndented,
		};
	}

	/// <summary>
	/// Applies the bytes written with a writer previously obtained from <see cref="CreateWriter"/> back to this object.
	/// </summary>
	/// <param name="writer">The writer to return. It is reset to prevent reuse after this call.</param>
	public void ReturnWriter(ref JsonWriter writer)
	{
		writer.Flush();

		(this.savedStack, this.savedDepth, this.savedPendingPropertyValue) = writer.ExportContainerState();

#if !NET
		// ref fields are not supported on .NET Framework, so we copy the struct back since the local copy will disappear.
		this.bufferWriter = writer.Writer.BufferMemoryWriter;
#endif

		writer = default;
		this.writerReturned = true;
	}

	/// <summary>
	/// Writes a value using the specified converter, streaming when the converter prefers asynchronous serialization.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="converter">The converter for the value.</param>
	/// <param name="value">The value to write.</param>
	/// <param name="context">The serialization context.</param>
	/// <returns>A task tracking the asynchronous write.</returns>
	public ValueTask WriteValueAsync<T>(JsonConverter<T> converter, T? value, SerializationContext context)
	{
		Requires.NotNull(converter);
		if (converter.PreferAsyncSerialization)
		{
			return converter.WriteAsync(this, value, context);
		}

		JsonWriter syncWriter = this.CreateWriter();
		converter.Write(ref syncWriter, value, context);
		this.ReturnWriter(ref syncWriter);
		return this.FlushIfAppropriateAsync(context);
	}

	/// <summary>
	/// Writes a JSON <see langword="null"/> literal.
	/// </summary>
	/// <param name="context">The serialization context.</param>
	/// <returns>A task tracking the write.</returns>
	public ValueTask WriteNullAsync(SerializationContext context)
	{
		JsonWriter writer = this.CreateWriter();
		writer.WriteNullValue();
		this.ReturnWriter(ref writer);
		return this.FlushIfAppropriateAsync(context);
	}

	/// <summary>
	/// Ensures everything previously written has been committed to the underlying <see cref="PipeWriter"/>.
	/// </summary>
	public void Flush()
	{
		this.ThrowIfWriterNotReturned();
		this.bufferWriter.Commit();
	}

	/// <summary>
	/// Flushes the pipe if the accumulated buffer is getting large, applying backpressure from the reader.
	/// </summary>
	/// <param name="context">The serialization context.</param>
	/// <returns>A task to await before writing further.</returns>
	public ValueTask FlushIfAppropriateAsync(SerializationContext context)
	{
		this.Flush();
		if (this.pipeWriter.CanGetUnflushedBytes && this.pipeWriter.UnflushedBytes > context.UnflushedBytesThreshold)
		{
			return FlushAsync(this.pipeWriter, context.CancellationToken);
		}

		return default;

		static async ValueTask FlushAsync(PipeWriter pipeWriter, CancellationToken cancellationToken)
		{
			FlushResult flushResult = await pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
			if (flushResult.IsCanceled)
			{
				throw new OperationCanceledException(cancellationToken);
			}

			if (flushResult.IsCompleted)
			{
				throw new EndOfStreamException("The receiver has stopped listening.");
			}
		}
	}

	/// <summary>
	/// Commits buffered bytes and flushes the pipe unconditionally.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task tracking the flush.</returns>
	public async ValueTask FlushAsync(CancellationToken cancellationToken)
	{
		this.Flush();
		FlushResult flushResult = await this.pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
		if (flushResult.IsCanceled)
		{
			throw new OperationCanceledException(cancellationToken);
		}
	}

	private void ThrowIfWriterNotReturned()
	{
		if (!this.writerReturned)
		{
			throw new InvalidOperationException("The previous synchronous writer must be returned before using this writer again.");
		}
	}
}
