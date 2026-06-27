// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Generated forwarding overloads are intentionally undocumented
#pragma warning disable SA1601 // Partial elements should be documented
#pragma warning disable CS1591 // Generated forwarding overloads are intentionally undocumented

using System;
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nerdbank.Json;

#if NET

public partial record JsonSerializer
{
	[ExcludeFromCodeCoverage]
	public void Serialize<T>(IBufferWriter<byte> writer, in T? value)
		where T : IShapeable<T> => this.Serialize(writer, value, T.GetTypeShape());

	[ExcludeFromCodeCoverage]
	public string Serialize<T>(in T? value)
		where T : IShapeable<T> => this.Serialize(value, T.GetTypeShape());

	[ExcludeFromCodeCoverage]
	public void Serialize<T>(Stream stream, in T? value)
		where T : IShapeable<T> => this.Serialize(stream, value, T.GetTypeShape());

	[ExcludeFromCodeCoverage]
	public ValueTask SerializeAsync<T>(Stream stream, T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeAsync(stream, value, T.GetTypeShape(), cancellationToken);

	[ExcludeFromCodeCoverage]
	public T Deserialize<T>(string json)
		where T : IShapeable<T> => this.Deserialize(json, T.GetTypeShape());

	[ExcludeFromCodeCoverage]
	public T Deserialize<T>(Stream stream)
		where T : IShapeable<T> => this.Deserialize(stream, T.GetTypeShape());

	[ExcludeFromCodeCoverage]
	public ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeAsync(stream, T.GetTypeShape(), cancellationToken);

	[ExcludeFromCodeCoverage]
	public void Serialize<T, TProvider>(IBufferWriter<byte> writer, in T? value)
		where TProvider : IShapeable<T> => this.Serialize(writer, value, TProvider.GetTypeShape());

	[ExcludeFromCodeCoverage]
	public string Serialize<T, TProvider>(in T? value)
		where TProvider : IShapeable<T> => this.Serialize(value, TProvider.GetTypeShape());

	[ExcludeFromCodeCoverage]
	public void Serialize<T, TProvider>(Stream stream, in T? value)
		where TProvider : IShapeable<T> => this.Serialize(stream, value, TProvider.GetTypeShape());

	[ExcludeFromCodeCoverage]
	public ValueTask SerializeAsync<T, TProvider>(Stream stream, T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.SerializeAsync(stream, value, TProvider.GetTypeShape(), cancellationToken);

	[ExcludeFromCodeCoverage]
	public T Deserialize<T, TProvider>(string json)
		where TProvider : IShapeable<T> => this.Deserialize(json, TProvider.GetTypeShape());

	[ExcludeFromCodeCoverage]
	public T Deserialize<T, TProvider>(Stream stream)
		where TProvider : IShapeable<T> => this.Deserialize(stream, TProvider.GetTypeShape());

	[ExcludeFromCodeCoverage]
	public ValueTask<T> DeserializeAsync<T, TProvider>(Stream stream, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.DeserializeAsync(stream, TProvider.GetTypeShape(), cancellationToken);
}

#endif

public static partial class JsonSerializerExtensions
{
	[ExcludeFromCodeCoverage]
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void Serialize<T>(this JsonSerializer self, IBufferWriter<byte> writer, in T? value)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing types without explicit generated shapes may require reflection metadata.")]
	public static void Serialize<T>(this JsonSerializer self, IBufferWriter<byte> writer, in T? value)
		=> RequireSerializer(self).SerializeDynamic(writer, value);
#endif

	[ExcludeFromCodeCoverage]
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static string Serialize<T>(this JsonSerializer self, in T? value)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing types without explicit generated shapes may require reflection metadata.")]
	public static string Serialize<T>(this JsonSerializer self, in T? value)
		=> RequireSerializer(self).SerializeDynamic(value);
#endif

	[ExcludeFromCodeCoverage]
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void Serialize<T>(this JsonSerializer self, Stream stream, in T? value)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing types without explicit generated shapes may require reflection metadata.")]
	public static void Serialize<T>(this JsonSerializer self, Stream stream, in T? value)
		=> RequireSerializer(self).SerializeDynamic(stream, value);
#endif

	[ExcludeFromCodeCoverage]
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueTask SerializeAsync<T>(this JsonSerializer self, Stream stream, T? value, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing types without explicit generated shapes may require reflection metadata.")]
	public static ValueTask SerializeAsync<T>(this JsonSerializer self, Stream stream, T? value, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).SerializeAsyncDynamic(stream, value, cancellationToken);
#endif

	[ExcludeFromCodeCoverage]
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static T Deserialize<T>(this JsonSerializer self, string json)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing types without explicit generated shapes may require reflection metadata.")]
	public static T Deserialize<T>(this JsonSerializer self, string json)
		=> RequireSerializer(self).DeserializeDynamic<T>(json);
#endif

	[ExcludeFromCodeCoverage]
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static T Deserialize<T>(this JsonSerializer self, Stream stream)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing types without explicit generated shapes may require reflection metadata.")]
	public static T Deserialize<T>(this JsonSerializer self, Stream stream)
		=> RequireSerializer(self).DeserializeDynamic<T>(stream);
#endif

	[ExcludeFromCodeCoverage]
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueTask<T> DeserializeAsync<T>(this JsonSerializer self, Stream stream, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing types without explicit generated shapes may require reflection metadata.")]
	public static ValueTask<T> DeserializeAsync<T>(this JsonSerializer self, Stream stream, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializeAsyncDynamic<T>(stream, cancellationToken);
#endif

	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void Serialize<T, TProvider>(this JsonSerializer self, IBufferWriter<byte> writer, in T? value)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing values with witness types may require reflection metadata.")]
	public static void Serialize<T, TProvider>(this JsonSerializer self, IBufferWriter<byte> writer, in T? value)
		=> RequireSerializer(self).Serialize(writer, value, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache));
#endif

	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static string Serialize<T, TProvider>(this JsonSerializer self, in T? value)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing values with witness types may require reflection metadata.")]
	public static string Serialize<T, TProvider>(this JsonSerializer self, in T? value)
		=> RequireSerializer(self).Serialize(value, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache));
#endif

	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static void Serialize<T, TProvider>(this JsonSerializer self, Stream stream, in T? value)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing values with witness types may require reflection metadata.")]
	public static void Serialize<T, TProvider>(this JsonSerializer self, Stream stream, in T? value)
		=> RequireSerializer(self).Serialize(stream, value, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache));
#endif

	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueTask SerializeAsync<T, TProvider>(this JsonSerializer self, Stream stream, T? value, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing values with witness types may require reflection metadata.")]
	public static ValueTask SerializeAsync<T, TProvider>(this JsonSerializer self, Stream stream, T? value, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).SerializeAsync(stream, value, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache), cancellationToken);
#endif

	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static T Deserialize<T, TProvider>(this JsonSerializer self, string json)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing values with witness types may require reflection metadata.")]
	public static T Deserialize<T, TProvider>(this JsonSerializer self, string json)
		=> RequireSerializer(self).Deserialize(json, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache));
#endif

	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static T Deserialize<T, TProvider>(this JsonSerializer self, Stream stream)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing values with witness types may require reflection metadata.")]
	public static T Deserialize<T, TProvider>(this JsonSerializer self, Stream stream)
		=> RequireSerializer(self).Deserialize(stream, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache));
#endif

	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static ValueTask<T> DeserializeAsync<T, TProvider>(this JsonSerializer self, Stream stream, CancellationToken cancellationToken = default)
		=> throw new NotSupportedException(JsonSerializer.PreferTypeConstrainedInstanceOverloads);
#else
	[RequiresUnreferencedCode("Serializing or deserializing values with witness types may require reflection metadata.")]
	public static ValueTask<T> DeserializeAsync<T, TProvider>(this JsonSerializer self, Stream stream, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializeAsync(stream, ResolveTypeShapeOrThrow<T, TProvider>(RequireSerializer(self).ConverterCache), cancellationToken);
#endif
}
