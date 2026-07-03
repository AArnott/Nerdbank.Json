// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Applies a custom <see cref="JsonConverter{T}"/> to a type or serialized member.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="JsonConverterAttribute"/> class.
/// </remarks>
/// <param name="converterType">The converter type to activate.</param>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class JsonConverterAttribute(Type converterType) : Attribute
{
	/// <summary>
	/// Gets the converter type to activate.
	/// </summary>
	public Type ConverterType { get; } = converterType ?? throw new ArgumentNullException(nameof(converterType));
}
