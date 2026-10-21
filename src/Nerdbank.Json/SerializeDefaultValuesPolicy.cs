// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Specifies the policy for serializing default values.
/// </summary>
/// <remarks>
/// A property's default is its matching constructor parameter's declared default, when available,
/// otherwise its <see cref="System.ComponentModel.DefaultValueAttribute"/> value, otherwise the CLR default.
/// Property initializers alone do not declare a serialization default.
/// </remarks>
[Flags]
public enum SerializeDefaultValuesPolicy
{
	/// <summary>
	/// Do not serialize any default values.
	/// </summary>
	Never = 0x0,

	/// <summary>
	/// Serialize default values when they are required by the contract.
	/// </summary>
	/// <remarks>
	/// Requiredness follows the type shape's constructor parameter metadata, including property
	/// initializers and explicit <see cref="PropertyShapeAttribute"/> overrides.
	/// </remarks>
	Required = 0x1,

	/// <summary>
	/// Serialize default values for value types.
	/// </summary>
	ValueTypes = 0x2,

	/// <summary>
	/// Serialize default values for reference types.
	/// </summary>
	ReferenceTypes = 0x4,

	/// <summary>
	/// Serialize all properties, regardless of their values.
	/// </summary>
	Always = Required | ValueTypes | ReferenceTypes,
}
