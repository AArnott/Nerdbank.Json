// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;
using PolyType.Abstractions;

[GenerateShapeFor<int>]
[GenerateShapeFor<int?>]
[GenerateShapeFor<List<Model>>]
[GenerateShapeFor<Dictionary<string, Model>>]
[GenerateShapeFor<object>]
[GenerateShapeFor<System.Text.Json.JsonElement>]
public partial class JsonSerializerExceptionTests
{
	[Test]
	[Arguments("sync")]
	[Arguments("sync-object")]
	[Arguments("pipe")]
	[Arguments("pipe-object")]
	[Arguments("stream")]
	[Arguments("stream-object")]
	public async Task ConverterFailures_RetainCause(string surface)
	{
		foreach (bool serialize in new[] { false, true })
		{
			FormatException cause = new("Converter failure.");
			JsonSerializer serializer = new() { Converters = [new FailingConverter(cause)] };
			JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(() => InvokeAsync(serializer, surface, serialize));
			Assert.Same(cause, exception.InnerException);
		}
	}

	[Test]
	[Arguments("sync")]
	[Arguments("sync-object")]
	[Arguments("pipe")]
	[Arguments("pipe-object")]
	[Arguments("stream")]
	[Arguments("stream-object")]
	public async Task ConverterCancellation_OnlyPropagatesWhenOperationCanceled(string surface)
	{
		foreach (bool serialize in new[] { false, true })
		{
			foreach (bool cancel in new[] { false, true })
			{
				using CancellationTokenSource cancellation = new();
				OperationCanceledException cause = new(cancellation.Token);
				JsonSerializer serializer = new() { Converters = [new FailingConverter(cause, cancel ? cancellation : null)] };
				if (cancel)
				{
					OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(() => InvokeAsync(serializer, surface, serialize, cancellation.Token));
					Assert.Same(cause, exception);
				}
				else
				{
					JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(() => InvokeAsync(serializer, surface, serialize, cancellation.Token));
					Assert.Same(cause, exception.InnerException);
				}
			}
		}
	}

	[Test]
	public async Task ExistingSerializationException_PreservesInstanceAndCode()
	{
		foreach (JsonSerializationException.ErrorCode code in new[] { JsonSerializationException.ErrorCode.MissingRequiredProperty, JsonSerializationException.ErrorCode.DoublePropertyAssignment })
		{
			JsonSerializationException cause = new("Specific failure.") { Code = code };
			JsonSerializer serializer = new() { Converters = [new FailingConverter(cause)] };
			foreach (string surface in new[] { "sync", "sync-object", "pipe", "pipe-object", "stream", "stream-object" })
			{
				foreach (bool serialize in new[] { false, true })
				{
					JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(() => InvokeAsync(serializer, surface, serialize));
					Assert.Same(cause, exception);
					Assert.Equal(code, exception.Code);
				}
			}
		}
	}

	[Test]
	[Arguments(ReferencePreservationMode.Off)]
	[Arguments(ReferencePreservationMode.AllowCycles)]
	public async Task NestedOptionalObjectAndCollections_NormalizeAtOperationBoundary(ReferencePreservationMode references)
	{
		await CheckAsync<int?>(1, Shape<int?>(), references);
		await CheckAsync(new Model { Count = 1 }, Shape<Model>(), references);
		await CheckAsync(new List<Model> { new() { Count = 1 } }, Shape<List<Model>>(), references);
		await CheckAsync(new Dictionary<string, Model> { ["item"] = new() { Count = 1 } }, Shape<Dictionary<string, Model>>(), references);
		await CheckAsync<object>(new Model { Count = 1 }, Shape<object>(), references, read: false);
	}

	[Test]
	public async Task OptionalAndNativeDom_MalformedInputIsNormalized()
	{
		JsonSerializer serializer = new JsonSerializer().WithObjectConverter().WithSystemTextJsonConverters();
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("""{"nested":[true,"""));
		JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeAsync(stream, Shape<object>()));
		Assert.IsType<FormatException>(exception.InnerException);

		stream.Position = 0;
		exception = await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeJsonValueAsync(stream));
		Assert.IsType<FormatException>(exception.InnerException);

		stream.Position = 0;
		exception = await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeAsync(stream, Shape<System.Text.Json.JsonElement>()));
		Assert.NotNull(exception.InnerException);
	}

	[Test]
	[Arguments("array")]
	[Arguments("ndjson")]
	[Arguments("path")]
	public async Task SequenceFailuresAndCancellation_AreNormalizedIncrementally(string surface)
	{
		foreach (bool cancel in new[] { false, true })
		{
			using CancellationTokenSource cancellation = new();
			OperationCanceledException cause = new(cancellation.Token);
			JsonSerializer serializer = new() { Converters = [new FailingConverter(cause, cancel ? cancellation : null)] };
			using MemoryStream stream = new(Encoding.UTF8.GetBytes(surface switch
			{
				"array" => "[1]",
				"ndjson" => "1\n",
				_ => """{"items":[1]}""",
			}));
			PipeReader reader = PipeReader.Create(stream);
			try
			{
				IAsyncEnumerable<int> sequence = surface switch
				{
					"array" => serializer.DeserializeArrayAsync(reader, Shape<int>()),
					"ndjson" => serializer.DeserializeNewlineDelimitedAsync(reader, Shape<int>()),
					_ => serializer.DeserializeArrayAtAsync(reader, JsonPath.Root.Member("items"), Shape<int>()),
				};
				await using IAsyncEnumerator<int> enumerator = sequence.GetAsyncEnumerator(cancellation.Token);
				if (cancel)
				{
					OperationCanceledException exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await enumerator.MoveNextAsync());
					Assert.Same(cause, exception);
				}
				else
				{
					JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(async () => await enumerator.MoveNextAsync());
					Assert.Same(cause, exception.InnerException);
				}
			}
			finally
			{
				await reader.CompleteAsync();
			}
		}
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task SequenceSourceFailure_IsNormalized(bool newlineDelimited)
	{
		FormatException cause = new("Source failed after its first element.");
		JsonSerializer serializer = new();
		using MemoryStream stream = new();
		JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(async () =>
		{
			if (newlineDelimited)
			{
				await serializer.SerializeNewlineDelimitedAsync(stream, Source(), Shape<int>());
			}
			else
			{
				await serializer.SerializeArrayAsync(stream, Source(), Shape<int>());
			}
		});
		Assert.Same(cause, exception.InnerException);
		Assert.True(stream.CanWrite);

		async IAsyncEnumerable<int> Source()
		{
			await Task.Yield();
			yield return 1;
			throw cause;
		}
	}

	[Test]
	public async Task NativeDom_RealCancellationIsNotWrapped()
	{
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();
		JsonSerializer serializer = new();
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("{}"));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await serializer.DeserializeJsonValueAsync(stream, cancellation.Token));
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await serializer.SerializeJsonValueAsync(stream, new JsonArray([]), cancellation.Token));
	}

	[Test]
	public void ArgumentValidation_IsNotWrapped()
	{
		JsonSerializer serializer = new();
		Assert.Throws<ArgumentNullException>(() => serializer.DeserializeAsync((PipeReader)null!, Shape<int>()));
		Assert.Throws<ArgumentNullException>(() => serializer.SerializeAsync((PipeWriter)null!, 1, Shape<int>()));
		Assert.Throws<ArgumentNullException>(() => serializer.DeserializeObjectAsync((PipeReader)null!, Shape<int>()));
		Assert.Throws<ArgumentNullException>(() => serializer.SerializeObjectAsync((PipeWriter)null!, 1, Shape<int>()));
		Assert.Throws<ArgumentNullException>(() => serializer.DeserializeArrayAsync((PipeReader)null!, Shape<int>()));
	}

	private static async Task CheckAsync<T>(T value, ITypeShape<T> shape, ReferencePreservationMode references, bool read = true)
	{
		JsonSerializer baseline = new JsonSerializer { PreserveReferences = references }.WithObjectConverter();
		byte[] bytes = Encoding.UTF8.GetBytes(baseline.Serialize(value, shape));
		FormatException cause = new("Nested converter failure.");
		JsonSerializer serializer = baseline with { Converters = [.. baseline.Converters, new FailingConverter(cause)] };
		if (read)
		{
			using MemoryStream stream = new(bytes);
			JsonSerializationException readException = await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.DeserializeAsync(stream, shape));
			Assert.Same(cause, readException.InnerException);
		}

		using MemoryStream output = new();
		JsonSerializationException exception = await Assert.ThrowsAsync<JsonSerializationException>(async () => await serializer.SerializeAsync(output, value, shape));
		Assert.Same(cause, exception.InnerException);
	}

	private static async Task InvokeAsync(JsonSerializer serializer, string surface, bool serialize, CancellationToken cancellationToken = default)
	{
		ITypeShape<int> shape = Shape<int>();
		using MemoryStream stream = new(Encoding.UTF8.GetBytes("1"));
		Pipe pipe = new();
		if (!serialize)
		{
			await pipe.Writer.WriteAsync("1"u8.ToArray(), CancellationToken.None);
			await pipe.Writer.CompleteAsync();
		}

		try
		{
			switch (surface, serialize)
			{
				case ("sync", true):
					serializer.Serialize(1, shape, cancellationToken);
					break;
				case ("sync", false):
					serializer.Deserialize("1", shape, cancellationToken);
					break;
				case ("sync-object", true):
					serializer.SerializeObject(1, shape, cancellationToken);
					break;
				case ("sync-object", false):
					serializer.DeserializeObject("1", shape, cancellationToken);
					break;
				case ("pipe", true):
					await serializer.SerializeAsync(pipe.Writer, 1, shape, cancellationToken);
					break;
				case ("pipe", false):
					await serializer.DeserializeAsync(pipe.Reader, shape, cancellationToken);
					break;
				case ("pipe-object", true):
					await serializer.SerializeObjectAsync(pipe.Writer, 1, shape, cancellationToken);
					break;
				case ("pipe-object", false):
					await serializer.DeserializeObjectAsync(pipe.Reader, shape, cancellationToken);
					break;
				case ("stream", true):
					await serializer.SerializeAsync(stream, 1, shape, cancellationToken);
					break;
				case ("stream", false):
					await serializer.DeserializeAsync(stream, shape, cancellationToken);
					break;
				case ("stream-object", true):
					await serializer.SerializeObjectAsync(stream, 1, shape, cancellationToken);
					break;
				case ("stream-object", false):
					await serializer.DeserializeObjectAsync(stream, shape, cancellationToken);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(surface));
			}
		}
		finally
		{
			if (serialize)
			{
				await pipe.Writer.CompleteAsync();
			}

			await pipe.Reader.CompleteAsync();
		}
	}

	private static ITypeShape<T> Shape<T>() => PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<T>()!;

	[GenerateShape]
	public partial class Model
	{
		public int? Count { get; set; }
	}

	private sealed class FailingConverter(Exception cause, CancellationTokenSource? cancellation = null) : JsonConverter<int>
	{
		public override bool PreferAsyncSerialization => true;

		public override void Write(ref JsonWriter writer, in int value, SerializationContext context)
		{
			cancellation?.Cancel();
			throw cause;
		}

		public override int Read(ref JsonReader reader, SerializationContext context)
		{
			cancellation?.Cancel();
			throw cause;
		}

		public override async ValueTask WriteAsync(JsonAsyncWriter writer, int value, SerializationContext context)
		{
			await Task.Yield();
			cancellation?.Cancel();
			throw cause;
		}

		public override async ValueTask<int> ReadAsync(JsonAsyncReader reader, SerializationContext context)
		{
			await Task.Yield();
			cancellation?.Cancel();
			throw cause;
		}
	}
}
