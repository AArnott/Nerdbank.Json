// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal state is intentionally undocumented in this file.

namespace Nerdbank.Json;

/// <summary>
/// Provides converter lookup services while a <see cref="IJsonConverterFactory"/> is creating a converter.
/// </summary>
public readonly struct ConverterContext
{
	private readonly ConverterCache? cache;

	internal ConverterContext(ConverterCache cache, ITypeShapeProvider typeShapeProvider)
	{
		this.cache = cache;
		this.TypeShapeProvider = typeShapeProvider;
	}

	/// <summary>Gets the provider used to discover shapes for nested values.</summary>
	public ITypeShapeProvider TypeShapeProvider { get; }

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

	/// <summary>Gets a converter for an untyped shape.</summary>
	/// <param name="shape">The type shape.</param>
	/// <returns>The converter.</returns>
	public JsonConverter GetConverter(ITypeShape shape)
	{
		Requires.NotNull(shape);
		return this.cache?.GetOrAddConverter(shape) ?? throw new InvalidOperationException("No converter factory context is active.");
	}

	/// <summary>Gets a converter using an explicitly supplied or contextual provider.</summary>
	/// <typeparam name="T">The type to convert.</typeparam>
	/// <param name="provider">The provider, or null to use <see cref="TypeShapeProvider"/>.</param>
	/// <returns>The converter.</returns>
	[System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
	public JsonConverter<T> GetConverter<T>(ITypeShapeProvider? provider)
		=> (JsonConverter<T>)this.GetConverter(typeof(T), provider);

	/// <summary>Gets a converter for a runtime type.</summary>
	/// <param name="type">The type to convert.</param>
	/// <param name="provider">The provider, or null to use <see cref="TypeShapeProvider"/>.</param>
	/// <returns>The converter.</returns>
	public JsonConverter GetConverter(Type type, ITypeShapeProvider? provider = null)
	{
		Requires.NotNull(type);
		if (this.cache is null)
		{
			throw new InvalidOperationException("No converter factory context is active.");
		}

		return this.cache.GetOrAddConverter(type, provider ?? this.TypeShapeProvider);
	}

	internal JsonConverter<T> GetConverterDynamically<T>()
		=> this.cache?.GetOrAddConverter<T>() ?? throw new InvalidOperationException("No converter factory context is active.");

#if NET8_0
	[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(JsonSerializerExtensions.ResolveDynamicMessage)]
#endif
	internal JsonConverter<T> GetConverterDynamically<T, TProvider>()
		=> this.GetConverter((this.cache ?? throw new InvalidOperationException("No converter factory context is active.")).ResolveDynamicTypeShapeOrThrow<T, TProvider>());
}
