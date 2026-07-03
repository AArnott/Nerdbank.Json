// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[GenerateShapeFor<int?>]
public partial class JsonObjectSerializerTests
{
	[Test]
	public void SerializeDeserialize_NullableStruct_Null_WithWitnessType()
	{
		int? value = null;

		string json = this.Serializer.Serialize<int?, JsonObjectSerializerTests>(value);
		int? roundTripped = this.Serializer.Deserialize<int?, JsonObjectSerializerTests>(json);

		Assert.Equal("null", json);
		Assert.Equal(value, roundTripped);
	}

	[Test]
	public void SerializeDeserialize_NullableStruct_Value_WithWitnessType()
	{
		int? value = 42;

		string json = this.Serializer.Serialize<int?, JsonObjectSerializerTests>(value);
		int? roundTripped = this.Serializer.Deserialize<int?, JsonObjectSerializerTests>(json);

		Assert.Equal("42", json);
		Assert.Equal(value, roundTripped);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithNullableStructProperty()
	{
		OptionalContainer value = new() { Count = 7 };

		this.AssertRoundtrip(value, """{"count":7}""");
	}

	[GenerateShape]
	internal partial class OptionalContainer
	{
		public int? Count { get; set; }
	}
}
