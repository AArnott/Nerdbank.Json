// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using PolyType.Abstractions;

public partial class JsonAsyncObjectStreamingTests : TestBase
{
	[Test]
	public async Task MutableObject_ProcessesBeforeClosingBrace()
	{
		ProbeConverter probe = new();
		JsonSerializer serializer = new() { Converters = new ConverterCollection([probe]) };
		Pipe pipe = new();

		ValueTask<MutableModel?> task = serializer.DeserializeAsync(pipe.Reader, Shape<MutableModel, MutableModel>());
		await WriteAsync(pipe.Writer, "{\"a\":1,\"b\":2,\"probe\":\"hello\"");

		await WaitWithTimeoutAsync(probe.Signal.Task, TimeSpan.FromSeconds(10));

		await WriteAsync(pipe.Writer, "}");
		await pipe.Writer.CompleteAsync();

		MutableModel? result = await task;
		Assert.NotNull(result);
		Assert.Equal(1, result!.A);
		Assert.Equal(2, result.B);
		Assert.Equal("hello", result.Probe.Text);
		Assert.True(result.CallbackObserved);
	}

	[Test]
	public async Task ConstructorObject_ProcessesBeforeClosingBrace()
	{
		ProbeConverter probe = new();
		JsonSerializer serializer = new() { Converters = new ConverterCollection([probe]) };
		Pipe pipe = new();

		ValueTask<CtorModel?> task = serializer.DeserializeAsync(pipe.Reader, Shape<CtorModel, CtorModel>());
		await WriteAsync(pipe.Writer, "{\"a\":1,\"b\":2,\"probe\":\"world\"");

		await WaitWithTimeoutAsync(probe.Signal.Task, TimeSpan.FromSeconds(10));

		await WriteAsync(pipe.Writer, "}");
		await pipe.Writer.CompleteAsync();

		CtorModel? result = await task;
		Assert.NotNull(result);
		Assert.Equal(1, result!.A);
		Assert.Equal(2, result.B);
		Assert.Equal("world", result.Probe.Text);
	}

	[Test]
	public async Task PreservedReference_ProcessesBeforeClosingBraces()
	{
		ProbeConverter probe = new();
		JsonSerializer serializer = new()
		{
			PreserveReferences = ReferencePreservationMode.RejectCycles,
			Converters = new ConverterCollection([probe]),
		};
		Pipe pipe = new();

		ValueTask<MutableModel?> task = serializer.DeserializeAsync(pipe.Reader, Shape<MutableModel, MutableModel>());
		await WriteAsync(pipe.Writer, "{\"$id\":1,\"$value\":{\"a\":7,\"b\":8,\"probe\":\"deep\"");

		await WaitWithTimeoutAsync(probe.Signal.Task, TimeSpan.FromSeconds(10));

		await WriteAsync(pipe.Writer, "}}");
		await pipe.Writer.CompleteAsync();

		MutableModel? result = await task;
		Assert.NotNull(result);
		Assert.Equal(7, result!.A);
		Assert.Equal("deep", result.Probe.Text);
	}

	[Test]
	public async Task PreservedReference_Cycle_RoundTrips()
	{
		JsonSerializer serializer = new() { PreserveReferences = ReferencePreservationMode.AllowCycles };
		Node node = new() { Name = "self" };
		node.Next = node;

		Pipe pipe = new();
		await serializer.SerializeAsync(pipe.Writer, node, Shape<Node, Node>());
		await pipe.Writer.CompleteAsync();
		Node? result = await serializer.DeserializeAsync(pipe.Reader, Shape<Node, Node>());

		Assert.NotNull(result);
		Assert.Equal("self", result!.Name);
		Assert.Same(result, result.Next);
	}

	[Test]
	public async Task MutableObject_DuplicateProperty_Throws()
	{
		Pipe pipe = new();
		ValueTask<MutableModel?> task = this.Serializer.DeserializeAsync(pipe.Reader, Shape<MutableModel, MutableModel>());
		await WriteAsync(pipe.Writer, """{"a":1,"a":2}""");
		await pipe.Writer.CompleteAsync();
		await Assert.ThrowsAsync<JsonSerializationException>(async () => await task);
	}

	[Test]
	public async Task ConstructorObject_MissingRequired_Throws()
	{
		Pipe pipe = new();
		ValueTask<CtorModel?> task = this.Serializer.DeserializeAsync(pipe.Reader, Shape<CtorModel, CtorModel>());
		await WriteAsync(pipe.Writer, """{"a":1}""");
		await pipe.Writer.CompleteAsync();
		await Assert.ThrowsAsync<FormatException>(async () => await task);
	}

	[Test]
	public async Task Object_Cancellation_Throws()
	{
		using CancellationTokenSource cts = new();
		Pipe pipe = new();
		ValueTask<MutableModel?> task = this.Serializer.DeserializeAsync(pipe.Reader, Shape<MutableModel, MutableModel>(), cts.Token);
		await WriteAsync(pipe.Writer, """{"a":1,""");
		cts.Cancel();
		await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
	}

	[Test]
	public async Task ExtensionData_RoundTrips_Async()
	{
		Pipe pipe = new();
		await WriteAsync(pipe.Writer, """{"name":"Ada","unknown":true,"extra":{"nested":5}}""");
		await pipe.Writer.CompleteAsync();
		ExtModel? result = await this.Serializer.DeserializeAsync(pipe.Reader, Shape<ExtModel, ExtModel>());
		Assert.NotNull(result);
		Assert.Equal("Ada", result!.Name);
		Assert.NotNull(result.ExtensionData);
		Assert.Equal("true", result.ExtensionData!["unknown"]);
		Assert.Equal("""{"nested":5}""", result.ExtensionData["extra"]);
	}

	[Test]
	public async Task NestedObject_SkipsUnknown_Async()
	{
		Pipe pipe = new();
		await WriteAsync(pipe.Writer, """{"a":1,"junk":{"deep":[1,2,3],"more":"x"},"b":2,"probe":"ok"}""");
		await pipe.Writer.CompleteAsync();
		ProbeConverter probe = new();
		JsonSerializer serializer = new() { Converters = new ConverterCollection([probe]) };
		MutableModel? result = await serializer.DeserializeAsync(pipe.Reader, Shape<MutableModel, MutableModel>());
		Assert.NotNull(result);
		Assert.Equal(1, result!.A);
		Assert.Equal(2, result.B);
		Assert.Equal("ok", result.Probe.Text);
	}

	private static async Task WriteAsync(PipeWriter writer, string text)
		=> await writer.WriteAsync(Encoding.UTF8.GetBytes(text));

	private static async Task WaitWithTimeoutAsync(Task task, TimeSpan timeout)
	{
		Task completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
		if (completed != task)
		{
			throw new TimeoutException("The expected processing did not occur before the closing brace arrived.");
		}

		await task.ConfigureAwait(false);
	}

	private static ITypeShape<T> Shape<T, TProvider>()
#if NET
		where TProvider : IShapeable<T> => TProvider.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
#endif

	[GenerateShape]
	internal partial class MutableModel : IJsonSerializationCallbacks
	{
		private bool callbackObserved;

		public int A { get; set; }

		public int B { get; set; }

		public Probe Probe { get; set; }

		internal bool CallbackObserved => this.callbackObserved;

		public void OnBeforeSerialize()
		{
		}

		public void OnAfterDeserialize() => this.callbackObserved = true;
	}

	[GenerateShape]
	internal partial record CtorModel(int A, int B, Probe Probe);

	[GenerateShape]
	internal partial class ExtModel
	{
		public string? Name { get; set; }

		[JsonExtensionData]
		public Dictionary<string, string>? ExtensionData { get; set; }
	}

	[GenerateShape]
	internal partial class Node
	{
		public string? Name { get; set; }

		public Node? Next { get; set; }
	}

	[GenerateShape]
	internal partial struct Probe
	{
		public Probe(string text) => this.Text = text;

		public string Text { get; set; }
	}

	internal sealed class ProbeConverter : JsonConverter<Probe>
	{
		internal TaskCompletionSource<bool> Signal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public override void Write(ref JsonWriter writer, Probe value, SerializationContext context)
			=> writer.WriteStringValue(value.Text);

		public override Probe Read(ref JsonReader reader, SerializationContext context)
		{
			string text = reader.ReadRequiredString();
			this.Signal.TrySetResult(true);
			return new Probe(text);
		}
	}
}
