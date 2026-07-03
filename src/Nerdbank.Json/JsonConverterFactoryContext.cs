// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal state is intentionally undocumented in this file.

namespace Nerdbank.Json;

/// <summary>
/// Provides converter lookup services while a <see cref="IJsonConverterFactory"/> is creating a converter.
/// </summary>
public readonly struct JsonConverterFactoryContext
{
	private readonly ConverterCache? cache;

	internal JsonConverterFactoryContext(ConverterCache cache)
	{
		this.cache = cache;
	}

#if NET
	/// <summary>
	/// Gets a converter for a specific type.
	/// </summary>
	/// <typeparam name="T">The type to convert.</typeparam>
	/// <returns>The converter.</returns>
	public JsonConverter<T> GetConverter<T>()
		where T : IShapeable<T>
		=> this.GetConverter(T.GetTypeShape());

	/// <summary>
	/// Gets a converter for a specific type.
	/// </summary>
	/// <typeparam name="T">The type to convert.</typeparam>
	/// <typeparam name="TProvider">The witness type that provides the shape for <typeparamref name="T"/>.</typeparam>
	/// <returns>The converter.</returns>
	public JsonConverter<T> GetConverter<T, TProvider>()
		where TProvider : IShapeable<T>
		=> this.GetConverter(TProvider.GetTypeShape());
#endif

	/// <summary>
	/// Gets a converter for a specific type shape.
	/// </summary>
	/// <typeparam name="T">The type to convert.</typeparam>
	/// <param name="shape">The type shape.</param>
	/// <returns>The converter.</returns>
	public JsonConverter<T> GetConverter<T>(ITypeShape<T> shape)
	{
		Requires.NotNull(shape);

		return this.cache?.GetOrAddConverter(shape) ?? throw new InvalidOperationException("No converter factory context is active.");
	}

	internal JsonConverter<T> GetConverterDynamically<T>()
		=> this.cache?.GetOrAddConverter<T>() ?? throw new InvalidOperationException("No converter factory context is active.");
}
