// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

[GenerateShapeFor<List<JsonObjectSerializerTests.Person>>]
[GenerateShapeFor<int[]>]
[GenerateShapeFor<ReadOnlyCollection<int>>]
public partial class JsonObjectSerializerTests
{
	[Test]
	public void SerializeDeserialize_ListOfObjects()
	{
		JsonSerializer serializer = new();
		List<Person> value =
		[
			new Person { Name = "Ada", Age = 37 },
			new Person { Name = "Grace", Age = 41, Address = new Address { City = "Arlington", PostalCode = 22201 } },
		];

		this.AssertRoundtrip<List<Person>, JsonObjectSerializerTests>(value, """[{"name":"Ada","age":37,"address":null},{"name":"Grace","age":41,"address":{"city":"Arlington","postalCode":22201}}]""", serializer);
	}

	[Test]
	public void SerializeDeserialize_ListOfObjects_WithWitnessType()
	{
		JsonSerializer serializer = new();
		List<Person> value =
		[
			new Person { Name = "Ada", Age = 37 },
			new Person { Name = "Grace", Age = 41 },
		];

		string json = serializer.Serialize<List<Person>, JsonObjectSerializerTests>(value);
		List<Person>? roundTripped = serializer.Deserialize<List<Person>, JsonObjectSerializerTests>(json);

		Assert.NotNull(roundTripped);
		AssertStructuralEqual(value, roundTripped, json);
	}

	[Test]
	public void SerializeDeserialize_Array_WithWitnessType()
	{
		JsonSerializer serializer = new();
		int[] value = [1, 2, 3, 5, 8];

		string json = serializer.Serialize<int[], JsonObjectSerializerTests>(value);
		int[]? roundTripped = serializer.Deserialize<int[], JsonObjectSerializerTests>(json);

		Assert.NotNull(roundTripped);
		AssertStructuralEqual(value, roundTripped, json);
	}

	[Test]
	public void Serialize_Array_WithWitnessType_CanIndentOutput()
	{
		JsonSerializer serializer = new() { WriteIndented = true };
		int[] value = [1, 2, 3];

		string json = serializer.Serialize<int[], JsonObjectSerializerTests>(value);

		Assert.Equal("[\n  1,\n  2,\n  3\n]", json);
	}

	[Test]
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

		this.AssertRoundtrip(value, """{"people":[{"name":"Ada","age":37,"address":null},{"name":"Grace","age":41,"address":null}],"luckyNumbers":[3,5,8]}""", serializer);
	}

	[Test]
	public void SerializeDeserialize_ReadOnlyCollection_WithWitnessType()
	{
		JsonSerializer serializer = new();
		ReadOnlyCollection<int> value = new([3, 5, 8]);

		string json = serializer.Serialize<ReadOnlyCollection<int>, JsonObjectSerializerTests>(value);
		ReadOnlyCollection<int>? roundTripped = serializer.Deserialize<ReadOnlyCollection<int>, JsonObjectSerializerTests>(json);

		Assert.NotNull(roundTripped);
		Assert.Equal("""[3,5,8]""", json);
		AssertStructuralEqual(value, roundTripped, json);
	}

	[Test]
	public void Deserialize_ObjectGraph_Populates_GetterOnlyEnumerableProperty()
	{
		JsonSerializer serializer = new();

		GetterOnlyEnumerableContainer? value = serializer.Deserialize<GetterOnlyEnumerableContainer>("""{"people":[{"name":"Ada","age":37,"address":null},{"name":"Grace","age":41,"address":null}]}""");

		Assert.NotNull(value);
		Assert.Equal(2, value.People.Count);
		Assert.Equal("Ada", value.People[0].Name);
		Assert.Equal("Grace", value.People[1].Name);
	}

	[GenerateShape]
	internal partial class EnumerableContainer
	{
		public List<Person>? People { get; set; }

		public int[]? LuckyNumbers { get; set; }
	}

	[GenerateShape]
	internal partial class GetterOnlyEnumerableContainer
	{
		public List<Person> People { get; } = [];
	}
}
