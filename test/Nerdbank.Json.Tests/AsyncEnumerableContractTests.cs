// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using PolyType.Abstractions;

[GenerateShapeFor<IAsyncEnumerable<int>>]
[GenerateShapeFor<IAsyncEnumerable<AsyncEnumerableContractTests.WrappedInt>>]
[GenerateShapeFor<AsyncEnumerableContractTests.ConcreteEnumerable>]
public partial class AsyncEnumerableContractTests : TestBase
{
	[Test]
	public async Task RootAndNestedEnumerables_WriteArrays()
	{
		Assert.Equal("[1,2,3]", await this.WriteAsync<IAsyncEnumerable<int>, AsyncEnumerableContractTests>(EnumerateAsync()));
		Assert.Equal("""{"values":[1,2,3]}""", await this.WriteAsync<Model, Model>(new() { Values = EnumerateAsync() }));
		Assert.Equal("""{"values":[1,2,3]}""", await this.WriteAsync<ConstructorModel, ConstructorModel>(new(EnumerateAsync())));
		Assert.Equal("[1,2,3]", await this.WriteAsync<ConcreteEnumerable, AsyncEnumerableContractTests>(new()));
	}

	[Test]
	public async Task NullAndEmptyEnumerables_AreSupported()
	{
		Assert.Equal("null", await this.WriteAsync<IAsyncEnumerable<int>, AsyncEnumerableContractTests>(null));
		Assert.Equal("[]", await this.WriteAsync<IAsyncEnumerable<int>, AsyncEnumerableContractTests>(EnumerateAsync(0)));
		Assert.Equal("""{"values":null}""", await this.WriteAsync<Model, Model>(new()));
	}

	[Test]
	public void SynchronousSerialization_RejectsAsyncOnlyEnumeration()
	{
		JsonSerializationException exception = Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize<IAsyncEnumerable<int>, AsyncEnumerableContractTests>(EnumerateAsync()));
		Assert.Contains("SerializeAsync", exception.Message, StringComparison.Ordinal);
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Serialize(new Model { Values = EnumerateAsync() }));
	}

	[Test]
	public async Task Read_AllowsNullButRejectsArrays()
	{
		ITypeShape<IAsyncEnumerable<int>> shape = TypeShapeResolver.ResolveDynamicOrThrow<IAsyncEnumerable<int>, AsyncEnumerableContractTests>();
		Assert.Null(this.Serializer.Deserialize("null", shape));
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize("[1]", shape));
		using MemoryStream nullStream = new("null"u8.ToArray());
		Assert.Null(await this.Serializer.DeserializeAsync(nullStream, shape));
		using MemoryStream arrayStream = new("[1]"u8.ToArray());
		await Assert.ThrowsAsync<NotSupportedException>(async () => await this.Serializer.DeserializeAsync(arrayStream, shape));
		Assert.Null(this.Serializer.Deserialize<Model>("""{"values":null}""")!.Values);
	}

	[Test]
	public async Task CustomValueConverter_UsesAsyncContract()
	{
		this.Serializer = this.Serializer with { Converters = new([new WrappedIntConverter()]) };
		Assert.Equal("""["v1","v2"]""", await this.WriteAsync<IAsyncEnumerable<WrappedInt>, AsyncEnumerableContractTests>(WrappedAsync()));
		Assert.Equal("""{"values":["v1","v2"]}""", await this.WriteAsync<CustomModel, CustomModel>(new() { Values = WrappedAsync() }));

		static async IAsyncEnumerable<WrappedInt> WrappedAsync()
		{
			yield return new(1);
			await Task.Yield();
			yield return new(2);
		}
	}

	[Test]
	public async Task Backpressure_StopsEnumerationBeforeNextElement()
	{
		this.Serializer = this.Serializer with { StartingContext = new() { UnflushedBytesThreshold = 0 } };
		Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 1, resumeWriterThreshold: 1));
		int enumerated = 0;
		bool disposed = false;
		using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
		Task serialization = this.Serializer.SerializeAsync(pipe.Writer, ValuesAsync(), TypeShapeResolver.ResolveDynamicOrThrow<IAsyncEnumerable<int>, AsyncEnumerableContractTests>(), timeout.Token).AsTask();
		ReadResult first = await pipe.Reader.ReadAsync(timeout.Token);
		Assert.Equal("[1", Encoding.UTF8.GetString(first.Buffer.ToArray()));
		Assert.Equal(1, enumerated);
		Assert.False(serialization.IsCompleted);
		pipe.Reader.AdvanceTo(first.Buffer.End);
		using MemoryStream rest = new();
		while (true)
		{
			ReadResult next = await pipe.Reader.ReadAsync(timeout.Token);
			byte[] bytes = next.Buffer.ToArray();
			rest.Write(bytes, 0, bytes.Length);
			bool ended = bytes.Length > 0 && bytes[bytes.Length - 1] == (byte)']';
			pipe.Reader.AdvanceTo(next.Buffer.End);
			if (ended)
			{
				break;
			}
		}

		await serialization;
		Assert.Equal(",2,3]", Encoding.UTF8.GetString(rest.ToArray()));
		Assert.True(disposed);
		await pipe.Writer.CompleteAsync();
		await pipe.Reader.CompleteAsync();

		async IAsyncEnumerable<int> ValuesAsync()
		{
			try
			{
				for (int i = 1; i <= 3; i++)
				{
					enumerated++;
					yield return i;
					await Task.Yield();
				}
			}
			finally
			{
				disposed = true;
			}
		}
	}

	[Test]
	public async Task Cancellation_ReachesEnumeratorAndDisposesIt()
	{
		using CancellationTokenSource cancellation = new();
		TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
		bool disposed = false;
		using MemoryStream output = new();
		Task serialization = this.Serializer.SerializeAsync(output, ValuesAsync(), TypeShapeResolver.ResolveDynamicOrThrow<IAsyncEnumerable<int>, AsyncEnumerableContractTests>(), cancellation.Token).AsTask();
		await started.Task;
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => serialization);
		Assert.True(disposed);

		async IAsyncEnumerable<int> ValuesAsync([EnumeratorCancellation] CancellationToken token = default)
		{
			try
			{
				yield return 1;
				started.SetResult(true);
				await Task.Delay(Timeout.Infinite, token);
			}
			finally
			{
				disposed = true;
			}
		}
	}

	private static async IAsyncEnumerable<int> EnumerateAsync(int count = 3)
	{
		for (int i = 1; i <= count; i++)
		{
			yield return i;
			await Task.Yield();
		}
	}

	private async Task<string> WriteAsync<T, TProvider>(T? value)
	{
		using MemoryStream stream = new();
		await this.Serializer.SerializeAsync(stream, value, TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>());
		return Encoding.UTF8.GetString(stream.ToArray());
	}

	internal readonly record struct WrappedInt(int Value);

	[GenerateShape]
	internal partial class Model
	{
		public IAsyncEnumerable<int>? Values { get; set; }
	}

	[GenerateShape]
	internal partial class ConstructorModel(IAsyncEnumerable<int> values)
	{
		public IAsyncEnumerable<int> Values { get; } = values;
	}

	[GenerateShape]
	internal partial class CustomModel
	{
		public IAsyncEnumerable<WrappedInt>? Values { get; set; }
	}

	internal sealed class ConcreteEnumerable : IAsyncEnumerable<int>
	{
		public IAsyncEnumerator<int> GetAsyncEnumerator(CancellationToken cancellationToken = default)
			=> EnumerateAsync().GetAsyncEnumerator(cancellationToken);
	}

	private sealed class WrappedIntConverter : JsonConverter<WrappedInt>
	{
		public override bool PreferAsyncSerialization => true;

		public override WrappedInt Read(ref JsonReader reader, SerializationContext context) => throw new NotSupportedException();

		public override void Write(ref JsonWriter writer, WrappedInt value, SerializationContext context) => throw new NotSupportedException();

		public override ValueTask WriteAsync(JsonAsyncWriter writer, WrappedInt value, SerializationContext context)
		{
			JsonWriter syncWriter = writer.CreateWriter();
			syncWriter.WriteStringValue("v" + value.Value);
			writer.ReturnWriter(ref syncWriter);
			return writer.FlushIfAppropriateAsync(context);
		}
	}
}
