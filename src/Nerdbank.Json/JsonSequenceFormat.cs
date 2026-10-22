// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>Specifies the framing used for a sequence of JSON values.</summary>
public enum JsonSequenceFormat
{
	/// <summary>Each value occupies a separate line (newline-delimited JSON).</summary>
	NDJson,

	/// <summary>The sequence is represented by a single top-level JSON array.</summary>
	JsonArray,
}
