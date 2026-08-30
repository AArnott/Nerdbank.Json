// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name (this file groups the navigation surface).

namespace Nerdbank.Json;

/// <summary>
/// Identifies one step of a targeted-deserialization path: an object member (or dictionary entry) by serialized name,
/// or an array element by index.
/// </summary>
/// <remarks>
/// This is passed to <see cref="JsonConverter.TryNavigate(ref JsonReader, in JsonNavigationSegment, JsonNavigationOptions)"/>
/// so a converter can translate a logical segment into a navigation over its own JSON representation.
/// </remarks>
public readonly struct JsonNavigationSegment
{
	private readonly byte[]? utf8Name;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonNavigationSegment"/> struct from a parsed path segment.
	/// </summary>
	/// <param name="segment">The parsed path segment.</param>
	internal JsonNavigationSegment(in JsonPathSegment segment)
	{
		this.IsIndex = segment.Kind == JsonPathSegmentKind.Index;
		this.Index = segment.Index;
		this.Name = segment.Name;
		this.utf8Name = segment.Utf8Name;
	}

	/// <summary>
	/// Gets a value indicating whether this segment selects an array element by <see cref="Index"/>.
	/// </summary>
	public bool IsIndex { get; }

	/// <summary>
	/// Gets the array element index when <see cref="IsIndex"/> is <see langword="true"/>.
	/// </summary>
	public int Index { get; }

	/// <summary>
	/// Gets the serialized member name when <see cref="IsIndex"/> is <see langword="false"/>.
	/// </summary>
	public string? Name { get; }

	/// <summary>
	/// Gets the precomputed UTF-8 encoding of <see cref="Name"/> used by the raw-token fast path.
	/// </summary>
	internal ReadOnlySpan<byte> Utf8Name => this.utf8Name;
}

/// <summary>
/// Options that flow through targeted-deserialization navigation.
/// </summary>
public readonly struct JsonNavigationOptions
{
	/// <summary>
	/// Initializes a new instance of the <see cref="JsonNavigationOptions"/> struct.
	/// </summary>
	/// <param name="nameComparer">The comparer used to match member names.</param>
	/// <param name="ignoreCase">Whether member names are matched case-insensitively.</param>
	internal JsonNavigationOptions(StringComparer nameComparer, bool ignoreCase)
	{
		this.NameComparer = nameComparer;
		this.IgnoreCase = ignoreCase;
	}

	/// <summary>
	/// Gets the comparer used to match member names.
	/// </summary>
	public StringComparer NameComparer { get; }

	/// <summary>
	/// Gets a value indicating whether member names are matched case-insensitively.
	/// </summary>
	public bool IgnoreCase { get; }
}
