// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Json;
using PolyType;
using Xunit;

[GenerateShapeFor<JsonObjectSerializerTests.SomeEnum>]
public partial class JsonObjectSerializerTests
{
	public enum SomeEnum
	{
		Zero,
		One,
		Two,
		Three,
	}

	[Test]
	public void SerializeDeserialize_EnumRoot_WithWitnessType()
	{
		JsonSerializer serializer = new();
		SomeEnum value = SomeEnum.Two;

		string json = serializer.Serialize<SomeEnum, JsonObjectSerializerTests>(value);
		SomeEnum roundTripped = serializer.Deserialize<SomeEnum, JsonObjectSerializerTests>(json);

		Assert.Equal("2", json);
		Assert.Equal(value, roundTripped);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithEnumProperty()
	{
		JsonSerializer serializer = new();
		EnumContainer value = new() { Value = SomeEnum.Three };

		string json = serializer.Serialize(value);

		Assert.Equal("{\"value\":3}", json);
		AssertRoundtrip(json, serializer, value);
	}

	[GenerateShape]
	internal partial class EnumContainer
	{
		public SomeEnum Value { get; set; }
	}
}
