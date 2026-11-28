// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PolyType.Utilities;

namespace Nerdbank.Json;

/// <summary>
/// A converter generation cache with weak provider keys, type keys, and converter values.
/// </summary>
/// <remarks>
/// <para>
/// Weak keys allow collectible types and providers to unload. Weak values prevent dependent handles
/// for globally rooted keys from retaining converters that refer back to the owning serializer configuration.
/// Provider scopes must not reference the value builder or the owning cache for the same reason.
/// </para>
/// <para>
/// Resolved top-level converters remain strongly owned by <see cref="ConverterCache"/>; their nested converters
/// and any in-progress generation context keep recursive converters alive while they are needed.
/// </para>
/// </remarks>
internal sealed class ConverterGenerationCache
{
	private readonly ConditionalWeakTable<ITypeShapeProvider, ProviderScope> providerScopes = new();
	private readonly Func<ConverterGenerationCache, TypeGenerationContext, ITypeShapeFunc> valueBuilderFactory;
	private readonly IDelayedValueFactory? delayedValueFactory;

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

		ProviderScope scope = this.GetScope(typeShape.Provider);

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
			&& this.providerScopes.TryGetValue(typeShape.Provider, out ProviderScope? scope)
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

	private static bool TryCommitResults(ProviderScope scope, TypeGenerationContext context)
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
				if (scope.Values.TryGetValue(entry.Key, out WeakReference<object>? reference))
				{
					reference.SetTarget(entry.Value!);
				}
				else
				{
					scope.Values.Add(entry.Key, new(entry.Value!));
				}
			}

			return true;
		}
	}

	private ProviderScope GetScope(ITypeShapeProvider provider) => this.providerScopes.GetValue(provider, static _ => new());

	private sealed class ValueBuilderProxy : ITypeShapeFunc
	{
		/// <summary>Gets or sets the function that builds values.</summary>
		internal ITypeShapeFunc? Inner { get; set; }

		public object? Invoke<T>(ITypeShape<T> typeShape, object? state = null) => this.Inner!.Invoke(typeShape, state);
	}

	private sealed class ProviderScope
	{
		/// <summary>Gets the weakly held converters keyed by their runtime types.</summary>
		internal ConditionalWeakTable<Type, WeakReference<object>> Values { get; } = new();

		/// <summary>Looks up a converter that is still reachable.</summary>
		/// <param name="type">The runtime type.</param>
		/// <param name="value">Receives the converter, if found.</param>
		/// <returns>Whether a live converter was found.</returns>
		internal bool TryGetValue(Type type, out object? value)
		{
			if (this.Values.TryGetValue(type, out WeakReference<object>? reference) && reference.TryGetTarget(out value))
			{
				return true;
			}

			value = null;
			return false;
		}
	}
}
