// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Nerdbank.Json;

/// <summary>
/// An immutable, pre-parsed path that selects a single value within a JSON document for targeted deserialization.
/// </summary>
/// <remarks>
/// <para>
/// A path is a sequence of segments applied to the root JSON value. Each <see cref="Member(string)"/> segment
/// selects an object property (or dictionary entry) by its <em>serialized</em> JSON name, and each
/// <see cref="Index(int)"/> segment selects an array element by position. Because the names are the wire names,
/// no naming policy is applied when a pre-parsed path is used; use the expression-based overloads of
/// <see cref="JsonSerializer"/> when you want CLR member names translated automatically.
/// </para>
/// <para>
/// Instances are immutable and safe to cache and reuse across many deserialization calls, which avoids repeated
/// path analysis on hot code paths.
/// </para>
/// </remarks>
public sealed class JsonPath
{
	private readonly JsonPathSegment[] segments;

	private JsonPath(JsonPathSegment[] segments)
	{
		this.segments = segments;
	}

	/// <summary>
	/// Gets the empty path, which selects the root value itself.
	/// </summary>
	public static JsonPath Root { get; } = new([]);

	/// <summary>Gets the ordered path segments.</summary>
	internal ReadOnlySpan<JsonPathSegment> Segments => this.segments;

	/// <summary>
	/// Returns a path that appends selection of an object property or dictionary entry by its serialized JSON name.
	/// </summary>
	/// <param name="name">The serialized JSON property name.</param>
	/// <returns>A new path.</returns>
	public JsonPath Member(string name)
	{
		Requires.NotNull(name);
		return this.Append(JsonPathSegment.ForMember(name));
	}

	/// <summary>
	/// Returns a path that appends selection of an array element by position.
	/// </summary>
	/// <param name="index">The zero-based element index.</param>
	/// <returns>A new path.</returns>
	public JsonPath Index(int index)
	{
		Requires.Range(index >= 0, nameof(index));
		return this.Append(JsonPathSegment.ForIndex(index));
	}

	/// <inheritdoc/>
	public override string ToString()
	{
		StringBuilder builder = new("$");
		foreach (JsonPathSegment segment in this.segments)
		{
			builder.Append(segment.Kind == JsonPathSegmentKind.Index ? $"[{segment.Index}]" : $".{segment.Name}");
		}

		return builder.ToString();
	}

	private JsonPath Append(JsonPathSegment segment)
	{
		JsonPathSegment[] combined = new JsonPathSegment[this.segments.Length + 1];
		Array.Copy(this.segments, combined, this.segments.Length);
		combined[this.segments.Length] = segment;
		return new JsonPath(combined);
	}
}
