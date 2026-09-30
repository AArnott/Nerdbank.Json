// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using PolyType.Abstractions;

[GenerateShapeFor<string>]
[GenerateShapeFor<int>]
public partial class JsonSerializerContextOverloadTests : TestBase
{
	private static readonly object StateKey = new();
	private static readonly ITypeShape<string> StringShape = Shape<string, JsonSerializerContextOverloadTests>();
	private static readonly ITypeShape<int> IntShape = Shape<int, JsonSerializerContextOverloadTests>();

	private readonly JsonSerializer serializer = new()
	{
		Converters = new ConverterCollection([new StateConverter()]),
	};

	[Test]
	public void Serialize_PerCallStateReachesConverter()
	{
		Assert.Equal("\"per call\"", this.SerializeWithContext(CreateContext("per call")));
	}

	[Test]
	public void Deserialize_PerCallStateReachesConverter()
	{
		JsonReader reader = new("\"input\""u8);

		Assert.Equal("per call", this.serializer.Deserialize(ref reader, StringShape, CreateContext("per call")));
	}

	[Test]
	public void SerializeObject_PerCallStateReachesConverter()
	{
		Pipe buffer = new();
		JsonWriter writer = new(buffer.Writer);

		this.serializer.SerializeObject(ref writer, "input", StringShape, CreateContext("per call"));
		writer.Flush();

		Assert.Equal("\"per call\"", ToString(buffer));
	}

	[Test]
	public void DeserializeObject_PerCallStateReachesConverter()
	{
		JsonReader reader = new("\"input\""u8);

		Assert.Equal("per call", this.serializer.DeserializeObject(ref reader, StringShape, CreateContext("per call")));
	}

	[Test]
	public async Task SerializeAsync_PerCallStateReachesConverter()
	{
		Pipe pipe = new();

		await this.serializer.SerializeAsync(pipe.Writer, "input", StringShape, CreateContext("per call"));
		await pipe.Writer.CompleteAsync();

		Assert.Equal("\"per call\"", await ReadAllAsync(pipe.Reader));
	}

	[Test]
	public async Task DeserializeAsync_PerCallStateReachesConverter()
	{
		Pipe pipe = new();
		await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("\"input\""));
		await pipe.Writer.CompleteAsync();

		Assert.Equal("per call", await this.serializer.DeserializeAsync(pipe.Reader, StringShape, CreateContext("per call")));
	}

	[Test]
	public void SequentialCallsSeeTheirOwnState()
	{
		SerializationContext startingContextBefore = this.serializer.StartingContext;

		Assert.Equal("\"first\"", this.SerializeWithContext(CreateContext("first")));
		Assert.Equal("\"second\"", this.SerializeWithContext(CreateContext("second")));

		Assert.Equal(startingContextBefore, this.serializer.StartingContext);
		Assert.Null(this.serializer.StartingContext[StateKey]);
	}

	[Test]
	public void PerCallStateIsNotMergedWithStartingContextState()
	{
		SerializationContext startingContext = new();
		startingContext[StateKey] = "serializer";
		JsonSerializer serializer = this.serializer with { StartingContext = startingContext };

		Assert.Equal("null", SerializeWithContext(serializer, new SerializationContext()));

		SerializationContext derived = serializer.StartingContext;
		Assert.Equal("\"serializer\"", SerializeWithContext(serializer, derived));
	}

	[Test]
	public void DefaultContextIsAccepted()
	{
		Assert.Equal("null", this.SerializeWithContext(default(SerializationContext)));
	}

	[Test]
	public void DefaultArgumentBindsToCancellationTokenOverload()
	{
		SerializationContext startingContext = new();
		startingContext[StateKey] = "serializer";
		JsonSerializer serializer = this.serializer with { StartingContext = startingContext };
		Pipe buffer = new();
		JsonWriter writer = new(buffer.Writer);

		serializer.Serialize(ref writer, "input", StringShape, default);
		writer.Flush();

		Assert.Equal("\"serializer\"", ToString(buffer));
	}

	[Test]
	public void CancellationComesFromPerCallContext()
	{
		SerializationContext context = new() { CancellationToken = new CancellationToken(canceled: true) };

		Assert.Throws<OperationCanceledException>(() => this.SerializeWithContext(context));
	}

	[Test]
	public async Task SerializeAsync_CancellationComesFromPerCallContext()
	{
		Pipe pipe = new();
		SerializationContext context = new() { CancellationToken = new CancellationToken(canceled: true) };

		await Assert.ThrowsAsync<OperationCanceledException>(async () => await this.serializer.SerializeAsync(pipe.Writer, "input", StringShape, context));
	}

	[Test]
	public void ContextFromActiveOperationIsRejected()
	{
		JsonSerializer serializer = new() { Converters = new ConverterCollection([new ReentrantConverter()]) };

		JsonSerializationException ex = Assert.Throws<JsonSerializationException>(() =>
		{
			Pipe buffer = new();
			JsonWriter writer = new(buffer.Writer);
			serializer.Serialize(ref writer, 5, IntShape, CancellationToken.None);
		});
		Assert.Equal("startingContext", Assert.IsType<ArgumentException>(ex.InnerException).ParamName);
	}

	private static SerializationContext CreateContext(string state)
	{
		SerializationContext context = new();
		context[StateKey] = state;
		return context;
	}

	private static ITypeShape<T> Shape<T, TProvider>()
#if NET
		where TProvider : IShapeable<T> => TProvider.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
#endif

	private static string SerializeWithContext(JsonSerializer serializer, SerializationContext context)
	{
		Pipe buffer = new();
		JsonWriter writer = new(buffer.Writer);
		serializer.Serialize(ref writer, "input", StringShape, context);
		writer.Flush();
		return ToString(buffer);
	}

	private static string ToString(Pipe buffer)
	{
		buffer.Writer.Complete();
		Assert.True(buffer.Reader.TryRead(out ReadResult result));
		string text = Encoding.UTF8.GetString(result.Buffer.ToArray());
		buffer.Reader.AdvanceTo(result.Buffer.End);
		return text;
	}

	private static async Task<string> ReadAllAsync(PipeReader reader)
	{
		while (true)
		{
			ReadResult result = await reader.ReadAsync();
			if (result.IsCompleted)
			{
				string text = Encoding.UTF8.GetString(result.Buffer.ToArray());
				reader.AdvanceTo(result.Buffer.End);
				return text;
			}

			reader.AdvanceTo(result.Buffer.Start, result.Buffer.End);
		}
	}

	private string SerializeWithContext(SerializationContext context) => SerializeWithContext(this.serializer, context);

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

	private sealed class ReentrantConverter : JsonConverter<int>
	{
		private readonly JsonSerializer inner = new();

		public override void Write(ref JsonWriter writer, int value, SerializationContext context)
			=> this.inner.Serialize(ref writer, "nested", StringShape, context);

		public override int Read(ref JsonReader reader, SerializationContext context) => throw new NotSupportedException();
	}
}
