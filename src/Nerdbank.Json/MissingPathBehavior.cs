// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Controls what happens when a targeted deserialization path selects a value that is not present in the document.
/// </summary>
public enum MissingPathBehavior
{
	/// <summary>
	/// A <see cref="JsonPathNotFoundException"/> is thrown when the path cannot be resolved.
	/// </summary>
	Throw,

	/// <summary>
	/// The default value of the target type is returned when the path cannot be resolved.
	/// </summary>
	ReturnDefault,
}
