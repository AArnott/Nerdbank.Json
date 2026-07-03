// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1601 // Partial elements should be documented
#pragma warning disable RS0026 // optional parameter on a method with overloads

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Json;

#if NET

public partial record JsonSerializer
{
	/// <inheritdoc cref="Serialize{T}(ref JsonWriter, in T, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public void Serialize<T>(ref JsonWriter writer, in T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Serialize(ref writer, value, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Serialize{T}(in T, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public string Serialize<T>(in T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Serialize(value, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Serialize{T}(IBufferWriter{byte}, in T, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public void Serialize<T>(IBufferWriter<byte> writer, in T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Serialize(writer, value, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Serialize{T}(Stream, in T, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public void Serialize<T>(Stream stream, in T? value, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Serialize(stream, value, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Deserialize{T}(string, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public T? Deserialize<T>(string json, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Deserialize(json, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Deserialize{T}(ref JsonReader, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public T? Deserialize<T>(ref JsonReader reader, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Deserialize(ref reader, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Deserialize{T}(ReadOnlyMemory{byte}, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public T? Deserialize<T>(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Deserialize(bytes, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Deserialize{T}(in ReadOnlySequence{byte}, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public T? Deserialize<T>(scoped in ReadOnlySequence<byte> bytes, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Deserialize(bytes, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Deserialize{T}(Stream, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public T? Deserialize<T>(Stream stream, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.Deserialize(stream, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Serialize{T}(ref JsonWriter, in T, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public void Serialize<T, TProvider>(ref JsonWriter writer, in T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.Serialize(ref writer, value, TProvider.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Serialize{T}(in T, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public string Serialize<T, TProvider>(in T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.Serialize(value, TProvider.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Serialize{T}(IBufferWriter{byte}, in T, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public void Serialize<T, TProvider>(IBufferWriter<byte> writer, in T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.Serialize(writer, value, TProvider.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Serialize{T}(Stream, in T, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public void Serialize<T, TProvider>(Stream stream, in T? value, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.Serialize(stream, value, TProvider.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Deserialize{T}(string, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public T? Deserialize<T, TProvider>(string json, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.Deserialize(json, TProvider.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Deserialize{T}(ref JsonReader, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public T? Deserialize<T, TProvider>(ref JsonReader reader, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.Deserialize(ref reader, TProvider.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Deserialize{T}(ReadOnlyMemory{byte}, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public T? Deserialize<T, TProvider>(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.Deserialize(bytes, TProvider.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Deserialize{T}(in ReadOnlySequence{byte}, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public T? Deserialize<T, TProvider>(scoped in ReadOnlySequence<byte> bytes, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.Deserialize(bytes, TProvider.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="Deserialize{T}(Stream, ITypeShape{T}, CancellationToken)" />
	[ExcludeFromCodeCoverage]
	public T? Deserialize<T, TProvider>(Stream stream, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<T> => this.Deserialize(stream, TProvider.GetTypeShape(), cancellationToken);
}

#endif

public static partial class JsonSerializerExtensions
{
	/// <inheritdoc cref="JsonSerializer.Serialize{T}(ref JsonWriter, in T, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> has no type shape created via the <see cref="GenerateShapeAttribute"/> source generator.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="T"/> is decorated with the <see cref="GenerateShapeAttribute"/>.
	/// For non-decorated types, apply <see cref="GenerateShapeForAttribute{T}"/> to a witness type and call a serialize overload that takes a TProvider generic type parameter instead,
	/// or use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static void Serialize<T>(this JsonSerializer self, ref JsonWriter writer, in T? value, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Serialize(ref writer, value, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Serialize{T}(in T, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> has no type shape created via the <see cref="GenerateShapeAttribute"/> source generator.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="T"/> is decorated with the <see cref="GenerateShapeAttribute"/>.
	/// For non-decorated types, apply <see cref="GenerateShapeForAttribute{T}"/> to a witness type and call a serialize overload that takes a TProvider generic type parameter instead,
	/// or use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static string Serialize<T>(this JsonSerializer self, in T? value, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Serialize(value, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Serialize{T}(IBufferWriter{byte}, in T, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> has no type shape created via the <see cref="GenerateShapeAttribute"/> source generator.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="T"/> is decorated with the <see cref="GenerateShapeAttribute"/>.
	/// For non-decorated types, apply <see cref="GenerateShapeForAttribute{T}"/> to a witness type and call a serialize overload that takes a TProvider generic type parameter instead,
	/// or use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static void Serialize<T>(this JsonSerializer self, IBufferWriter<byte> writer, in T? value, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Serialize(writer, value, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Serialize{T}(Stream, in T, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> has no type shape created via the <see cref="GenerateShapeAttribute"/> source generator.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="T"/> is decorated with the <see cref="GenerateShapeAttribute"/>.
	/// For non-decorated types, apply <see cref="GenerateShapeForAttribute{T}"/> to a witness type and call a serialize overload that takes a TProvider generic type parameter instead,
	/// or use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static void Serialize<T>(this JsonSerializer self, Stream stream, in T? value, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Serialize(stream, value, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Deserialize{T}(string, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> has no type shape created via the <see cref="GenerateShapeAttribute"/> source generator.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="T"/> is decorated with the <see cref="GenerateShapeAttribute"/>.
	/// For non-decorated types, apply <see cref="GenerateShapeForAttribute{T}"/> to a witness type and call a serialize overload that takes a TProvider generic type parameter instead,
	/// or use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static T? Deserialize<T>(this JsonSerializer self, string json, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Deserialize(json, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Deserialize{T}(ref JsonReader, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> has no type shape created via the <see cref="GenerateShapeAttribute"/> source generator.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="T"/> is decorated with the <see cref="GenerateShapeAttribute"/>.
	/// For non-decorated types, apply <see cref="GenerateShapeForAttribute{T}"/> to a witness type and call a serialize overload that takes a TProvider generic type parameter instead,
	/// or use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static T? Deserialize<T>(this JsonSerializer self, ref JsonReader reader, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Deserialize(ref reader, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Deserialize{T}(ReadOnlyMemory{byte}, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> has no type shape created via the <see cref="GenerateShapeAttribute"/> source generator.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="T"/> is decorated with the <see cref="GenerateShapeAttribute"/>.
	/// For non-decorated types, apply <see cref="GenerateShapeForAttribute{T}"/> to a witness type and call a serialize overload that takes a TProvider generic type parameter instead,
	/// or use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static T? Deserialize<T>(this JsonSerializer self, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Deserialize(bytes, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Deserialize{T}(in ReadOnlySequence{byte}, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> has no type shape created via the <see cref="GenerateShapeAttribute"/> source generator.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="T"/> is decorated with the <see cref="GenerateShapeAttribute"/>.
	/// For non-decorated types, apply <see cref="GenerateShapeForAttribute{T}"/> to a witness type and call a serialize overload that takes a TProvider generic type parameter instead,
	/// or use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static T? Deserialize<T>(this JsonSerializer self, scoped in ReadOnlySequence<byte> bytes, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Deserialize(bytes, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Deserialize{T}(Stream, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> has no type shape created via the <see cref="GenerateShapeAttribute"/> source generator.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="T"/> is decorated with the <see cref="GenerateShapeAttribute"/>.
	/// For non-decorated types, apply <see cref="GenerateShapeForAttribute{T}"/> to a witness type and call a serialize overload that takes a TProvider generic type parameter instead,
	/// or use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static T? Deserialize<T>(this JsonSerializer self, Stream stream, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Deserialize(stream, ResolveTypeShapeOrThrow<T>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Serialize{T}(ref JsonWriter, in T, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="TProvider"/> has no <see cref="GenerateShapeForAttribute{T}"/> source generator attribute for <typeparamref name="T"/>.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="TProvider"/> is decorated with a <see cref="GenerateShapeForAttribute{T}"/>.
	/// Use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static void Serialize<T, TProvider>(this JsonSerializer self, ref JsonWriter writer, in T? value, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Serialize(ref writer, value, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Serialize{T}(in T, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="TProvider"/> has no <see cref="GenerateShapeForAttribute{T}"/> source generator attribute for <typeparamref name="T"/>.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="TProvider"/> is decorated with a <see cref="GenerateShapeForAttribute{T}"/>.
	/// Use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static string Serialize<T, TProvider>(this JsonSerializer self, in T? value, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Serialize(value, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Serialize{T}(IBufferWriter{byte}, in T, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="TProvider"/> has no <see cref="GenerateShapeForAttribute{T}"/> source generator attribute for <typeparamref name="T"/>.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="TProvider"/> is decorated with a <see cref="GenerateShapeForAttribute{T}"/>.
	/// Use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static void Serialize<T, TProvider>(this JsonSerializer self, IBufferWriter<byte> writer, in T? value, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Serialize(writer, value, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Serialize{T}(Stream, in T, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="TProvider"/> has no <see cref="GenerateShapeForAttribute{T}"/> source generator attribute for <typeparamref name="T"/>.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="TProvider"/> is decorated with a <see cref="GenerateShapeForAttribute{T}"/>.
	/// Use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static void Serialize<T, TProvider>(this JsonSerializer self, Stream stream, in T? value, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Serialize(stream, value, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Deserialize{T}(string, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="TProvider"/> has no <see cref="GenerateShapeForAttribute{T}"/> source generator attribute for <typeparamref name="T"/>.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="TProvider"/> is decorated with a <see cref="GenerateShapeForAttribute{T}"/>.
	/// Use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static T? Deserialize<T, TProvider>(this JsonSerializer self, string json, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Deserialize(json, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Deserialize{T}(ref JsonReader, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="TProvider"/> has no <see cref="GenerateShapeForAttribute{T}"/> source generator attribute for <typeparamref name="T"/>.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="TProvider"/> is decorated with a <see cref="GenerateShapeForAttribute{T}"/>.
	/// Use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static T? Deserialize<T, TProvider>(this JsonSerializer self, ref JsonReader reader, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Deserialize(ref reader, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Deserialize{T}(ReadOnlyMemory{byte}, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="TProvider"/> has no <see cref="GenerateShapeForAttribute{T}"/> source generator attribute for <typeparamref name="T"/>.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="TProvider"/> is decorated with a <see cref="GenerateShapeForAttribute{T}"/>.
	/// Use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static T? Deserialize<T, TProvider>(this JsonSerializer self, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Deserialize(bytes, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Deserialize{T}(in ReadOnlySequence{byte}, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="TProvider"/> has no <see cref="GenerateShapeForAttribute{T}"/> source generator attribute for <typeparamref name="T"/>.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="TProvider"/> is decorated with a <see cref="GenerateShapeForAttribute{T}"/>.
	/// Use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static T? Deserialize<T, TProvider>(this JsonSerializer self, scoped in ReadOnlySequence<byte> bytes, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Deserialize(bytes, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);

	/// <inheritdoc cref="JsonSerializer.Deserialize{T}(Stream, ITypeShape{T}, CancellationToken)" />
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="TProvider"/> has no <see cref="GenerateShapeForAttribute{T}"/> source generator attribute for <typeparamref name="T"/>.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="TProvider"/> is decorated with a <see cref="GenerateShapeForAttribute{T}"/>.
	/// Use an overload that accepts a <see cref="ITypeShape{T}"/> for an option that does not require source generation.
	/// </remarks>
	[ExcludeFromCodeCoverage]
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static T? Deserialize<T, TProvider>(this JsonSerializer self, Stream stream, CancellationToken cancellationToken = default)
		=> Requires.NotNull(self).Deserialize(stream, ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), cancellationToken);
}
