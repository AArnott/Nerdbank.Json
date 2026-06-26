// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Controls whether reference equality is preserved in serialized object graphs.
/// </summary>
public enum ReferencePreservationMode
{
	/// <summary>
	/// References are not preserved.
	/// </summary>
	Off,

	/// <summary>
	/// Repeated references are preserved and reference cycles are rejected.
	/// </summary>
	RejectCycles,
}
