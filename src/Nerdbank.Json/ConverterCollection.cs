// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal helper members are intentionally undocumented in this file.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Json;

/// <summary>
/// An immutable collection of concrete JSON converters.
/// </summary>
public class ConverterCollection : IReadOnlyCollection<JsonConverter>
{
	private readonly Dictionary<Type, JsonConverter> map;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConverterCollection"/> class.
	/// </summary>
	/// <remarks>The created collection is empty.</remarks>
	public ConverterCollection()
		: this([])
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConverterCollection"/> class from a sequence of converters.
	/// </summary>
	/// <param name="converters">The converters to include.</param>
	public ConverterCollection(IEnumerable<JsonConverter> converters)
	{
		Requires.NotNull(converters);

		this.map = [];
		foreach (JsonConverter converter in converters)
		{
			if (converter is null)
			{
				throw new ArgumentException("Null elements are not allowed.", nameof(converters));
			}

			this.map.Add(converter.DataType, converter);
		}
	}

	/// <inheritdoc/>
	public int Count => this.map.Count;

	/// <inheritdoc/>
	public IEnumerator<JsonConverter> GetEnumerator() => this.map.Values.GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

	internal bool TryGetConverter(Type dataType, [NotNullWhen(true)] out JsonConverter? converter)
		=> this.map.TryGetValue(dataType, out converter);
}
