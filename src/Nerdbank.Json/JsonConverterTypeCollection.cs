// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal helper members are intentionally undocumented in this file.

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Json;

/// <summary>
/// An immutable collection of <see cref="JsonConverter{T}"/> types.
/// </summary>
public class JsonConverterTypeCollection : IReadOnlyCollection<Type>
{
	private readonly Dictionary<Type, Type> map;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonConverterTypeCollection"/> class.
	/// </summary>
	/// <remarks>The created collection is empty.</remarks>
	public JsonConverterTypeCollection()
		: this([])
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonConverterTypeCollection"/> class from a sequence of converter types.
	/// </summary>
	/// <param name="converterTypes">The converter types to include.</param>
	public JsonConverterTypeCollection(IEnumerable<Type> converterTypes)
	{
		Requires.NotNull(converterTypes);

		this.map = [];
		foreach (Type converterType in converterTypes)
		{
			if (converterType is null)
			{
				throw new ArgumentException("Null elements are not allowed.", nameof(converterTypes));
			}

			Type dataType = GetDataType(converterType);
			this.map[dataType] = converterType;
		}
	}

	/// <inheritdoc/>
	public int Count => this.map.Count;

	/// <inheritdoc/>
	public IEnumerator<Type> GetEnumerator() => this.map.Values.GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

	internal bool TryGetConverterType(Type dataType, [NotNullWhen(true)] out Type? converterType)
		=> this.map.TryGetValue(dataType, out converterType);

	private static Type GetDataType(Type converterType)
	{
		Type? baseType = converterType;
		while (baseType is not null)
		{
			if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(JsonConverter<>))
			{
				Type dataType = baseType.GetGenericArguments()[0];
				return dataType.ContainsGenericParameters && dataType.IsGenericType ? dataType.GetGenericTypeDefinition() : dataType;
			}

			baseType = baseType.BaseType;
		}

		throw new ArgumentException($"Type must derive from {typeof(JsonConverter<>).Name}.", nameof(converterType));
	}
}
