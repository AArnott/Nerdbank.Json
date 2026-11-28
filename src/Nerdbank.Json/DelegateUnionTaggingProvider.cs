// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Selects C# union tagging modes using a delegate keyed by the union type.
/// </summary>
/// <param name="resolver">The thread-safe resolver, whose results must remain consistent while the provider is in use.</param>
public sealed class DelegateUnionTaggingProvider(UnionTaggingResolver resolver) : IUnionTaggingProvider
{
	private readonly UnionTaggingResolver resolver = Requires.NotNull(resolver);

	/// <inheritdoc/>
	public UnionTagging? GetTagging(IUnionTypeShape unionShape)
	{
		Requires.NotNull(unionShape);
		return this.resolver(unionShape.Type);
	}
}
