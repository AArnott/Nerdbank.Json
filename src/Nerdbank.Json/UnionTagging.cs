// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Controls how a C# union identifies its case on the wire.
/// </summary>
/// <remarks>
/// Derived type hierarchies and F# unions ignore this mode.
/// </remarks>
public enum UnionTagging
{
	/// <summary>
	/// Every object case is written with <c>$type</c>.
	/// A scalar or collection is written bare only when no other case shares its JSON kind.
	/// </summary>
	Tagged,

	/// <summary>
	/// A case is written bare when its JSON kind is unique in the union, including a single object case.
	/// Any other case is written with <c>$type</c>.
	/// </summary>
	Auto,

	/// <summary>
	/// No case is wrapped in <c>$type</c>, except a case with a runtime alias.
	/// A union whose cases cannot be told apart is rejected when its converter is built.
	/// </summary>
	Untagged,
}
