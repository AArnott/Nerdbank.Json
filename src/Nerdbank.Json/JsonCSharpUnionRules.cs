// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1204 // Keep helper methods grouped by their dependent state.
#pragma warning disable SA1402 // This file contains closely related policy helpers.
#pragma warning disable SA1600 // Internal policy helpers are intentionally undocumented.
#pragma warning disable SA1602 // Internal enum members are intentionally undocumented.
#pragma warning disable SA1649 // The file is named after the policy.

namespace Nerdbank.Json;

internal enum JsonCSharpUnionCaseRepresentation
{
	Tagged,
	Boolean,
	Number,
	String,
	Array,
	Object,
}

internal sealed class JsonCSharpUnionFormat
{
	private readonly bool tagObjects;
	private readonly HashSet<JsonCSharpUnionCaseRepresentation>? collided;

	internal JsonCSharpUnionFormat(IReadOnlyList<JsonCSharpUnionCaseRepresentation> representations, UnionTagging mode)
	{
		bool hasTagged = false;
		Dictionary<JsonCSharpUnionCaseRepresentation, int> counts = [];
		foreach (JsonCSharpUnionCaseRepresentation representation in representations)
		{
			if (representation == JsonCSharpUnionCaseRepresentation.Tagged)
			{
				hasTagged = true;
				continue;
			}

			counts.TryGetValue(representation, out int count);
			counts[representation] = count + 1;
		}

		foreach (KeyValuePair<JsonCSharpUnionCaseRepresentation, int> pair in counts)
		{
			if (pair.Value <= 1)
			{
				continue;
			}

			if (mode == UnionTagging.Untagged)
			{
				throw new NotSupportedException($"C# union cases with the '{pair.Key}' JSON representation require explicit case identifiers.");
			}

			(this.collided ??= []).Add(pair.Key);
		}

		bool objectCollidesWithEnvelope = counts.ContainsKey(JsonCSharpUnionCaseRepresentation.Object) && (hasTagged || this.collided is not null);
		if (objectCollidesWithEnvelope)
		{
			if (mode == UnionTagging.Untagged)
			{
				throw new NotSupportedException("C# union object cases cannot be distinguished from a $type wrapper without explicit case identifiers.");
			}

			this.tagObjects = true;
		}
	}

	internal bool IsTagged(JsonCSharpUnionCaseRepresentation representation)
		=> representation == JsonCSharpUnionCaseRepresentation.Tagged
			|| (this.tagObjects && representation == JsonCSharpUnionCaseRepresentation.Object)
			|| (this.collided?.Contains(representation) ?? false);
}

internal static class JsonCSharpUnionRules
{
	internal static UnionTagging ResolveMode(IUnionTypeShape unionShape, ConverterCache owner)
	{
		if (owner.UnionTaggingProvider?.GetTagging(unionShape) is UnionTagging provided)
		{
			return provided;
		}

		if (unionShape.AttributeProvider is not null)
		{
			foreach (UnionTaggingAttribute attribute in unionShape.AttributeProvider.GetCustomAttributes<UnionTaggingAttribute>(inherit: false))
			{
				return attribute.Mode;
			}
		}

		return owner.DefaultUnionTagging;
	}

	internal static JsonCSharpUnionCaseRepresentation GetRepresentation(ITypeShape shape, bool hasExplicitIdentifier, ConverterCache owner, UnionTagging mode)
	{
		if (hasExplicitIdentifier || owner.ShouldPreserveReferences(shape.Type) || (owner.TryGetCustomConverter(shape, out JsonConverter? customConverter) && customConverter is not null))
		{
			return JsonCSharpUnionCaseRepresentation.Tagged;
		}

		Type type = shape.Type;
		if (type == typeof(bool))
		{
			return JsonCSharpUnionCaseRepresentation.Boolean;
		}

		if (IsNumber(type))
		{
			return JsonCSharpUnionCaseRepresentation.Number;
		}

		if (IsString(type))
		{
			return JsonCSharpUnionCaseRepresentation.String;
		}

		if (type == typeof(System.Drawing.Point))
		{
			return JsonCSharpUnionCaseRepresentation.Array;
		}

		if (shape.Kind is TypeShapeKind.Enumerable)
		{
			return JsonCSharpUnionCaseRepresentation.Array;
		}

		if (shape.Kind is TypeShapeKind.Object or TypeShapeKind.Dictionary)
		{
			return mode == UnionTagging.Tagged
				? JsonCSharpUnionCaseRepresentation.Tagged
				: JsonCSharpUnionCaseRepresentation.Object;
		}

		return JsonCSharpUnionCaseRepresentation.Tagged;
	}

	private static bool IsNumber(Type type)
		=> type == typeof(byte)
			|| type == typeof(sbyte)
			|| type == typeof(short)
			|| type == typeof(ushort)
			|| type == typeof(int)
			|| type == typeof(uint)
			|| type == typeof(long)
			|| type == typeof(ulong)
			|| type == typeof(float)
			|| type == typeof(double)
			|| type == typeof(decimal)
			|| type == typeof(System.Numerics.BigInteger)
			|| type == typeof(System.Drawing.Color)
#if NET
			|| type == typeof(Half)
			|| type == typeof(Int128)
			|| type == typeof(UInt128)
#endif
			;

	private static bool IsString(Type type)
		=> type == typeof(string)
			|| type == typeof(char)
			|| type == typeof(DateTime)
			|| type == typeof(DateTimeOffset)
			|| type == typeof(TimeSpan)
			|| type == typeof(Guid)
			|| type == typeof(Version)
			|| type == typeof(Uri)
			|| type == typeof(System.Globalization.CultureInfo)
			|| type == typeof(System.Text.Encoding)
			|| type == typeof(byte[])
			|| type == typeof(Memory<byte>)
			|| type == typeof(ReadOnlyMemory<byte>)
#if NET
			|| type == typeof(DateOnly)
			|| type == typeof(TimeOnly)
			|| type == typeof(System.Text.Rune)
#endif
			;
}
