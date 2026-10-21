// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;
using PolyType;

[GenerateShapeFor<string>]
public partial class JsonObjectAsyncTests
{
	[Test]
	public async Task StreamUntypedRoundTripAndPerCallState()
	{
		ITypeShape<string> shape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<string>()!;
		JsonSerializer serializer = new() { Converters = [new StateConverter()] };
		SerializationContext context = new();
		context["suffix"] = "!";
		using MemoryStream stream = new();
		await serializer.SerializeObjectAsync(stream, "value", shape, context);
		Assert.True(stream.CanWrite);
		Assert.Equal("\"value!\"", Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		Assert.Equal("value!!", await serializer.DeserializeObjectAsync(stream, shape, context));
		Assert.True(stream.CanRead);

		stream.SetLength(0);
		await serializer.SerializeObjectAsync(stream, "ordinary", shape);
		stream.Position = 0;
		Assert.Equal("ordinary", await serializer.DeserializeObjectAsync(stream, shape));
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task PipeUntypedRoundTrip(bool perCallState)
	{
		ITypeShape<string> shape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<string>()!;
		JsonSerializer serializer = new() { Converters = [new StateConverter()] };
		SerializationContext context = new();
		context["suffix"] = "!";
		Pipe pipe = new();
		if (perCallState)
		{
			await serializer.SerializeObjectAsync(pipe.Writer, "hello", shape, context);
		}
		else
		{
			await serializer.SerializeObjectAsync(pipe.Writer, "hello", shape);
		}

		await pipe.Writer.CompleteAsync();
		Assert.Equal(
			perCallState ? "hello!!" : "hello",
			perCallState ? await serializer.DeserializeObjectAsync(pipe.Reader, shape, context) : await serializer.DeserializeObjectAsync(pipe.Reader, shape));
		await pipe.Reader.CompleteAsync();
	}

	[Test]
	public async Task TypedStreamPerCallStateAndLegacyWitnessOverloads()
	{
		JsonSerializer serializer = new() { Converters = [new StateConverter()] };
		SerializationContext context = new();
		context["suffix"] = "!";
		using MemoryStream stream = new();
		await serializer.SerializeAsync<string, JsonObjectAsyncTests>(stream, "value", context);
		stream.Position = 0;
		Assert.Equal("value!!", await serializer.DeserializeAsync<string, JsonObjectAsyncTests>(stream, context));
		stream.SetLength(0);
		await serializer.SerializeAsync<string, JsonObjectAsyncTests>(stream, "ordinary");
		stream.Position = 0;
		Assert.Equal("ordinary", await serializer.DeserializeAsync<string, JsonObjectAsyncTests>(stream));
	}

	[Test]
	public async Task GeneratedAsyncContextConvenienceOverloads()
	{
		JsonSerializer serializer = new() { Converters = [new StateConverter()] };
		SerializationContext context = new();
		context["suffix"] = "!";
		AsyncModel value = new() { Value = "value" };
		using MemoryStream stream = new();
		await serializer.SerializeAsync(stream, value, context);
		stream.Position = 0;
		Assert.Equal("value!!", (await serializer.DeserializeAsync<AsyncModel>(stream, context))!.Value);
		Pipe pipe = new();
		await serializer.SerializeAsync(pipe.Writer, value, context);
		await pipe.Writer.CompleteAsync();
		Assert.Equal("value!!", (await serializer.DeserializeAsync<AsyncModel>(pipe.Reader, context))!.Value);
		await pipe.Reader.CompleteAsync();
		pipe = new();
		await serializer.SerializeAsync<string, JsonObjectAsyncTests>(pipe.Writer, "value", context);
		await pipe.Writer.CompleteAsync();
		Assert.Equal("value!!", await serializer.DeserializeAsync<string, JsonObjectAsyncTests>(pipe.Reader, context));
		await pipe.Reader.CompleteAsync();
	}

	[Test]
	public async Task UntypedCancellationAndTrailingInput()
	{
		ITypeShape<string> shape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<string>()!;
		JsonSerializer serializer = new();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("\"value\" trailing"));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await serializer.DeserializeObjectAsync(stream, shape, cancellation.Token));
		await Assert.ThrowsAsync<FormatException>(async () => await serializer.DeserializeObjectAsync(stream, shape));
		Assert.True(stream.CanRead);
	}

	[GenerateShape]
	public partial class AsyncModel
	{
		public string? Value { get; set; }
	}

	private sealed class StateConverter : JsonConverter<string>
	{
		public override void Write(ref JsonWriter writer, in string? value, SerializationContext context)
		{
			Assert.NotNull(context.TypeShapeProvider);
			writer.Write(value + context["suffix"]);
		}

		public override string? Read(ref JsonReader reader, SerializationContext context) => reader.ReadString() + context["suffix"];
	}
}
