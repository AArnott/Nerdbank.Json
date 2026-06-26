// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Nerdbank.Json;
using PolyType;
using Xunit;

[GenerateShapeFor<Dictionary<string, int>>]
[GenerateShapeFor<Dictionary<int, string>>]
[GenerateShapeFor<Dictionary<Guid, int>>]
[GenerateShapeFor<Dictionary<ComplexKey, int>>]
[GenerateShapeFor<ReadOnlyDictionary<string, int>>]
public partial class JsonObjectSerializerTests
{
	[Test]
	public void SerializeDeserialize_Dictionary_LeavesKeysUntouchedByDefault()
	{
		JsonSerializer serializer = new();
		Dictionary<string, int> value = new(StringComparer.Ordinal)
		{
			["PascalCaseKey"] = 1,
			["snake_case_key"] = 2,
		};

		string json = serializer.Serialize(value);

		Assert.Equal("{\"PascalCaseKey\":1,\"snake_case_key\":2}", json);
		AssertRoundtrip(json, serializer, value);
	}

	[Test]
	public void SerializeDeserialize_Dictionary_CanApplyNamingPolicyToKeys()
	{
		JsonSerializer serializer = new() { DictionaryKeyNamingPolicy = JsonNamingPolicy.CamelCase };
		Dictionary<string, int> value = new(StringComparer.Ordinal)
		{
			["PascalCaseKey"] = 1,
			["AnotherKey"] = 2,
		};

		string json = serializer.Serialize(value);

		Assert.Equal("{\"pascalCaseKey\":1,\"anotherKey\":2}", json);

		Dictionary<string, int> roundTripped = serializer.Deserialize<Dictionary<string, int>>(json);
		Assert.Equal(2, roundTripped.Count);
		Assert.Equal(1, roundTripped["pascalCaseKey"]);
		Assert.Equal(2, roundTripped["anotherKey"]);
	}

	[Test]
	public void SerializeDeserialize_Dictionary_WithWitnessType()
	{
		JsonSerializer serializer = new();
		Dictionary<string, int> value = new(StringComparer.Ordinal)
		{
			["PascalCaseKey"] = 1,
			["snake_case_key"] = 2,
		};

		string json = serializer.Serialize<Dictionary<string, int>, JsonObjectSerializerTests>(value);
		Dictionary<string, int> roundTripped = serializer.Deserialize<Dictionary<string, int>, JsonObjectSerializerTests>(json);

		AssertStructuralEqual(value, roundTripped, json);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithDictionaryProperty()
	{
		JsonSerializer serializer = new();
		DictionaryContainer value = new()
		{
			Scores = new Dictionary<string, int>(StringComparer.Ordinal)
			{
				["FirstScore"] = 10,
				["second_score"] = 20,
			},
		};

		string json = serializer.Serialize(value);

		Assert.Equal("{\"scores\":{\"FirstScore\":10,\"second_score\":20}}", json);
		AssertRoundtrip(json, serializer, value);
	}

	[Test]
	public void SerializeDeserialize_Dictionary_WithIntKeys()
	{
		JsonSerializer serializer = new();
		Dictionary<int, string> value = new()
		{
			[1] = "one",
			[20] = "twenty",
		};

		string json = serializer.Serialize(value);

		Assert.Equal("{\"1\":\"one\",\"20\":\"twenty\"}", json);
		AssertRoundtrip(json, serializer, value);
	}

	[Test]
	public void SerializeDeserialize_Dictionary_WithGuidKeys()
	{
		JsonSerializer serializer = new();
		Guid first = Guid.Parse("f2cb13e4-7c12-4db6-b978-6a83abf1e9bf");
		Guid second = Guid.Parse("cefe95cc-6f79-4c5d-9c7e-8b8d9c61c4b2");
		Dictionary<Guid, int> value = new()
		{
			[first] = 1,
			[second] = 2,
		};

		string json = serializer.Serialize(value);

		Assert.Equal("{\"f2cb13e4-7c12-4db6-b978-6a83abf1e9bf\":1,\"cefe95cc-6f79-4c5d-9c7e-8b8d9c61c4b2\":2}", json);
		AssertRoundtrip(json, serializer, value);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithIntKeyDictionaryProperty()
	{
		JsonSerializer serializer = new();
		IntKeyDictionaryContainer value = new()
		{
			Counts = new Dictionary<int, string>
			{
				[3] = "three",
				[5] = "five",
			},
		};

		string json = serializer.Serialize(value);

		Assert.Equal("{\"counts\":{\"3\":\"three\",\"5\":\"five\"}}", json);
		AssertRoundtrip(json, serializer, value);
	}

	[Test]
	public void Serialize_Dictionary_WithComplexKeys_ThrowsNotSupportedException()
	{
		JsonSerializer serializer = new();
		Dictionary<ComplexKey, int> value = new()
		{
			[new ComplexKey { Name = "alpha" }] = 1,
		};

		NotSupportedException exception = Assert.Throws<NotSupportedException>(() => serializer.Serialize(value));
		Assert.Contains(typeof(ComplexKey).FullName ?? nameof(ComplexKey), exception.Message, StringComparison.Ordinal);
	}

	[Test]
	public void SerializeDeserialize_ReadOnlyDictionary_WithWitnessType()
	{
		JsonSerializer serializer = new();
		ReadOnlyDictionary<string, int> value = new(new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["FirstScore"] = 10,
			["second_score"] = 20,
		});

		string json = serializer.Serialize<ReadOnlyDictionary<string, int>, JsonObjectSerializerTests>(value);
		ReadOnlyDictionary<string, int> roundTripped = serializer.Deserialize<ReadOnlyDictionary<string, int>, JsonObjectSerializerTests>(json);

		Assert.Equal("{\"FirstScore\":10,\"second_score\":20}", json);
		AssertStructuralEqual(value, roundTripped, json);
	}

	[Test]
	public void Deserialize_ObjectGraph_Populates_GetterOnlyDictionaryProperty()
	{
		JsonSerializer serializer = new();

		GetterOnlyDictionaryContainer value = serializer.Deserialize<GetterOnlyDictionaryContainer>("{\"scores\":{\"FirstScore\":10,\"second_score\":20}}");

		Assert.Equal(2, value.Scores.Count);
		Assert.Equal(10, value.Scores["FirstScore"]);
		Assert.Equal(20, value.Scores["second_score"]);
	}

	[GenerateShape]
	internal partial class DictionaryContainer
	{
		public Dictionary<string, int>? Scores { get; set; }
	}

	[GenerateShape]
	internal partial class IntKeyDictionaryContainer
	{
		public Dictionary<int, string>? Counts { get; set; }
	}

	[GenerateShape]
	internal partial class GetterOnlyDictionaryContainer
	{
		public Dictionary<string, int> Scores { get; } = new(StringComparer.Ordinal);
	}

	[GenerateShape]
	internal partial class ComplexKey
	{
		public string? Name { get; set; }
	}
}
