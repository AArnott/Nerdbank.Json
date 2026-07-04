// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

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
		Dictionary<string, int> value = new(StringComparer.Ordinal)
		{
			["PascalCaseKey"] = 1,
			["snake_case_key"] = 2,
		};

		this.AssertRoundtrip<Dictionary<string, int>, JsonObjectSerializerTests>(value, """{"PascalCaseKey":1,"snake_case_key":2}""");
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

		string json = serializer.Serialize<Dictionary<string, int>, JsonObjectSerializerTests>(value);

		Assert.Equal("""{"pascalCaseKey":1,"anotherKey":2}""", json);

		Dictionary<string, int>? roundTripped = serializer.Deserialize<Dictionary<string, int>, JsonObjectSerializerTests>(json);
		Assert.NotNull(roundTripped);
		Assert.Equal(2, roundTripped.Count);
		Assert.Equal(1, roundTripped["pascalCaseKey"]);
		Assert.Equal(2, roundTripped["anotherKey"]);
	}

	[Test]
	public void Deserialize_Dictionary_UsesComparerProvider()
	{
		JsonSerializer serializer = new() { ComparerProvider = CaseInsensitiveStringComparerProvider.Instance };

		Assert.Throws<ArgumentException>(() => serializer.Deserialize<Dictionary<string, int>, JsonObjectSerializerTests>("""{"Key":1,"key":2}"""));
	}

	[Test]
	public void SerializeDeserialize_Dictionary_WithWitnessType()
	{
		Dictionary<string, int> value = new(StringComparer.Ordinal)
		{
			["PascalCaseKey"] = 1,
			["snake_case_key"] = 2,
		};

		this.AssertRoundtrip<Dictionary<string, int>, JsonObjectSerializerTests>(value);
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithDictionaryProperty()
	{
		DictionaryContainer value = new()
		{
			Scores = new Dictionary<string, int>(StringComparer.Ordinal)
			{
				["FirstScore"] = 10,
				["second_score"] = 20,
			},
		};

		this.AssertRoundtrip(value, """{"scores":{"FirstScore":10,"second_score":20}}""");
	}

	[Test]
	public void SerializeDeserialize_Dictionary_WithIntKeys()
	{
		Dictionary<int, string> value = new()
		{
			[1] = "one",
			[20] = "twenty",
		};

		this.AssertRoundtrip<Dictionary<int, string>, JsonObjectSerializerTests>(value, """{"1":"one","20":"twenty"}""");
	}

	[Test]
	public void SerializeDeserialize_Dictionary_WithGuidKeys()
	{
		Guid first = Guid.Parse("f2cb13e4-7c12-4db6-b978-6a83abf1e9bf");
		Guid second = Guid.Parse("cefe95cc-6f79-4c5d-9c7e-8b8d9c61c4b2");
		Dictionary<Guid, int> value = new()
		{
			[first] = 1,
			[second] = 2,
		};

		this.AssertRoundtrip<Dictionary<Guid, int>, JsonObjectSerializerTests>(value, """{"f2cb13e4-7c12-4db6-b978-6a83abf1e9bf":1,"cefe95cc-6f79-4c5d-9c7e-8b8d9c61c4b2":2}""");
	}

	[Test]
	public void SerializeDeserialize_ObjectGraph_WithIntKeyDictionaryProperty()
	{
		IntKeyDictionaryContainer value = new()
		{
			Counts = new Dictionary<int, string>
			{
				[3] = "three",
				[5] = "five",
			},
		};

		this.AssertRoundtrip<IntKeyDictionaryContainer>(value, """{"counts":{"3":"three","5":"five"}}""");
	}

	[Test]
	public void Serialize_Dictionary_WithComplexKeys_ThrowsNotSupportedException()
	{
		Dictionary<ComplexKey, int> value = new()
		{
			[new ComplexKey { Name = "alpha" }] = 1,
		};

		NotSupportedException exception = Assert.Throws<NotSupportedException>(() => this.Serializer.Serialize<Dictionary<ComplexKey, int>, JsonObjectSerializerTests>(value));
		Assert.Contains(typeof(ComplexKey).FullName ?? nameof(ComplexKey), exception.Message, StringComparison.Ordinal);
	}

	[Test]
	public void SerializeDeserialize_ReadOnlyDictionary_WithWitnessType()
	{
		ReadOnlyDictionary<string, int> value = new(new Dictionary<string, int>(StringComparer.Ordinal)
		{
			["FirstScore"] = 10,
			["second_score"] = 20,
		});

		this.AssertRoundtrip<ReadOnlyDictionary<string, int>, JsonObjectSerializerTests>(value, """{"FirstScore":10,"second_score":20}""");
	}

	[Test]
	public void Deserialize_ObjectGraph_Populates_GetterOnlyDictionaryProperty()
	{
		GetterOnlyDictionaryContainer? value = this.Serializer.Deserialize<GetterOnlyDictionaryContainer>("""{"scores":{"FirstScore":10,"second_score":20}}""");

		Assert.NotNull(value);
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

	private class CaseInsensitiveStringComparerProvider : Nerdbank.MessagePack.IComparerProvider
	{
		internal static readonly CaseInsensitiveStringComparerProvider Instance = new();

		public IComparer<T>? GetComparer<T>(ITypeShape<T> shape) => null;

		public IEqualityComparer<T>? GetEqualityComparer<T>(ITypeShape<T> shape)
			=> typeof(T) == typeof(string) ? (IEqualityComparer<T>)StringComparer.OrdinalIgnoreCase : null;
	}
}
