// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;

namespace Nerdbank.Json;

public partial record JsonSerializer
{
	/// <summary>
	/// Deserializes any JSON text into the native, dependency-free <see cref="JsonValue"/> document object model.
	/// </summary>
	/// <param name="json">The JSON text.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The parsed value.</returns>
	/// <remarks>
	/// This entry point does not require a static model or a generated shape, and it neither roots
	/// <c>System.Text.Json</c> nor uses reflection, so it keeps the default serializer trimming- and NativeAOT-safe.
	/// </remarks>
	public JsonValue? DeserializeJsonValue(string json, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(json);
		byte[] utf8Json = Encoding.UTF8.GetBytes(json);
		JsonReader reader = new(utf8Json, this.AllowTrailingCommas, this.ReadCommentHandling);
		return this.DeserializeJsonValueCore(ref reader, cancellationToken, requireFullyConsumed: true);
	}

	/// <summary>
	/// Deserializes the first UTF-8 JSON value into the native <see cref="JsonValue"/> document object model.
	/// </summary>
	/// <param name="utf8Json">The UTF-8 JSON bytes.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The parsed value.</returns>
	public JsonValue? DeserializeJsonValue(ReadOnlyMemory<byte> utf8Json, CancellationToken cancellationToken = default)
		=> this.DeserializeJsonValue(utf8Json.Span, cancellationToken);

	/// <summary>
	/// Deserializes the first UTF-8 JSON value, possibly spanning multiple segments, into the native <see cref="JsonValue"/> document
	/// object model.
	/// </summary>
	/// <param name="utf8Json">The UTF-8 JSON bytes, possibly spanning multiple segments.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The parsed value.</returns>
	public JsonValue? DeserializeJsonValue(scoped in ReadOnlySequence<byte> utf8Json, CancellationToken cancellationToken = default)
	{
		JsonReader reader = new(utf8Json, this.AllowTrailingCommas, this.ReadCommentHandling);
		return this.DeserializeJsonValueCore(ref reader, cancellationToken);
	}

	/// <summary>
	/// Deserializes the first UTF-8 JSON value from a stream into the native <see cref="JsonValue"/> document object model.
	/// </summary>
	/// <param name="stream">The stream. It is read but not disposed by this method.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The parsed value.</returns>
	/// <remarks>The stream is buffered to its end and left open.</remarks>
	public JsonValue? DeserializeJsonValue(Stream stream, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		using MemoryStream buffer = new();
		stream.CopyTo(buffer);
		return this.DeserializeJsonValue(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)), cancellationToken);
	}

	/// <summary>
	/// Deserializes the first UTF-8 JSON value from a <see cref="PipeReader"/> into the native <see cref="JsonValue"/> document object
	/// model, reading incrementally without buffering the whole document.
	/// </summary>
	/// <param name="reader">The pipe. It is advanced but not completed by this method.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The parsed value.</returns>
	/// <remarks>Unread bytes remain in the pipe for the caller.</remarks>
	public ValueTask<JsonValue?> DeserializeJsonValueAsync(PipeReader reader, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(reader);
		return this.DeserializeJsonValueAsyncCore(reader, cancellationToken);
	}

	/// <summary>
	/// Deserializes JSON from a <see cref="Stream"/> into the native <see cref="JsonValue"/> document object model.
	/// </summary>
	/// <param name="stream">The stream. It is read but not disposed by this method.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The parsed value.</returns>
	public async ValueTask<JsonValue?> DeserializeJsonValueAsync(Stream stream, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		PipeReader reader = PipeReader.Create(stream, StreamPipeReaderOptions);
		try
		{
			return await this.DeserializeJsonValueAsyncCore(reader, cancellationToken, requireFullyConsumed: true).ConfigureAwait(false);
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Serializes a <see cref="JsonValue"/> to JSON text.
	/// </summary>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The serialized JSON text.</returns>
	public string SerializeJsonValue(JsonValue? value, CancellationToken cancellationToken = default)
	{
		(byte[] array, scratchArray) = (scratchArray ?? new byte[65536], null);
		try
		{
			JsonWriter writer = new(SequencePool<byte>.Shared, array)
			{
				WriteIndented = this.WriteIndented,
			};
			this.SerializeJsonValueCore(ref writer, value, cancellationToken);
			return writer.FlushAndGetString();
		}
		finally
		{
			scratchArray = array;
		}
	}

	/// <summary>
	/// Serializes a <see cref="JsonValue"/> as UTF-8 JSON to a buffer.
	/// </summary>
	/// <param name="writer">The destination buffer.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public void SerializeJsonValue(IBufferWriter<byte> writer, JsonValue? value, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(writer);
		JsonWriter jsonWriter = new(writer)
		{
			WriteIndented = this.WriteIndented,
		};
		this.SerializeJsonValueCore(ref jsonWriter, value, cancellationToken);
		jsonWriter.Flush();
	}

	/// <summary>
	/// Serializes a <see cref="JsonValue"/> as UTF-8 JSON to a stream.
	/// </summary>
	/// <param name="stream">The destination stream. It is flushed but not disposed by this method.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public void SerializeJsonValue(Stream stream, JsonValue? value, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		this.SerializeJsonValue(new StreamBufferWriter(stream), value, cancellationToken);
	}

	/// <summary>
	/// Serializes a <see cref="JsonValue"/> as UTF-8 JSON to a <see cref="PipeWriter"/> incrementally.
	/// </summary>
	/// <param name="writer">The pipe. It is flushed but not completed by this method.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task tracking the serialization.</returns>
	public async ValueTask SerializeJsonValueAsync(PipeWriter writer, JsonValue? value, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(writer);
		SerializationContext context = this.CreateSerializationContext(cancellationToken);
		try
		{
			JsonAsyncWriter asyncWriter = new(writer, this.WriteIndented);
			await JsonValueConverter.Instance.WriteAsync(asyncWriter, value, context).ConfigureAwait(false);
			await asyncWriter.FlushAsync(context.CancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException("An error occurred during serialization.", ex);
		}
	}

	/// <summary>
	/// Serializes a <see cref="JsonValue"/> document object model to a <see cref="Stream"/>.
	/// </summary>
	/// <param name="stream">The destination stream. It is flushed but not disposed by this method.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that completes when serialization finishes.</returns>
	public async ValueTask SerializeJsonValueAsync(Stream stream, JsonValue? value, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		PipeWriter writer = PipeWriter.Create(stream, StreamPipeWriterLeaveOpen);
		try
		{
			await this.SerializeJsonValueAsync(writer, value, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			await writer.CompleteAsync().ConfigureAwait(false);
		}
	}

	private JsonValue? DeserializeJsonValue(scoped ReadOnlySpan<byte> utf8Json, CancellationToken cancellationToken)
	{
		JsonReader reader = new(utf8Json, this.AllowTrailingCommas, this.ReadCommentHandling);
		return this.DeserializeJsonValueCore(ref reader, cancellationToken);
	}

	private JsonValue? DeserializeJsonValueCore(ref JsonReader reader, CancellationToken cancellationToken, bool requireFullyConsumed = false)
	{
		SerializationContext context = this.CreateSerializationContext(cancellationToken);
		try
		{
			JsonValue? value = JsonValueConverter.Instance.Read(ref reader, context);
			if (requireFullyConsumed)
			{
				reader.EnsureFullyConsumed();
			}

			return value;
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException("An error occurred during deserialization.", ex);
		}
	}

	private async ValueTask<JsonValue?> DeserializeJsonValueAsyncCore(PipeReader reader, CancellationToken cancellationToken, bool requireFullyConsumed = false)
	{
		SerializationContext context = this.CreateSerializationContext(cancellationToken);
		try
		{
			using JsonAsyncReader asyncReader = new(reader, this.AllowTrailingCommas, this.ReadCommentHandling)
			{
				CancellationToken = context.CancellationToken,
			};
			JsonValue? result = await JsonValueConverter.Instance.ReadAsync(asyncReader, context).ConfigureAwait(false);
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

	private void SerializeJsonValueCore(ref JsonWriter writer, JsonValue? value, CancellationToken cancellationToken)
	{
		SerializationContext context = this.CreateSerializationContext(cancellationToken);
		try
		{
			JsonValueConverter.Instance.Write(ref writer, value, context);
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException("An error occurred during serialization.", ex);
		}
	}
}
