// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1601 // Partial elements should be documented

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Json;

public static partial class JsonSerializerExtensions
{
	/// <summary>
	/// Gets a converter for a specific type.
	/// </summary>
	/// <typeparam name="T">The type to convert.</typeparam>
	/// <param name="context">The converter factory context.</param>
	/// <returns>The converter.</returns>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[Obsolete(JsonSerializer.PreferTypeConstrainedInstanceOverloads, error: true)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#endif
	public static JsonConverter<T> GetConverter<T>(this in JsonConverterFactoryContext context)
		=> context.GetConverterDynamically<T>();
}
