// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>Extension methods for converter factories.</summary>
public static class JsonConverterFactoryExtensions
{
	/// <summary>Invokes a factory with the generic type described by a shape.</summary>
	/// <typeparam name="T">The factory type.</typeparam>
	/// <param name="self">The factory.</param>
	/// <param name="shape">The type shape.</param>
	/// <param name="state">Optional state passed to the factory.</param>
	/// <returns>The converter, or null when the factory does not apply.</returns>
	public static JsonConverter? Invoke<T>(this T self, ITypeShape shape, object? state = null)
		where T : IJsonConverterFactory, ITypeShapeFunc
	{
		Requires.NotNull(shape);
		return (JsonConverter?)shape.Invoke(self, state);
	}
}
