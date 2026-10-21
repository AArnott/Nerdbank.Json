// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using PolyType;
using PolyType.Abstractions;

namespace Nerdbank.Json.AspNetCoreMvcFormatter;

/// <summary>
/// Resolves runtime type shapes for the source-generated serialization APIs on
/// <see cref="JsonSerializer"/> using an explicitly supplied <see cref="ITypeShapeProvider"/>.
/// </summary>
/// <remarks>
/// Only default-value calculation requires a generic visitor. Serialization uses the runtime-object APIs
/// without reflection or expression compilation, keeping the formatters trimming- and NativeAOT-safe.
/// </remarks>
internal static class RuntimeShapeSerialization
{
	/// <summary>
	/// Deserializes a complete JSON document of the specified runtime <paramref name="type"/> from a stream.
	/// </summary>
	/// <param name="serializer">The serializer to use.</param>
	/// <param name="provider">The shape provider that must contain a shape for <paramref name="type"/>.</param>
	/// <param name="type">The runtime type to deserialize.</param>
	/// <param name="stream">The stream to read from, which is left open.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value, boxed.</returns>
	/// <exception cref="MissingTypeShapeException">Thrown when <paramref name="provider"/> has no shape for <paramref name="type"/>.</exception>
	internal static ValueTask<object?> DeserializeAsync(JsonSerializer serializer, ITypeShapeProvider provider, Type type, Stream stream, CancellationToken cancellationToken)
	{
		ITypeShape shape = provider.GetTypeShape(type) ?? throw new MissingTypeShapeException(type, provider);
		return serializer.DeserializeObjectAsync(stream, shape, cancellationToken);
	}

	/// <summary>
	/// Serializes a boxed <paramref name="value"/> of the specified runtime <paramref name="type"/> to a
	/// <see cref="PipeWriter"/>.
	/// </summary>
	/// <param name="serializer">The serializer to use.</param>
	/// <param name="provider">The shape provider that must contain a shape for <paramref name="type"/>.</param>
	/// <param name="type">The runtime type to serialize as.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="writer">The pipe to write to.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task tracking the asynchronous serialization.</returns>
	/// <exception cref="MissingTypeShapeException">Thrown when <paramref name="provider"/> has no shape for <paramref name="type"/>.</exception>
	internal static ValueTask SerializeAsync(JsonSerializer serializer, ITypeShapeProvider provider, Type type, object? value, PipeWriter writer, CancellationToken cancellationToken)
	{
		ITypeShape shape = provider.GetTypeShape(type) ?? throw new MissingTypeShapeException(type, provider);
		return serializer.SerializeObjectAsync(writer, value, shape, cancellationToken);
	}

	/// <summary>
	/// Gets the boxed default value (<c>default(T)</c>) for the runtime <paramref name="type"/> without reflection.
	/// </summary>
	/// <param name="provider">The shape provider that must contain a shape for <paramref name="type"/>.</param>
	/// <param name="type">The runtime type whose default value is requested.</param>
	/// <returns>The boxed default value: <see langword="null"/> for reference types and a zero-initialized instance for value types.</returns>
	/// <exception cref="MissingTypeShapeException">Thrown when <paramref name="provider"/> has no shape for <paramref name="type"/>.</exception>
	internal static object? GetDefaultValue(ITypeShapeProvider provider, Type type)
	{
		ITypeShape shape = provider.GetTypeShape(type) ?? throw new MissingTypeShapeException(type, provider);
		return shape.Invoke(DefaultValueFunc.Instance);
	}

	private sealed class DefaultValueFunc : ITypeShapeFunc
	{
		internal static readonly DefaultValueFunc Instance = new();

		public object? Invoke<T>(ITypeShape<T> typeShape, object? state) => default(T);
	}
}
