// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1601 // Partial elements should be documented

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace Nerdbank.Json;

public static partial class JsonSerializerExtensions
{
	/// <inheritdoc cref="JsonSerializer.SerializeAsync{T}(PipeWriter, T, ITypeShape{T}, CancellationToken)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static ValueTask SerializeAsync<T>(this JsonSerializer self, PipeWriter writer, T? value, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).SerializeAsync(writer, value, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.SerializeAsync{T}(PipeWriter, T, ITypeShape{T}, CancellationToken)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static ValueTask SerializeAsync<T, TProvider>(this JsonSerializer self, PipeWriter writer, T? value, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).SerializeAsync(writer, value, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.SerializeAsync{T}(Stream, T, ITypeShape{T}, CancellationToken)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static ValueTask SerializeAsync<T>(this JsonSerializer self, Stream stream, T? value, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).SerializeAsync(stream, value, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.SerializeAsync{T}(Stream, T, ITypeShape{T}, CancellationToken)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static ValueTask SerializeAsync<T, TProvider>(this JsonSerializer self, Stream stream, T? value, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).SerializeAsync(stream, value, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializeAsync{T}(PipeReader, ITypeShape{T}, CancellationToken)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static ValueTask<T?> DeserializeAsync<T>(this JsonSerializer self, PipeReader reader, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializeAsync(reader, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializeAsync{T}(PipeReader, ITypeShape{T}, CancellationToken)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static ValueTask<T?> DeserializeAsync<T, TProvider>(this JsonSerializer self, PipeReader reader, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializeAsync(reader, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializeAsync{T}(Stream, ITypeShape{T}, CancellationToken)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static ValueTask<T?> DeserializeAsync<T>(this JsonSerializer self, Stream stream, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializeAsync(stream, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializeAsync{T}(Stream, ITypeShape{T}, CancellationToken)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static ValueTask<T?> DeserializeAsync<T, TProvider>(this JsonSerializer self, Stream stream, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializeAsync(stream, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.SerializeAsync{T}(PipeWriter, T, ITypeShape{T}, SerializationContext)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	[OverloadResolutionPriority(-1)]
	public static ValueTask SerializeAsync<T>(this JsonSerializer self, PipeWriter writer, T? value, SerializationContext startingContext)
		=> RequireSerializer(self).SerializeAsync(writer, value, ResolveTypeShapeOrThrow<T>(self.ConverterCache), startingContext);

	/// <inheritdoc cref="JsonSerializer.SerializeAsync{T}(PipeWriter, T, ITypeShape{T}, SerializationContext)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	[OverloadResolutionPriority(-1)]
	public static ValueTask SerializeAsync<T, TProvider>(this JsonSerializer self, PipeWriter writer, T? value, SerializationContext startingContext)
		=> RequireSerializer(self).SerializeAsync(writer, value, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), startingContext);

	/// <inheritdoc cref="JsonSerializer.SerializeAsync{T}(Stream, T, ITypeShape{T}, SerializationContext)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	[OverloadResolutionPriority(-1)]
	public static ValueTask SerializeAsync<T>(this JsonSerializer self, Stream stream, T? value, SerializationContext startingContext)
		=> RequireSerializer(self).SerializeAsync(stream, value, ResolveTypeShapeOrThrow<T>(self.ConverterCache), startingContext);

	/// <inheritdoc cref="JsonSerializer.SerializeAsync{T}(Stream, T, ITypeShape{T}, SerializationContext)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	[OverloadResolutionPriority(-1)]
	public static ValueTask SerializeAsync<T, TProvider>(this JsonSerializer self, Stream stream, T? value, SerializationContext startingContext)
		=> RequireSerializer(self).SerializeAsync(stream, value, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), startingContext);

	/// <inheritdoc cref="JsonSerializer.DeserializeAsync{T}(PipeReader, ITypeShape{T}, SerializationContext)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	[OverloadResolutionPriority(-1)]
	public static ValueTask<T?> DeserializeAsync<T>(this JsonSerializer self, PipeReader reader, SerializationContext startingContext)
		=> RequireSerializer(self).DeserializeAsync(reader, ResolveTypeShapeOrThrow<T>(self.ConverterCache), startingContext);

	/// <inheritdoc cref="JsonSerializer.DeserializeAsync{T}(PipeReader, ITypeShape{T}, SerializationContext)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	[OverloadResolutionPriority(-1)]
	public static ValueTask<T?> DeserializeAsync<T, TProvider>(this JsonSerializer self, PipeReader reader, SerializationContext startingContext)
		=> RequireSerializer(self).DeserializeAsync(reader, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), startingContext);

	/// <inheritdoc cref="JsonSerializer.DeserializeAsync{T}(Stream, ITypeShape{T}, SerializationContext)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	[OverloadResolutionPriority(-1)]
	public static ValueTask<T?> DeserializeAsync<T>(this JsonSerializer self, Stream stream, SerializationContext startingContext)
		=> RequireSerializer(self).DeserializeAsync(stream, ResolveTypeShapeOrThrow<T>(self.ConverterCache), startingContext);

	/// <inheritdoc cref="JsonSerializer.DeserializeAsync{T}(Stream, ITypeShape{T}, SerializationContext)"/>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	[OverloadResolutionPriority(-1)]
	public static ValueTask<T?> DeserializeAsync<T, TProvider>(this JsonSerializer self, Stream stream, SerializationContext startingContext)
		=> RequireSerializer(self).DeserializeAsync(stream, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), startingContext);
}
