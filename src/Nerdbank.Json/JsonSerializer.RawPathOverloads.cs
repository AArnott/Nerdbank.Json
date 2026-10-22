// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1601 // Partial elements are documented on their primary declaration.
#pragma warning disable SA1402 // Related serializer overloads share this file.

using System.IO.Pipelines;

namespace Nerdbank.Json;

public partial record JsonSerializer
{
#if NET
	/// <inheritdoc cref="DeserializePath{TValue}(ref JsonReader, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
	public TValue? DeserializePath<TValue, TProvider>(ref JsonReader reader, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TValue> => this.DeserializePath(ref reader, path, TProvider.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TValue}(ReadOnlyMemory{byte}, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
	public TValue? DeserializePath<TValue, TProvider>(ReadOnlyMemory<byte> utf8Json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TValue> => this.DeserializePath(utf8Json, path, TProvider.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TValue}(in ReadOnlySequence{byte}, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
	public TValue? DeserializePath<TValue, TProvider>(scoped in ReadOnlySequence<byte> utf8Json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TValue> => this.DeserializePath(utf8Json, path, TProvider.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TValue}(Stream, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
	public TValue? DeserializePath<TValue, TProvider>(Stream stream, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TValue> => this.DeserializePath(stream, path, TProvider.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="DeserializePathEnumerableAsync{TElement}(PipeReader, JsonPath, ITypeShape{TElement}, MissingPathBehavior, CancellationToken)"/>
	public IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TElement, TProvider>(PipeReader reader, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TElement> => this.DeserializePathEnumerableAsync(reader, path, TProvider.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="DeserializePathEnumerableAsync{TElement}(Stream, JsonPath, ITypeShape{TElement}, MissingPathBehavior, CancellationToken)"/>
	public IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TElement, TProvider>(Stream stream, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TElement> => this.DeserializePathEnumerableAsync(stream, path, TProvider.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TValue}(ref JsonReader, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
	public TValue? DeserializePath<TValue>(ref JsonReader reader, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TValue : IShapeable<TValue> => this.DeserializePath(ref reader, path, TValue.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TValue}(ReadOnlyMemory{byte}, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
	public TValue? DeserializePath<TValue>(ReadOnlyMemory<byte> utf8Json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TValue : IShapeable<TValue> => this.DeserializePath(utf8Json, path, TValue.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TValue}(in ReadOnlySequence{byte}, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
	public TValue? DeserializePath<TValue>(scoped in ReadOnlySequence<byte> utf8Json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TValue : IShapeable<TValue> => this.DeserializePath(utf8Json, path, TValue.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TValue}(Stream, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
	public TValue? DeserializePath<TValue>(Stream stream, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TValue : IShapeable<TValue> => this.DeserializePath(stream, path, TValue.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="DeserializePathEnumerableAsync{TElement}(PipeReader, JsonPath, ITypeShape{TElement}, MissingPathBehavior, CancellationToken)"/>
	public IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TElement>(PipeReader reader, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TElement : IShapeable<TElement> => this.DeserializePathEnumerableAsync(reader, path, TElement.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="DeserializePathEnumerableAsync{TElement}(Stream, JsonPath, ITypeShape{TElement}, MissingPathBehavior, CancellationToken)"/>
	public IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TElement>(Stream stream, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TElement : IShapeable<TElement> => this.DeserializePathEnumerableAsync(stream, path, TElement.GetTypeShape(), missingBehavior, cancellationToken);
#endif
}

public static partial class JsonSerializerExtensions
{
	/// <inheritdoc cref="JsonSerializer.DeserializePath{TValue}(ref JsonReader, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TValue? DeserializePath<TValue>(this JsonSerializer self, ref JsonReader reader, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(ref reader, path, ResolveTypeShapeOrThrow<TValue>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TValue}(ref JsonReader, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TValue? DeserializePath<TValue, TProvider>(this JsonSerializer self, ref JsonReader reader, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(ref reader, path, ResolveTypeShapeOrThrow<TValue, TProvider>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TValue}(ReadOnlyMemory{byte}, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TValue? DeserializePath<TValue>(this JsonSerializer self, ReadOnlyMemory<byte> utf8Json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(utf8Json, path, ResolveTypeShapeOrThrow<TValue>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TValue}(ReadOnlyMemory{byte}, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TValue? DeserializePath<TValue, TProvider>(this JsonSerializer self, ReadOnlyMemory<byte> utf8Json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(utf8Json, path, ResolveTypeShapeOrThrow<TValue, TProvider>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TValue}(in ReadOnlySequence{byte}, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TValue? DeserializePath<TValue>(this JsonSerializer self, scoped in ReadOnlySequence<byte> utf8Json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(utf8Json, path, ResolveTypeShapeOrThrow<TValue>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TValue}(in ReadOnlySequence{byte}, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TValue? DeserializePath<TValue, TProvider>(this JsonSerializer self, scoped in ReadOnlySequence<byte> utf8Json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(utf8Json, path, ResolveTypeShapeOrThrow<TValue, TProvider>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TValue}(Stream, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TValue? DeserializePath<TValue>(this JsonSerializer self, Stream stream, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(stream, path, ResolveTypeShapeOrThrow<TValue>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TValue}(Stream, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TValue? DeserializePath<TValue, TProvider>(this JsonSerializer self, Stream stream, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(stream, path, ResolveTypeShapeOrThrow<TValue, TProvider>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TValue}(string, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TValue? DeserializePath<TValue>(this JsonSerializer self, string json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(json, path, ResolveTypeShapeOrThrow<TValue>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TValue}(string, JsonPath, ITypeShape{TValue}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TValue? DeserializePath<TValue, TProvider>(this JsonSerializer self, string json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(json, path, ResolveTypeShapeOrThrow<TValue, TProvider>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePathEnumerableAsync{TElement}(PipeReader, JsonPath, ITypeShape{TElement}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TElement>(this JsonSerializer self, PipeReader reader, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePathEnumerableAsync(reader, path, ResolveTypeShapeOrThrow<TElement>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePathEnumerableAsync{TElement}(PipeReader, JsonPath, ITypeShape{TElement}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TElement, TProvider>(this JsonSerializer self, PipeReader reader, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePathEnumerableAsync(reader, path, ResolveTypeShapeOrThrow<TElement, TProvider>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePathEnumerableAsync{TElement}(Stream, JsonPath, ITypeShape{TElement}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TElement>(this JsonSerializer self, Stream stream, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePathEnumerableAsync(stream, path, ResolveTypeShapeOrThrow<TElement>(self.ConverterCache), missingBehavior, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePathEnumerableAsync{TElement}(Stream, JsonPath, ITypeShape{TElement}, MissingPathBehavior, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TElement, TProvider>(this JsonSerializer self, Stream stream, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePathEnumerableAsync(stream, path, ResolveTypeShapeOrThrow<TElement, TProvider>(self.ConverterCache), missingBehavior, cancellationToken);
}
