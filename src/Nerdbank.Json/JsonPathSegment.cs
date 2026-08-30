// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal path segment types are intentionally undocumented in this file.
#pragma warning disable SA1602 // Internal enumeration items are intentionally undocumented in this file.

using System.Text;

namespace Nerdbank.Json;

internal enum JsonPathSegmentKind
{
	Member,
	Index,
}

internal readonly struct JsonPathSegment
{
	private JsonPathSegment(JsonPathSegmentKind kind, string? name, byte[]? utf8Name, int index)
	{
		this.Kind = kind;
		this.Name = name;
		this.Utf8Name = utf8Name;
		this.Index = index;
	}

	internal JsonPathSegmentKind Kind { get; }

	internal string? Name { get; }

	internal byte[]? Utf8Name { get; }

	internal int Index { get; }

	internal static JsonPathSegment ForMember(string name) => new(JsonPathSegmentKind.Member, name, Encoding.UTF8.GetBytes(name), 0);

	internal static JsonPathSegment ForIndex(int index) => new(JsonPathSegmentKind.Index, null, null, index);
}
