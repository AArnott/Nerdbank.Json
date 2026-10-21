// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

[GenerateShapeFor<int>]
public partial class ConverterCollectionTests
{
	[Test]
	public void CollectionsAreSnapshots()
	{
		JsonConverter[] source = [new IntConverter()];
		ConverterCollection collection = ConverterCollection.Create(source);
		source[0] = null!;
		Assert.Single(collection);
		Assert.IsType<IntConverter>(collection.Single());
		Assert.Single(((IEnumerable)collection).Cast<JsonConverter>());
		Assert.Throws<ArgumentException>(() => ConverterCollection.Create([new IntConverter(), new IntConverter()]));
		Assert.Throws<ArgumentException>(() => ConverterCollection.Create([null!]));
	}

	[Test]
	public void ConverterTypeCollectionValidatesAndReplacesDuplicates()
	{
		ConverterTypeCollection types = [typeof(IntConverter), typeof(IntConverter)];
		Assert.Single(types);
		Assert.Equal(typeof(IntConverter), types.Single().Type);
		Assert.Single(((IEnumerable)types).Cast<ConverterTypeCollection.ConverterType>());
		Assert.Throws<ArgumentException>(() => ConverterTypeCollection.Create([typeof(string)]));
		Assert.Throws<ArgumentException>(() => ConverterTypeCollection.Create([typeof(JsonConverter<int>)]));
		Assert.Throws<ArgumentException>(() => ConverterTypeCollection.Create([default]));
	}

	[Test]
	public void FactorySettingsRejectInvalidArrays()
	{
		Assert.Throws<ArgumentException>(() => new JsonSerializer { ConverterFactories = default });
		Assert.Throws<ArgumentException>(() => new JsonSerializer { ConverterFactories = [null!] });
	}

	[Test]
	public void RegisteredClosedConverterNeedsNoAssociatedShape()
	{
		JsonSerializer serializer = new() { ConverterTypes = [typeof(IntConverter)] };
		Assert.Equal("42", serializer.Serialize<int, ConverterCollectionTests>(42));
		Assert.Equal(42, serializer.Deserialize<int, ConverterCollectionTests>("42"));
	}

	private sealed class IntConverter : JsonConverter<int>
	{
		public IntConverter()
		{
		}

		public override void Write(ref JsonWriter writer, in int value, SerializationContext context) => writer.WriteNumberValue(value);

		public override int Read(ref JsonReader reader, SerializationContext context) => int.Parse(reader.ReadNumberToken(), System.Globalization.CultureInfo.InvariantCulture);
	}
}
