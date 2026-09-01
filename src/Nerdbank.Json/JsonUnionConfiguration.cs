// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// An immutable, serializer-scoped set of runtime overrides for polymorphic union (sub-type) serialization.
/// </summary>
/// <remarks>
/// <para>
/// By default, unions are described entirely by shape-generated metadata and use the two-element
/// <c>[discriminator, payload]</c> representation. This configuration lets a caller add a union for a base type,
/// replace or extend the generated cases, or disable union handling, using explicitly supplied
/// <see cref="ITypeShape{T}"/> instances so the feature remains NativeAOT-safe.
/// </para>
/// <para>
/// Instances are immutable; every method returns a new configuration. Reusing the same instance across serializers
/// preserves converter-cache identity, while assigning a different instance rebuilds the cache.
/// </para>
/// </remarks>
public sealed class JsonUnionConfiguration
{
	/// <summary>A sentinel value stored to indicate that union handling is disabled for a type.</summary>
	internal static readonly object DisabledSentinel = new();

	private readonly Dictionary<Type, object> entries;

	private JsonUnionConfiguration(Dictionary<Type, object> entries)
	{
		this.entries = entries;
	}

	/// <summary>
	/// Gets the empty configuration that applies no runtime overrides.
	/// </summary>
	public static JsonUnionConfiguration Default { get; } = new(new Dictionary<Type, object>());

	/// <summary>
	/// Returns a configuration that applies the specified union definition to <typeparamref name="TBase"/>.
	/// </summary>
	/// <typeparam name="TBase">The base type whose union handling is overridden.</typeparam>
	/// <param name="union">The union definition to apply.</param>
	/// <returns>A new configuration.</returns>
	public JsonUnionConfiguration WithUnion<TBase>(JsonUnion<TBase> union)
	{
		Requires.NotNull(union);
		return new(this.Clone(typeof(TBase), union));
	}

	/// <summary>
	/// Returns a configuration that disables union handling for <typeparamref name="TBase"/>, serializing values as
	/// the plain base type without a discriminator envelope.
	/// </summary>
	/// <typeparam name="TBase">The base type whose union handling is disabled.</typeparam>
	/// <returns>A new configuration.</returns>
	public JsonUnionConfiguration WithoutUnion<TBase>() => new(this.Clone(typeof(TBase), DisabledSentinel));

	/// <summary>Attempts to get the configured entry for a base type.</summary>
	/// <param name="baseType">The base type to look up.</param>
	/// <param name="entry">Receives the entry: a <see cref="JsonUnion{TBase}"/> or <see cref="DisabledSentinel"/>.</param>
	/// <returns><see langword="true"/> if an entry exists; otherwise <see langword="false"/>.</returns>
	internal bool TryGetEntry(Type baseType, out object? entry) => this.entries.TryGetValue(baseType, out entry);

	private Dictionary<Type, object> Clone(Type key, object value)
	{
		Dictionary<Type, object> copy = new(this.entries) { [key] = value };
		return copy;
	}
}
