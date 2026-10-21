// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal helper members are intentionally undocumented in this file.

using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static Nerdbank.Json.ConverterTypeCollection;

namespace Nerdbank.Json;

/// <summary>
/// An immutable collection of <see cref="JsonConverter{T}"/> types.
/// </summary>
[CollectionBuilder(typeof(ConverterTypeCollection), nameof(Create))]
public class ConverterTypeCollection : IReadOnlyCollection<ConverterType>
{
	private readonly Dictionary<Type, ConverterType> map;
	private readonly ImmutableArray<ConverterType> values;

	private ConverterTypeCollection(Dictionary<Type, ConverterType> map)
	{
		this.map = map;
		this.values = [.. map.Values];
	}

	/// <inheritdoc/>
	public int Count => this.map.Count;

	/// <summary>
	/// Creates an immutable snapshot of converter types.
	/// </summary>
	/// <param name="converterTypes">The converter types to include.</param>
	/// <returns>The initialized collection.</returns>
	public static ConverterTypeCollection Create(ReadOnlySpan<ConverterType> converterTypes)
	{
		Dictionary<Type, ConverterType> map = [];
		foreach (Type converterType in converterTypes)
		{
			if (converterType is null)
			{
				throw new ArgumentException("Null elements are not allowed.", nameof(converterTypes));
			}

			if (!converterType.IsClass || converterType.IsAbstract)
			{
				throw new ArgumentException("All types must be concrete classes.", nameof(converterTypes));
			}

			Type dataType = GetDataType(converterType);
			map[dataType] = converterType;
		}

		return new(map);
	}

	/// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
	public ImmutableArray<ConverterType>.Enumerator GetEnumerator() => this.values.GetEnumerator();

	/// <inheritdoc/>
	IEnumerator<ConverterType> IEnumerable<ConverterType>.GetEnumerator() => ((IEnumerable<ConverterType>)this.values).GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ConverterType>)this).GetEnumerator();

	internal bool TryGetConverterType(Type dataType, [NotNullWhen(true), DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] out Type? converterType)
	{
		if (this.map.TryGetValue(dataType, out ConverterType result))
		{
			converterType = result;
			return true;
		}

		converterType = null;
		return false;
	}

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

	/// <summary>A converter type whose public constructors must be preserved when trimming.</summary>
	public readonly struct ConverterType
	{
		/// <summary>Initializes a new instance of the <see cref="ConverterType"/> struct.</summary>
		/// <param name="type">The converter type.</param>
		public ConverterType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
		{
			this.Type = type;
		}

		/// <summary>Gets the converter type.</summary>
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
		public Type Type { get; }

		/// <summary>Wraps a converter type.</summary>
		/// <param name="type">The converter type.</param>
		public static implicit operator ConverterType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type) => new(type);

		/// <summary>Unwraps a converter type.</summary>
		/// <param name="type">The converter type wrapper.</param>
		[return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
		public static implicit operator Type(ConverterType type) => type.Type;
	}
}
