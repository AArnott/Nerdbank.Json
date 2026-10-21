// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using PolyType.Abstractions;

public partial class JsonAsyncSequenceTests : TestBase
{
	[Test]
	public async Task RawPath_WitnessOverloads()
	{
		byte[] bytes = Encoding.UTF8.GetBytes("""{"items":[1,2]}""");
		using MemoryStream stream = new(bytes);
		JsonPath path = JsonPath.Root.Member("items");
		Assert.Equal([1, 2], await CollectAsync(this.Serializer.DeserializePathEnumerableAsync<int, Witness>(stream, path)));
		Pipe pipe = new();
		await WriteAsync(pipe.Writer, """{"items":[1,2]}""");
		await pipe.Writer.CompleteAsync();
		Assert.Equal([1, 2], await CollectAsync(this.Serializer.DeserializePathEnumerableAsync<int, Witness>(pipe.Reader, path)));
		await pipe.Reader.CompleteAsync();
	}

	[Test]
	public async Task PathOptions_EarlyDisposal_DoesNotMarkUnreadBytesExamined()
	{
		Pipe pipe = new();
		await WriteAsync(pipe.Writer, """{"inner":{"items":[1,2,3]}}""");
		JsonSerializer.StreamingEnumerationOptions<Envelope, int> options = new(x => x.Inner.Items) { LeaveOpen = true };
		await foreach (int value in this.Serializer.DeserializePathEnumerableAsync<Envelope, int, Envelope>(pipe.Reader, options))
		{
			Assert.Equal(1, value);
			break;
		}

		Assert.True(pipe.Reader.TryRead(out ReadResult remaining));
		Assert.Equal(",2,3]}}", Encoding.UTF8.GetString(remaining.Buffer.ToArray()));
		pipe.Reader.AdvanceTo(remaining.Buffer.End);
		await pipe.Writer.CompleteAsync();
		await pipe.Reader.CompleteAsync();
	}

	[Test]
	public async Task IndexHook_CustomEnvelope_TracksOpenContainers()
	{
		JsonSerializer serializer = new() { Converters = new ConverterCollection([new WrappedArrayConverter()]) };
		const string json = """{"groups":{"data":[{"items":[1]},{"items":[2,3]}],"tail":true},"tail":42}""";
		using ChunkedStream stream = new(Encoding.UTF8.GetBytes(json), 1);
		JsonSerializer.StreamingEnumerationOptions<GroupEnvelope, int> options = new(x => x.Groups[1].Items);
		Assert.Equal([2, 3], await CollectAsync(serializer.DeserializePathEnumerableAsync(stream, Shape<GroupEnvelope, GroupEnvelope>(), options)));
		Assert.Equal(3, serializer.DeserializePath(json, Shape<GroupEnvelope, GroupEnvelope>(), new JsonSerializer.DeserializePathOptions<GroupEnvelope, int>(x => x.Groups[1].Items[1])));
	}

	[Test]
	[Arguments(JsonSequenceFormat.NDJson, "1\n2\n3\n")]
	[Arguments(JsonSequenceFormat.JsonArray, "[1,2,3]")]
	public async Task Enumerable_Options_RoundTrip(JsonSequenceFormat format, string expected)
	{
		JsonSerializer.SequenceOptions options = new() { Format = format };
		using MemoryStream stream = new();
		await this.Serializer.SerializeEnumerableAsync<int, Witness>(stream, Produce(), options);
		Assert.Equal(expected, Encoding.UTF8.GetString(stream.ToArray()));
		stream.Position = 0;
		Assert.Equal([1, 2, 3], await CollectAsync(this.Serializer.DeserializeEnumerableAsync<int, Witness>(stream, options)));
		Assert.True(stream.CanRead);

		static async IAsyncEnumerable<int> Produce()
		{
			await Task.Yield();
			yield return 1;
			yield return 2;
			yield return 3;
		}
	}

	[Test]
	public async Task Enumerable_Options_RejectUnknownFormat()
	{
		JsonSerializer.SequenceOptions options = new() { Format = (JsonSequenceFormat)99 };
		using MemoryStream stream = new();
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await CollectAsync(this.Serializer.DeserializeEnumerableAsync<int, Witness>(stream, options)));
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await this.Serializer.SerializeEnumerableAsync<int, Witness>(stream, Empty(), options));

		static async IAsyncEnumerable<int> Empty()
		{
			await Task.Yield();
			yield break;
		}
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task PathOptions_OwnsPipeUnlessLeaveOpen(bool leaveOpen)
	{
		Pipe pipe = new();
		await WriteAsync(pipe.Writer, """{"inner":{"items":[1,2]},"tail":true}""");
		await pipe.Writer.CompleteAsync();
		JsonSerializer.StreamingEnumerationOptions<Envelope, int> options = new(x => x.Inner.Items) { LeaveOpen = leaveOpen };
		Assert.Equal([1, 2], await CollectAsync(this.Serializer.DeserializePathEnumerableAsync(pipe.Reader, Shape<Envelope, Envelope>(), options)));
		if (leaveOpen)
		{
			ReadResult remaining = await pipe.Reader.ReadAsync();
			Assert.True(remaining.IsCompleted);
			Assert.True(remaining.Buffer.IsEmpty);
			pipe.Reader.AdvanceTo(remaining.Buffer.End);
			await pipe.Reader.CompleteAsync();
		}
		else
		{
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await pipe.Reader.ReadAsync());
		}
	}

	[Test]
	[Arguments("""{"inner":{"items":null}}""")]
	[Arguments("""{"inner":null}""")]
	[Arguments("""{"inner":{}}""")]
	public async Task PathOptions_MissingOrNull(string json)
	{
		JsonSerializer.StreamingEnumerationOptions<Envelope, int> options = new(x => x.Inner.Items);
		using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
		await Assert.ThrowsAsync<JsonPathNotFoundException>(async () => await CollectAsync(this.Serializer.DeserializePathEnumerableAsync(stream, Shape<Envelope, Envelope>(), options)));
		stream.Position = 0;
		Assert.Empty(await CollectAsync(this.Serializer.DeserializePathEnumerableAsync<Envelope, int, Envelope>(stream, options with { EmptySequenceForUndiscoverablePath = true })));
		Assert.True(stream.CanRead);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task PathOptions_EarlyDisposal(bool leaveOpen)
	{
		Pipe pipe = new();
		await WriteAsync(pipe.Writer, """{"inner":{"items":[1,2,3]}}""");
		await pipe.Writer.CompleteAsync();
		JsonSerializer.StreamingEnumerationOptions<Envelope, int> options = new(x => x.Inner.Items) { LeaveOpen = leaveOpen };
		await foreach (int value in this.Serializer.DeserializePathEnumerableAsync(pipe.Reader, Shape<Envelope, Envelope>(), options))
		{
			Assert.Equal(1, value);
			break;
		}

		if (leaveOpen)
		{
			ReadResult remaining = await pipe.Reader.ReadAsync();
			Assert.Equal(",2,3]}}", Encoding.UTF8.GetString(remaining.Buffer.ToArray()));
			pipe.Reader.AdvanceTo(remaining.Buffer.End);
			await pipe.Reader.CompleteAsync();
		}
		else
		{
			await Assert.ThrowsAsync<InvalidOperationException>(async () => await pipe.Reader.ReadAsync());
		}
	}

	[Test]
	public async Task Array_RoundTrips_Chunked()
	{
		Item[] value = [new(1, "a"), new(2, "b"), new(3, "c")];
		byte[] utf8 = this.SerializeToUtf8(value, Shape<Item[], Witness>());
		for (int chunk = 1; chunk <= 5; chunk++)
		{
			using ChunkedStream stream = new(utf8, chunk);
			List<Item> result = await CollectAsync(this.Serializer.DeserializeArrayAsync(stream, Shape<Item, Item>()));
			Assert.Equal(value, result);
		}
	}

	[Test]
	public async Task Array_Empty()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("[]");
		using ChunkedStream stream = new(utf8, 1);
		List<Item> result = await CollectAsync(this.Serializer.DeserializeArrayAsync(stream, Shape<Item, Item>()));
		Assert.Empty(result);
	}

	[Test]
	public async Task Array_TopLevelNull_IsEmpty()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("null");
		using ChunkedStream stream = new(utf8, 1);
		List<Item> result = await CollectAsync(this.Serializer.DeserializeArrayAsync(stream, Shape<Item, Item>()));
		Assert.Empty(result);
	}

	[Test]
	public async Task Array_TrailingData_Throws()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("[1,2] garbage");
		using ChunkedStream stream = new(utf8, 3);
		await Assert.ThrowsAsync<FormatException>(async () =>
			await CollectAsync(this.Serializer.DeserializeArrayAsync(stream, Shape<int, Witness>())));
	}

	[Test]
	public async Task Array_MalformedElement_Throws()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("""[{"id":1,"name":"a"},{"id":"oops","name":"b"}]""");
		using ChunkedStream stream = new(utf8, 4);
		await Assert.ThrowsAsync<FormatException>(async () =>
			await CollectAsync(this.Serializer.DeserializeArrayAsync(stream, Shape<Item, Item>())));
	}

	[Test]
	public async Task Array_NoReadAhead_YieldsBeforeClose()
	{
		Pipe pipe = new();
		IAsyncEnumerator<Item?> e = this.Serializer.DeserializeArrayAsync(pipe.Reader, Shape<Item, Item>()).GetAsyncEnumerator();
		try
		{
			await WriteAsync(pipe.Writer, "[{\"id\":1,\"name\":\"a\"}");
			Assert.True(await e.MoveNextAsync());
			Assert.Equal(1, e.Current!.Id);

			await WriteAsync(pipe.Writer, ",{\"id\":2,\"name\":\"b\"}]");
			await pipe.Writer.CompleteAsync();
			Assert.True(await e.MoveNextAsync());
			Assert.Equal(2, e.Current!.Id);
			Assert.False(await e.MoveNextAsync());
		}
		finally
		{
			await e.DisposeAsync();
		}
	}

	[Test]
	public async Task Array_EarlyDisposal_LeavesPipeOpen()
	{
		Pipe pipe = new();
		await WriteAsync(pipe.Writer, "[1,2,3,4,5]");
		await pipe.Writer.CompleteAsync();

		int count = 0;
		await foreach (int value in this.Serializer.DeserializeArrayAsync(pipe.Reader, Shape<int, Witness>()))
		{
			count++;
			if (count == 2)
			{
				break;
			}
		}

		Assert.Equal(2, count);

		// The reader is owned by the caller; completing it here should not throw.
		await pipe.Reader.CompleteAsync();
	}

	[Test]
	public async Task Array_Cancellation_Throws()
	{
		using CancellationTokenSource cts = new();
		Pipe pipe = new();
		await WriteAsync(pipe.Writer, "[1,");
		await Assert.ThrowsAsync<OperationCanceledException>(async () =>
		{
			await foreach (int value in this.Serializer.DeserializeArrayAsync(pipe.Reader, Shape<int, Witness>(), cts.Token))
			{
				cts.Cancel();
			}
		});
	}

	[Test]
	public async Task NewlineDelimited_RoundTrips()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("{\"id\":1,\"name\":\"a\"}\n{\"id\":2,\"name\":\"b\"}\n{\"id\":3,\"name\":\"c\"}\n");
		for (int chunk = 1; chunk <= 4; chunk++)
		{
			using ChunkedStream stream = new(utf8, chunk);
			List<Item> result = await CollectAsync(this.Serializer.DeserializeEnumerableAsync(stream, Shape<Item, Item>()));
			Assert.Equal([new Item(1, "a"), new Item(2, "b"), new Item(3, "c")], result);
		}
	}

	[Test]
	public async Task NewlineDelimited_Empty()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("   \n  \n");
		using ChunkedStream stream = new(utf8, 2);
		List<int> result = await CollectAsync(this.Serializer.DeserializeEnumerableAsync(stream, Shape<int, Witness>()));
		Assert.Empty(result);
	}

	[Test]
	public async Task NewlineDelimited_TwoValuesOneLine_Throws()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("1 2\n");
		using ChunkedStream stream = new(utf8, 1);
		await Assert.ThrowsAsync<FormatException>(async () =>
			await CollectAsync(this.Serializer.DeserializeEnumerableAsync(stream, Shape<int, Witness>())));
	}

	[Test]
	public async Task PathPreParsed_EnvelopeArray_DrainsRemainder()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"meta":"x","items":[10,20,30],"count":3}""");
		for (int chunk = 1; chunk <= 6; chunk++)
		{
			using ChunkedStream stream = new(utf8, chunk);
			List<int> result = await CollectAsync(this.Serializer.DeserializePathEnumerableAsync(stream, JsonPath.Root.Member("items"), Shape<int, Witness>()));
			Assert.Equal([10, 20, 30], result);
		}
	}

	[Test]
	public async Task PathPreParsed_Missing_Throws()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"other":[1,2]}""");
		using ChunkedStream stream = new(utf8, 3);
		await Assert.ThrowsAsync<JsonPathNotFoundException>(async () =>
			await CollectAsync(this.Serializer.DeserializePathEnumerableAsync(stream, JsonPath.Root.Member("items"), Shape<int, Witness>())));
	}

	[Test]
	public async Task PathPreParsed_Missing_ReturnsEmpty()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"other":[1,2]}""");
		using ChunkedStream stream = new(utf8, 3);
		List<int> result = await CollectAsync(this.Serializer.DeserializePathEnumerableAsync(stream, JsonPath.Root.Member("items"), Shape<int, Witness>(), MissingPathBehavior.ReturnDefault));
		Assert.Empty(result);
	}

	[Test]
	public async Task PathPreParsed_NullTarget_IsEmpty()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"items":null,"count":0}""");
		using ChunkedStream stream = new(utf8, 3);
		List<int> result = await CollectAsync(this.Serializer.DeserializePathEnumerableAsync(stream, JsonPath.Root.Member("items"), Shape<int, Witness>()));
		Assert.Empty(result);
	}

	[Test]
	public async Task PathPreParsed_NonArrayTarget_Throws()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"items":42}""");
		using ChunkedStream stream = new(utf8, 3);
		await Assert.ThrowsAsync<FormatException>(async () =>
			await CollectAsync(this.Serializer.DeserializePathEnumerableAsync(stream, JsonPath.Root.Member("items"), Shape<int, Witness>())));
	}

	[Test]
	public async Task PathExpression_Nested()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"inner":{"items":[7,8,9]},"trailing":true}""");
		using ChunkedStream stream = new(utf8, 2);
		List<int> result = await CollectAsync(this.Serializer.DeserializePathEnumerableAsync<Envelope, int>(stream, Shape<Envelope, Envelope>(), new(e => e.Inner.Items)));
		Assert.Equal([7, 8, 9], result);
	}

	[Test]
	public async Task PathExpression_ThroughUnion()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"pet":["Cat",{"toys":["ball","mouse"],"lives":9}]}""");
		using ChunkedStream stream = new(utf8, 3);
		List<string> result = await CollectAsync(this.Serializer.DeserializePathEnumerableAsync<UnionEnvelope, string>(stream, Shape<UnionEnvelope, UnionEnvelope>(), new(e => e.Pet.Toys)));
		Assert.Equal(["ball", "mouse"], result);
	}

	[Test]
	public async Task PathExpression_ThroughCustomConverter()
	{
		JsonSerializer serializer = new() { Converters = ConverterCollection.Create([new CustomListConverter()]) };
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"list":{"v":[1,2,3]}}""");
		using ChunkedStream stream = new(utf8, 2);
		List<int> result = await CollectAsync(serializer.DeserializePathEnumerableAsync<CustomEnvelope, int>(stream, Shape<CustomEnvelope, CustomEnvelope>(), new(e => e.List.Values)));
		Assert.Equal([1, 2, 3], result);
	}

	[Test]
	public async Task PathPreParsed_ThroughUnion_WireThrows()
	{
		// A pre-parsed (wire) path cannot traverse a union envelope; it needs converter metadata.
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"pet":["Cat",{"toys":[1]}]}""");
		using ChunkedStream stream = new(utf8, 4);
		await Assert.ThrowsAsync<NotSupportedException>(async () =>
			await CollectAsync(this.Serializer.DeserializePathEnumerableAsync(stream, JsonPath.Root.Member("pet").Member("toys"), Shape<int, Witness>())));
	}

	[Test]
	public async Task SerializeArray_RoundTrips_OverPipe()
	{
		Pipe pipe = new();
		JsonSerializer serializer = new() { StartingContext = new SerializationContext { UnflushedBytesThreshold = 64 } };
		await serializer.SerializeArrayAsync(pipe.Writer, Source(200), Shape<int, Witness>());
		await pipe.Writer.CompleteAsync();
		List<int> result = await CollectAsync(serializer.DeserializeArrayAsync(pipe.Reader, Shape<int, Witness>()));
		Assert.Equal(200, result.Count);
		Assert.Equal(0, result[0]);
		Assert.Equal(199, result[199]);

		static async IAsyncEnumerable<int> Source(int count)
		{
			for (int i = 0; i < count; i++)
			{
				await Task.Yield();
				yield return i;
			}
		}
	}

	[Test]
	public async Task SerializeNewlineDelimited_RoundTrips()
	{
		using MemoryStream stream = new();
		await this.Serializer.SerializeEnumerableAsync(stream, Source(), Shape<Item, Item>());
		stream.Position = 0;
		List<Item> result = await CollectAsync(this.Serializer.DeserializeEnumerableAsync(stream, Shape<Item, Item>()));
		Assert.Equal([new Item(1, "a"), new Item(2, "b")], result);

		static async IAsyncEnumerable<Item> Source()
		{
			await Task.Yield();
			yield return new Item(1, "a");
			yield return new Item(2, "b");
		}
	}

	private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T?> sequence)
	{
		List<T> list = [];
		await foreach (T? item in sequence)
		{
			list.Add(item!);
		}

		return list;
	}

	private static async Task WriteAsync(PipeWriter writer, string text)
		=> await writer.WriteAsync(Encoding.UTF8.GetBytes(text));

	private static ITypeShape<T> Shape<T, TProvider>()
#if NET
		where TProvider : IShapeable<T> => TProvider.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
#endif

	private byte[] SerializeToUtf8<T>(T value, ITypeShape<T> shape)
		=> Encoding.UTF8.GetBytes(this.Serializer.Serialize(value, shape));

	[GenerateShape]
	internal partial record Item(int Id, string Name);

	[GenerateShape]
	internal partial record Envelope(Inner Inner);

	[GenerateShape]
	internal partial record Inner(int[] Items);

	[GenerateShape]
	internal partial record GroupEnvelope(Inner[] Groups);

	private sealed class WrappedArrayConverter : JsonConverter<Inner[]>
	{
		public override void Write(ref JsonWriter writer, Inner[]? value, SerializationContext context) => throw new NotSupportedException();

		public override Inner[]? Read(ref JsonReader reader, SerializationContext context) => throw new NotSupportedException();

		public override bool SkipToIndexValue(ref JsonReader reader, object? index, SerializationContext context)
		{
			reader.ReadStartObject();
			Assert.Equal("data", reader.ReadRequiredString());
			reader.ReadNameSeparator();
			return base.SkipToIndexValue(ref reader, index, context);
		}

		public override async ValueTask<bool> SkipToIndexValueAsync(JsonAsyncReader reader, object? index, SerializationContext context)
		{
			await reader.ReadStartObjectAsync(context);
			Assert.Equal("data", await reader.ReadPropertyNameAsync(context));
			return await base.SkipToIndexValueAsync(reader, index, context);
		}
	}

	[GenerateShape]
	[DerivedTypeShape(typeof(Cat))]
	internal partial record Animal(string[] Toys);

	[GenerateShape]
	internal partial record Cat(string[] Toys, int Lives) : Animal(Toys);

	[GenerateShape]
	internal partial record UnionEnvelope(Animal Pet);

	[GenerateShape]
	internal partial record CustomEnvelope(CustomList List);

	[GenerateShape]
	internal partial record CustomList(int[] Values);

	[GenerateShapeFor<int>]
	[GenerateShapeFor<Item[]>]
	internal partial class Witness;

	internal sealed class CustomListConverter : JsonConverter<CustomList>
	{
		public override void Write(ref JsonWriter writer, in CustomList? value, SerializationContext context)
		{
			writer.WriteStartObject();
			writer.WritePropertyName("v");
			writer.WriteStartArray();
			bool first = true;
			foreach (int item in value!.Values)
			{
				if (!first)
				{
					writer.WriteValueSeparator();
				}

				first = false;
				writer.Write(item);
			}

			writer.WriteEndArray();
			writer.WriteEndObject();
		}

		public override CustomList? Read(ref JsonReader reader, SerializationContext context)
		{
			reader.ReadStartObject();
			reader.ReadRequiredString();
			reader.ReadNameSeparator();
			List<int> values = [];
			reader.ReadStartArray();
			if (!reader.TryReadEndArray())
			{
				while (true)
				{
					values.Add(int.Parse(reader.ReadNumberToken(), System.Globalization.CultureInfo.InvariantCulture));
					if (reader.TryReadEndArray())
					{
						break;
					}

					reader.ReadValueSeparator();
				}
			}

			reader.TryReadEndObject();
			return new CustomList(values.ToArray());
		}

		public override async ValueTask<bool> SkipToPropertyValueAsync(JsonAsyncReader reader, IPropertyShape propertyShape, SerializationContext context)
		{
			await reader.ReadStartObjectAsync(context);
			string name = await reader.ReadPropertyNameAsync(context);
			if (name == "v" && propertyShape.Name == "Values")
			{
				return true;
			}

			return false;
		}
	}

	private sealed class ChunkedStream : Stream
	{
		private readonly byte[] data;
		private readonly int chunkSize;
		private int position;

		internal ChunkedStream(byte[] data, int chunkSize)
		{
			this.data = data;
			this.chunkSize = chunkSize;
		}

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => this.data.Length;

		public override long Position
		{
			get => this.position;
			set => throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int available = this.data.Length - this.position;
			if (available <= 0)
			{
				return 0;
			}

			int toCopy = Math.Min(Math.Min(count, this.chunkSize), available);
			Array.Copy(this.data, this.position, buffer, offset, toCopy);
			this.position += toCopy;
			return toCopy;
		}

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}
}
