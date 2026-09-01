using System;
using Nerdbank.Json;
using PolyType;

// <MultidimensionalArrays>
[GenerateShapeFor<int[,]>]
public partial class Witness;

JsonSerializer serializer = new();

int[,] matrix =
{
	{ 1, 2, 3 },
	{ 4, 5, 6 },
};

// Serializes as [[1,2,3],[4,5,6]].
string json = serializer.Serialize(matrix, Witness.GetTypeShape());

int[,] roundTripped = serializer.Deserialize(json, Witness.GetTypeShape())!;
Console.WriteLine(roundTripped[1, 2]); // 6
// </MultidimensionalArrays>
