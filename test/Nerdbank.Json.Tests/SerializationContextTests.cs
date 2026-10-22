// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

[GenerateShapeFor<string>]
[GenerateShapeFor<SerializationContextTests.ContextPair>]
[GenerateShapeFor<SerializationContextTests.ConstructorContextPair>]
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
			Converters = ConverterCollection.Create([new StateConverter()]),
		};

		Assert.Equal("\"from context\"", serializer.Serialize<string, SerializationContextTests>("input"));
		Assert.Equal("from context", serializer.Deserialize<string, SerializationContextTests>("\"input\""));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void PropertyConverters_CannotMutateSiblingOrStartingContext(bool constructorBound)
	{
		SerializationContext startingContext = new() { MaxDepth = 2 };
		startingContext[StateKey] = "original";
		MutatingStateConverter converter = new();
		JsonSerializer serializer = new()
		{
			StartingContext = startingContext,
			Converters = [converter],
		};

		const string expected = "{\"first\":\"one\",\"second\":\"two\"}";
		string json = constructorBound
			? serializer.Serialize<ConstructorContextPair, SerializationContextTests>(new("one", "two"))
			: serializer.Serialize<ContextPair, SerializationContextTests>(new() { First = "one", Second = "two" });
		Assert.Equal(expected, json);
		AssertContexts();

		converter.Contexts.Clear();
		if (constructorBound)
		{
			Assert.Equal(new("one", "two"), serializer.Deserialize<ConstructorContextPair, SerializationContextTests>(json));
		}
		else
		{
			ContextPair result = serializer.Deserialize<ContextPair, SerializationContextTests>(json)!;
			Assert.Equal("one", result.First);
			Assert.Equal("two", result.Second);
		}

		AssertContexts();

		void AssertContexts()
		{
			Assert.Equal(2, converter.Contexts.Count);
			foreach (SerializationContext context in converter.Contexts)
			{
				Assert.Equal(1, context.MaxDepth);
				Assert.Equal("original", context[StateKey]);
				Assert.Same(PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default, context.TypeShapeProvider);
			}

			Assert.Equal(2, serializer.StartingContext.MaxDepth);
			Assert.Equal("original", serializer.StartingContext[StateKey]);
			Assert.Throws<InvalidOperationException>(() => serializer.StartingContext.TypeShapeProvider);
		}
	}

	public sealed class ContextPair
	{
		public string First { get; set; } = string.Empty;

		public string Second { get; set; } = string.Empty;
	}

	public sealed record ConstructorContextPair(string First, string Second);

	private sealed class MutatingStateConverter : JsonConverter<string>
	{
		internal List<SerializationContext> Contexts { get; } = [];

		public override void Write(ref JsonWriter writer, in string? value, SerializationContext context)
		{
			this.Mutate(ref context);
			writer.Write(value);
		}

		public override string? Read(ref JsonReader reader, SerializationContext context)
		{
			this.Mutate(ref context);
			return reader.ReadString();
		}

		private void Mutate(ref SerializationContext context)
		{
			this.Contexts.Add(context);
			context.DepthStep();
			context[StateKey] = "changed";
		}
	}

	private sealed class StateConverter : JsonConverter<string>
	{
		public override void Write(ref JsonWriter writer, in string? value, SerializationContext context)
			=> writer.Write(context[StateKey] as string);

		public override string? Read(ref JsonReader reader, SerializationContext context)
		{
			reader.ReadString();
			return context[StateKey] as string;
		}
	}
}
