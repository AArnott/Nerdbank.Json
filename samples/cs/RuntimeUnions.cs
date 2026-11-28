// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable DuckTyping // Demonstrates the experimental union strategy.
#pragma warning disable CS1591, SA1600, SA1601 // Documentation samples do not require API documentation.
#pragma warning disable SA1402, SA1649 // Documentation samples share a file.
#pragma warning disable SA1512, SA1513, SA1515 // Documentation region markers.

using Nerdbank.Json;
using PolyType;
using PolyType.Abstractions;

[GenerateShape]
public partial record Drawing;

[GenerateShape]
public partial record Circle(double Radius) : Drawing;

[GenerateShape]
public partial record Square(double Side) : Drawing;

internal static class RuntimeUnionSamples
{
	internal static string TaggedJson()
	{
		// <RuntimeUnions>
		// Configure a union at runtime by supplying explicit, source-generated case shapes.
		JsonSerializer serializer = new()
		{
			DerivedTypeUnions = [new DerivedShapeMapping<Drawing> { { "circle", ShapeOf<Circle>() }, { "square", ShapeOf<Square>() } }],
		};

		Drawing value = new Circle(2.5);

		// Serializes using the default discriminator object: {"$type":"circle","radius":2.5}.
		string json = serializer.Serialize<Drawing>(value);
		Drawing roundTripped = serializer.Deserialize<Drawing>(json)!;

		// </RuntimeUnions>
		return json;
	}

	internal static string DuckJson()
	{
		// <DuckTypingUnions>
		// Duck typing omits $type and selects a case from required property names.
		// Drawing has none, so it is used only when neither Circle nor Square matches.
		JsonSerializer duckSerializer = new()
		{
			DerivedTypeUnions = [new DerivedTypeDuckTyping(ShapeOf<Drawing>(), ShapeOf<Circle>(), ShapeOf<Square>())],
		};

		// Serializes as the bare object {"radius":2.5}.
		string duckJson = duckSerializer.Serialize<Drawing>(new Circle(2.5));
		Drawing duckResult = duckSerializer.Deserialize<Drawing>(duckJson)!;

		// </DuckTypingUnions>
		return duckJson;
	}

	private static ITypeShape<T> ShapeOf<T>()
#if NET
		where T : IShapeable<T>
#endif
	{
#if NET
		return T.GetTypeShape();
#else
		return TypeShapeResolver.ResolveDynamicOrThrow<T>();
#endif
	}
}
