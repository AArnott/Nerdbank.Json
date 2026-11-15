// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Resolves the tagging mode for a C# union type.
/// </summary>
/// <param name="unionType">The union type.</param>
/// <returns>The mode, or <see langword="null"/> to use the attribute or serializer default.</returns>
public delegate UnionTagging? UnionTaggingResolver(Type unionType);
