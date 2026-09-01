// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using PolyType;
using PolyType.Abstractions;

namespace Nerdbank.Json.AspNetCore;

/// <summary>
/// Bridges a runtime <see cref="Type"/> to the strongly typed, source-generated serialization APIs on
/// <see cref="JsonSerializer"/> using an explicitly supplied <see cref="ITypeShapeProvider"/>.
/// </summary>
/// <remarks>
/// The bridge uses PolyType's <see cref="ITypeShape.Invoke(ITypeShapeFunc, object?)"/> visitor to enter a generic
/// context without reflection or expression compilation, keeping the formatters trimming- and NativeAOT-safe.
/// </remarks>
internal static class RuntimeShapeSerialization
{
	/// <summary>
	/// Deserializes a value of the specified runtime <paramref name="type"/> from a <see cref="PipeReader"/>.
	/// </summary>
	/// <param name="serializer">The serializer to use.</param>
	/// <param name="provider">The shape provider that must contain a shape for <paramref name="type"/>.</param>
	/// <param name="type">The runtime type to deserialize.</param>
	/// <param name="reader">The pipe to read from.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value, boxed.</returns>
	/// <exception cref="MissingTypeShapeException">Thrown when <paramref name="provider"/> has no shape for <paramref name="type"/>.</exception>
	internal static Task<object?> DeserializeAsync(JsonSerializer serializer, ITypeShapeProvider provider, Type type, PipeReader reader, CancellationToken cancellationToken)
	{
		ITypeShape shape = provider.GetTypeShape(type) ?? throw new MissingTypeShapeException(type, provider);
		DeserializeFunc func = new(serializer, reader, cancellationToken);
		return (Task<object?>)shape.Invoke(func)!;
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
	internal static Task SerializeAsync(JsonSerializer serializer, ITypeShapeProvider provider, Type type, object? value, PipeWriter writer, CancellationToken cancellationToken)
	{
		ITypeShape shape = provider.GetTypeShape(type) ?? throw new MissingTypeShapeException(type, provider);
		SerializeFunc func = new(serializer, value, writer, cancellationToken);
		return (Task)shape.Invoke(func)!;
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

	private sealed class DeserializeFunc(JsonSerializer serializer, PipeReader reader, CancellationToken cancellationToken) : ITypeShapeFunc
	{
		public object? Invoke<T>(ITypeShape<T> typeShape, object? state) => this.InvokeAsync(typeShape);

		private async Task<object?> InvokeAsync<T>(ITypeShape<T> typeShape)
			=> await serializer.DeserializeAsync(reader, typeShape, cancellationToken).ConfigureAwait(false);
	}

	private sealed class SerializeFunc(JsonSerializer serializer, object? value, PipeWriter writer, CancellationToken cancellationToken) : ITypeShapeFunc
	{
		public object? Invoke<T>(ITypeShape<T> typeShape, object? state) => this.InvokeAsync(typeShape);

		private async Task InvokeAsync<T>(ITypeShape<T> typeShape)
			=> await serializer.SerializeAsync(writer, (T?)value, typeShape, cancellationToken).ConfigureAwait(false);
	}

	private sealed class DefaultValueFunc : ITypeShapeFunc
	{
		internal static readonly DefaultValueFunc Instance = new();

		public object? Invoke<T>(ITypeShape<T> typeShape, object? state) => default(T);
	}
}
