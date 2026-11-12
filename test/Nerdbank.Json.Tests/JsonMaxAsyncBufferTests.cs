// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Pipelines;
using System.Text;

[GenerateShapeFor<string>]
public partial class JsonMaxAsyncBufferTests : TestBase
{
	[Test]
	public void Default_IsOneMegabyte()
	{
		Assert.Equal(1024 * 1024, new JsonSerializer().MaxAsyncBuffer);
	}

	[Test]
	public void Negative_Throws()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new JsonSerializer { MaxAsyncBuffer = -1 });
	}

	[Test]
	public async Task Cap_SelectsSynchronousReaderWhenTheValueFits()
	{
		byte[] utf8 = """{"text":"hello"}"""u8.ToArray();
		CountingConverter belowCap = new();
		JsonSerializer synchronous = new()
		{
			MaxAsyncBuffer = utf8.Length + 8,
			Converters = [belowCap],
		};

		TrackedValue? buffered = await synchronous.DeserializeAsync(new FragmentedPipeReader(utf8, chunkSize: 1), Shape<TrackedValue>());

		Assert.Equal("hello", buffered!.Text);
		Assert.Equal(0, belowCap.AsyncReads);

		CountingConverter aboveCap = new();
		JsonSerializer incremental = new()
		{
			MaxAsyncBuffer = 1,
			Converters = [aboveCap],
		};

		TrackedValue? streamed = await incremental.DeserializeAsync(new FragmentedPipeReader(utf8, chunkSize: 1), Shape<TrackedValue>());

		Assert.Equal("hello", streamed!.Text);
		Assert.Equal(1, aboveCap.AsyncReads);
	}

	[Test]
	public async Task Zero_AlwaysUsesIncrementalReader()
	{
		CountingConverter converter = new();
		JsonSerializer serializer = new()
		{
			MaxAsyncBuffer = 0,
			Converters = [converter],
		};
		byte[] utf8 = """{"text":"hello"}"""u8.ToArray();

		TrackedValue? value = await serializer.DeserializeAsync(PipeReader.Create(new ReadOnlySequence<byte>(utf8)), Shape<TrackedValue>());

		Assert.Equal("hello", value!.Text);
		Assert.Equal(1, converter.AsyncReads);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task ObjectAndJsonValue_PreserveTrailingBytes(bool forceIncremental)
	{
		JsonSerializer serializer = new() { MaxAsyncBuffer = forceIncremental ? 0 : 1024 * 1024 };
		byte[] utf8 = Encoding.UTF8.GetBytes("\"hello\"rest");

		PipeReader typedReader = PipeReader.Create(new ReadOnlySequence<byte>(utf8));
		Assert.Equal("hello", await serializer.DeserializeAsync(typedReader, Shape<string>()));
		ReadResult typedRemainder = await typedReader.ReadAsync();
		Assert.Equal("rest"u8.ToArray(), typedRemainder.Buffer.ToArray());
		typedReader.AdvanceTo(typedRemainder.Buffer.Start);

		PipeReader objectReader = PipeReader.Create(new ReadOnlySequence<byte>(utf8));
		Assert.Equal("hello", await serializer.DeserializeObjectAsync(objectReader, Shape<string>()));
		ReadResult objectRemainder = await objectReader.ReadAsync();
		Assert.Equal("rest"u8.ToArray(), objectRemainder.Buffer.ToArray());
		objectReader.AdvanceTo(objectRemainder.Buffer.Start);

		PipeReader jsonReader = PipeReader.Create(new ReadOnlySequence<byte>(utf8));
		JsonValue? jsonValue = await serializer.DeserializeJsonValueAsync(jsonReader);
		Assert.Equal("hello", Assert.IsType<JsonString>(jsonValue).Value);
		ReadResult jsonRemainder = await jsonReader.ReadAsync();
		Assert.Equal("rest"u8.ToArray(), jsonRemainder.Buffer.ToArray());
	}

	[Test]
	public async Task IncompletePipe_ReturnsFirstValueWithoutFillingTheCap()
	{
		Pipe pipe = new();
		await pipe.Writer.WriteAsync("\"hello\"rest"u8.ToArray());
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
		JsonSerializer serializer = new() { MaxAsyncBuffer = 1024 * 1024 };

		string? value = await serializer.DeserializeAsync(pipe.Reader, Shape<string>(), timeout.Token);

		Assert.Equal("hello", value);
		ReadResult remainder = await pipe.Reader.ReadAsync(timeout.Token);
		Assert.Equal("rest"u8.ToArray(), remainder.Buffer.ToArray());
		await pipe.Writer.CompleteAsync();
		await pipe.Reader.CompleteAsync();
	}

	private static ITypeShape<T> Shape<T>() => PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<T>()!;

	[GenerateShape]
	internal partial record TrackedValue(string Text);

	private sealed class CountingConverter : JsonConverter<TrackedValue>
	{
		public override bool PreferAsyncSerialization => true;

		internal int AsyncReads { get; private set; }

		public override void Write(ref JsonWriter writer, in TrackedValue? value, SerializationContext context)
			=> writer.Write(value!.Text);

		public override TrackedValue? Read(ref JsonReader reader, SerializationContext context)
		{
			reader.ReadStartObject();
			Assert.Equal("text", reader.ReadRequiredString());
			reader.ReadNameSeparator();
			string text = reader.ReadRequiredString();
			Assert.True(reader.TryReadEndObject());
			return new TrackedValue(text);
		}

		public override ValueTask<TrackedValue?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
		{
			this.AsyncReads++;
			return base.ReadAsync(reader, context);
		}
	}

	/// <summary>
	/// A pipe reader that releases a preloaded buffer a few bytes at a time and completes only at the end.
	/// </summary>
	private sealed class FragmentedPipeReader : PipeReader
	{
		private readonly ReadOnlySequence<byte> buffer;
		private readonly int chunkSize;
		private SequencePosition consumed;
		private SequencePosition examined;
		private bool expectAdvance;

		internal FragmentedPipeReader(byte[] data, int chunkSize)
		{
			this.buffer = new ReadOnlySequence<byte>(data);
			this.chunkSize = chunkSize;
			this.consumed = this.buffer.Start;
			this.examined = this.buffer.Start;
		}

		public override void AdvanceTo(SequencePosition consumed) => this.AdvanceTo(consumed, consumed);

		public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
		{
			this.consumed = consumed;
			this.examined = examined;
			this.expectAdvance = false;
		}

		public override void CancelPendingRead() => throw new NotSupportedException();

		public override void Complete(Exception? exception = null)
		{
		}

		public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
		{
			if (this.expectAdvance)
			{
				throw new InvalidOperationException("AdvanceTo was not called.");
			}

			long examinedIndex = this.buffer.Slice(0, this.examined).Length;
			long endIndex = Math.Min(examinedIndex + this.chunkSize, this.buffer.Length);
			SequencePosition chunkEnd = this.buffer.GetPosition(endIndex);
			ReadOnlySequence<byte> chunk = this.buffer.Slice(this.consumed, chunkEnd);
			this.expectAdvance = true;
			return new ValueTask<ReadResult>(new ReadResult(chunk, isCanceled: false, endIndex == this.buffer.Length));
		}

		public override bool TryRead(out ReadResult result) => throw new NotSupportedException();
	}
}
