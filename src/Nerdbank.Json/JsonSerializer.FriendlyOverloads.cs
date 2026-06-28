// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;

namespace Nerdbank.Json;

public partial record JsonSerializer
{
	private static readonly StreamPipeWriterOptions PipeWriterOptions = new(MemoryPool<byte>.Shared, leaveOpen: true);

	/// <summary>
	/// A thread-local, recyclable array that may be used for short bursts of code.
	/// </summary>
	[ThreadStatic]
	private static byte[]? scratchArray;

	/// <summary>
	/// Serializes a value as UTF-8 JSON to a byte buffer.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="writer">The destination buffer.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public void Serialize<T>(IBufferWriter<byte> writer, in T? value, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(writer);
		Requires.NotNull(shape);

		JsonWriter jsonWriter = new(writer)
		{
			WriteIndented = this.WriteIndented,
		};
		JsonReferenceEqualityTracker? priorTracker = currentReferenceTracker;
		currentReferenceTracker = this.PreserveReferences == ReferencePreservationMode.Off ? null : new JsonReferenceEqualityTracker();
		try
		{
			this.Serialize(ref jsonWriter, value, shape, cancellationToken);
			jsonWriter.Flush();
		}
		finally
		{
			currentReferenceTracker = priorTracker;
		}
	}

	/// <summary>
	/// Serializes a value to JSON text.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The serialized JSON text.</returns>
	public string Serialize<T>(in T? value, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		// Although the static array is thread-local, we still want to null it out while using it
		// to avoid any potential issues with re-entrancy due to a converter that makes a (bad) top-level call to the serializer.
		(byte[] array, scratchArray) = (scratchArray ?? new byte[65536], null);
		try
		{
			JsonWriter writer = new(SequencePool<byte>.Shared, array)
			{
				WriteIndented = this.WriteIndented,
			};
			JsonReferenceEqualityTracker? priorTracker = currentReferenceTracker;
			currentReferenceTracker = this.PreserveReferences == ReferencePreservationMode.Off ? null : new JsonReferenceEqualityTracker();
			try
			{
				this.Serialize(ref writer, value, shape, cancellationToken);
			}
			finally
			{
				currentReferenceTracker = priorTracker;
			}

			return writer.FlushAndGetString();
		}
		finally
		{
			scratchArray = array;
		}
	}

	/// <summary>
	/// Serializes a value as UTF-8 JSON to a stream.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="stream">The destination stream.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public void Serialize<T>(Stream stream, in T? value, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);

		this.Serialize(new StreamBufferWriter(stream), value, shape, cancellationToken);
	}

	/// <summary>
	/// Deserializes JSON text.
	/// </summary>
	/// <typeparam name="T">The type to deserialize.</typeparam>
	/// <param name="json">The JSON text.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T>(string json, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(json);
		Requires.NotNull(shape);

		JsonReader reader = new(json.AsSpan(), this.AllowTrailingCommas, this.ReadCommentHandling);
		JsonReferenceEqualityTracker? priorTracker = currentReferenceTracker;
		currentReferenceTracker = this.PreserveReferences == ReferencePreservationMode.Off ? null : new JsonReferenceEqualityTracker();
		try
		{
			T? value = this.Deserialize(ref reader, shape, cancellationToken);
			reader.EnsureFullyConsumed();
			return value;
		}
		finally
		{
			currentReferenceTracker = priorTracker;
		}
	}

	/// <summary>
	/// Deserializes UTF-8 JSON from a stream.
	/// </summary>
	/// <typeparam name="T">The type to deserialize.</typeparam>
	/// <param name="stream">The JSON stream.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public T? Deserialize<T>(Stream stream, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);

		using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true);
		return this.Deserialize(reader.ReadToEnd(), shape, cancellationToken);
	}

	/// <inheritdoc cref="Deserialize{T}(in ReadOnlySequence{byte}, ITypeShape{T}, CancellationToken)"/>
	public T? Deserialize<T>(ReadOnlyMemory<byte> buffer, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		string json = Encoding.UTF8.GetString(buffer.Span);
		return this.Deserialize(json, shape, cancellationToken);
	}

	/// <inheritdoc cref="Deserialize{T}(ref JsonReader, ITypeShape{T}, CancellationToken)"/>
	/// <param name="buffer">The msgpack to deserialize from.</param>
#pragma warning disable CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)
	public T? Deserialize<T>(scoped in ReadOnlySequence<byte> buffer, ITypeShape<T> shape, CancellationToken cancellationToken = default)
#pragma warning restore CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)
	{
		string json = Encoding.UTF8.GetString(buffer);
		return this.Deserialize(json, shape, cancellationToken);
	}
}
