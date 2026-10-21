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
	private JsonPathSegment(JsonPathSegmentKind kind, string? name, byte[]? utf8Name, int index, IPropertyShape? propertyShape = null, object? key = null)
	{
		this.Kind = kind;
		this.Name = name;
		this.Utf8Name = utf8Name;
		this.Index = index;
		this.PropertyShape = propertyShape;
		this.Key = key;
	}

	internal JsonPathSegmentKind Kind { get; }

	internal string? Name { get; }

	internal byte[]? Utf8Name { get; }

	internal int Index { get; }

	/// <summary>Gets the logical property for expression-based navigation.</summary>
	internal IPropertyShape? PropertyShape { get; }

	/// <summary>Gets the untransformed dictionary key for expression-based navigation.</summary>
	internal object? Key { get; }

	internal static JsonPathSegment ForMember(string name, IPropertyShape? propertyShape = null, object? key = null) => new(JsonPathSegmentKind.Member, name, Encoding.UTF8.GetBytes(name), 0, propertyShape, key);

	internal static JsonPathSegment ForIndex(int index) => new(JsonPathSegmentKind.Index, null, null, index);
}
