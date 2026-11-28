// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using PolyType.Utilities;

namespace Nerdbank.Json;

/// <summary>
/// A converter cache keyed by type shape provider and type, whose entries are strongly owned by the cache instance.
/// </summary>
/// <remarks>
/// <para>
/// PolyType's <see cref="MultiProviderTypeCache"/> and <see cref="TypeCache"/> store their values in a
/// <see cref="ConditionalWeakTable{TKey, TValue}"/> keyed by the <see cref="ITypeShapeProvider"/> and <see cref="Type"/>.
/// Generated shape providers and runtime types generally live for the life of the process,
/// and the cached converters (and the value builder factory) refer back to the owning <see cref="ConverterCache"/>.
/// The dependent handles therefore keep the table itself reachable, so neither the table nor the serializer
/// configuration that owns it (including any connection-specific converter factories) can ever be collected.
/// </para>
/// <para>
/// This cache avoids that by holding its entries in ordinary dictionaries, so that it and everything it references
/// become collectible as soon as the owning serializer is unreachable.
/// </para>
/// </remarks>
internal sealed class ConverterGenerationCache
{
	private readonly ConcurrentDictionary<ITypeShapeProvider, ConcurrentDictionary<Type, object?>> providerScopes = new(ProviderReferenceComparer.Instance);
	private readonly Func<ConverterGenerationCache, TypeGenerationContext, ITypeShapeFunc> valueBuilderFactory;
	private readonly IDelayedValueFactory? delayedValueFactory;
	private readonly Func<ITypeShapeProvider, ConcurrentDictionary<Type, object?>> createScope = _ => new();

	/// <summary>
	/// Initializes a new instance of the <see cref="ConverterGenerationCache"/> class.
	/// </summary>
	/// <param name="valueBuilderFactory">Creates the function that builds values for a new generation context.</param>
	/// <param name="delayedValueFactory">The factory for delayed values used to handle recursive types.</param>
	internal ConverterGenerationCache(Func<ConverterGenerationCache, TypeGenerationContext, ITypeShapeFunc> valueBuilderFactory, IDelayedValueFactory? delayedValueFactory)
	{
		this.valueBuilderFactory = valueBuilderFactory;
		this.delayedValueFactory = delayedValueFactory;
	}

	/// <summary>
	/// Gets or adds the value for the specified type shape.
	/// </summary>
	/// <param name="typeShape">The type shape.</param>
	/// <returns>The cached or newly generated value.</returns>
	internal object? GetOrAdd(ITypeShape typeShape)
	{
		Requires.NotNull(typeShape);
		if (this.TryGetValue(typeShape, out object? value))
		{
			return value;
		}

		ConcurrentDictionary<Type, object?> scope = this.GetScope(typeShape.Provider);

		// Uses optimistic concurrency when committing values to the cache.
		// If conflicting entries are found in the cache, the value is re-evaluated.
		while (true)
		{
			TypeGenerationContext context = this.CreateGenerationContext(out _);
			value = typeShape.Invoke(context);
			if (TryCommitResults(scope, context))
			{
				return value;
			}

			if (this.TryGetValue(typeShape, out object? existing))
			{
				return existing;
			}
		}
	}

	/// <summary>
	/// Looks up a completed value for the specified type shape.
	/// </summary>
	/// <param name="typeShape">The type shape.</param>
	/// <param name="value">Receives the value, if found.</param>
	/// <returns><see langword="true"/> if a value was found; otherwise <see langword="false"/>.</returns>
	/// <remarks>Contextual shapes are never found in the cache.</remarks>
	internal bool TryGetValue(ITypeShape typeShape, out object? value)
	{
		if (!typeShape.IsContextual
			&& this.providerScopes.TryGetValue(typeShape.Provider, out ConcurrentDictionary<Type, object?>? scope)
			&& scope.TryGetValue(typeShape.Type, out value))
		{
			return true;
		}

		value = null;
		return false;
	}

	/// <summary>
	/// Creates a new generation context whose results are not committed to this cache.
	/// </summary>
	/// <param name="valueBuilder">Receives the value builder used by the context.</param>
	/// <returns>The new generation context.</returns>
	internal TypeGenerationContext CreateGenerationContext(out ITypeShapeFunc valueBuilder)
	{
		ValueBuilderProxy proxy = new();
		TypeGenerationContext context = new()
		{
			ValueBuilder = proxy,
			DelayedValueFactory = this.delayedValueFactory,
		};
		valueBuilder = proxy.Inner = this.valueBuilderFactory(this, context);
		return context;
	}

	private static bool TryCommitResults(ConcurrentDictionary<Type, object?> scope, TypeGenerationContext context)
	{
		IReadOnlyDictionary<Type, object?> results = context;
		lock (scope)
		{
			foreach (KeyValuePair<Type, object?> entry in results)
			{
				if (scope.TryGetValue(entry.Key, out object? conflict) && !ReferenceEquals(entry.Value, conflict))
				{
					return false;
				}
			}

			foreach (KeyValuePair<Type, object?> entry in results)
			{
				scope.TryAdd(entry.Key, entry.Value);
			}

			return true;
		}
	}

	private ConcurrentDictionary<Type, object?> GetScope(ITypeShapeProvider provider) => this.providerScopes.GetOrAdd(provider, this.createScope);

	private sealed class ValueBuilderProxy : ITypeShapeFunc
	{
		/// <summary>Gets or sets the function that builds values.</summary>
		internal ITypeShapeFunc? Inner { get; set; }

		public object? Invoke<T>(ITypeShape<T> typeShape, object? state = null) => this.Inner!.Invoke(typeShape, state);
	}

	private sealed class ProviderReferenceComparer : IEqualityComparer<ITypeShapeProvider>
	{
		/// <summary>Gets the comparer for exact provider identity.</summary>
		internal static readonly ProviderReferenceComparer Instance = new();

		public bool Equals(ITypeShapeProvider? x, ITypeShapeProvider? y) => ReferenceEquals(x, y);

		public int GetHashCode(ITypeShapeProvider obj) => RuntimeHelpers.GetHashCode(obj);
	}
}
