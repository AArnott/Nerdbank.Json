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
	internal JsonCSharpUnionFormat(IReadOnlyList<JsonCSharpUnionCaseRepresentation> representations, bool useDiscriminatorObjects)
	{
		bool hasArray = false;
		bool hasObject = false;
		HashSet<JsonCSharpUnionCaseRepresentation> nativeRepresentations = [];
		foreach (JsonCSharpUnionCaseRepresentation representation in representations)
		{
			if (representation == JsonCSharpUnionCaseRepresentation.Tagged)
			{
				continue;
			}

			if (!nativeRepresentations.Add(representation))
			{
				throw new NotSupportedException($"C# union cases with the '{representation}' JSON representation require explicit case identifiers.");
			}

			hasArray |= representation == JsonCSharpUnionCaseRepresentation.Array;
			hasObject |= representation == JsonCSharpUnionCaseRepresentation.Object;
		}

		if (hasArray && hasObject)
		{
			throw new NotSupportedException("A C# union cannot contain both untagged array and object collection cases. Assign an explicit case identifier to at least one case.");
		}

		this.UseObjectEnvelope = hasArray || (!hasObject && useDiscriminatorObjects);
	}

	internal bool UseObjectEnvelope { get; }
}

internal static class JsonCSharpUnionRules
{
	internal static JsonCSharpUnionCaseRepresentation GetRepresentation(ITypeShape shape, bool hasExplicitIdentifier, ConverterCache owner)
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

		return shape.Kind switch
		{
			TypeShapeKind.Enumerable => JsonCSharpUnionCaseRepresentation.Array,
			TypeShapeKind.Dictionary => JsonCSharpUnionCaseRepresentation.Object,
			_ => JsonCSharpUnionCaseRepresentation.Tagged,
		};
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
#if NET8_0_OR_GREATER
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
#if NET8_0_OR_GREATER
			|| type == typeof(DateOnly)
			|| type == typeof(TimeOnly)
			|| type == typeof(System.Text.Rune)
#endif
			;
}
