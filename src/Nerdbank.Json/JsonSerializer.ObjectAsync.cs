// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace Nerdbank.Json;

public partial record JsonSerializer
{
	/// <summary>Serializes an untyped value asynchronously to a pipe.</summary>
	/// <param name="writer">The destination pipe, which is not completed.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The shape describing the value.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task tracking the write.</returns>
	public ValueTask SerializeObjectAsync(PipeWriter writer, object? value, ITypeShape shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(writer);
		Requires.NotNull(shape);
		return this.SerializeObjectAsyncCore(writer, value, shape, this.CreateSerializationContext(shape.Provider, cancellationToken));
	}

	/// <summary>Serializes an untyped value asynchronously to a pipe using per-call state.</summary>
	/// <param name="writer">The destination pipe, which is not completed.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The shape describing the value.</param>
	/// <param name="startingContext">The inactive starting context for this operation.</param>
	/// <returns>A task tracking the write.</returns>
	[OverloadResolutionPriority(-1)]
	public ValueTask SerializeObjectAsync(PipeWriter writer, object? value, ITypeShape shape, SerializationContext startingContext)
	{
		Requires.NotNull(writer);
		Requires.NotNull(shape);
		return this.SerializeObjectAsyncCore(writer, value, shape, this.CreateSerializationContext(startingContext) with { TypeShapeProvider = shape.Provider });
	}

	/// <summary>Serializes an untyped value asynchronously to a stream.</summary>
	/// <param name="stream">The destination stream, which is left open.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The shape describing the value.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task tracking the write.</returns>
	public async ValueTask SerializeObjectAsync(Stream stream, object? value, ITypeShape shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		Requires.NotNull(shape);
		PipeWriter writer = PipeWriter.Create(stream, StreamPipeWriterLeaveOpen);
		try
		{
			await this.SerializeObjectAsync(writer, value, shape, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await writer.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Serializes an untyped value asynchronously to a stream using per-call state.</summary>
	/// <param name="stream">The destination stream, which is left open.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The shape describing the value.</param>
	/// <param name="startingContext">The inactive starting context for this operation.</param>
	/// <returns>A task tracking the write.</returns>
	[OverloadResolutionPriority(-1)]
	public async ValueTask SerializeObjectAsync(Stream stream, object? value, ITypeShape shape, SerializationContext startingContext)
	{
		Requires.NotNull(stream);
		Requires.NotNull(shape);
		ThrowIfOperationInProgress(startingContext);
		PipeWriter writer = PipeWriter.Create(stream, StreamPipeWriterLeaveOpen);
		try
		{
			await this.SerializeObjectAsync(writer, value, shape, startingContext).ConfigureAwait(false);
		}
		finally
		{
			await writer.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Deserializes an untyped value asynchronously from a pipe.</summary>
	/// <param name="reader">The source pipe, which is not completed.</param>
	/// <param name="shape">The shape describing the value.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public ValueTask<object?> DeserializeObjectAsync(PipeReader reader, ITypeShape shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(reader);
		Requires.NotNull(shape);
		return this.DeserializeObjectAsyncCore(reader, shape, this.CreateSerializationContext(shape.Provider, cancellationToken));
	}

	/// <summary>Deserializes an untyped value asynchronously from a pipe using per-call state.</summary>
	/// <param name="reader">The source pipe, which is not completed.</param>
	/// <param name="shape">The shape describing the value.</param>
	/// <param name="startingContext">The inactive starting context for this operation.</param>
	/// <returns>The deserialized value.</returns>
	[OverloadResolutionPriority(-1)]
	public ValueTask<object?> DeserializeObjectAsync(PipeReader reader, ITypeShape shape, SerializationContext startingContext)
	{
		Requires.NotNull(reader);
		Requires.NotNull(shape);
		return this.DeserializeObjectAsyncCore(reader, shape, this.CreateSerializationContext(startingContext) with { TypeShapeProvider = shape.Provider });
	}

	/// <summary>Deserializes an untyped value asynchronously from a stream.</summary>
	/// <param name="stream">The source stream, which is left open.</param>
	/// <param name="shape">The shape describing the value.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public async ValueTask<object?> DeserializeObjectAsync(Stream stream, ITypeShape shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		Requires.NotNull(shape);
		PipeReader reader = PipeReader.Create(stream, StreamPipeReaderOptions);
		try
		{
			return await this.DeserializeObjectAsync(reader, shape, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>Deserializes an untyped value asynchronously from a stream using per-call state.</summary>
	/// <param name="stream">The source stream, which is left open.</param>
	/// <param name="shape">The shape describing the value.</param>
	/// <param name="startingContext">The inactive starting context for this operation.</param>
	/// <returns>The deserialized value.</returns>
	[OverloadResolutionPriority(-1)]
	public async ValueTask<object?> DeserializeObjectAsync(Stream stream, ITypeShape shape, SerializationContext startingContext)
	{
		Requires.NotNull(stream);
		Requires.NotNull(shape);
		ThrowIfOperationInProgress(startingContext);
		PipeReader reader = PipeReader.Create(stream, StreamPipeReaderOptions);
		try
		{
			return await this.DeserializeObjectAsync(reader, shape, startingContext).ConfigureAwait(false);
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	private async ValueTask SerializeObjectAsyncCore(PipeWriter writer, object? value, ITypeShape shape, SerializationContext context)
	{
		try
		{
			JsonAsyncWriter asyncWriter = new(writer, this.WriteIndented);
			await this.ConverterCache.GetOrAddConverter(shape).WriteObjectAsync(asyncWriter, value, context).ConfigureAwait(false);
			await asyncWriter.FlushAsync(context.CancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException("An error occurred during serialization.", ex);
		}
	}

	private async ValueTask<object?> DeserializeObjectAsyncCore(PipeReader reader, ITypeShape shape, SerializationContext context)
	{
		try
		{
			using JsonAsyncReader asyncReader = new(reader, this.AllowTrailingCommas, this.ReadCommentHandling)
			{
				CancellationToken = context.CancellationToken,
			};
			object? result = await this.ConverterCache.GetOrAddConverter(shape).ReadObjectAsync(asyncReader, context).ConfigureAwait(false);
			await asyncReader.EnsureFullyConsumedAsync(context).ConfigureAwait(false);
			return result;
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException("An error occurred during deserialization.", ex);
		}
	}
}
