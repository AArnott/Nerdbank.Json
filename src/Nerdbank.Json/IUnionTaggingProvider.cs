// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Selects the tagging mode for C# unions that cannot be annotated.
/// </summary>
/// <remarks>
/// <para>
/// A non-null result overrides <see cref="UnionTaggingAttribute"/> and <see cref="JsonSerializer.DefaultUnionTagging"/>.
/// Returning <see langword="null"/> leaves that decision to the attribute, then the serializer property.
/// Derived type hierarchies and F# unions are not consulted.
/// </para>
/// <para>
/// Implementations must be thread-safe and return the same result for as long as a serializer uses them.
/// The mode is resolved while constructing converters, not for each serialized value.
/// </para>
/// </remarks>
public interface IUnionTaggingProvider
{
	/// <summary>
	/// Gets the tagging mode for a C# union.
	/// </summary>
	/// <param name="unionShape">The union shape.</param>
	/// <returns>The mode, or <see langword="null"/> to use the attribute or serializer default.</returns>
	UnionTagging? GetTagging(IUnionTypeShape unionShape);
}
