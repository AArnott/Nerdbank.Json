// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may contain closely related model types.

using System.IO;

internal static class StreamingTree
{
	internal static async Task RunAsync()
	{
		Tree tree = new()
		{
			Fruits = [new Fruit(3), new Fruit(5)],
		};

		JsonSerializer serializer = new();
		string json = serializer.Serialize(tree);
		Console.WriteLine(json);

		Tree roundTripped = serializer.Deserialize<Tree>(json)!;
		Console.WriteLine($"Tree with {roundTripped.Fruits.Count} fruit.");

		using MemoryStream stream = new();
		await serializer.SerializeAsync(stream, tree);
		stream.Position = 0;

		Tree asyncRoundTripped = await serializer.DeserializeAsync<Tree>(stream);
		foreach (Fruit fruit in asyncRoundTripped.Fruits)
		{
			Console.WriteLine($"  Fruit with {fruit.Seeds} seeds");
		}
	}
}

[GenerateShape]
internal partial class Tree
{
	public List<Fruit> Fruits { get; set; } = [];
}

[GenerateShape]
internal partial record Fruit(int Seeds);
