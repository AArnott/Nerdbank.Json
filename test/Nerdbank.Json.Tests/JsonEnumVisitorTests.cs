// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[GenerateShapeFor<JsonObjectSerializerTests.SomeEnum>]
[GenerateShapeFor<JsonObjectSerializerTests.FlagEnum>]
public partial class JsonObjectSerializerTests
{
	public enum SomeEnum
	{
		Zero,
		One,
		Two,
		Three,
	}

	[Flags]
	public enum FlagEnum
	{
		None = 0,
		Read = 1,
		Write = 2,
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

		this.AssertRoundtrip(value, """{"value":3}""");
	}

	[Test]
	public void SerializeDeserialize_EnumRoot_ByName()
	{
		JsonSerializer serializer = new() { SerializeEnumValuesByName = true, PropertyNamingPolicy = null };
		SomeEnum value = SomeEnum.Two;

		string json = serializer.Serialize<SomeEnum, JsonObjectSerializerTests>(value);
		SomeEnum roundTripped = serializer.Deserialize<SomeEnum, JsonObjectSerializerTests>(json);

		Assert.Equal("\"Two\"", json);
		Assert.Equal(value, roundTripped);
	}

	[Test]
	public void SerializeDeserialize_EnumRoot_ByName_CamelCase()
	{
		JsonSerializer serializer = new() { SerializeEnumValuesByName = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
		SomeEnum value = SomeEnum.Two;

		string json = serializer.Serialize<SomeEnum, JsonObjectSerializerTests>(value);
		SomeEnum roundTripped = serializer.Deserialize<SomeEnum, JsonObjectSerializerTests>(json);

		Assert.Equal("\"two\"", json);
		Assert.Equal(value, roundTripped);
	}

	[Test]
	public void Deserialize_EnumRoot_ByName_IsCaseInsensitive()
	{
		JsonSerializer serializer = new() { SerializeEnumValuesByName = true };

		SomeEnum value = serializer.Deserialize<SomeEnum, JsonObjectSerializerTests>("\"tWo\"");

		Assert.Equal(SomeEnum.Two, value);
	}

	[Test]
	public void Serialize_EnumRoot_ByName_FallsBackToNumberWhenUnnamed()
	{
		this.Serializer = new() { SerializeEnumValuesByName = true };
		FlagEnum value = FlagEnum.Read | FlagEnum.Write;

		string json = this.Serializer.Serialize<FlagEnum, JsonObjectSerializerTests>(value);

		Assert.Equal("3", json);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithEnumProperty_ByName()
	{
		this.Serializer = new() { SerializeEnumValuesByName = true };
		EnumContainer value = new() { Value = SomeEnum.Three };

		this.AssertRoundtrip(value, """{"value":"three"}""");
	}

	[GenerateShape]
	internal partial class EnumContainer
	{
		public SomeEnum Value { get; set; }
	}
}
