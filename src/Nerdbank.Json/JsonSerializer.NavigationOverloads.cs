// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1601 // Partial elements are documented on their primary declaration.
#pragma warning disable SA1402 // Related serializer overloads share this file.

namespace Nerdbank.Json;

public partial record JsonSerializer
{
#if NET
	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(ref JsonReader, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
	public TElement? DeserializePath<TRoot, TElement>(ref JsonReader reader, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializePath(ref reader, TRoot.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(ReadOnlyMemory{byte}, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
	public TElement? DeserializePath<TRoot, TElement>(ReadOnlyMemory<byte> utf8Json, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializePath(utf8Json, TRoot.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(in ReadOnlySequence{byte}, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
	public TElement? DeserializePath<TRoot, TElement>(scoped in ReadOnlySequence<byte> utf8Json, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializePath(utf8Json, TRoot.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(Stream, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
	public TElement? DeserializePath<TRoot, TElement>(Stream stream, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializePath(stream, TRoot.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(string, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
	public TElement? DeserializePath<TRoot, TElement>(string json, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializePath(json, TRoot.GetTypeShape(), options, cancellationToken);
#endif
}

public static partial class JsonSerializerExtensions
{
	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(ref JsonReader, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement>(this JsonSerializer self, ref JsonReader reader, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(ref reader, ResolveTypeShapeOrThrow<TRoot>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(ref JsonReader, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement, TProvider>(this JsonSerializer self, ref JsonReader reader, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(ref reader, ResolveTypeShapeOrThrow<TRoot, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(ReadOnlyMemory{byte}, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement>(this JsonSerializer self, ReadOnlyMemory<byte> utf8Json, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(utf8Json, ResolveTypeShapeOrThrow<TRoot>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(ReadOnlyMemory{byte}, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement, TProvider>(this JsonSerializer self, ReadOnlyMemory<byte> utf8Json, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(utf8Json, ResolveTypeShapeOrThrow<TRoot, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(in ReadOnlySequence{byte}, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement>(this JsonSerializer self, scoped in ReadOnlySequence<byte> utf8Json, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(utf8Json, ResolveTypeShapeOrThrow<TRoot>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(in ReadOnlySequence{byte}, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement, TProvider>(this JsonSerializer self, scoped in ReadOnlySequence<byte> utf8Json, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(utf8Json, ResolveTypeShapeOrThrow<TRoot, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(Stream, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement>(this JsonSerializer self, Stream stream, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(stream, ResolveTypeShapeOrThrow<TRoot>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(Stream, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement, TProvider>(this JsonSerializer self, Stream stream, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(stream, ResolveTypeShapeOrThrow<TRoot, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(string, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement>(this JsonSerializer self, string json, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(json, ResolveTypeShapeOrThrow<TRoot>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(string, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement, TProvider>(this JsonSerializer self, string json, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(json, ResolveTypeShapeOrThrow<TRoot, TProvider>(self.ConverterCache), options, cancellationToken);
}
