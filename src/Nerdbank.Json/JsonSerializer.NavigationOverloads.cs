// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1601 // Partial elements are documented on their primary declaration.
#pragma warning disable SA1402 // Related serializer overloads share this file.

namespace Nerdbank.Json;

public partial record JsonSerializer
{
#if NET
	/// <inheritdoc cref="DeserializePathEnumerableAsync{TRoot, TElement}(System.IO.Pipelines.PipeReader, ITypeShape{TRoot}, StreamingEnumerationOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected array element type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="reader">The input pipe. <paramref name="options"/> controls whether it is completed.</param>
	/// <param name="options">The path, missing-path behavior, and pipe ownership options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TRoot, TElement, TProvider>(System.IO.Pipelines.PipeReader reader, StreamingEnumerationOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TRoot> => this.DeserializePathEnumerableAsync(reader, TProvider.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePathEnumerableAsync{TRoot, TElement}(Stream, ITypeShape{TRoot}, StreamingEnumerationOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected array element type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="stream">The input stream, which remains open.</param>
	/// <param name="options">The path and enumeration options. The internally created pipe is always completed.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TRoot, TElement, TProvider>(Stream stream, StreamingEnumerationOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TRoot> => this.DeserializePathEnumerableAsync(stream, TProvider.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(ref JsonReader, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="reader">The reader positioned at the root document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement, TProvider>(ref JsonReader reader, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TRoot> => this.DeserializePath(ref reader, TProvider.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(ReadOnlyMemory{byte}, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="utf8Json">The UTF-8 document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement, TProvider>(ReadOnlyMemory<byte> utf8Json, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TRoot> => this.DeserializePath(utf8Json, TProvider.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(in ReadOnlySequence{byte}, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="utf8Json">The possibly segmented UTF-8 document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement, TProvider>(scoped in ReadOnlySequence<byte> utf8Json, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TRoot> => this.DeserializePath(utf8Json, TProvider.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(Stream, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="stream">The document stream, which is read to its end and remains open.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement, TProvider>(Stream stream, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TRoot> => this.DeserializePath(stream, TProvider.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(string, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement, TProvider>(string json, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TRoot> => this.DeserializePath(json, TProvider.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePathEnumerableAsync{TRoot, TElement}(System.IO.Pipelines.PipeReader, ITypeShape{TRoot}, StreamingEnumerationOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type, which supplies its shape.</typeparam>
	/// <typeparam name="TElement">The selected array element type.</typeparam>
	/// <param name="reader">The input pipe. <paramref name="options"/> controls whether it is completed.</param>
	/// <param name="options">The path, missing-path behavior, and pipe ownership options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TRoot, TElement>(System.IO.Pipelines.PipeReader reader, StreamingEnumerationOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializePathEnumerableAsync(reader, TRoot.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePathEnumerableAsync{TRoot, TElement}(Stream, ITypeShape{TRoot}, StreamingEnumerationOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type, which supplies its shape.</typeparam>
	/// <typeparam name="TElement">The selected array element type.</typeparam>
	/// <param name="stream">The input stream, which remains open.</param>
	/// <param name="options">The path and enumeration options. The internally created pipe is always completed.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TRoot, TElement>(Stream stream, StreamingEnumerationOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializePathEnumerableAsync(stream, TRoot.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(ref JsonReader, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type, which supplies its shape.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <param name="reader">The reader positioned at the root document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement>(ref JsonReader reader, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializePath(ref reader, TRoot.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(ReadOnlyMemory{byte}, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type, which supplies its shape.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <param name="utf8Json">The UTF-8 document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement>(ReadOnlyMemory<byte> utf8Json, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializePath(utf8Json, TRoot.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(in ReadOnlySequence{byte}, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type, which supplies its shape.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <param name="utf8Json">The possibly segmented UTF-8 document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement>(scoped in ReadOnlySequence<byte> utf8Json, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializePath(utf8Json, TRoot.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(Stream, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type, which supplies its shape.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <param name="stream">The document stream, which is read to its end and remains open.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement>(Stream stream, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializePath(stream, TRoot.GetTypeShape(), options, cancellationToken);

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(string, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type, which supplies its shape.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement>(string json, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializePath(json, TRoot.GetTypeShape(), options, cancellationToken);
#endif
}

public static partial class JsonSerializerExtensions
{
	/// <inheritdoc cref="JsonSerializer.DeserializePathEnumerableAsync{TRoot, TElement}(System.IO.Pipelines.PipeReader, ITypeShape{TRoot}, JsonSerializer.StreamingEnumerationOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected array element type.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="reader">The input pipe. <paramref name="options"/> controls whether it is completed.</param>
	/// <param name="options">The path, missing-path behavior, and pipe ownership options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TRoot, TElement>(this JsonSerializer self, System.IO.Pipelines.PipeReader reader, JsonSerializer.StreamingEnumerationOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePathEnumerableAsync(reader, ResolveTypeShapeOrThrow<TRoot>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePathEnumerableAsync{TRoot, TElement}(System.IO.Pipelines.PipeReader, ITypeShape{TRoot}, JsonSerializer.StreamingEnumerationOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected array element type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="reader">The input pipe. <paramref name="options"/> controls whether it is completed.</param>
	/// <param name="options">The path, missing-path behavior, and pipe ownership options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TRoot, TElement, TProvider>(this JsonSerializer self, System.IO.Pipelines.PipeReader reader, JsonSerializer.StreamingEnumerationOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePathEnumerableAsync(reader, ResolveTypeShapeOrThrow<TRoot, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePathEnumerableAsync{TRoot, TElement}(Stream, ITypeShape{TRoot}, JsonSerializer.StreamingEnumerationOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected array element type.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="stream">The input stream, which remains open.</param>
	/// <param name="options">The path and enumeration options. The internally created pipe is always completed.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TRoot, TElement>(this JsonSerializer self, Stream stream, JsonSerializer.StreamingEnumerationOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePathEnumerableAsync(stream, ResolveTypeShapeOrThrow<TRoot>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePathEnumerableAsync{TRoot, TElement}(Stream, ITypeShape{TRoot}, JsonSerializer.StreamingEnumerationOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected array element type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="stream">The input stream, which remains open.</param>
	/// <param name="options">The path and enumeration options. The internally created pipe is always completed.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static IAsyncEnumerable<TElement?> DeserializePathEnumerableAsync<TRoot, TElement, TProvider>(this JsonSerializer self, Stream stream, JsonSerializer.StreamingEnumerationOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePathEnumerableAsync(stream, ResolveTypeShapeOrThrow<TRoot, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(ref JsonReader, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="reader">The reader positioned at the root document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement>(this JsonSerializer self, ref JsonReader reader, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(ref reader, ResolveTypeShapeOrThrow<TRoot>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(ref JsonReader, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="reader">The reader positioned at the root document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement, TProvider>(this JsonSerializer self, ref JsonReader reader, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(ref reader, ResolveTypeShapeOrThrow<TRoot, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(ReadOnlyMemory{byte}, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="utf8Json">The UTF-8 document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement>(this JsonSerializer self, ReadOnlyMemory<byte> utf8Json, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(utf8Json, ResolveTypeShapeOrThrow<TRoot>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(ReadOnlyMemory{byte}, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="utf8Json">The UTF-8 document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement, TProvider>(this JsonSerializer self, ReadOnlyMemory<byte> utf8Json, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(utf8Json, ResolveTypeShapeOrThrow<TRoot, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(in ReadOnlySequence{byte}, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="utf8Json">The possibly segmented UTF-8 document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement>(this JsonSerializer self, scoped in ReadOnlySequence<byte> utf8Json, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(utf8Json, ResolveTypeShapeOrThrow<TRoot>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(in ReadOnlySequence{byte}, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="utf8Json">The possibly segmented UTF-8 document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement, TProvider>(this JsonSerializer self, scoped in ReadOnlySequence<byte> utf8Json, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(utf8Json, ResolveTypeShapeOrThrow<TRoot, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(Stream, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="stream">The document stream, which is read to its end and remains open.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement>(this JsonSerializer self, Stream stream, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(stream, ResolveTypeShapeOrThrow<TRoot>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(Stream, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="stream">The document stream, which is read to its end and remains open.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement, TProvider>(this JsonSerializer self, Stream stream, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(stream, ResolveTypeShapeOrThrow<TRoot, TProvider>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(string, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="json">The JSON document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement>(this JsonSerializer self, string json, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(json, ResolveTypeShapeOrThrow<TRoot>(self.ConverterCache), options, cancellationToken);

	/// <inheritdoc cref="JsonSerializer.DeserializePath{TRoot, TElement}(string, ITypeShape{TRoot}, in JsonSerializer.DeserializePathOptions{TRoot, TElement}, CancellationToken)" path="/*[not(self::param or self::typeparam)]"/>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <typeparam name="TProvider">The root type shape witness.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="json">The JSON document.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(ResolveDynamicMessage)]
#endif
	public static TElement? DeserializePath<TRoot, TElement, TProvider>(this JsonSerializer self, string json, in JsonSerializer.DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
		=> RequireSerializer(self).DeserializePath(json, ResolveTypeShapeOrThrow<TRoot, TProvider>(self.ConverterCache), options, cancellationToken);
}
