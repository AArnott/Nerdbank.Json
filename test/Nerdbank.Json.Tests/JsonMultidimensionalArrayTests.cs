// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1500 // Multidimensional array literals use a compact nested-brace layout.

using System.Buffers;
using System.Linq;
using PolyType.Abstractions;

[GenerateShapeFor<int[,]>]
[GenerateShapeFor<int[,,]>]
[GenerateShapeFor<int[,,,]>]
[GenerateShapeFor<string[,]>]
public partial class JsonMultidimensionalArrayTests : TestBase
{
	[Test]
	public void Rank2_Int_RoundTrips()
	{
		int[,] value = { { 1, 2, 3 }, { 4, 5, 6 } };
		this.AssertArrayRoundtrip<int[,], JsonMultidimensionalArrayTests>(value, "[[1,2,3],[4,5,6]]");
	}

	[Test]
	public void Rank3_Int_RoundTrips()
	{
		int[,,] value = { { { 1, 2 }, { 3, 4 } }, { { 5, 6 }, { 7, 8 } } };
		this.AssertArrayRoundtrip<int[,,], JsonMultidimensionalArrayTests>(value, "[[[1,2],[3,4]],[[5,6],[7,8]]]");
	}

	[Test]
	public void Rank4_Int_RoundTrips()
	{
		int[,,,] value = { { { { 1, 2 } } }, { { { 3, 4 } } } };
		this.AssertArrayRoundtrip<int[,,,], JsonMultidimensionalArrayTests>(value, "[[[[1,2]]],[[[3,4]]]]");
	}

	[Test]
	public void Rank2_String_RoundTrips()
	{
		string[,] value = { { "a", "b" }, { "c", "d" } };
		this.AssertArrayRoundtrip<string[,], JsonMultidimensionalArrayTests>(value, """[["a","b"],["c","d"]]""");
	}

	[Test]
	public void Rank2_EmptyInnerDimension_RoundTrips()
	{
		int[,] value = new int[2, 0];
		this.AssertArrayRoundtrip<int[,], JsonMultidimensionalArrayTests>(value, "[[],[]]");
	}

	[Test]
	public void Rank2_EmptyOuterDimension_RoundTrips()
	{
		int[,] value = new int[0, 0];
		string json = this.Serializer.Serialize(value, Shape<int[,], JsonMultidimensionalArrayTests>());
		Assert.Equal("[]", json);

		int[,]? result = this.Serializer.Deserialize(json, Shape<int[,], JsonMultidimensionalArrayTests>());
		Assert.NotNull(result);
		Assert.Equal(0, result.GetLength(0));
		Assert.Equal(0, result.GetLength(1));
	}

	[Test]
	public void Rank2_SingleEmptyRow_RoundTrips()
	{
		int[,] value = new int[1, 0];
		this.AssertArrayRoundtrip<int[,], JsonMultidimensionalArrayTests>(value, "[[]]");
	}

	[Test]
	public void Null_RoundTrips()
	{
		int[,]? value = null;
		string json = this.Serializer.Serialize(value, Shape<int[,], JsonMultidimensionalArrayTests>());
		Assert.Equal("null", json);
		Assert.Null(this.Serializer.Deserialize(json, Shape<int[,], JsonMultidimensionalArrayTests>()));
	}

	[Test]
	public void NestedObjectElements_RoundTrip()
	{
		Cell[,] value =
		{
			{ new Cell { Value = 1 }, new Cell { Value = 2 } },
			{ new Cell { Value = 3 }, new Cell { Value = 4 } },
		};

		string json = this.Serializer.Serialize(value, Shape<Cell[,], CellWitness>());
		Assert.Equal("""[[{"value":1},{"value":2}],[{"value":3},{"value":4}]]""", json);

		Cell[,]? result = this.Serializer.Deserialize(json, Shape<Cell[,], CellWitness>());
		Assert.NotNull(result);
		Assert.Equal(2, result.GetLength(0));
		Assert.Equal(2, result.GetLength(1));
		Assert.Equal(1, result[0, 0].Value);
		Assert.Equal(4, result[1, 1].Value);
	}

	[Test]
	public void MemberProperty_RoundTrips()
	{
		Grid value = new() { Cells = new[,] { { 1, 2 }, { 3, 4 } } };

		string json = this.Serializer.Serialize(value, Shape<Grid, Grid>());
		Assert.Equal("""{"cells":[[1,2],[3,4]]}""", json);

		Grid? result = this.Serializer.Deserialize(json, Shape<Grid, Grid>());
		Assert.NotNull(result?.Cells);
		Assert.Equal(2, result.Cells.GetLength(0));
		Assert.Equal(4, result.Cells[1, 1]);
	}

	[Test]
	public void RaggedInput_Throws()
	{
		JsonSerializationException ex = Assert.Throws<JsonSerializationException>(
			() => this.Serializer.Deserialize("[[1,2],[3]]", Shape<int[,], JsonMultidimensionalArrayTests>()));
		Assert.Contains("Ragged", ex.Message, StringComparison.Ordinal);
	}

	[Test]
	public void RaggedInput_UnequalOuterCounts_Throws()
	{
		Assert.Throws<JsonSerializationException>(
			() => this.Serializer.Deserialize("[[[1],[2]],[[3]]]", Shape<int[,,], JsonMultidimensionalArrayTests>()));
	}

	[Test]
	public void TooShallowInput_Throws()
	{
		JsonSerializationException ex = Assert.Throws<JsonSerializationException>(
			() => this.Serializer.Deserialize("[1,2]", Shape<int[,], JsonMultidimensionalArrayTests>()));
		Assert.Contains("nested", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Test]
	public void MixedDepthInput_Throws()
	{
		Assert.Throws<JsonSerializationException>(
			() => this.Serializer.Deserialize("[[1,2],3]", Shape<int[,], JsonMultidimensionalArrayTests>()));
	}

	[Test]
	public void TooDeepInput_Throws()
	{
		Assert.ThrowsAny<Exception>(
			() => this.Serializer.Deserialize("[[[1]]]", Shape<int[,], JsonMultidimensionalArrayTests>()));
	}

	[Test]
	public void MultiSegmentInput_RoundTrips()
	{
		byte[] utf8 = System.Text.Encoding.UTF8.GetBytes("[[1,2,3],[4,5,6]]");
		ReadOnlySequence<byte> sequence = CreateSequence(utf8.AsMemory(0, 5), utf8.AsMemory(5, 4), utf8.AsMemory(9));

		int[,]? result = this.Serializer.Deserialize(sequence, Shape<int[,], JsonMultidimensionalArrayTests>());
		Assert.NotNull(result);
		Assert.Equal(2, result.GetLength(0));
		Assert.Equal(3, result.GetLength(1));
		Assert.Equal(6, result[1, 2]);
	}

	[Test]
	public void ExceededDepth_Throws()
	{
		JsonSerializer shallow = new() { StartingContext = new SerializationContext { MaxDepth = 2 } };

		Assert.Throws<InvalidOperationException>(
			() => shallow.Deserialize("[[[1,2],[3,4]],[[5,6],[7,8]]]", Shape<int[,,], JsonMultidimensionalArrayTests>()));
	}

	[Test]
	public void Cancellation_IsHonored()
	{
		using CancellationTokenSource cts = new();
		cts.Cancel();

		Assert.Throws<OperationCanceledException>(
			() => this.Serializer.Deserialize("[[1,2],[3,4]]", Shape<int[,], JsonMultidimensionalArrayTests>(), cts.Token));
	}

	private static ITypeShape<T> Shape<T, TProvider>()
#if NET
		where TProvider : IShapeable<T> => TProvider.GetTypeShape();
#else
		=> TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>();
#endif

	private static ReadOnlySequence<byte> CreateSequence(params ReadOnlyMemory<byte>[] segments)
	{
		Segment? first = null;
		Segment? last = null;
		foreach (ReadOnlyMemory<byte> segment in segments)
		{
			Segment current = new(segment);
			if (first is null)
			{
				first = current;
			}
			else
			{
				last!.SetNext(current);
			}

			last = current;
		}

		return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
	}

	private void AssertArrayRoundtrip<T, TProvider>(T value, string expectedJson)
#if NET
		where TProvider : IShapeable<T>
#endif
	{
		ITypeShape<T> shape = Shape<T, TProvider>();
		string json = this.Serializer.Serialize(value, shape);
		Assert.Equal(expectedJson, json);

		T result = this.Serializer.Deserialize(json, shape)!;
		Array expected = (Array)(object)value!;
		Array actual = (Array)(object)result!;
		Assert.Equal(expected.Rank, actual.Rank);
		for (int i = 0; i < expected.Rank; i++)
		{
			Assert.Equal(expected.GetLength(i), actual.GetLength(i));
		}

		Assert.Equal(expected.Cast<object>(), actual.Cast<object>());
	}

	[GenerateShape]
	internal partial class Grid
	{
		public int[,]? Cells { get; set; }
	}

	[GenerateShape]
	internal partial class Cell
	{
		public int Value { get; set; }
	}

	[GenerateShapeFor<Cell[,]>]
	internal partial class CellWitness;

	private sealed class Segment : ReadOnlySequenceSegment<byte>
	{
		internal Segment(ReadOnlyMemory<byte> memory) => this.Memory = memory;

		internal void SetNext(Segment next)
		{
			next.RunningIndex = this.RunningIndex + this.Memory.Length;
			this.Next = next;
		}
	}
}
