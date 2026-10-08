// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Identifies a supported JSON Schema dialect.
/// </summary>
public enum JsonSchemaDialect
{
	/// <summary>
	/// JSON Schema Draft 4.
	/// </summary>
	Draft4 = 4,

	/// <summary>
	/// JSON Schema Draft 2020-12.
	/// </summary>
	Draft2020_12 = 9,
}
