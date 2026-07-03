// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Creates <see cref="JsonConverter"/> instances for types discovered at runtime.
/// </summary>
public interface IJsonConverterFactory
{
	/// <summary>
	/// Creates a converter for the requested type if this factory applies.
	/// </summary>
	/// <param name="type">The requested data type.</param>
	/// <param name="shape">The PolyType shape describing <paramref name="type"/>, when available.</param>
	/// <param name="context">A context that can resolve additional converters needed by the created converter.</param>
	/// <returns>A converter if this factory applies to the type; otherwise, <see langword="null"/>.</returns>
	JsonConverter? CreateConverter(Type type, ITypeShape? shape, in JsonConverterFactoryContext context);
}
