// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Specifies the policy for serializing default values.
/// </summary>
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
	/// Properties are considered required when they have the <c>required</c> modifier on them
	/// or they correspond to required constructor parameters.
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
