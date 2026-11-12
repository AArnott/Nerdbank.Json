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
	public async Task OpenPipe_CompleteValueBeyondCap_UsesIncrementalReader()
	{
		byte[] utf8 = """{"text":"hello"}"""u8.ToArray();
		CountingConverter converter = new();
		JsonSerializer serializer = new()
		{
			MaxAsyncBuffer = 1,
			Converters = [converter],
		};
		Pipe pipe = new();
		await pipe.Writer.WriteAsync(utf8);
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

		TrackedValue? value = await serializer.DeserializeAsync(pipe.Reader, Shape<TrackedValue>(), timeout.Token);

		Assert.Equal("hello", value!.Text);
		Assert.Equal(1, converter.AsyncReads);
		await pipe.Writer.CompleteAsync();
		await pipe.Reader.CompleteAsync();
	}

	[Test]
	public async Task SpeculativeCancel_CompletedValue_DoesNotWaitForMoreData()
	{
		byte[] utf8 = """{"text":"hello"}"""u8.ToArray();
		CancelRacePipeReader reader = new(utf8);
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));

		TrackedValue? value = await new JsonSerializer().DeserializeAsync(reader, Shape<TrackedValue>(), timeout.Token);

		Assert.Equal("hello", value!.Text);
		Assert.Equal(3, reader.Reads);
	}

	[Test]
	public async Task OpenPipe_ReferenceEnvelope_UsesConverterDepth()
	{
		JsonSerializer serializer = new()
		{
			PreserveReferences = ReferencePreservationMode.AllowCycles,
			StartingContext = new SerializationContext { MaxDepth = 1 },
		};
		string json = serializer.Serialize(new NamedValue { Name = "a" }, Shape<NamedValue>());
		Pipe pipe = new();
		await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(json));
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

		NamedValue? value = await serializer.DeserializeAsync(pipe.Reader, Shape<NamedValue>(), timeout.Token);

		Assert.Equal("a", value!.Name);
		await pipe.Writer.CompleteAsync();
		await pipe.Reader.CompleteAsync();
	}

	[Test]
	public async Task AllowCycles_AsyncOnlyChild_IsNotRejectedAsDuplicateReference()
	{
		AsyncOnlyIntConverter converter = new();
		JsonSerializer serializer = new()
		{
			PreserveReferences = ReferencePreservationMode.AllowCycles,
			Converters = [converter],
		};
		byte[] utf8 = """{"$id":1,"$value":{"child":4}}"""u8.ToArray();
		Pipe pipe = new();
		await pipe.Writer.WriteAsync(utf8);
		await pipe.Writer.CompleteAsync();

		Parent? value = await serializer.DeserializeAsync(pipe.Reader, Shape<Parent>());
		object? untyped = await serializer.DeserializeObjectAsync(PipeReader.Create(new ReadOnlySequence<byte>(utf8)), Shape<Parent>());

		Assert.Equal(4, value!.Child);
		Assert.Equal(4, Assert.IsType<Parent>(untyped).Child);
		Assert.Equal(0, converter.SynchronousReads);
		Assert.Equal(2, converter.AsynchronousReads);
	}

	[Test]
	public async Task ExaminedPrefix_OverCap_DoesNotRewindCursor()
	{
		byte[] utf8 = """{"text":"hello"}"""u8.ToArray();
		CountingConverter converter = new();
		JsonSerializer serializer = new()
		{
			MaxAsyncBuffer = 8,
			Converters = [converter],
		};
		FragmentedPipeReader reader = new(utf8, chunkSize: 4);

		TrackedValue? value = await serializer.DeserializeAsync(reader, Shape<TrackedValue>());

		Assert.Equal("hello", value!.Text);
		Assert.Equal(1, converter.AsyncReads);
		Assert.Equal(utf8.Length, reader.ConsumedLength);
	}

	[Test]
	public async Task SpeculativeCancel_IncompleteValue_DoesNotWaitForMoreInput()
	{
		byte[] utf8 = """{"text":"hello"}"""u8.ToArray();
		CancelRacePipeReader reader = new(utf8);
		CountingConverter converter = new();
		JsonSerializer serializer = new()
		{
			MaxAsyncBuffer = 4,
			Converters = [converter],
		};
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));

		TrackedValue? value = await serializer.DeserializeAsync(reader, Shape<TrackedValue>(), timeout.Token);

		Assert.Equal("hello", value!.Text);
		Assert.Equal(1, converter.AsyncReads);
		Assert.True(reader.Reads < 4);
	}

	[Test]
	public async Task OverCapRacedBuffer_TrailingValue_StaysReadable()
	{
		byte[] utf8 = """{"text":"one"}{"text":"two"}"""u8.ToArray();
		TrailingValueRacePipeReader reader = new(utf8);
		CountingConverter converter = new();
		JsonSerializer serializer = new() { MaxAsyncBuffer = 4, Converters = [converter] };
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));

		TrackedValue? first = await serializer.DeserializeAsync(reader, Shape<TrackedValue>(), timeout.Token);
		TrackedValue? second = await serializer.DeserializeAsync(reader, Shape<TrackedValue>(), timeout.Token);

		Assert.Equal("one", first!.Text);
		Assert.Equal("two", second!.Text);
		Assert.True(converter.AsyncReads >= 1);
	}

	[Test]
	public async Task CanceledFollowUp_DeliversBytesThatCompleteTheValue()
	{
		byte[] utf8 = """{"text":"hello"}"""u8.ToArray();
		CompletingCancelPipeReader reader = new(utf8);
		CountingConverter converter = new();
		JsonSerializer serializer = new() { MaxAsyncBuffer = 8, Converters = [converter] };
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));

		TrackedValue? value = await serializer.DeserializeAsync(reader, Shape<TrackedValue>(), timeout.Token);

		Assert.Equal("hello", value!.Text);
		Assert.Equal(1, converter.AsyncReads);
	}

	[Test]
	public async Task CanceledIncrementalRead_ReleasesThePipe()
	{
		byte[] utf8 = """{"text":"hello"}"""u8.ToArray();
		CancelAfterPrefixPipeReader reader = new(utf8);
		JsonSerializer serializer = new() { MaxAsyncBuffer = 8 };
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));

		JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(
			() => serializer.DeserializeAsync(reader, Shape<TrackedValue>(), timeout.Token).AsTask());
		Assert.IsType<OperationCanceledException>(exception.InnerException);

		ReadResult remaining = await reader.ReadAsync(timeout.Token);
		Assert.True(remaining.Buffer.Length > 0);
		reader.AdvanceTo(remaining.Buffer.Start, remaining.Buffer.End);
	}

	[Test]
	public async Task OverCapPipe_ReleasesConsumedBytes()
	{
		JsonSerializer serializer = new() { MaxAsyncBuffer = 1 };
		Pipe pipe = new();
		await pipe.Writer.WriteAsync("\"hello\"rest"u8.ToArray());
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));

		string? value = await serializer.DeserializeAsync(pipe.Reader, Shape<string>(), timeout.Token);

		Assert.Equal("hello", value);
		ReadResult remainder = await pipe.Reader.ReadAsync(timeout.Token);
		Assert.Equal("rest"u8.ToArray(), remainder.Buffer.ToArray());
		await pipe.Writer.CompleteAsync();
		await pipe.Reader.CompleteAsync();
	}

	[Test]
	public async Task SyncReadFailure_PreservesExaminedPosition()
	{
		byte[] utf8 = """{"text":"hello"}"""u8.ToArray();
		JsonSerializer serializer = new()
		{
			MaxAsyncBuffer = utf8.Length + 8,
			Converters = [new ThrowingTrackedConverter()],
		};

		JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(
			() => serializer.DeserializeAsync(new FragmentedPipeReader(utf8, chunkSize: 4), Shape<TrackedValue>()).AsTask());

		Assert.IsType<NotSupportedException>(exception.InnerException);
	}

	[Test]
	public async Task CanceledCompleteValue_AllowCycles_DoesNotReportCancellation()
	{
		byte[] utf8 = """{"$id":1,"$value":{"text":"hello"}}"""u8.ToArray();
		JsonSerializer serializer = new() { PreserveReferences = ReferencePreservationMode.AllowCycles };
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));

		TrackedValue? value = await serializer.DeserializeAsync(new CanceledPayloadPipeReader(utf8), Shape<TrackedValue>(), timeout.Token);

		Assert.Equal("hello", value!.Text);
	}

	[Test]
	public async Task IncrementalParse_DrainsSpeculativeCancellation()
	{
		byte[] utf8 = """{"text":"hello"}"""u8.ToArray();
		CancelRacePipeReader reader = new(utf8);
		JsonSerializer serializer = new() { Converters = [new ParsingTrackedConverter()] };
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));

		TrackedValue? value = await serializer.DeserializeAsync(reader, Shape<TrackedValue>(), timeout.Token);

		Assert.Equal("hello", value!.Text);
		Assert.Equal(3, reader.Reads);
	}

	[Test]
	public async Task ConverterThrowsBeforeRead_ReleasesOutstandingBuffer()
	{
		byte[] utf8 = """{"text":"hello"}"""u8.ToArray();
		TrailingValueRacePipeReader reader = new(utf8);
		JsonSerializer failing = new() { Converters = [new ThrowBeforeReadConverter()] };
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));

		JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(
			() => failing.DeserializeAsync(reader, Shape<TrackedValue>(), timeout.Token).AsTask());

		Assert.IsType<NotSupportedException>(exception.InnerException);

		TrackedValue? value = await new JsonSerializer().DeserializeAsync(reader, Shape<TrackedValue>(), timeout.Token);
		Assert.Equal("hello", value!.Text);
	}

	[Test]
	public async Task ObjectSyncReadFailure_RacedBuffer_RemainsReadable()
	{
		byte[] utf8 = """{"text":"hello"}"""u8.ToArray();
		TrailingValueRacePipeReader reader = new(utf8);
		JsonSerializer failing = new() { Converters = [new ThrowBeforeReadConverter()] };
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));

		JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(
			() => failing.DeserializeObjectAsync(reader, Shape<TrackedValue>(), timeout.Token).AsTask());

		Assert.IsType<NotSupportedException>(exception.InnerException);

		object? value = await new JsonSerializer().DeserializeObjectAsync(reader, Shape<TrackedValue>(), timeout.Token);
		Assert.Equal("hello", Assert.IsType<TrackedValue>(value).Text);
	}

	[Test]
	public async Task SpeculativeCancel_TrailingBufferedValue_StaysReadable()
	{
		byte[] utf8 = """{"text":"one"}{"text":"two"}"""u8.ToArray();
		TrailingValueRacePipeReader reader = new(utf8);
		JsonSerializer serializer = new() { Converters = [new ParsingTrackedConverter()] };
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));

		TrackedValue? first = await serializer.DeserializeAsync(reader, Shape<TrackedValue>(), timeout.Token);
		TrackedValue? second = await serializer.DeserializeAsync(reader, Shape<TrackedValue>(), timeout.Token);

		Assert.Equal("one", first!.Text);
		Assert.Equal("two", second!.Text);
	}

	[Test]
	public async Task NestedConverter_WithoutPreferAsync_UsesSynchronousRead()
	{
		CountingIntConverter converter = new();
		JsonSerializer serializer = new() { Converters = [converter] };
		byte[] utf8 = """{"value":4}"""u8.ToArray();

		NestedValueHolder? value = await serializer.DeserializeAsync(PipeReader.Create(new ReadOnlySequence<byte>(utf8)), Shape<NestedValueHolder>());

		Assert.Equal(4, value!.Value);
		Assert.Equal(1, converter.SynchronousReads);
		Assert.Equal(0, converter.AsynchronousReads);
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

	[GenerateShape]
	internal partial class NamedValue
	{
		public string? Name { get; set; }
	}

	[GenerateShape]
	internal partial class Parent
	{
		public int Child { get; set; }
	}

	[GenerateShape]
	internal partial class NestedValueHolder
	{
		public int Value { get; set; }
	}

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

		internal long ConsumedLength => this.buffer.Slice(0, this.consumed).Length;

		public override void AdvanceTo(SequencePosition consumed) => this.AdvanceTo(consumed, consumed);

		public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
		{
			long consumedIndex = this.buffer.Slice(0, consumed).Length;
			long examinedIndex = this.buffer.Slice(0, examined).Length;
			long previousExamined = this.buffer.Slice(0, this.examined).Length;
			if (examinedIndex < previousExamined)
			{
				throw new InvalidOperationException("The examined position cannot be earlier than the previously examined position.");
			}

			if (consumedIndex > examinedIndex)
			{
				throw new InvalidOperationException("The consumed position cannot be later than the examined position.");
			}

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

	private sealed class AsyncOnlyIntConverter : JsonConverter<int>
	{
		public override bool PreferAsyncSerialization => true;

		internal int SynchronousReads { get; private set; }

		internal int AsynchronousReads { get; private set; }

		public override int Read(ref JsonReader reader, SerializationContext context)
		{
			this.SynchronousReads++;
			throw new NotSupportedException();
		}

		public override void Write(ref JsonWriter writer, in int value, SerializationContext context) => writer.Write(value);

		public override async ValueTask<int> ReadAsync(JsonAsyncReader reader, SerializationContext context)
		{
			this.AsynchronousReads++;
			return int.Parse(await reader.ReadNumberTokenAsync(context), System.Globalization.CultureInfo.InvariantCulture);
		}
	}

	private sealed class ThrowingTrackedConverter : JsonConverter<TrackedValue>
	{
		public override void Write(ref JsonWriter writer, in TrackedValue? value, SerializationContext context)
			=> writer.Write(value!.Text);

		public override TrackedValue? Read(ref JsonReader reader, SerializationContext context)
			=> throw new NotSupportedException();
	}

	private sealed class ParsingTrackedConverter : JsonConverter<TrackedValue>
	{
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

		public override async ValueTask<TrackedValue?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
		{
			await reader.BufferNextValueAsync(context);
			JsonReader sync = reader.CreateBufferedReader();
			TrackedValue? value = this.Read(ref sync, context);
			reader.ReturnReader(ref sync);
			return value;
		}
	}

	private sealed class CountingIntConverter : JsonConverter<int>
	{
		internal int SynchronousReads { get; private set; }

		internal int AsynchronousReads { get; private set; }

		public override int Read(ref JsonReader reader, SerializationContext context)
		{
			this.SynchronousReads++;
			return int.Parse(reader.ReadNumberToken(), System.Globalization.CultureInfo.InvariantCulture);
		}

		public override void Write(ref JsonWriter writer, in int value, SerializationContext context) => writer.Write(value);

		public override async ValueTask<int> ReadAsync(JsonAsyncReader reader, SerializationContext context)
		{
			this.AsynchronousReads++;
			return int.Parse(await reader.ReadNumberTokenAsync(context), System.Globalization.CultureInfo.InvariantCulture);
		}
	}

	private sealed class ThrowBeforeReadConverter : JsonConverter<TrackedValue>
	{
		public override void Write(ref JsonWriter writer, in TrackedValue? value, SerializationContext context)
			=> writer.Write(value!.Text);

		public override TrackedValue? Read(ref JsonReader reader, SerializationContext context)
			=> throw new NotSupportedException();

		public override ValueTask<TrackedValue?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
			=> throw new NotSupportedException("before read");
	}

	/// <summary>
	/// Completes an in-flight read with a full value when that read is canceled, then reports the next read as canceled.
	/// A further read waits until the caller's cancellation token fires.
	/// </summary>
	private sealed class CancelRacePipeReader : PipeReader
	{
		private readonly byte[] payload;
		private int reads;
		private bool expectAdvance;
		private TaskCompletionSource<ReadResult>? secondRead;

		internal CancelRacePipeReader(byte[] payload) => this.payload = payload;

		internal int Reads => this.reads;

		public override void AdvanceTo(SequencePosition consumed) => this.AdvanceTo(consumed, consumed);

		public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) => this.expectAdvance = false;

		public override void CancelPendingRead()
			=> this.secondRead?.TrySetResult(new ReadResult(new ReadOnlySequence<byte>(this.payload), isCanceled: false, isCompleted: false));

		public override void Complete(Exception? exception = null)
		{
		}

		public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
		{
			if (this.expectAdvance)
			{
				throw new InvalidOperationException("AdvanceTo was not called.");
			}

			this.reads++;
			this.expectAdvance = true;
			if (this.reads == 1)
			{
				return new ValueTask<ReadResult>(new ReadResult(new ReadOnlySequence<byte>(this.payload, 0, 1), isCanceled: false, isCompleted: false));
			}

			if (this.reads == 2)
			{
				this.secondRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
				return new ValueTask<ReadResult>(this.secondRead.Task);
			}

			if (this.reads == 3)
			{
				return new ValueTask<ReadResult>(new ReadResult(new ReadOnlySequence<byte>(this.payload), isCanceled: true, isCompleted: false));
			}

			return new ValueTask<ReadResult>(WaitAsync(cancellationToken));

			static async Task<ReadResult> WaitAsync(CancellationToken cancellationToken)
			{
				await Task.Delay(Timeout.Infinite, cancellationToken);
				return new ReadResult(ReadOnlySequence<byte>.Empty, isCanceled: true, isCompleted: false);
			}
		}

		public override bool TryRead(out ReadResult result) => throw new NotSupportedException();
	}

	/// <summary>
	/// Returns one byte, then completes the canceled follow-up with the full payload marked canceled.
	/// </summary>
	private sealed class CanceledPayloadPipeReader : PipeReader
	{
		private readonly byte[] payload;
		private int reads;
		private bool expectAdvance;
		private TaskCompletionSource<ReadResult>? secondRead;

		internal CanceledPayloadPipeReader(byte[] payload) => this.payload = payload;

		public override void AdvanceTo(SequencePosition consumed) => this.AdvanceTo(consumed, consumed);

		public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) => this.expectAdvance = false;

		public override void CancelPendingRead()
			=> this.secondRead?.TrySetResult(new ReadResult(new ReadOnlySequence<byte>(this.payload), isCanceled: true, isCompleted: false));

		public override void Complete(Exception? exception = null)
		{
		}

		public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
		{
			if (this.expectAdvance)
			{
				throw new InvalidOperationException("AdvanceTo was not called.");
			}

			this.reads++;
			this.expectAdvance = true;
			if (this.reads == 1)
			{
				return new ValueTask<ReadResult>(new ReadResult(new ReadOnlySequence<byte>(this.payload, 0, 1), isCanceled: false, isCompleted: false));
			}

			this.secondRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
			return new ValueTask<ReadResult>(this.secondRead.Task);
		}

		public override bool TryRead(out ReadResult result) => throw new NotSupportedException();
	}

	/// <summary>
	/// Serves one byte, then a canceled in-flight read that already holds every byte.
	/// The following read is the one-shot cancellation and contains only the bytes that have not been consumed.
	/// A later read blocks once every buffered byte has been examined.
	/// </summary>
	private sealed class TrailingValueRacePipeReader : PipeReader
	{
		private readonly ReadOnlySequence<byte> buffer;
		private SequencePosition consumed;
		private SequencePosition examined;
		private int reads;
		private bool expectAdvance;
		private TaskCompletionSource<ReadResult>? secondRead;

		internal TrailingValueRacePipeReader(byte[] payload)
		{
			this.buffer = new ReadOnlySequence<byte>(payload);
			this.consumed = this.buffer.Start;
			this.examined = this.buffer.Start;
		}

		public override void AdvanceTo(SequencePosition consumed) => this.AdvanceTo(consumed, consumed);

		public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
		{
			long consumedIndex = this.buffer.Slice(0, consumed).Length;
			long examinedIndex = this.buffer.Slice(0, examined).Length;
			long previousExamined = this.buffer.Slice(0, this.examined).Length;
			if (examinedIndex < previousExamined)
			{
				throw new InvalidOperationException("The examined position cannot be earlier than the previously examined position.");
			}

			if (consumedIndex > examinedIndex)
			{
				throw new InvalidOperationException("The consumed position cannot be later than the examined position.");
			}

			this.consumed = consumed;
			this.examined = examined;
			this.expectAdvance = false;
		}

		public override void CancelPendingRead()
			=> this.secondRead?.TrySetResult(this.CreateResult(isCanceled: false));

		public override void Complete(Exception? exception = null)
		{
		}

		public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
		{
			if (this.expectAdvance)
			{
				throw new InvalidOperationException("AdvanceTo was not called.");
			}

			this.reads++;
			if (this.reads == 2)
			{
				this.secondRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
				this.expectAdvance = true;
				return new ValueTask<ReadResult>(this.secondRead.Task);
			}

			long examinedIndex = this.buffer.Slice(0, this.examined).Length;
			if (this.reads > 3 && examinedIndex >= this.buffer.Length)
			{
				return new ValueTask<ReadResult>(WaitAsync(cancellationToken));
			}

			return new ValueTask<ReadResult>(this.CreateResult(isCanceled: this.reads == 3));

			static async Task<ReadResult> WaitAsync(CancellationToken cancellationToken)
			{
				await Task.Delay(Timeout.Infinite, cancellationToken);
				return new ReadResult(ReadOnlySequence<byte>.Empty, isCanceled: true, isCompleted: false);
			}
		}

		public override bool TryRead(out ReadResult result) => throw new NotSupportedException();

		private ReadResult CreateResult(bool isCanceled)
		{
			this.secondRead = null;
			this.expectAdvance = true;
			long endIndex = this.reads == 1
				? Math.Min(this.buffer.Slice(0, this.consumed).Length + 1, this.buffer.Length)
				: this.buffer.Length;
			ReadOnlySequence<byte> slice = this.buffer.Slice(this.consumed, this.buffer.GetPosition(endIndex));
			return new ReadResult(slice, isCanceled, isCompleted: false);
		}
	}

	/// <summary>
	/// Returns a one-byte prefix, an incomplete raced prefix, then a canceled follow-up that still lacks the
	/// closing bytes. The next read is canceled and contains the rest of the value.
	/// </summary>
	private sealed class CompletingCancelPipeReader : PipeReader
	{
		private readonly ReadOnlySequence<byte> buffer;
		private SequencePosition consumed;
		private int reads;
		private bool expectAdvance;
		private TaskCompletionSource<ReadResult>? pending;

		internal CompletingCancelPipeReader(byte[] payload)
		{
			this.buffer = new ReadOnlySequence<byte>(payload);
			this.consumed = this.buffer.Start;
		}

		public override void AdvanceTo(SequencePosition consumed) => this.AdvanceTo(consumed, consumed);

		public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
		{
			long consumedIndex = this.buffer.Slice(0, consumed).Length;
			long examinedIndex = this.buffer.Slice(0, examined).Length;
			if (consumedIndex > examinedIndex)
			{
				throw new InvalidOperationException("The consumed position cannot be later than the examined position.");
			}

			this.consumed = consumed;
			this.expectAdvance = false;
		}

		public override void CancelPendingRead()
		{
			int read = this.reads;
			this.pending?.TrySetResult(this.Slice(read == 2 || read == 3 ? 5 : this.buffer.Length, isCanceled: read != 2));
		}

		public override void Complete(Exception? exception = null)
		{
		}

		public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
		{
			if (this.expectAdvance)
			{
				throw new InvalidOperationException("AdvanceTo was not called.");
			}

			this.reads++;
			this.expectAdvance = true;
			if (this.reads == 1)
			{
				return new ValueTask<ReadResult>(this.Slice(1, isCanceled: false));
			}

			if (this.reads is 2 or 3)
			{
				this.pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
				return new ValueTask<ReadResult>(this.pending.Task);
			}

			if (this.reads == 4)
			{
				return new ValueTask<ReadResult>(this.Slice(this.buffer.Length, isCanceled: true));
			}

			return new ValueTask<ReadResult>(WaitAsync(cancellationToken));

			static async Task<ReadResult> WaitAsync(CancellationToken cancellationToken)
			{
				await Task.Delay(Timeout.Infinite, cancellationToken);
				return new ReadResult(ReadOnlySequence<byte>.Empty, isCanceled: true, isCompleted: false);
			}
		}

		public override bool TryRead(out ReadResult result) => throw new NotSupportedException();

		private ReadResult Slice(long endIndex, bool isCanceled)
		{
			this.pending = null;
			long consumedIndex = this.buffer.Slice(0, this.consumed).Length;
			if (endIndex < consumedIndex)
			{
				endIndex = consumedIndex;
			}

			ReadOnlySequence<byte> slice = this.buffer.Slice(this.consumed, this.buffer.GetPosition(endIndex));
			return new ReadResult(slice, isCanceled, isCompleted: false);
		}
	}

	/// <summary>
	/// Hands the incremental reader an incomplete prefix, then cancels the read that would have completed the value.
	/// </summary>
	private sealed class CancelAfterPrefixPipeReader : PipeReader
	{
		private readonly ReadOnlySequence<byte> buffer;
		private SequencePosition consumed;
		private SequencePosition examined;
		private int reads;
		private bool expectAdvance;
		private TaskCompletionSource<ReadResult>? pending;

		internal CancelAfterPrefixPipeReader(byte[] payload)
		{
			this.buffer = new ReadOnlySequence<byte>(payload);
			this.consumed = this.buffer.Start;
			this.examined = this.buffer.Start;
		}

		public override void AdvanceTo(SequencePosition consumed) => this.AdvanceTo(consumed, consumed);

		public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
		{
			long consumedIndex = this.buffer.Slice(0, consumed).Length;
			long examinedIndex = this.buffer.Slice(0, examined).Length;
			long previousExamined = this.buffer.Slice(0, this.examined).Length;
			if (examinedIndex < previousExamined)
			{
				throw new InvalidOperationException("The examined position cannot be earlier than the previously examined position.");
			}

			if (consumedIndex > examinedIndex)
			{
				throw new InvalidOperationException("The consumed position cannot be later than the examined position.");
			}

			this.consumed = consumed;
			this.examined = examined;
			this.expectAdvance = false;
		}

		public override void CancelPendingRead()
			=> this.pending?.TrySetResult(this.Slice(5, isCanceled: false));

		public override void Complete(Exception? exception = null)
		{
		}

		public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
		{
			if (this.expectAdvance)
			{
				throw new InvalidOperationException("AdvanceTo was not called.");
			}

			this.reads++;
			this.expectAdvance = true;
			if (this.reads == 1)
			{
				return new ValueTask<ReadResult>(this.Slice(1, isCanceled: false));
			}

			if (this.reads == 2)
			{
				this.pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
				return new ValueTask<ReadResult>(this.pending.Task);
			}

			if (this.reads == 3)
			{
				return new ValueTask<ReadResult>(this.Slice(5, isCanceled: false));
			}

			long examinedIndex = this.buffer.Slice(0, this.examined).Length;
			if (examinedIndex >= this.buffer.Length)
			{
				return new ValueTask<ReadResult>(WaitAsync(cancellationToken));
			}

			return new ValueTask<ReadResult>(this.Slice(this.buffer.Length, isCanceled: true));

			static async Task<ReadResult> WaitAsync(CancellationToken cancellationToken)
			{
				await Task.Delay(Timeout.Infinite, cancellationToken);
				return new ReadResult(ReadOnlySequence<byte>.Empty, isCanceled: true, isCompleted: false);
			}
		}

		public override bool TryRead(out ReadResult result) => throw new NotSupportedException();

		private ReadResult Slice(long endIndex, bool isCanceled)
		{
			this.pending = null;
			long consumedIndex = this.buffer.Slice(0, this.consumed).Length;
			if (endIndex < consumedIndex)
			{
				endIndex = consumedIndex;
			}

			ReadOnlySequence<byte> slice = this.buffer.Slice(this.consumed, this.buffer.GetPosition(endIndex));
			return new ReadResult(slice, isCanceled, isCompleted: false);
		}
	}
}
