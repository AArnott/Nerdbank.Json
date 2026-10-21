// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1601 // Partial elements are documented on their primary declaration.

using System.IO.Pipelines;

namespace Nerdbank.Json;

public static partial class JsonSerializerExtensions
{
	/// <inheritdoc cref="JsonSerializer.DeserializeEnumerableAsync{T}(PipeReader, ITypeShape{T}, JsonSerializer.SequenceOptions, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static IAsyncEnumerable<T?> DeserializeEnumerableAsync<T>(this JsonSerializer self, PipeReader reader, JsonSerializer.SequenceOptions options = default, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializeEnumerableAsync(reader, ResolveTypeShapeOrThrow<T>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializeEnumerableAsync{T}(PipeReader, ITypeShape{T}, JsonSerializer.SequenceOptions, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static IAsyncEnumerable<T?> DeserializeEnumerableAsync<T, TProvider>(this JsonSerializer self, PipeReader reader, JsonSerializer.SequenceOptions options = default, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializeEnumerableAsync(reader, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializeEnumerableAsync{T}(Stream, ITypeShape{T}, JsonSerializer.SequenceOptions, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static IAsyncEnumerable<T?> DeserializeEnumerableAsync<T>(this JsonSerializer self, Stream stream, JsonSerializer.SequenceOptions options = default, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializeEnumerableAsync(stream, ResolveTypeShapeOrThrow<T>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializeEnumerableAsync{T}(Stream, ITypeShape{T}, JsonSerializer.SequenceOptions, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static IAsyncEnumerable<T?> DeserializeEnumerableAsync<T, TProvider>(this JsonSerializer self, Stream stream, JsonSerializer.SequenceOptions options = default, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializeEnumerableAsync(stream, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.SerializeEnumerableAsync{T}(PipeWriter, IAsyncEnumerable{T}, ITypeShape{T}, JsonSerializer.SequenceOptions, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static ValueTask SerializeEnumerableAsync<T>(this JsonSerializer self, PipeWriter writer, IAsyncEnumerable<T> values, JsonSerializer.SequenceOptions options = default, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).SerializeEnumerableAsync(writer, values, ResolveTypeShapeOrThrow<T>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.SerializeEnumerableAsync{T}(PipeWriter, IAsyncEnumerable{T}, ITypeShape{T}, JsonSerializer.SequenceOptions, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static ValueTask SerializeEnumerableAsync<T, TProvider>(this JsonSerializer self, PipeWriter writer, IAsyncEnumerable<T> values, JsonSerializer.SequenceOptions options = default, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).SerializeEnumerableAsync(writer, values, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.SerializeEnumerableAsync{T}(Stream, IAsyncEnumerable{T}, ITypeShape{T}, JsonSerializer.SequenceOptions, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static ValueTask SerializeEnumerableAsync<T>(this JsonSerializer self, Stream stream, IAsyncEnumerable<T> values, JsonSerializer.SequenceOptions options = default, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).SerializeEnumerableAsync(stream, values, ResolveTypeShapeOrThrow<T>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.SerializeEnumerableAsync{T}(Stream, IAsyncEnumerable{T}, ITypeShape{T}, JsonSerializer.SequenceOptions, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static ValueTask SerializeEnumerableAsync<T, TProvider>(this JsonSerializer self, Stream stream, IAsyncEnumerable<T> values, JsonSerializer.SequenceOptions options = default, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).SerializeEnumerableAsync(stream, values, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), options, cancellationToken);
}
