// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Json;
using PolyType;
using Xunit;

public partial class JsonObjectSerializerTests
{
	[Fact]
	public void SerializeDeserialize_RecordWithParameterizedConstructor()
	{
		JsonSerializer serializer = new();
		ParameterizedRecord value = new("Ada", 37);

		string json = serializer.Serialize(value);
		ParameterizedRecord roundTripped = serializer.Deserialize<ParameterizedRecord>(json);

		Assert.Equal("{\"name\":\"Ada\",\"age\":37}", json);
		Assert.Equal(value, roundTripped);
	}

	[GenerateShape]
	internal partial record ParameterizedRecord(string Name, int Age);
}
