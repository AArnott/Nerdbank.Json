// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;
using System.Threading.Tasks;
using PolyType.Abstractions;

public partial class JsonAsyncIOTests : TestBase
{
	[Test]
	public async Task RoundTrip_Object_OverStream()
	{
		Record value = new("Ada", 42, [1, 2, 3]);
		using MemoryStream stream = new();
		await this.Serializer.SerializeAsync(stream, value, Shape<Record, Record>());
		stream.Position = 0;
		Record? result = await this.Serializer.DeserializeAsync(stream, Shape<Record, Record>());
		AssertRecord(value, result);
	}

	[Test]
	public async Task RoundTrip_Object_OverPipe()
	{
		Record value = new("Grace", 7, [9]);
		Pipe pipe = new();
		await this.Serializer.SerializeAsync(pipe.Writer, value, Shape<Record, Record>());
		await pipe.Writer.CompleteAsync();
		Record? result = await this.Serializer.DeserializeAsync(pipe.Reader, Shape<Record, Record>());
		AssertRecord(value, result);
	}

	[Test]
	public async Task Deserialize_OneBytePerRead_FramesAcrossBoundaries()
	{
		Record value = new("A\"b\\c\u00e9\ud83d\ude00", -12345, [0, 1000000, -7]);
		byte[] utf8 = this.SerializeToUtf8(value, Shape<Record, Record>());
		using ChunkedStream stream = new(utf8, chunkSize: 1);
		Record? result = await this.Serializer.DeserializeAsync(stream, Shape<Record, Record>());
		AssertRecord(value, result);
	}

	[Test]
	public async Task Deserialize_ChunkedArrayOfObjects()
	{
		Record[] value = [new("a", 1, [1]), new("b", 2, []), new("c", 3, [3, 3, 3])];
		byte[] utf8 = this.SerializeToUtf8(value, Shape<Record[], Witness>());
		for (int chunk = 1; chunk <= 4; chunk++)
		{
			using ChunkedStream stream = new(utf8, chunk);
			Record[]? result = await this.Serializer.DeserializeAsync(stream, Shape<Record[], Witness>());
			AssertRecords(value, result);
		}
	}

	[Test]
	public async Task Deserialize_SkipsBom()
	{
		Record value = new("bom", 1, []);
		byte[] utf8 = this.SerializeToUtf8(value, Shape<Record, Record>());
		byte[] withBom = [0xEF, 0xBB, 0xBF, .. utf8];
		using ChunkedStream stream = new(withBom, chunkSize: 2);
		Record? result = await this.Serializer.DeserializeAsync(stream, Shape<Record, Record>());
		AssertRecord(value, result);
	}

	[Test]
	public async Task Deserialize_TrailingData_Throws()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"name":"x","age":1,"values":[]} garbage""");
		using ChunkedStream stream = new(utf8, chunkSize: 3);
		await Assert.ThrowsAsync<FormatException>(async () =>
			await this.Serializer.DeserializeAsync(stream, Shape<Record, Record>()));
	}

	[Test]
	public async Task Deserialize_TrailingComment_Allowed_WhenSkip()
	{
		JsonSerializer serializer = new() { ReadCommentHandling = JsonCommentHandling.Skip };
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"name":"x","age":1,"values":[]} /* trailing */""");
		using ChunkedStream stream = new(utf8, chunkSize: 5);
		Record? result = await serializer.DeserializeAsync(stream, Shape<Record, Record>());
		AssertRecord(new Record("x", 1, []), result);
	}

	[Test]
	public async Task Deserialize_InnerComments_Chunked()
	{
		JsonSerializer serializer = new() { ReadCommentHandling = JsonCommentHandling.Skip };
		byte[] utf8 = Encoding.UTF8.GetBytes("""{/* a */"name":"x"/* b */,"age":5,"values":[1/*x*/,2]}""");
		using ChunkedStream stream = new(utf8, chunkSize: 1);
		Record? result = await serializer.DeserializeAsync(stream, Shape<Record, Record>());
		AssertRecord(new Record("x", 5, [1, 2]), result);
	}

	[Test]
	public async Task Deserialize_TrailingCommas_Chunked()
	{
		JsonSerializer serializer = new() { AllowTrailingCommas = true };
		byte[] utf8 = Encoding.UTF8.GetBytes("""{"name":"x","age":5,"values":[1,2,],}""");
		using ChunkedStream stream = new(utf8, chunkSize: 2);
		Record? result = await serializer.DeserializeAsync(stream, Shape<Record, Record>());
		AssertRecord(new Record("x", 5, [1, 2]), result);
	}

	[Test]
	public async Task Deserialize_Cancellation_Throws()
	{
		using CancellationTokenSource cts = new();
		cts.Cancel();
		byte[] utf8 = this.SerializeToUtf8(new Record("x", 1, []), Shape<Record, Record>());
		using ChunkedStream stream = new(utf8, chunkSize: 1);
		await Assert.ThrowsAsync<OperationCanceledException>(async () =>
			await this.Serializer.DeserializeAsync(stream, Shape<Record, Record>(), cts.Token));
	}

	[Test]
	public async Task Deserialize_Scalar_AtEof()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("12345");
		using ChunkedStream stream = new(utf8, chunkSize: 1);
		int result = await this.Serializer.DeserializeAsync(stream, Shape<int, Witness>());
		Assert.Equal(12345, result);
	}

	[Test]
	public async Task Serialize_LargeArray_FlushesIncrementally()
	{
		int[] value = new int[20000];
		for (int i = 0; i < value.Length; i++)
		{
			value[i] = i;
		}

		JsonSerializer serializer = new()
		{
			StartingContext = new SerializationContext { UnflushedBytesThreshold = 256 },
		};

		CountingStream stream = new();
		await serializer.SerializeAsync(stream, value, Shape<int[], Witness>());

		Assert.True(stream.WriteCount > 1, $"Expected multiple incremental writes, got {stream.WriteCount}.");

		using ChunkedStream input = new(stream.ToArray(), chunkSize: 128);
		int[]? result = await serializer.DeserializeAsync(input, Shape<int[], Witness>());
		Assert.Equal(value, result);
	}

	[Test]
	public async Task RoundTrip_LargeDictionary_Chunked()
	{
		Dictionary<string, int> value = new();
		for (int i = 0; i < 500; i++)
		{
			value[$"key{i}"] = i;
		}

		byte[] utf8 = this.SerializeToUtf8(value, Shape<Dictionary<string, int>, Witness>());
		using ChunkedStream stream = new(utf8, chunkSize: 7);
		Dictionary<string, int>? result = await this.Serializer.DeserializeAsync(stream, Shape<Dictionary<string, int>, Witness>());
		Assert.NotNull(result);
		Assert.Equal(value.Count, result!.Count);
		foreach (KeyValuePair<string, int> entry in value)
		{
			Assert.Equal(entry.Value, result[entry.Key]);
		}
	}

	[Test]
	public async Task RoundTrip_NestedObjectWithArray_OverPipe()
	{
		Record value = new("nested", 99, [5, 6, 7, 8]);
		Pipe pipe = new();
		await this.Serializer.SerializeAsync(pipe.Writer, value, Shape<Record, Record>());
		await pipe.Writer.CompleteAsync();
		Record? result = await this.Serializer.DeserializeAsync(pipe.Reader, Shape<Record, Record>());
		AssertRecord(value, result);
	}

	[Test]
	public async Task CustomAsyncConverter_IsInvoked()
	{
		TrackingConverter converter = new();
		JsonSerializer serializer = new()
		{
			Converters = new ConverterCollection([converter]),
		};

		Tracked value = new("hello");
		Pipe pipe = new();
		await serializer.SerializeAsync(pipe.Writer, value, Shape<Tracked, Tracked>());
		await pipe.Writer.CompleteAsync();
		Tracked? result = await serializer.DeserializeAsync(pipe.Reader, Shape<Tracked, Tracked>());

		Assert.Equal("hello", result!.Text);
		Assert.True(converter.WriteAsyncCalls > 0);
		Assert.True(converter.ReadAsyncCalls > 0);
	}

	[Test]
	public async Task Reader_OwnershipGuard_Throws()
	{
		byte[] utf8 = Encoding.UTF8.GetBytes("[1,2,3]");
		Pipe pipe = new();
		await pipe.Writer.WriteAsync(utf8);
		await pipe.Writer.CompleteAsync();
		using JsonAsyncReader reader = new(pipe.Reader) { CancellationToken = default };
		SerializationContext context = default;
		await reader.BufferNextValueAsync(context);
		JsonReader sync = reader.CreateBufferedReader();
		Assert.Throws<InvalidOperationException>(() => reader.CreateBufferedReader());
		reader.ReturnReader(ref sync);
	}

	[Test]
	public void Writer_OwnershipGuard_Throws()
	{
		Pipe pipe = new();
		JsonAsyncWriter writer = new(pipe.Writer);
		JsonWriter sync = writer.CreateWriter();
		Assert.Throws<InvalidOperationException>(() => writer.CreateWriter());
		writer.ReturnWriter(ref sync);
	}

	private static void AssertRecord(Record expected, Record? actual)
	{
		Assert.NotNull(actual);
		Assert.Equal(expected.Name, actual!.Name);
		Assert.Equal(expected.Age, actual.Age);
		Assert.Equal(expected.Values, actual.Values);
	}

	private static void AssertRecords(Record[] expected, Record[]? actual)
	{
		Assert.NotNull(actual);
		Assert.Equal(expected.Length, actual!.Length);
		for (int i = 0; i < expected.Length; i++)
		{
			AssertRecord(expected[i], actual[i]);
		}
	}

	private static ITypeShape<T> Shape<T, TProvider>()
#if NET
		where TProvider : IShapeable<T> => TProvider.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
#endif

	private byte[] SerializeToUtf8<T>(T value, ITypeShape<T> shape)
	{
		string json = this.Serializer.Serialize(value, shape);
		return Encoding.UTF8.GetBytes(json);
	}

	[GenerateShape]
	internal partial record Record(string Name, int Age, int[] Values);

	[GenerateShape]
	internal partial record Tracked(string Text);

	[GenerateShapeFor<int>]
	[GenerateShapeFor<Record[]>]
	[GenerateShapeFor<int[]>]
	[GenerateShapeFor<Dictionary<string, int>>]
	internal partial class Witness;

	internal sealed class TrackingConverter : JsonConverter<Tracked>
	{
		public override bool PreferAsyncSerialization => true;

		internal int WriteAsyncCalls { get; private set; }

		internal int ReadAsyncCalls { get; private set; }

		public override void Write(ref JsonWriter writer, Tracked? value, SerializationContext context)
			=> writer.WriteStringValue(value?.Text);

		public override Tracked? Read(ref JsonReader reader, SerializationContext context)
		{
			string? text = reader.ReadString();
			return text is null ? null : new Tracked(text);
		}

		public override async ValueTask WriteAsync(JsonAsyncWriter writer, Tracked? value, SerializationContext context)
		{
			this.WriteAsyncCalls++;
			JsonWriter syncWriter = writer.CreateWriter();
			syncWriter.WriteStringValue(value?.Text);
			writer.ReturnWriter(ref syncWriter);
			await writer.FlushIfAppropriateAsync(context);
		}

		public override async ValueTask<Tracked?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
		{
			this.ReadAsyncCalls++;
			await reader.BufferNextValueAsync(context);
			JsonReader syncReader = reader.CreateBufferedReader();
			string? text = syncReader.ReadString();
			reader.ReturnReader(ref syncReader);
			return text is null ? null : new Tracked(text);
		}
	}

	private sealed class CountingStream : Stream
	{
		private readonly MemoryStream inner = new();

		public override bool CanRead => false;

		public override bool CanSeek => false;

		public override bool CanWrite => true;

		public override long Length => this.inner.Length;

		public override long Position
		{
			get => this.inner.Position;
			set => throw new NotSupportedException();
		}

		internal int WriteCount { get; private set; }

		public byte[] ToArray() => this.inner.ToArray();

		public override void Write(byte[] buffer, int offset, int count)
		{
			this.WriteCount++;
			this.inner.Write(buffer, offset, count);
		}

#if NET
		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
		{
			this.WriteCount++;
			this.inner.Write(buffer.Span);
			return default;
		}
#endif

		public override void Flush() => this.inner.Flush();

		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();
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

#if NET
		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await Task.Yield();
			int available = this.data.Length - this.position;
			if (available <= 0)
			{
				return 0;
			}

			int toCopy = Math.Min(Math.Min(buffer.Length, this.chunkSize), available);
			this.data.AsSpan(this.position, toCopy).CopyTo(buffer.Span);
			this.position += toCopy;
			return toCopy;
		}
#endif

		public override void Flush()
		{
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}
}
