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

	/// <summary>
	/// Repeated references are preserved and reference cycles are allowed.
	/// </summary>
	/// <remarks>
	/// This mode uses the same <c>$id</c>/<c>$ref</c> wire format as <see cref="RejectCycles"/>, but additionally
	/// permits an object to reference itself directly or indirectly. During deserialization, mutable objects,
	/// collections, and dictionaries are registered before their members are populated so that back-references
	/// resolve to the object under construction. Immutable or constructor-bound objects cannot be registered early,
	/// so a back-reference to such an object that is still being constructed fails with a clear error.
	/// </remarks>
	AllowCycles,
}
