// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public partial class NativeAOTTests
{
	[Test]
	public async Task RoundtripSyncAndAsync()
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

		using MemoryStream stream = new();
		CancellationToken cancellationToken = TUnit.Core.TestContext.Current?.Execution.CancellationToken ?? default;
		await serializer.SerializeAsync(stream, tree, cancellationToken);
		stream.Position = 0;

		Tree asyncRoundTripped = (await serializer.DeserializeAsync<Tree>(stream, cancellationToken))!;
		Assert.Equal(2, asyncRoundTripped.Fruits.Count);
		Assert.Equal(3, asyncRoundTripped.Fruits[0].Seeds);
		Assert.Equal(5, asyncRoundTripped.Fruits[1].Seeds);
	}

	[GenerateShape]
	public partial class Tree
	{
		public List<Fruit> Fruits { get; set; } = [];
	}

	[GenerateShape]
	public partial record Fruit(int Seeds);
}
