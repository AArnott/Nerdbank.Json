// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Specifies how JSON comments are handled during deserialization.
/// </summary>
public enum JsonCommentHandling
{
	/// <summary>
	/// Comments are not allowed.
	/// </summary>
	Disallow,

	/// <summary>
	/// Comments are skipped anywhere insignificant whitespace is permitted.
	/// </summary>
	Skip,
}
