// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Enumerates the possible formats for serializing multi-dimensional arrays.
/// </summary>
public enum MultiDimensionalArrayFormat
{
	/// <summary>
	/// A format that nests arrays for each dimension.
	/// </summary>
	Nested,

	/// <summary>
	/// A format that writes dimension lengths and flattened elements as two arrays:
	/// <c>[[length0, length1, ...], [element0, element1, ...]]</c>.
	/// </summary>
	Flat,
}
