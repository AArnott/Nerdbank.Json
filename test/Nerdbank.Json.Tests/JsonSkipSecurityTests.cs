// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using PolyType.Abstractions;

[GenerateShapeFor<int>]
[GenerateShapeFor<object>]
[GenerateShapeFor<System.Dynamic.ExpandoObject>]
[GenerateShapeFor<System.Collections.Specialized.NameValueCollection>]
[GenerateShapeFor<System.Text.Json.JsonDocument>]
[GenerateShapeFor<System.Text.Json.JsonElement>]
[GenerateShapeFor<System.Text.Json.Nodes.JsonNode>]
public partial class JsonSkipSecurityTests : TestBase
{
	[Test]
	[Arguments("[]", 0)]
	[Arguments("{}", 0)]
	[Arguments("[[]]", 1)]
	[Arguments("""{"a":{}}""", 1)]
	[Arguments("""[{"a":[{}]}]""", 3)]
	public void Skip_RejectsBeforeEnteringContainer(string json, int maxDepth)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		SerializationContext context = new() { MaxDepth = maxDepth };
		Assert.Throws<InvalidOperationException>(() => Read(bytes, context, raw: false));
		Assert.Throws<InvalidOperationException>(() => Read(bytes, context, raw: true));
		Assert.Equal(maxDepth, context.MaxDepth);
	}

	[Test]
	[Arguments("null", 0)]
	[Arguments("true", 0)]
	[Arguments("-1.25e+3", 0)]
	[Arguments("\"text\\\"more\"", 0)]
	[Arguments("[]", 1)]
	[Arguments("{}", 1)]
	[Arguments("[[],[],[0,1,2],{}]", 2)]
	[Arguments("""{"a":[],"b":{},"c":{"d":null}}""", 2)]
	public void Skip_AcceptsBoundaryAndPreservesSiblingBudget(string json, int maxDepth)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		SerializationContext context = new() { MaxDepth = maxDepth };
		Read(bytes, context, raw: false);
		Assert.Equal(json, Read(bytes, context, raw: true));
		Assert.Equal(maxDepth, context.MaxDepth);
	}

	[Test]
	public void ReadRawValue_ConvenienceUsesDefaultDepthLimit()
	{
		Assert.Equal(NestedArrays(64), ReadRawDefault(NestedArrays(64)));
		Assert.Throws<InvalidOperationException>(() => ReadRawDefault(NestedArrays(65)));

		static string ReadRawDefault(string json)
		{
			JsonReader reader = new(Encoding.UTF8.GetBytes(json));
			return reader.ReadRawValue();
		}
	}

	[Test]
	public void Skip_RejectsDeepInputBeforeConsumingOverBudgetContainer()
	{
		JsonReader reader = new(Encoding.UTF8.GetBytes(NestedArrays(32)));
		try
		{
			reader.Skip(new SerializationContext { MaxDepth = 3 });
			Assert.Fail("Expected the remaining depth limit to reject the fourth container.");
		}
		catch (InvalidOperationException exception)
		{
			Assert.Contains("maximum depth", exception.Message, StringComparison.Ordinal);
			Assert.Equal(3, reader.Consumed);
		}
	}

	[Test]
	[Arguments("null")]
	[Arguments("true")]
	[Arguments("1234567890")]
	[Arguments("\"string\"")]
	[Arguments("[[],{}]")]
	[Arguments("   /* comment */ 0")]
	public void Skip_HonorsCancellationForEveryTokenKind(string json)
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		SerializationContext context = new() { CancellationToken = cancellation.Token };
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		Assert.Throws<OperationCanceledException>(() => Read(bytes, context, raw: false));
		Assert.Throws<OperationCanceledException>(() => Read(bytes, context, raw: true));
	}

	[Test]
	[Arguments("[1,]")]
	[Arguments("""{"a":[1,]}""")]
	[Arguments("[01]")]
	[Arguments("[1e]")]
	[Arguments("""{"a" 1}""")]
	[Arguments("[1 2]")]
	public void Skip_PreservesMalformedSyntaxChecks(string json)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		Assert.Throws<FormatException>(() => Read(bytes, new SerializationContext(), raw: false));
		Assert.Throws<FormatException>(() => Read(bytes, new SerializationContext(), raw: true));
	}

	[Test]
	public void Skip_PreservesCommentAndTrailingCommaOptions()
	{
		JsonReader reader = new("""/*head*/{"a":[1,/*tail*/],}"""u8, allowTrailingCommas: true, JsonCommentHandling.Skip);
		reader.Skip(new SerializationContext { MaxDepth = 2 });
		reader.EnsureFullyConsumed();
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task AsyncSkip_EnforcesDepthAcrossBufferBoundaries(bool raw)
	{
		Pipe pipe = new();
		using JsonAsyncReader reader = new(pipe.Reader) { CancellationToken = default };
		SerializationContext context = new() { MaxDepth = 2 };
		Task pending = raw ? reader.ReadRawValueAsync(context).AsTask() : reader.SkipValueAsync(context).AsTask();
		await pipe.Writer.WriteAsync("[["u8.ToArray());
		await pipe.Writer.WriteAsync("["u8.ToArray());
		try
		{
			// The third opening token must fail without waiting for closing tokens or EOF.
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
		}
		finally
		{
			reader.Dispose();
			await pipe.Writer.CompleteAsync();
			await pipe.Reader.CompleteAsync();
		}
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task AsyncSkip_PreservesSiblingBudget(bool raw)
	{
		const string json = """[[],{},{"a":[],"b":{}},[[],[]]]""";
		Pipe pipe = new();
		await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(json));
		await pipe.Writer.CompleteAsync();
		using JsonAsyncReader reader = new(pipe.Reader) { CancellationToken = default };
		SerializationContext context = new() { MaxDepth = 3 };
		if (raw)
		{
			Assert.Equal(json, await reader.ReadRawValueAsync(context));
		}
		else
		{
			await reader.SkipValueAsync(context);
		}

		await reader.EnsureFullyConsumedAsync(context);
		Assert.Equal(3, context.MaxDepth);
		reader.Dispose();
		await pipe.Reader.CompleteAsync();
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task AsyncSkip_CancellationWhileWaitingForTokenRemainder(bool raw)
	{
		using CancellationTokenSource cancellation = new();
		Pipe pipe = new();
		using JsonAsyncReader reader = new(pipe.Reader) { CancellationToken = cancellation.Token };
		SerializationContext context = new() { CancellationToken = cancellation.Token };
		await pipe.Writer.WriteAsync("\"unfinished"u8.ToArray());
		Task pending = raw ? reader.ReadRawValueAsync(context).AsTask() : reader.SkipValueAsync(context).AsTask();
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
		reader.Dispose();
		await pipe.Writer.CompleteAsync();
		await pipe.Reader.CompleteAsync();
	}

	[Test]
	[Arguments("mutable")]
	[Arguments("constructor")]
	[Arguments("retained")]
	[Arguments("constructor-retained")]
	[Arguments("custom")]
	[Arguments("document")]
	[Arguments("element")]
	[Arguments("node")]
	[Arguments("object")]
	[Arguments("dynamic")]
	[Arguments("expando")]
	public async Task UnknownOrRawValues_UseActiveContextSyncAndAsync(string route)
	{
		switch (route)
		{
			case "mutable":
				await Exercise(Shape<Mutable>());
				break;
			case "constructor":
				await Exercise(Shape<Constructor>());
				break;
			case "retained":
				await Exercise(Shape<Retained>());
				break;
			case "constructor-retained":
				await Exercise(Shape<ConstructorRetained>());
				break;
			case "custom":
				await Exercise(Shape<RawValue>());
				break;
			case "document":
				await Exercise(Shape<System.Text.Json.JsonDocument, JsonSkipSecurityTests>());
				break;
			case "element":
				await Exercise(Shape<System.Text.Json.JsonElement, JsonSkipSecurityTests>());
				break;
			case "node":
				await Exercise(Shape<System.Text.Json.Nodes.JsonNode, JsonSkipSecurityTests>());
				break;
			case "object":
			case "dynamic":
				await Exercise(Shape<object, JsonSkipSecurityTests>());
				break;
			case "expando":
				await Exercise(Shape<System.Dynamic.ExpandoObject, JsonSkipSecurityTests>());
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(route));
		}

		async Task Exercise<T>(ITypeShape<T> shape)
		{
			JsonSerializer serializer = new JsonSerializer
			{
				StartingContext = new SerializationContext { MaxDepth = 3 },
				Converters = ConverterCollection.Create([new RawConverter()]),
			}.WithSystemTextJsonConverters();
			serializer = route == "dynamic" ? serializer.WithDynamicObjectConverter() : serializer.WithObjectConverter();
			serializer = serializer.WithExpandoObjectConverter();
			const string accepted = """{"value":7,"a":[[],{}],"b":{"c":[]}}""";
			string rejected = """{"value":7,"unknown":""" + NestedArrays(32) + "}";
			Assert.NotNull(serializer.Deserialize(accepted, shape));
			AssertDepthException(Assert.ThrowsAny<Exception>(() => serializer.Deserialize(rejected, shape)));
			using MemoryStream goodStream = new(Encoding.UTF8.GetBytes(accepted));
			Assert.NotNull(await serializer.DeserializeAsync(goodStream, shape));
			using MemoryStream badStream = new(Encoding.UTF8.GetBytes(rejected));
			AssertDepthException(await Assert.ThrowsAnyAsync<Exception>(async () => await serializer.DeserializeAsync(badStream, shape)));
		}
	}

	[Test]
	public async Task NameValueCollection_CustomStringCapturePreservesRemainingDepth()
	{
		JsonSerializer serializer = new JsonSerializer
		{
			StartingContext = new SerializationContext { MaxDepth = 3 },
			Converters = [new RawStringConverter()],
		}.WithNameValueCollectionConverter();
		ITypeShape<System.Collections.Specialized.NameValueCollection> shape = Shape<System.Collections.Specialized.NameValueCollection, JsonSkipSecurityTests>();
		const string accepted = """{"a":[{},[]],"b":[{},[]]}""";
		const string rejected = """{"a":[[[]]]}""";
		Assert.NotNull(serializer.Deserialize(accepted, shape));
		AssertDepthException(Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(rejected, shape)));
		using MemoryStream good = new(Encoding.UTF8.GetBytes(accepted));
		Assert.NotNull(await serializer.DeserializeAsync(good, shape));
		using MemoryStream bad = new(Encoding.UTF8.GetBytes(rejected));
		AssertDepthException(await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeAsync(bad, shape)));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public void TargetedSkip_UsesRemainingDepth(bool expression)
	{
		JsonSerializer serializer = new() { StartingContext = new SerializationContext { MaxDepth = 3 } };
		const string accepted = """{"unknown":[[],{}],"value":7}""";
		const string rejected = """{"unknown":[[[]]],"value":7}""";
		Assert.Equal(7, Select(accepted));
		AssertDepthException(Assert.ThrowsAny<Exception>(() => Select(rejected)));

		int Select(string json) => expression
			? serializer.DeserializePath(json, Shape<Mutable>(), new JsonSerializer.DeserializePathOptions<Mutable, int>(x => x.Value))
			: serializer.DeserializePath<int, JsonSkipSecurityTests>(json, JsonPath.Root.Member("value"));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task TargetedStreamingSkip_ChecksPayloadBeforeAndAfterSelection(bool after)
	{
		JsonSerializer serializer = new() { StartingContext = new SerializationContext { MaxDepth = 3 } };
		string good = after ? """{"items":[7],"unknown":[[],{}]}""" : """{"unknown":[[],{}],"items":[7]}""";
		string bad = after ? """{"items":[7],"unknown":[[[]]]}""" : """{"unknown":[[[]]],"items":[7]}""";
		await Collect(good);
		AssertDepthException(await Assert.ThrowsAnyAsync<Exception>(() => Collect(bad)));

		async Task Collect(string json)
		{
			using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
			await foreach (int value in serializer.DeserializePathEnumerableAsync<int, JsonSkipSecurityTests>(stream, JsonPath.Root.Member("items")))
			{
				Assert.Equal(7, value);
			}
		}
	}

	[Test]
	public async Task TargetedStreamingRawValue_DoesNotResetDepthAtSelection()
	{
		JsonSerializer serializer = new()
		{
			StartingContext = new SerializationContext { MaxDepth = 3 },
			Converters = ConverterCollection.Create([new RawConverter()]),
		};
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("""{"items":[{"unknown":[]}]}"""));
		AssertDepthException(await Assert.ThrowsAnyAsync<Exception>(async () =>
		{
			await foreach (RawValue? value in serializer.DeserializePathEnumerableAsync(stream, JsonPath.Root.Member("items"), Shape<RawValue>()))
			{
				Assert.NotNull(value);
			}
		}));
	}

	[Test]
	public async Task ArrayStreamingSkip_CountsEnclosingArray()
	{
		JsonSerializer serializer = new() { StartingContext = new SerializationContext { MaxDepth = 2 } };
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("""[{"value":7,"unknown":[]}]"""));
		AssertDepthException(await Assert.ThrowsAnyAsync<Exception>(async () =>
		{
			await foreach (Mutable? value in serializer.DeserializeArrayAsync(stream, Shape<Mutable>()))
			{
				Assert.NotNull(value);
			}
		}));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task UnionNavigation_CountsEnvelopeAndPayload(bool runtime)
	{
		JsonSerializer serializer = new()
		{
			StartingContext = new SerializationContext { MaxDepth = 4 },
			DerivedTypeUnions = runtime
				? [new DerivedShapeMapping<UnionBase> { { "UnionCase", Shape<UnionCase>() } }]
				: [],
		};
		const string accepted = """{"pet":["UnionCase",{"unknown":[],"value":7,"items":[7]}]}""";
		const string rejected = """{"pet":["UnionCase",{"unknown":[[]],"value":7,"items":[7]}]}""";
		JsonSerializer.DeserializePathOptions<UnionHolder, int> selectValue = new(x => x.Pet.Value);
		JsonSerializer.StreamingEnumerationOptions<UnionHolder, int> selectItems = new(x => x.Pet.Items);
		Assert.Equal(7, serializer.DeserializePath(accepted, Shape<UnionHolder>(), selectValue));
		AssertDepthException(Assert.ThrowsAny<Exception>(() => serializer.DeserializePath(rejected, Shape<UnionHolder>(), selectValue)));
		await Collect(serializer, accepted);
		AssertDepthException(await Assert.ThrowsAnyAsync<Exception>(() => Collect(serializer, rejected)));

		JsonSerializer shallower = serializer with { StartingContext = new SerializationContext { MaxDepth = 3 } };
		const string targetTooDeep = """{"pet":["UnionCase",{"value":7,"items":[7]}]}""";
		JsonSerializer.DeserializePathOptions<UnionHolder, int[]> selectArray = new(x => x.Pet.Items);
		AssertDepthException(Assert.ThrowsAny<Exception>(() => shallower.DeserializePath(targetTooDeep, Shape<UnionHolder>(), selectArray)));
		AssertDepthException(await Assert.ThrowsAnyAsync<Exception>(() => Collect(shallower, targetTooDeep)));

		async Task Collect(JsonSerializer owner, string json)
		{
			using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
			await foreach (int value in owner.DeserializePathEnumerableAsync(stream, Shape<UnionHolder>(), selectItems))
			{
				Assert.Equal(7, value);
			}
		}
	}

	private static string NestedArrays(int depth) => new string('[', depth) + "0" + new string(']', depth);

	private static string? Read(byte[] bytes, SerializationContext context, bool raw)
	{
		JsonReader reader = new(new ReadOnlySequence<byte>(bytes), commentHandling: JsonCommentHandling.Skip);
		string? result = null;
		if (raw)
		{
			result = reader.ReadRawValue(context);
		}
		else
		{
			reader.Skip(context);
		}

		reader.EnsureFullyConsumed();
		return result;
	}

	private static void AssertDepthException(Exception? exception)
	{
		Assert.NotNull(exception);
		Exception cause = Assert.IsType<JsonSerializationException>(exception).InnerException!;
		Assert.IsType<InvalidOperationException>(cause);
		Assert.Contains("maximum depth", cause.Message, StringComparison.Ordinal);
	}

	private static ITypeShape<T> Shape<T>()
#if NET
		where T : IShapeable<T> => T.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T>();
#endif

	private static ITypeShape<T> Shape<T, TProvider>()
#if NET
		where TProvider : IShapeable<T> => TProvider.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
#endif

	[GenerateShape]
	internal partial class Mutable
	{
		public int Value { get; set; }
	}

	[GenerateShape]
	internal partial record Constructor(int Value);

	[GenerateShape]
	internal partial class Retained
	{
		public int Value { get; set; }

		public UnusedDataPacket? Extra { get; set; }
	}

	[GenerateShape]
	internal partial record ConstructorRetained(int Value)
	{
		public UnusedDataPacket? Extra { get; set; }
	}

	[GenerateShape]
	internal partial record RawValue(string Json);

	[GenerateShape]
	internal partial record UnionHolder(UnionBase Pet);

	[GenerateShape]
	[DerivedTypeShape(typeof(UnionCase))]
	internal abstract partial record UnionBase(int Value, int[] Items);

	[GenerateShape]
	internal partial record UnionCase(int Value, int[] Items) : UnionBase(Value, Items);

	private sealed class RawConverter : JsonConverter<RawValue>
	{
		public override RawValue Read(ref JsonReader reader, SerializationContext context) => new(reader.ReadRawValue(context));

		public override void Write(ref JsonWriter writer, in RawValue? value, SerializationContext context) => throw new NotSupportedException();
	}

	private sealed class RawStringConverter : JsonConverter<string>
	{
		public override string Read(ref JsonReader reader, SerializationContext context) => reader.ReadRawValue(context);

		public override void Write(ref JsonWriter writer, in string? value, SerializationContext context) => throw new NotSupportedException();
	}
}
