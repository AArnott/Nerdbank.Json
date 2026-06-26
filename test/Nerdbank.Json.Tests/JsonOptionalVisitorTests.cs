// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Json;
using PolyType;
using Xunit;

[GenerateShapeFor<int?>]
public partial class JsonObjectSerializerTests
{
	[Test]
	public void SerializeDeserialize_NullableStruct_Null_WithWitnessType()
	{
		JsonSerializer serializer = new();
		int? value = null;

		string json = serializer.Serialize<int?, JsonObjectSerializerTests>(value);
		int? roundTripped = serializer.Deserialize<int?, JsonObjectSerializerTests>(json);

		Assert.Equal("null", json);
		Assert.Equal(value, roundTripped);
	}

	[Test]
	public void SerializeDeserialize_NullableStruct_Value_WithWitnessType()
	{
		JsonSerializer serializer = new();
		int? value = 42;

		string json = serializer.Serialize<int?, JsonObjectSerializerTests>(value);
		int? roundTripped = serializer.Deserialize<int?, JsonObjectSerializerTests>(json);

		Assert.Equal("42", json);
		Assert.Equal(value, roundTripped);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithNullableStructProperty()
	{
		JsonSerializer serializer = new();
		OptionalContainer value = new() { Count = 7 };

		string json = serializer.Serialize(value);

		Assert.Equal("{\"count\":7}", json);
		AssertRoundtrip(json, serializer, value);
	}

	[GenerateShape]
	internal partial class OptionalContainer
	{
		public int? Count { get; set; }
	}
}
