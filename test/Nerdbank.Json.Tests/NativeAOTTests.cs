// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public partial class NativeAOTTests
{
	[Test]
	public async Task Roundtrip()
	{
		JsonSerializer serializer = new();
		Tree tree = new()
		{
			Fruits = [new Fruit(3), new Fruit(5)],
		};

		string json = serializer.Serialize(tree);
		Assert.Equal("{\"fruits\":[{\"seeds\":3},{\"seeds\":5}]}", json);

		Tree roundTripped = serializer.Deserialize<Tree>(json)!;
		Assert.Equal(2, roundTripped.Fruits.Count);
		Assert.Equal(3, roundTripped.Fruits[0].Seeds);
		Assert.Equal(5, roundTripped.Fruits[1].Seeds);
	}

	[GenerateShape]
	public partial class Tree
	{
		public List<Fruit> Fruits { get; set; } = [];
	}

	[GenerateShape]
	public partial record Fruit(int Seeds);
}
