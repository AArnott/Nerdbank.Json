// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal state is intentionally undocumented in this file.

using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Json;

/// <summary>
/// Provides converter lookup services while a <see cref="IJsonConverterFactory"/> is creating a converter.
/// </summary>
public readonly struct JsonConverterFactoryContext
{
	private readonly JsonConverterCache? cache;

	internal JsonConverterFactoryContext(JsonConverterCache cache)
	{
		this.cache = cache;
	}

	/// <summary>
	/// Gets a converter for a specific type.
	/// </summary>
	/// <typeparam name="T">The type to convert.</typeparam>
	/// <returns>The converter.</returns>
#if NET
	[RequiresDynamicCode("Getting a converter without an explicit type shape may require runtime-generated shapes.")]
	[RequiresUnreferencedCode("Getting a converter without an explicit generated shape may require reflection metadata.")]
#endif
	public JsonConverter<T> GetConverter<T>()
		=> this.cache?.GetOrAddConverter<T>() ?? throw new InvalidOperationException("No converter factory context is active.");

	/// <summary>
	/// Gets a converter for a specific type shape.
	/// </summary>
	/// <typeparam name="T">The type to convert.</typeparam>
	/// <param name="shape">The type shape.</param>
	/// <returns>The converter.</returns>
	public JsonConverter<T> GetConverter<T>(ITypeShape<T> shape)
	{
		if (shape is null)
		{
			throw new ArgumentNullException(nameof(shape));
		}

		return this.cache?.GetOrAddConverter(shape) ?? throw new InvalidOperationException("No converter factory context is active.");
	}
}
