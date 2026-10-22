// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal helper members are intentionally undocumented in this file.

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Nerdbank.Json;

/// <summary>
/// An immutable collection of concrete JSON converters.
/// </summary>
[CollectionBuilder(typeof(ConverterCollection), nameof(Create))]
public class ConverterCollection : IReadOnlyCollection<JsonConverter>
{
	private readonly Dictionary<Type, JsonConverter> map;
	private readonly ImmutableArray<JsonConverter> values;

	private ConverterCollection(Dictionary<Type, JsonConverter> map)
	{
		this.map = map;
		this.values = [.. map.Values];
	}

	/// <inheritdoc/>
	public int Count => this.map.Count;

	/// <summary>
	/// Creates an immutable snapshot of a collection of converters.
	/// </summary>
	/// <param name="converters">The converters to include.</param>
	/// <returns>The initialized collection.</returns>
	public static ConverterCollection Create(ReadOnlySpan<JsonConverter> converters)
	{
		Dictionary<Type, JsonConverter> map = [];
		foreach (JsonConverter converter in converters)
		{
			if (converter is null)
			{
				throw new ArgumentException("Null elements are not allowed.", nameof(converters));
			}

			map.Add(converter.DataType, converter);
		}

		return new(map);
	}

	/// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
	public ImmutableArray<JsonConverter>.Enumerator GetEnumerator() => this.values.GetEnumerator();

	/// <inheritdoc/>
	IEnumerator<JsonConverter> IEnumerable<JsonConverter>.GetEnumerator() => ((IEnumerable<JsonConverter>)this.values).GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<JsonConverter>)this).GetEnumerator();

	internal bool TryGetConverter(Type dataType, [NotNullWhen(true)] out JsonConverter? converter)
		=> this.map.TryGetValue(dataType, out converter);
}
