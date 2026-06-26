// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nerdbank.Json;

/// <summary>
/// Serializes .NET values to JSON.
/// </summary>
/// <remarks>
/// This type is immutable and thread-safe.
/// </remarks>
public partial record JsonSerializer
{
	private JsonSerializerConfiguration configuration = JsonSerializerConfiguration.Default;

	/// <summary>
	/// Gets the transformation applied to object property names during serialization and deserialization.
	/// </summary>
	/// <remarks>
	/// The default value is <see cref="JsonNamingPolicy.CamelCase"/>.
	/// Set this property to <see langword="null"/> to preserve declared property names.
	/// </remarks>
	public JsonNamingPolicy? PropertyNamingPolicy
	{
		get => this.configuration.PropertyNamingPolicy;
		init => this.configuration = this.configuration with { PropertyNamingPolicy = value };
	}

	/// <summary>
	/// Gets the transformation applied to dictionary keys during serialization and deserialization.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="null"/>, which preserves dictionary keys as declared.
	/// </remarks>
	public JsonNamingPolicy? DictionaryKeyNamingPolicy
	{
		get => this.configuration.DictionaryKeyNamingPolicy;
		init => this.configuration = this.configuration with { DictionaryKeyNamingPolicy = value };
	}

	/// <summary>
	/// Gets the converter cache derived from this serializer's immutable configuration.
	/// </summary>
	internal JsonConverterCache ConverterCache => this.configuration.ConverterCache;

	/// <summary>
	/// Serializes a value as UTF-8 JSON to a byte buffer.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="writer">The destination buffer.</param>
	/// <param name="value">The value to serialize.</param>
	public void Serialize<T>(IBufferWriter<byte> writer, T value)
	{
		if (writer is null)
		{
			throw new ArgumentNullException(nameof(writer));
		}

		JsonWriter jsonWriter = new(writer);
		this.Serialize(ref jsonWriter, value);
	}

	/// <summary>
	/// Serializes a value to JSON text.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The serialized JSON text.</returns>
	public string Serialize<T>(T value)
	{
		BufferWriter buffer = new();
		this.Serialize(buffer, value);
		return Encoding.UTF8.GetString(buffer.WrittenArray, 0, buffer.WrittenCount);
	}

	/// <summary>
	/// Serializes a value as UTF-8 JSON to a stream.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="stream">The destination stream.</param>
	/// <param name="value">The value to serialize.</param>
	public void Serialize<T>(Stream stream, T value)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		BufferWriter buffer = new();
		this.Serialize(buffer, value);
		stream.Write(buffer.WrittenArray, 0, buffer.WrittenCount);
	}

	/// <summary>
	/// Serializes a value as UTF-8 JSON to a stream.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="stream">The destination stream.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that completes when serialization finishes.</returns>
	public async ValueTask SerializeAsync<T>(Stream stream, T value, CancellationToken cancellationToken = default)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		BufferWriter buffer = new();
		this.Serialize(buffer, value);
		await stream.WriteAsync(buffer.WrittenArray, 0, buffer.WrittenCount, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Serializes a string value to JSON text.
	/// </summary>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The serialized JSON text.</returns>
	public string Serialize(string? value) => this.Serialize<string?>(value);

	/// <summary>
	/// Serializes a boolean value to JSON text.
	/// </summary>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The serialized JSON text.</returns>
	public string Serialize(bool value) => this.Serialize<bool>(value);

	/// <summary>
	/// Deserializes JSON text.
	/// </summary>
	/// <typeparam name="T">The type to deserialize.</typeparam>
	/// <param name="json">The JSON text.</param>
	/// <returns>The deserialized value.</returns>
	public T Deserialize<T>(string json)
	{
		if (json is null)
		{
			throw new ArgumentNullException(nameof(json));
		}

		JsonReader reader = new(json.AsSpan());
		T value = this.Deserialize<T>(ref reader);
		reader.EnsureFullyConsumed();
		return value;
	}

	/// <summary>
	/// Deserializes UTF-8 JSON from a stream.
	/// </summary>
	/// <typeparam name="T">The type to deserialize.</typeparam>
	/// <param name="stream">The JSON stream.</param>
	/// <returns>The deserialized value.</returns>
	public T Deserialize<T>(Stream stream)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true);
		return this.Deserialize<T>(reader.ReadToEnd());
	}

	/// <summary>
	/// Deserializes UTF-8 JSON from a stream.
	/// </summary>
	/// <typeparam name="T">The type to deserialize.</typeparam>
	/// <param name="stream">The JSON stream.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that produces the deserialized value.</returns>
	public async ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		using MemoryStream buffer = new();
		await stream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
		return this.Deserialize<T>(Encoding.UTF8.GetString(buffer.ToArray()));
	}

	private void Serialize<T>(ref JsonWriter writer, T value)
	{
		if (BuiltInJsonConverters.TrySerialize(ref writer, value))
		{
			return;
		}

		this.ConverterCache.GetOrAddConverter<T>().Write(ref writer, value, this);
	}

	private T Deserialize<T>(ref JsonReader reader)
	{
		if (BuiltInJsonConverters.TryDeserialize(ref reader, out T value))
		{
			return value;
		}

		return this.ConverterCache.GetOrAddConverter<T>().Read(ref reader, this)!;
	}

	private sealed class BufferWriter : IBufferWriter<byte>
	{
		private byte[] buffer = new byte[64];

		internal int WrittenCount { get; private set; }

		internal byte[] WrittenArray => this.buffer;

		public void Advance(int count)
		{
			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(count));
			}

			this.WrittenCount += count;
		}

		public Memory<byte> GetMemory(int sizeHint = 0)
		{
			this.EnsureCapacity(sizeHint);
			return this.buffer.AsMemory(this.WrittenCount);
		}

		public Span<byte> GetSpan(int sizeHint = 0)
		{
			this.EnsureCapacity(sizeHint);
			return this.buffer.AsSpan(this.WrittenCount);
		}

		private void EnsureCapacity(int sizeHint)
		{
			if (sizeHint < 1)
			{
				sizeHint = 1;
			}

			int available = this.buffer.Length - this.WrittenCount;
			if (available >= sizeHint)
			{
				return;
			}

			int newLength = Math.Max(this.buffer.Length * 2, this.WrittenCount + sizeHint);
			Array.Resize(ref this.buffer, newLength);
		}
	}
}
