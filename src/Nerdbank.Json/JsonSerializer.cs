// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable SA1600 // Internal helper members added for overload forwarding are intentionally undocumented.

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
	[ThreadStatic]
	private static JsonReferenceEqualityTracker? currentReferenceTracker;

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
	/// Gets the policy that determines whether properties with default values are serialized.
	/// </summary>
	/// <remarks>
	/// The default value is <see cref="SerializeDefaultValuesPolicy.Always"/>.
	/// </remarks>
	public SerializeDefaultValuesPolicy SerializeDefaultValues
	{
		get => this.configuration.SerializeDefaultValues;
		init => this.configuration = this.configuration with { SerializeDefaultValues = value };
	}

	/// <summary>
	/// Gets a value indicating whether enum values should be serialized by name rather than by numeric value when possible.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="false"/>.
	/// </remarks>
	public bool SerializeEnumValuesByName
	{
		get => this.configuration.SerializeEnumValuesByName;
		init => this.configuration = this.configuration with { SerializeEnumValuesByName = value };
	}

	/// <summary>
	/// Gets the policy that determines how deserialization handles missing or <see langword="null"/> values.
	/// </summary>
	/// <remarks>
	/// The default value is <see cref="DeserializeDefaultValuesPolicy.Default"/>.
	/// </remarks>
	public DeserializeDefaultValuesPolicy DeserializeDefaultValues
	{
		get => this.configuration.DeserializeDefaultValues;
		init => this.configuration = this.configuration with { DeserializeDefaultValues = value };
	}

	/// <summary>
	/// Gets the mode that preserves reference equality during serialization and deserialization.
	/// </summary>
	public ReferencePreservationMode PreserveReferences
	{
		get => this.configuration.PreserveReferences;
		init => this.configuration = this.configuration with { PreserveReferences = value };
	}

	/// <summary>
	/// Gets the converter cache derived from this serializer's immutable configuration.
	/// </summary>
	internal JsonConverterCache ConverterCache => this.configuration.ConverterCache;

	internal JsonReferenceEqualityTracker ReferenceTracker => currentReferenceTracker ?? throw new InvalidOperationException("Reference tracking is only available within an active serialization or deserialization operation.");

	/// <summary>
	/// Serializes a value as UTF-8 JSON to a byte buffer.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="writer">The destination buffer.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	public void Serialize<T>(IBufferWriter<byte> writer, in T? value, ITypeShape<T> shape)
	{
		if (writer is null)
		{
			throw new ArgumentNullException(nameof(writer));
		}

		if (shape is null)
		{
			throw new ArgumentNullException(nameof(shape));
		}

		JsonWriter jsonWriter = new(writer);
		JsonReferenceEqualityTracker? priorTracker = currentReferenceTracker;
		currentReferenceTracker = this.PreserveReferences == ReferencePreservationMode.Off ? null : new JsonReferenceEqualityTracker();
		try
		{
			this.Serialize(ref jsonWriter, value, shape);
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
	/// <returns>The serialized JSON text.</returns>
	public string Serialize<T>(in T? value, ITypeShape<T> shape)
	{
		BufferWriter buffer = new();
		this.Serialize(buffer, value, shape);
		return Encoding.UTF8.GetString(buffer.WrittenArray, 0, buffer.WrittenCount);
	}

	/// <summary>
	/// Serializes a value as UTF-8 JSON to a stream.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="stream">The destination stream.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	public void Serialize<T>(Stream stream, in T? value, ITypeShape<T> shape)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		BufferWriter buffer = new();
		this.Serialize(buffer, value, shape);
		stream.Write(buffer.WrittenArray, 0, buffer.WrittenCount);
	}

	/// <summary>
	/// Serializes a value as UTF-8 JSON to a stream.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="stream">The destination stream.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that completes when serialization finishes.</returns>
	public async ValueTask SerializeAsync<T>(Stream stream, T? value, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		BufferWriter buffer = new();
		this.Serialize(buffer, value, shape);
		await stream.WriteAsync(buffer.WrittenArray, 0, buffer.WrittenCount, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Serializes a string value to JSON text.
	/// </summary>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The serialized JSON text.</returns>
	public string Serialize(string? value) => this.SerializeDynamic<string?>(value);

	/// <summary>
	/// Serializes a boolean value to JSON text.
	/// </summary>
	/// <param name="value">The value to serialize.</param>
	/// <returns>The serialized JSON text.</returns>
	public string Serialize(bool value) => this.SerializeDynamic<bool>(value);

	/// <summary>
	/// Deserializes JSON text.
	/// </summary>
	/// <typeparam name="T">The type to deserialize.</typeparam>
	/// <param name="json">The JSON text.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <returns>The deserialized value.</returns>
	public T Deserialize<T>(string json, ITypeShape<T> shape)
	{
		if (json is null)
		{
			throw new ArgumentNullException(nameof(json));
		}

		if (shape is null)
		{
			throw new ArgumentNullException(nameof(shape));
		}

		JsonReader reader = new(json.AsSpan());
		JsonReferenceEqualityTracker? priorTracker = currentReferenceTracker;
		currentReferenceTracker = this.PreserveReferences == ReferencePreservationMode.Off ? null : new JsonReferenceEqualityTracker();
		try
		{
			T value = this.Deserialize(ref reader, shape);
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
	/// <returns>The deserialized value.</returns>
	public T Deserialize<T>(Stream stream, ITypeShape<T> shape)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true);
		return this.Deserialize(reader.ReadToEnd(), shape);
	}

	/// <summary>
	/// Deserializes UTF-8 JSON from a stream.
	/// </summary>
	/// <typeparam name="T">The type to deserialize.</typeparam>
	/// <param name="stream">The JSON stream.</param>
	/// <param name="shape">The type shape describing <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that produces the deserialized value.</returns>
	public async ValueTask<T> DeserializeAsync<T>(Stream stream, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		using MemoryStream buffer = new();
		await stream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
		return this.Deserialize(Encoding.UTF8.GetString(buffer.ToArray()), shape);
	}

	internal void SerializeDynamic<T>(IBufferWriter<byte> writer, in T? value)
	{
		if (writer is null)
		{
			throw new ArgumentNullException(nameof(writer));
		}

		JsonWriter jsonWriter = new(writer);
		JsonReferenceEqualityTracker? priorTracker = currentReferenceTracker;
		currentReferenceTracker = this.PreserveReferences == ReferencePreservationMode.Off ? null : new JsonReferenceEqualityTracker();
		try
		{
			this.Serialize(ref jsonWriter, value, null);
		}
		finally
		{
			currentReferenceTracker = priorTracker;
		}
	}

	internal string SerializeDynamic<T>(in T? value)
	{
		BufferWriter buffer = new();
		this.SerializeDynamic(buffer, value);
		return Encoding.UTF8.GetString(buffer.WrittenArray, 0, buffer.WrittenCount);
	}

	internal void SerializeDynamic<T>(Stream stream, in T? value)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		BufferWriter buffer = new();
		this.SerializeDynamic(buffer, value);
		stream.Write(buffer.WrittenArray, 0, buffer.WrittenCount);
	}

	internal async ValueTask SerializeAsyncDynamic<T>(Stream stream, T? value, CancellationToken cancellationToken = default)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		BufferWriter buffer = new();
		this.SerializeDynamic(buffer, value);
		await stream.WriteAsync(buffer.WrittenArray, 0, buffer.WrittenCount, cancellationToken).ConfigureAwait(false);
	}

	internal T DeserializeDynamic<T>(string json)
	{
		if (json is null)
		{
			throw new ArgumentNullException(nameof(json));
		}

		JsonReader reader = new(json.AsSpan());
		JsonReferenceEqualityTracker? priorTracker = currentReferenceTracker;
		currentReferenceTracker = this.PreserveReferences == ReferencePreservationMode.Off ? null : new JsonReferenceEqualityTracker();
		try
		{
			T value = this.Deserialize<T>(ref reader, null);
			reader.EnsureFullyConsumed();
			return value;
		}
		finally
		{
			currentReferenceTracker = priorTracker;
		}
	}

	internal T DeserializeDynamic<T>(Stream stream)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true);
		return this.DeserializeDynamic<T>(reader.ReadToEnd());
	}

	internal async ValueTask<T> DeserializeAsyncDynamic<T>(Stream stream, CancellationToken cancellationToken = default)
	{
		if (stream is null)
		{
			throw new ArgumentNullException(nameof(stream));
		}

		using MemoryStream buffer = new();
		await stream.CopyToAsync(buffer, 81920, cancellationToken).ConfigureAwait(false);
		return this.DeserializeDynamic<T>(Encoding.UTF8.GetString(buffer.ToArray()));
	}

	internal void Serialize<T>(ref JsonWriter writer, in T? value, ITypeShape<T>? shape)
	{
		if (this.CanUseBuiltInFastPath(typeof(T)) && BuiltInJsonConverters.TrySerialize(ref writer, value))
		{
			return;
		}

		(shape is null ? this.ConverterCache.GetOrAddConverter<T>() : this.ConverterCache.GetOrAddConverter(shape)).Write(ref writer, value, this);
	}

	internal T Deserialize<T>(ref JsonReader reader, ITypeShape<T>? shape)
	{
		if (this.CanUseBuiltInFastPath(typeof(T)) && BuiltInJsonConverters.TryDeserialize(ref reader, out T value))
		{
			return value;
		}

		return (shape is null ? this.ConverterCache.GetOrAddConverter<T>() : this.ConverterCache.GetOrAddConverter(shape)).Read(ref reader, this)!;
	}

	private bool CanUseBuiltInFastPath(Type type) => this.PreserveReferences == ReferencePreservationMode.Off || !RequiresReferencePreservation(type);

	private static bool RequiresReferencePreservation(Type type) => !type.IsValueType && !BuiltInJsonConverters.IsSupported(type);

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
