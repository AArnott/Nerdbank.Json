// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1500 // Multidimensional array literals use a compact nested-brace layout.

using System.IO.Pipelines;
using System.Linq;
using System.Text;

[GenerateShapeFor<FlatProbe[,]>]
public partial class JsonMultidimensionalArrayTests
{
	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task Flat_Rank2_RoundTrips(bool async)
	{
		this.UseFlatFormat();
		int[,] value = { { 1, 2, 3 }, { 4, 5, 6 } };
		await this.AssertFlatRoundtrip(value, Shape<int[,], JsonMultidimensionalArrayTests>(), "[[2,3],[1,2,3,4,5,6]]", async);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task Flat_Rank3_RoundTrips(bool async)
	{
		this.UseFlatFormat();
		int[,,] value = { { { 1, 2 }, { 3, 4 } }, { { 5, 6 }, { 7, 8 } } };
		await this.AssertFlatRoundtrip(value, Shape<int[,,], JsonMultidimensionalArrayTests>(), "[[2,2,2],[1,2,3,4,5,6,7,8]]", async);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task Flat_Rank4_RoundTrips(bool async)
	{
		this.UseFlatFormat();
		int[,,,] value = { { { { 1, 2 } } }, { { { 3, 4 } } } };
		await this.AssertFlatRoundtrip(value, Shape<int[,,,], JsonMultidimensionalArrayTests>(), "[[2,1,1,2],[1,2,3,4]]", async);
	}

	[Test]
	[Arguments(0, 3)]
	[Arguments(3, 0)]
	[Arguments(0, 0)]
	public async Task Flat_Rank2_EmptyDimensionsArePreserved(int first, int second)
	{
		this.UseFlatFormat();
		int[,] value = new int[first, second];
		foreach (bool async in new[] { false, true })
		{
			await this.AssertFlatRoundtrip(value, Shape<int[,], JsonMultidimensionalArrayTests>(), $"[[{first},{second}],[]]", async);
		}
	}

	[Test]
	[Arguments(0, 2, 3)]
	[Arguments(2, 0, 3)]
	[Arguments(2, 3, 0)]
	[Arguments(0, 0, 0)]
	public async Task Flat_Rank3_EmptyDimensionsArePreserved(int first, int second, int third)
	{
		this.UseFlatFormat();
		int[,,] value = new int[first, second, third];
		foreach (bool async in new[] { false, true })
		{
			await this.AssertFlatRoundtrip(value, Shape<int[,,], JsonMultidimensionalArrayTests>(), $"[[{first},{second},{third}],[]]", async);
		}
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
#if NET
	[System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Both int[,] and int[,,] are statically referenced by generated shapes.")]
#endif
	public async Task Flat_NonZeroLowerBounds_AreNormalized(bool async)
	{
		this.UseFlatFormat();
		int[,] rank2 = (int[,])Array.CreateInstance(typeof(int), [2, 2], [-2, 3]);
		rank2[-2, 3] = 1;
		rank2[-2, 4] = 2;
		rank2[-1, 3] = 3;
		rank2[-1, 4] = 4;
		await this.AssertFlatRoundtrip(rank2, Shape<int[,], JsonMultidimensionalArrayTests>(), "[[2,2],[1,2,3,4]]", async);

		int[,,] rank3 = (int[,,])Array.CreateInstance(typeof(int), [1, 1, 2], [3, -2, 4]);
		rank3[3, -2, 4] = 1;
		rank3[3, -2, 5] = 2;
		await this.AssertFlatRoundtrip(rank3, Shape<int[,,], JsonMultidimensionalArrayTests>(), "[[1,1,2],[1,2]]", async);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task Flat_Null_RoundTrips(bool async)
	{
		this.UseFlatFormat();
		ITypeShape<int[,]> shape = Shape<int[,], JsonMultidimensionalArrayTests>();
		Assert.Equal("null", await this.WriteFlat<int[,]>(null, shape, async));
		Assert.Null(await this.ReadFlat("null", shape, async));
	}

	[Test]
	[Arguments("[]")]
	[Arguments("[[1,1]]")]
	[Arguments("[[1,1],[1],[]]")]
	[Arguments("[[1],[1]]")]
	[Arguments("[[1,1,1],[1]]")]
	[Arguments("[[-1,1],[]]")]
	[Arguments("[[1.0,1],[1]]")]
	[Arguments("[[1e0,1],[1]]")]
	[Arguments("""[["1",1],[1]]""")]
	[Arguments("[[null,1],[1]]")]
	[Arguments("[[2147483648,1],[]]")]
	[Arguments("[[2,2],[1,2,3]]")]
	[Arguments("[[1,1],[1,2]]")]
	[Arguments("[[0,3],[1]]")]
	[Arguments("[[10000,10000],[]]")]
	[Arguments("[[2147483647,2147483647],[]]")]
	[Arguments("[[1,1],[[1]]]")]
	[Arguments("[[1,1],[1]")]
	public async Task Flat_InvalidRank2Input_IsRejected(string json)
	{
		this.UseFlatFormat();
		ITypeShape<int[,]> shape = Shape<int[,], JsonMultidimensionalArrayTests>();
		Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize(json, shape));
		Exception error = await Assert.ThrowsAnyAsync<Exception>(async () => await this.ReadFlat(json, shape, async: true));
		Assert.True(error is JsonSerializationException or FormatException or EndOfStreamException, error.ToString());
	}

	[Test]
	[Arguments("[[2,2,2],[1,2,3]]")]
	[Arguments("[[0,1,-1],[]]")]
	[Arguments("[[2147483647,2147483647,2147483647],[]]")]
	public async Task Flat_InvalidRank3Input_IsRejected(string json)
	{
		this.UseFlatFormat();
		ITypeShape<int[,,]> shape = Shape<int[,,], JsonMultidimensionalArrayTests>();
		JsonSerializationException error = Assert.Throws<JsonSerializationException>(() => this.Serializer.Deserialize(json, shape));
		Assert.IsNotType<OutOfMemoryException>(error.InnerException);
		Exception asyncError = await Assert.ThrowsAnyAsync<Exception>(async () => await this.ReadFlat(json, shape, async: true));
		Assert.True(asyncError is JsonSerializationException or FormatException or EndOfStreamException, asyncError.ToString());
	}

	[Test]
	public void Flat_ConfigurationClone_DoesNotReuseNestedConverters()
	{
		int[,] value = { { 1, 2 } };
		ITypeShape<int[,]> shape = Shape<int[,], JsonMultidimensionalArrayTests>();
		JsonSerializer nested = this.Serializer;
		Assert.Equal(MultiDimensionalArrayFormat.Nested, nested.MultiDimensionalArrayFormat);
		Assert.Equal("[[1,2]]", nested.Serialize(value, shape));

		JsonSerializer flat = nested with { MultiDimensionalArrayFormat = MultiDimensionalArrayFormat.Flat };
		Assert.Equal("[[1,2],[1,2]]", flat.Serialize(value, shape));
		Assert.Equal("[[1,2]]", nested.Serialize(value, shape));
		Assert.Equal("[[1,2]]", (flat with { MultiDimensionalArrayFormat = MultiDimensionalArrayFormat.Nested }).Serialize(value, shape));
	}

	[Test]
	public void Flat_InvalidFormat_IsRejected()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => new JsonSerializer { MultiDimensionalArrayFormat = (MultiDimensionalArrayFormat)42 });
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task Flat_LogicalArrayDepth_MatchesMessagePack(bool async)
	{
		this.UseFlatFormat();
		this.Serializer = this.Serializer with { StartingContext = new SerializationContext { MaxDepth = 1 } };
		int[,,] value = new int[1, 1, 1];
		await this.AssertFlatRoundtrip(value, Shape<int[,,], JsonMultidimensionalArrayTests>(), "[[1,1,1],[0]]", async);

		this.Serializer = this.Serializer with { StartingContext = new SerializationContext { MaxDepth = 0 } };
		Exception readError = await Assert.ThrowsAnyAsync<Exception>(async () => await this.ReadFlat("[[1,1,1],[0]]", Shape<int[,,], JsonMultidimensionalArrayTests>(), async));
		Assert.Contains("maximum depth", readError.Message, StringComparison.Ordinal);
		Exception writeError = await Assert.ThrowsAnyAsync<Exception>(async () => await this.WriteFlat(value, Shape<int[,,], JsonMultidimensionalArrayTests>(), async));
		Assert.Contains("maximum depth", writeError.Message, StringComparison.Ordinal);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task Flat_NestedElements_ConsumeAdditionalDepth(bool async)
	{
		this.UseFlatFormat();
		this.Serializer = this.Serializer with { StartingContext = new SerializationContext { MaxDepth = 1 } };
		Cell[,] value = { { new() { Value = 1 } } };
		Exception error = await Assert.ThrowsAnyAsync<Exception>(async () => await this.WriteFlat(value, Shape<Cell[,], CellWitness>(), async));
		Assert.Contains("maximum depth", error.Message, StringComparison.Ordinal);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task Flat_ReferencePreservation_RetainsSharedArrayIdentity(bool async)
	{
		this.UseFlatFormat();
		this.Serializer = this.Serializer with { PreserveReferences = ReferencePreservationMode.RejectCycles };
		int[,] array = { { 1, 2 } };
		FlatArrayPair value = new() { First = array, Second = array };
		ITypeShape<FlatArrayPair> shape = Shape<FlatArrayPair, FlatArrayPair>();
		string json = await this.WriteFlat(value, shape, async);
		Assert.Equal("""{"$id":1,"$value":{"first":{"$id":2,"$value":[[1,2],[1,2]]},"second":{"$ref":2}}}""", json);
		FlatArrayPair result = (await this.ReadFlat(json, shape, async))!;
		Assert.Same(result.First, result.Second);
	}

	[Test]
	[Arguments(false)]
	[Arguments(true)]
	public async Task Flat_ReferencePreservation_RetainsSharedElementIdentity(bool async)
	{
		this.UseFlatFormat();
		this.Serializer = this.Serializer with { PreserveReferences = ReferencePreservationMode.AllowCycles };
		Cell cell = new() { Value = 3 };
		Cell[,] value = { { cell, cell } };
		ITypeShape<Cell[,]> shape = Shape<Cell[,], CellWitness>();
		string json = await this.WriteFlat(value, shape, async);
		Cell[,] result = (await this.ReadFlat(json, shape, async))!;
		Assert.Same(result[0, 0], result[0, 1]);
		Assert.Equal(3, result[0, 0].Value);
	}

	[Test]
	public async Task Flat_Async_UsesAsynchronousElementConverters()
	{
		this.UseFlatFormat();
		FlatProbeConverter converter = new();
		this.Serializer = this.Serializer with { Converters = new ConverterCollection([converter]) };
		FlatProbe[,] value = { { new(1), new(2) } };
		ITypeShape<FlatProbe[,]> shape = Shape<FlatProbe[,], JsonMultidimensionalArrayTests>();
		string json = await this.WriteFlat(value, shape, async: true);
		Assert.Equal("[[1,2],[1,2]]", json);
		FlatProbe[,] result = (await this.ReadFlat(json, shape, async: true))!;
		Assert.Equal(2, result[0, 1].Value);
	}

	[Test]
	public async Task Flat_Async_CancellationDuringPartialInput_IsHonored()
	{
		this.UseFlatFormat();
		Pipe pipe = new();
		using CancellationTokenSource cts = new();
		await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("[[1,2],[1,"));
		Task<int[,]?> task = this.Serializer.DeserializeAsync(pipe.Reader, Shape<int[,], JsonMultidimensionalArrayTests>(), cts.Token).AsTask();
		cts.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
		await pipe.Writer.CompleteAsync();
		await pipe.Reader.CompleteAsync();
	}

	[Test]
	[Arguments(JsonSchemaDialect.Draft4, "items")]
	[Arguments(JsonSchemaDialect.Draft7, "items")]
	[Arguments(JsonSchemaDialect.Draft2020_12, "prefixItems")]
	public void Flat_Schema_DescribesDimensionsAndData(JsonSchemaDialect dialect, string tupleKeyword)
	{
		this.UseFlatFormat();
		string schema = this.Serializer.GetJsonSchema(Shape<int[,,], JsonMultidimensionalArrayTests>(), new JsonSchemaOptions { Dialect = dialect });
		Assert.Contains($"\"{tupleKeyword}\":[", schema, StringComparison.Ordinal);
		Assert.Contains("""{"type":"array","minItems":3,"maxItems":3,"items":{"type":"integer","minimum":0,"maximum":2147483647}}""", schema, StringComparison.Ordinal);
		Assert.Contains("""{"type":"array","items":{"type":"integer","minimum":-2147483648,"maximum":2147483647}}""", schema, StringComparison.Ordinal);
		Assert.Contains("\"minItems\":2,\"maxItems\":2", schema, StringComparison.Ordinal);
	}

	private void UseFlatFormat() => this.Serializer = this.Serializer with { MultiDimensionalArrayFormat = MultiDimensionalArrayFormat.Flat };

	private async Task<T?> ReadFlat<T>(string json, ITypeShape<T> shape, bool async)
	{
		if (!async)
		{
			return this.Serializer.Deserialize(json, shape);
		}

		Pipe pipe = new();
		await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes(json));
		await pipe.Writer.CompleteAsync();
		try
		{
			return await this.Serializer.DeserializeAsync(pipe.Reader, shape);
		}
		finally
		{
			await pipe.Reader.CompleteAsync();
		}
	}

	private async Task<string> WriteFlat<T>(T? value, ITypeShape<T> shape, bool async)
	{
		if (!async)
		{
			return this.Serializer.Serialize(value, shape);
		}

		using MemoryStream stream = new();
		await this.Serializer.SerializeAsync(stream, value, shape);
		return Encoding.UTF8.GetString(stream.ToArray());
	}

	private async Task AssertFlatRoundtrip<T>(T value, ITypeShape<T> shape, string expectedJson, bool async)
	{
		string json = await this.WriteFlat(value, shape, async);
		Assert.Equal(expectedJson, json);
		T result = (await this.ReadFlat(json, shape, async))!;
		Array expected = (Array)(object)value!;
		Array actual = (Array)(object)result!;
		Assert.Equal(expected.Rank, actual.Rank);
		for (int i = 0; i < expected.Rank; i++)
		{
			Assert.Equal(expected.GetLength(i), actual.GetLength(i));
			Assert.Equal(0, actual.GetLowerBound(i));
		}

		Assert.Equal(expected.Cast<object>(), actual.Cast<object>());
	}

	[GenerateShape]
	internal partial record struct FlatProbe(int Value);

	[GenerateShape]
	internal partial class FlatArrayPair
	{
		public int[,]? First { get; set; }

		public int[,]? Second { get; set; }
	}

	private sealed class FlatProbeConverter : JsonConverter<FlatProbe>
	{
		public override bool PreferAsyncSerialization => true;

		public override void Write(ref JsonWriter writer, FlatProbe value, SerializationContext context) => throw new NotSupportedException();

		public override FlatProbe Read(ref JsonReader reader, SerializationContext context) => throw new NotSupportedException();

		public override async ValueTask WriteAsync(JsonAsyncWriter writer, FlatProbe value, SerializationContext context)
		{
			JsonWriter syncWriter = writer.CreateWriter();
			syncWriter.WriteNumberValue(value.Value);
			writer.ReturnWriter(ref syncWriter);
			await writer.FlushIfAppropriateAsync(context);
		}

		public override async ValueTask<FlatProbe> ReadAsync(JsonAsyncReader reader, SerializationContext context)
		{
			await reader.BufferNextValueAsync(context);
			JsonReader syncReader = reader.CreateBufferedReader();
			int value = int.Parse(syncReader.ReadNumberToken(), System.Globalization.CultureInfo.InvariantCulture);
			reader.ReturnReader(ref syncReader);
			return new(value);
		}
	}
}
