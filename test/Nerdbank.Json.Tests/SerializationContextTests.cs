// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[GenerateShapeFor<string>]
public partial class SerializationContextTests : TestBase
{
	private static readonly object StateKey = new();

	[Test]
	public void Indexer_GetReturnsNullForMissingKey()
	{
		SerializationContext context = new();

		Assert.Null(context[StateKey]);
	}

	[Test]
	public void Indexer_SetAndGetValue()
	{
		SerializationContext context = new();
		object value = new();

		context[StateKey] = value;

		Assert.Same(value, context[StateKey]);
	}

	[Test]
	public void Indexer_SettingNullRemovesValue()
	{
		SerializationContext context = new();
		context[StateKey] = "value";

		context[StateKey] = null;

		Assert.Null(context[StateKey]);
	}

	[Test]
	public void Indexer_ChangesAreIsolatedBetweenCopies()
	{
		SerializationContext original = new();
		original[StateKey] = "original";
		SerializationContext copy = original;

		copy[StateKey] = "copy";

		Assert.Equal("original", original[StateKey]);
		Assert.Equal("copy", copy[StateKey]);
	}

	[Test]
	public void StartingContextStateIsAvailableToConverters()
	{
		SerializationContext startingContext = new();
		startingContext[StateKey] = "from context";
		JsonSerializer serializer = new()
		{
			StartingContext = startingContext,
			Converters = new ConverterCollection([new StateConverter()]),
		};

		Assert.Equal("\"from context\"", serializer.Serialize<string, SerializationContextTests>("input"));
		Assert.Equal("from context", serializer.Deserialize<string, SerializationContextTests>("\"input\""));
	}

	private sealed class StateConverter : JsonConverter<string>
	{
		public override void Write(ref JsonWriter writer, string? value, SerializationContext context)
			=> writer.WriteStringValue(context[StateKey] as string);

		public override string? Read(ref JsonReader reader, SerializationContext context)
		{
			reader.ReadString();
			return context[StateKey] as string;
		}
	}
}
