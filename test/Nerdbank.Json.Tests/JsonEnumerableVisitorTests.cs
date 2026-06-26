// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Nerdbank.Json;
using PolyType;
using Xunit;

[GenerateShapeFor<List<JsonObjectSerializerTests.Person>>]
[GenerateShapeFor<int[]>]
public partial class JsonObjectSerializerTests
{
	[Fact]
	public void SerializeDeserialize_ListOfObjects()
	{
		JsonSerializer serializer = new();
		List<Person> value =
		[
			new Person { Name = "Ada", Age = 37 },
			new Person { Name = "Grace", Age = 41, Address = new Address { City = "Arlington", PostalCode = 22201 } },
		];

		string json = serializer.Serialize(value);

		Assert.Equal("[{\"name\":\"Ada\",\"age\":37,\"address\":null},{\"name\":\"Grace\",\"age\":41,\"address\":{\"city\":\"Arlington\",\"postalCode\":22201}}]", json);
		AssertRoundtrip(json, serializer, value);
	}

	[Fact]
	public void SerializeDeserialize_ListOfObjects_WithWitnessType()
	{
		JsonSerializer serializer = new();
		List<Person> value =
		[
			new Person { Name = "Ada", Age = 37 },
			new Person { Name = "Grace", Age = 41 },
		];

		string json = serializer.Serialize<List<Person>, JsonObjectSerializerTests>(value);
		List<Person> roundTripped = serializer.Deserialize<List<Person>, JsonObjectSerializerTests>(json);

		AssertStructuralEqual(value, roundTripped, json);
	}

	[Fact]
	public void SerializeDeserialize_Array_WithWitnessType()
	{
		JsonSerializer serializer = new();
		int[] value = [1, 2, 3, 5, 8];

		string json = serializer.Serialize<int[], JsonObjectSerializerTests>(value);
		int[] roundTripped = serializer.Deserialize<int[], JsonObjectSerializerTests>(json);

		AssertStructuralEqual(value, roundTripped, json);
	}

	[Fact]
	public void SerializeDeserialize_ObjectGraph_WithEnumerableProperties()
	{
		JsonSerializer serializer = new();
		EnumerableContainer value = new()
		{
			People =
			[
				new Person { Name = "Ada", Age = 37 },
				new Person { Name = "Grace", Age = 41 },
			],
			LuckyNumbers = [3, 5, 8],
		};

		string json = serializer.Serialize(value);

		Assert.Equal("{\"people\":[{\"name\":\"Ada\",\"age\":37,\"address\":null},{\"name\":\"Grace\",\"age\":41,\"address\":null}],\"luckyNumbers\":[3,5,8]}", json);
		AssertRoundtrip(json, serializer, value);
	}

	[GenerateShape]
	internal partial class EnumerableContainer
	{
		public List<Person>? People { get; set; }

		public int[]? LuckyNumbers { get; set; }
	}
}
