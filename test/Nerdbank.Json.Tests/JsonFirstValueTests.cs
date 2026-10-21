// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using PolyType.Abstractions;

[GenerateShapeFor<int>]
[GenerateShapeFor<int[]>]
[GenerateShapeFor<JsonValue>]
public partial class JsonFirstValueTests
{
	[Test]
	[Arguments("123 456", "123")]
	[Arguments("true false", "true")]
	[Arguments("null null", "null")]
	[Arguments("\"one\"\"two\"", "\"one\"")]
	[Arguments("[1][2]", "[1]")]
	[Arguments("{\"value\":1}{\"value\":2}", "{\"value\":1}")]
	public void BuffersAndSynchronousStreams_ReturnFirstValue(string json, string first)
	{
		JsonSerializer serializer = new JsonSerializer().WithJsonValueConverter();
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		ITypeShape<JsonValue> shape = Shape<JsonValue>();
		Assert.Equal(first, serializer.SerializeJsonValue(serializer.Deserialize(bytes.AsMemory(), shape)));
		Assert.Equal(first, serializer.SerializeJsonValue(Assert.IsAssignableFrom<JsonValue>(serializer.DeserializeObject(bytes.AsMemory(), shape))));
		Assert.Equal(first, serializer.SerializeJsonValue(serializer.DeserializeJsonValue(bytes.AsMemory())));

		for (int split = 1; split < bytes.Length; split++)
		{
			Segment start = new(bytes.AsMemory(0, split));
			Segment end = start.Append(bytes.AsMemory(split));
			ReadOnlySequence<byte> sequence = new(start, 0, end, end.Memory.Length);
			Assert.Equal(first, serializer.SerializeJsonValue(serializer.Deserialize(sequence, shape)));
			Assert.Equal(first, serializer.SerializeJsonValue(Assert.IsAssignableFrom<JsonValue>(serializer.DeserializeObject(sequence, shape))));
			Assert.Equal(first, serializer.SerializeJsonValue(serializer.DeserializeJsonValue(sequence)));
		}

		using MemoryStream stream = new(bytes);
		Assert.Equal(first, serializer.SerializeJsonValue(serializer.Deserialize(stream, shape)));
		Assert.Equal(stream.Length, stream.Position);
		Assert.True(stream.CanRead);
		stream.Position = 0;
		Assert.Equal(first, serializer.SerializeJsonValue(Assert.IsAssignableFrom<JsonValue>(serializer.DeserializeObject(stream, shape))));
		stream.Position = 0;
		Assert.Equal(first, serializer.SerializeJsonValue(serializer.DeserializeJsonValue(stream)));
	}

	[Test]
	public void StringOverloads_RemainStrictDocuments()
	{
		JsonSerializer serializer = new JsonSerializer().WithJsonValueConverter();
		const string Json = "[1][2]";
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(Json, Shape<JsonValue>()));
		Assert.Throws<JsonSerializationException>(() => serializer.DeserializeObject(Json, Shape<JsonValue>()));
		Assert.Throws<JsonSerializationException>(() => serializer.DeserializeJsonValue(Json));
	}

	[Test]
	public void Reader_LeavesPositionAtNextValue()
	{
		JsonSerializer serializer = new();
		JsonReader reader = new("1 2 "u8);
		Assert.Equal(1, serializer.Deserialize(ref reader, Shape<int>()));
		Assert.Equal(2, serializer.DeserializeObject(ref reader, Shape<int>()));
		reader.EnsureFullyConsumed();
	}

	[Test]
	[Arguments("typed")]
	[Arguments("object")]
	[Arguments("typed-context")]
	[Arguments("object-context")]
	[Arguments("native")]
	public async Task Pipe_FirstValuePreservesUnreadAndUnexaminedBytes(string surface)
	{
		Pipe pipe = new();
		using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
		await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("""{"value":1}{"value":2}"""), deadline.Token);
		try
		{
			Assert.Equal("""{"value":1}""", await ReadModelAsync(pipe.Reader, surface, deadline.Token));
			Assert.True(pipe.Reader.TryRead(out ReadResult remaining));
			Assert.Equal("""{"value":2}""", Encoding.UTF8.GetString(remaining.Buffer.ToArray()));
			pipe.Reader.AdvanceTo(remaining.Buffer.Start);
			Assert.Equal("""{"value":2}""", await ReadModelAsync(pipe.Reader, surface, deadline.Token));
		}
		finally
		{
			await pipe.Writer.CompleteAsync();
			await pipe.Reader.CompleteAsync();
		}
	}

	[Test]
	[Arguments("123 456 ", "123", " 456 ")]
	[Arguments("true false ", "true", " false ")]
	[Arguments("null null ", "null", " null ")]
	[Arguments("\"one\"\"two\"", "\"one\"", "\"two\"")]
	[Arguments("[1][2]", "[1]", "[2]")]
	public async Task Pipe_ScalarAndContainerBoundaries_DoNotRequireEof(string json, string first, string unread)
	{
		Pipe pipe = new();
		using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
		await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(json), deadline.Token);
		try
		{
			JsonSerializer serializer = new();
			Assert.Equal(first, serializer.SerializeJsonValue(await serializer.DeserializeJsonValueAsync(pipe.Reader, deadline.Token)));
			Assert.True(pipe.Reader.TryRead(out ReadResult remaining));
			Assert.Equal(unread, Encoding.UTF8.GetString(remaining.Buffer.ToArray()));
			pipe.Reader.AdvanceTo(remaining.Buffer.Start);
		}
		finally
		{
			await pipe.Writer.CompleteAsync();
			await pipe.Reader.CompleteAsync();
		}
	}

	[Test]
	[Arguments("truefalse")]
	[Arguments("truefalse ")]
	[Arguments("false0")]
	[Arguments("nulltrue")]
	[Arguments("123abc")]
	[Arguments("01")]
	[Arguments("-01")]
	[Arguments("1e+-2")]
	[Arguments("1.2.3")]
	[Arguments("1e")]
	public async Task InvalidScalarPrefixes_AreNotAcceptedAsFirstValues(string json)
	{
		JsonSerializer serializer = new JsonSerializer().WithJsonValueConverter();
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		Assert.Throws<JsonSerializationException>(() => serializer.Deserialize(bytes.AsMemory(), Shape<JsonValue>()));
		Assert.Throws<JsonSerializationException>(() => serializer.DeserializeObject(bytes.AsMemory(), Shape<JsonValue>()));
		Assert.Throws<JsonSerializationException>(() => serializer.DeserializeJsonValue(bytes.AsMemory()));

		for (int chunk = 1; chunk <= bytes.Length; chunk++)
		{
			using ChunkedStream stream = new(bytes, chunk);
			PipeReader reader = PipeReader.Create(stream);
			try
			{
				JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeAsync(reader, Shape<JsonValue>()));
				Assert.IsType<FormatException>(exception.InnerException);
			}
			finally
			{
				await reader.CompleteAsync();
			}
		}
	}

	[Test]
	[Arguments("123 456", "123")]
	[Arguments("true false", "true")]
	[Arguments("null null", "null")]
	[Arguments("\"one\"\"two\"", "\"one\"")]
	[Arguments("[1][2]", "[1]")]
	[Arguments("{\"value\":1}{\"value\":2}", "{\"value\":1}")]
	public async Task Pipe_ChunkedTokenBoundaries_ReturnFirstValue(string json, string first)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		for (int chunk = 1; chunk <= bytes.Length; chunk++)
		{
			using ChunkedStream stream = new(bytes, chunk);
			PipeReader reader = PipeReader.Create(stream);
			try
			{
				JsonSerializer serializer = new();
				Assert.Equal(first, serializer.SerializeJsonValue(await serializer.DeserializeJsonValueAsync(reader)));
			}
			finally
			{
				await reader.CompleteAsync();
			}
		}
	}

	[Test]
	public async Task Pipe_CancellationWhileFraming_DoesNotLoseBufferedPrefix()
	{
		Pipe pipe = new();
		using CancellationTokenSource cancellation = new();
		using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
		await pipe.Writer.WriteAsync("12"u8.ToArray(), CancellationToken.None);
		JsonSerializer serializer = new();
		try
		{
			ValueTask<int> pending = serializer.DeserializeAsync(pipe.Reader, Shape<int>(), cancellation.Token);
			cancellation.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
			await pipe.Writer.WriteAsync("3 "u8.ToArray(), CancellationToken.None);
			Assert.Equal(123, await serializer.DeserializeAsync(pipe.Reader, Shape<int>(), deadline.Token));
		}
		finally
		{
			await pipe.Writer.CompleteAsync();
			await pipe.Reader.CompleteAsync();
		}
	}

	[Test]
	public async Task Pipe_CancelPendingReadWithoutCanceledToken_IsFailureAndLeavesDataAvailable()
	{
		Pipe pipe = new();
		using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(10));
		await pipe.Writer.WriteAsync("1 "u8.ToArray(), CancellationToken.None);
		pipe.Reader.CancelPendingRead();
		JsonSerializer serializer = new();
		try
		{
			JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeAsync(pipe.Reader, Shape<int>()));
			Assert.IsType<OperationCanceledException>(exception.InnerException);
			Assert.Equal(1, await serializer.DeserializeAsync(pipe.Reader, Shape<int>(), deadline.Token));
		}
		finally
		{
			await pipe.Writer.CompleteAsync();
			await pipe.Reader.CompleteAsync();
		}
	}

	[Test]
	[Arguments("typed")]
	[Arguments("object")]
	[Arguments("typed-context")]
	[Arguments("object-context")]
	[Arguments("native")]
	public async Task AsyncStreams_RemainStrictRatherThanDiscardReadAhead(string surface)
	{
		using ChunkedStream stream = new(Encoding.UTF8.GetBytes("""{"value":1}{"value":2}"""), 64);
		JsonSerializer serializer = new();
		JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(async () =>
		{
			switch (surface)
			{
				case "typed":
					await serializer.DeserializeAsync(stream, Shape<Model>());
					break;
				case "object":
					await serializer.DeserializeObjectAsync(stream, Shape<Model>());
					break;
				case "typed-context":
					await serializer.DeserializeAsync(stream, Shape<Model>(), new SerializationContext());
					break;
				case "object-context":
					await serializer.DeserializeObjectAsync(stream, Shape<Model>(), new SerializationContext());
					break;
				default:
					await serializer.DeserializeJsonValueAsync(stream);
					break;
			}
		});
		Assert.IsType<FormatException>(exception.InnerException);
		Assert.True(stream.CanRead);
	}

	private static async Task<string> ReadModelAsync(PipeReader reader, string surface, CancellationToken cancellationToken)
	{
		JsonSerializer serializer = new();
		SerializationContext context = new() { CancellationToken = cancellationToken };
		return surface switch
		{
			"typed" => serializer.Serialize(await serializer.DeserializeAsync(reader, Shape<Model>(), cancellationToken), Shape<Model>(), cancellationToken),
			"object" => serializer.SerializeObject(await serializer.DeserializeObjectAsync(reader, Shape<Model>(), cancellationToken), Shape<Model>(), cancellationToken),
			"typed-context" => serializer.Serialize(await serializer.DeserializeAsync(reader, Shape<Model>(), context), Shape<Model>(), cancellationToken),
			"object-context" => serializer.SerializeObject(await serializer.DeserializeObjectAsync(reader, Shape<Model>(), context), Shape<Model>(), cancellationToken),
			_ => serializer.SerializeJsonValue(await serializer.DeserializeJsonValueAsync(reader, cancellationToken), cancellationToken),
		};
	}

	private static ITypeShape<T> Shape<T>() => PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<T>()!;

	[GenerateShape]
	public partial record Model(int Value);

	private sealed class Segment : ReadOnlySequenceSegment<byte>
	{
		internal Segment(ReadOnlyMemory<byte> memory) => this.Memory = memory;

		internal Segment Append(ReadOnlyMemory<byte> memory)
		{
			Segment next = new(memory) { RunningIndex = this.RunningIndex + this.Memory.Length };
			this.Next = next;
			return next;
		}
	}

	private sealed class ChunkedStream(byte[] bytes, int chunkSize) : MemoryStream(bytes)
	{
		public override bool CanSeek => false;

		public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, chunkSize));

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
			=> base.ReadAsync(buffer, offset, Math.Min(count, chunkSize), cancellationToken);

#if NET
		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
			=> base.ReadAsync(buffer[..Math.Min(buffer.Length, chunkSize)], cancellationToken);
#endif
	}
}
