// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Nerdbank.Json;

/// <summary>
/// Extension methods for the <see cref="JsonSerializer"/> class.
/// </summary>
/// <remarks>
/// These extension methods preserve the convenient serializer entry points for target frameworks
/// that cannot use the constrained <c>IShapeable&lt;T&gt;</c> instance overloads, and for values that
/// rely on dynamic shape discovery or witness types.
/// </remarks>
public static partial class JsonSerializerExtensions
{
	private static JsonSerializer RequireSerializer(JsonSerializer? serializer)
		=> serializer ?? throw new ArgumentNullException(nameof(serializer));

	private static ITypeShape<T> ResolveTypeShapeOrThrow<T, TProvider>(JsonConverterCache cache)
		=> cache.ResolveDynamicTypeShapeOrThrow<T, TProvider>();
}
