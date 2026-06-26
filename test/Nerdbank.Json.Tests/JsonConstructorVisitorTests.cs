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

	[Fact]
	public void Deserialize_TypeWithParameterizedConstructor_AndSettableProperty()
	{
		JsonSerializer serializer = new();

		MixedConstructorType value = serializer.Deserialize<MixedConstructorType>("{\"name\":\"Ada\",\"age\":37}");

		Assert.Equal("Ada", value.Name);
		Assert.Equal(37, value.Age);
	}

	[Fact]
	public void Deserialize_RecordWithParameterizedConstructor_MissingRequiredParameter_ThrowsFormatException()
	{
		JsonSerializer serializer = new();

		FormatException exception = Assert.Throws<FormatException>(() => serializer.Deserialize<ParameterizedRecord>("{\"age\":37}"));
		Assert.Contains("Name", exception.Message);
	}

	[Fact]
	public void Deserialize_RecordWithParameterizedConstructor_DuplicateParameter_ThrowsFormatException()
	{
		JsonSerializer serializer = new();

		FormatException exception = Assert.Throws<FormatException>(() => serializer.Deserialize<ParameterizedRecord>("{\"name\":\"Ada\",\"name\":\"Grace\",\"age\":37}"));
		Assert.Contains("name", exception.Message, System.StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Deserialize_RecordWithOptionalConstructorParameter_UsesDefaultWhenMissing()
	{
		JsonSerializer serializer = new();

		OptionalParameterizedRecord value = serializer.Deserialize<OptionalParameterizedRecord>("{\"name\":\"Ada\"}");

		Assert.Equal(new OptionalParameterizedRecord("Ada", 21), value);
	}

	[GenerateShape]
	internal partial record ParameterizedRecord(string Name, int Age);

	[GenerateShape]
	internal partial record OptionalParameterizedRecord(string Name, int Age = 21);

	[GenerateShape]
	internal partial class MixedConstructorType
	{
		public MixedConstructorType(string name)
		{
			this.Name = name;
		}

		public string Name { get; }

		public int Age { get; set; }
	}
}
