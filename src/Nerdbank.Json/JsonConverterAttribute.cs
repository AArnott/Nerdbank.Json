// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Nerdbank.Json;

/// <summary>
/// Applies a custom <see cref="JsonConverter{T}"/> to a type or serialized member.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class JsonConverterAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonConverterAttribute"/> class.
	/// </summary>
	/// <param name="converterType">The converter type to activate.</param>
	public JsonConverterAttribute(Type converterType)
	{
		this.ConverterType = converterType ?? throw new ArgumentNullException(nameof(converterType));
	}

	/// <summary>
	/// Gets the converter type to activate.
	/// </summary>
	public Type ConverterType { get; }
}
