// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Selects the tagging mode for one C# union.
/// </summary>
/// <remarks>
/// This overrides <see cref="JsonSerializer.DefaultUnionTagging"/> and is overridden by
/// <see cref="JsonSerializer.UnionTaggingProvider"/>.
/// Derived type hierarchies and F# unions ignore it.
/// </remarks>
/// <param name="mode">The tagging mode for the decorated union.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class UnionTaggingAttribute(UnionTagging mode) : Attribute
{
	/// <summary>
	/// Gets the tagging mode for the decorated union.
	/// </summary>
	public UnionTagging Mode { get; } = mode;
}
