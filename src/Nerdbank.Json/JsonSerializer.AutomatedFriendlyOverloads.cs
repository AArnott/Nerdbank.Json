// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Generated forwarding overloads are intentionally undocumented
#pragma warning disable SA1601 // Partial elements should be documented
#pragma warning disable CS1591 // Generated forwarding overloads are intentionally undocumented

using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nerdbank.Json;

#if NET

public partial record JsonSerializer
{
	public void Serialize<T>(IBufferWriter<byte> writer, in T? value)
		where T : IShapeable<T> => this.Serialize(writer, value, T.GetTypeShape());

	public string Serialize<T>(in T? value)
		where T : IShapeable<T> => this.Serialize(value, T.GetTypeShape());

	public void Serialize<T>(Stream stream, in T? value)
		where T : IShapeable<T> => this.Serialize(stream, value, T.GetTypeShape());

	public ValueTask SerializeAsync<T>(Stream stream, T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeAsync(stream, value, T.GetTypeShape(), cancellationToken);

	public T Deserialize<T>(string json)
		where T : IShapeable<T> => this.Deserialize(json, T.GetTypeShape());

	public T Deserialize<T>(Stream stream)
		where T : IShapeable<T> => this.Deserialize(stream, T.GetTypeShape());

	public ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAsync(stream, T.GetTypeShape(), cancellationToken);

	public void Serialize<T, TProvider>(IBufferWriter<byte> writer, in T? value)
		where TProvider : IShapeable<T> => this.Serialize(writer, value, TProvider.GetTypeShape());

	public string Serialize<T, TProvider>(in T? value)
		where TProvider : IShapeable<T> => this.Serialize(value, TProvider.GetTypeShape());

	public void Serialize<T, TProvider>(Stream stream, in T? value)
		where TProvider : IShapeable<T> => this.Serialize(stream, value, TProvider.GetTypeShape());

	public ValueTask SerializeAsync<T, TProvider>(Stream stream, T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.SerializeAsync(stream, value, TProvider.GetTypeShape(), cancellationToken);

	public T Deserialize<T, TProvider>(string json)
		where TProvider : IShapeable<T> => this.Deserialize(json, TProvider.GetTypeShape());

	public T Deserialize<T, TProvider>(Stream stream)
		where TProvider : IShapeable<T> => this.Deserialize(stream, TProvider.GetTypeShape());

	public ValueTask<T> DeserializeAsync<T, TProvider>(Stream stream, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.DeserializeAsync(stream, TProvider.GetTypeShape(), cancellationToken);
}

#endif

public static partial class JsonSerializerExtensions
{
	public static void Serialize<T>(this JsonSerializer self, IBufferWriter<byte> writer, in T? value)
		=> RequireSerializer(self).SerializeDynamic(writer, value);

	public static string Serialize<T>(this JsonSerializer self, in T? value)
		=> RequireSerializer(self).SerializeDynamic(value);

	public static void Serialize<T>(this JsonSerializer self, Stream stream, in T? value)
		=> RequireSerializer(self).SerializeDynamic(stream, value);

	public static ValueTask SerializeAsync<T>(this JsonSerializer self, Stream stream, T? value, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).SerializeAsyncDynamic(stream, value, cancellationToken);

	public static T Deserialize<T>(this JsonSerializer self, string json)
		=> RequireSerializer(self).DeserializeDynamic<T>(json);

	public static T Deserialize<T>(this JsonSerializer self, Stream stream)
		=> RequireSerializer(self).DeserializeDynamic<T>(stream);

	public static ValueTask<T> DeserializeAsync<T>(this JsonSerializer self, Stream stream, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializeAsyncDynamic<T>(stream, cancellationToken);

	public static void Serialize<T, TProvider>(this JsonSerializer self, IBufferWriter<byte> writer, in T? value)
		=> RequireSerializer(self).Serialize(writer, value, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache));

	public static string Serialize<T, TProvider>(this JsonSerializer self, in T? value)
		=> RequireSerializer(self).Serialize(value, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache));

	public static void Serialize<T, TProvider>(this JsonSerializer self, Stream stream, in T? value)
		=> RequireSerializer(self).Serialize(stream, value, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache));

	public static ValueTask SerializeAsync<T, TProvider>(this JsonSerializer self, Stream stream, T? value, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).SerializeAsync(stream, value, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache), cancellationToken);

	public static T Deserialize<T, TProvider>(this JsonSerializer self, string json)
		=> RequireSerializer(self).Deserialize(json, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache));

	public static T Deserialize<T, TProvider>(this JsonSerializer self, Stream stream)
		=> RequireSerializer(self).Deserialize(stream, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache));

	public static ValueTask<T> DeserializeAsync<T, TProvider>(this JsonSerializer self, Stream stream, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializeAsync(stream, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache), cancellationToken);
}
