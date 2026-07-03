// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

public partial class JsonObjectSerializerTests
{
	[Test]
	public void SerializeDeserialize_SurrogateBackedType()
	{
		JsonSerializer serializer = new();
		SurrogateBackedType value = new(3, 5);

		string json = serializer.Serialize(value);
		SurrogateBackedType? roundTripped = serializer.Deserialize<SurrogateBackedType>(json);

		Assert.Equal("""{"a":3,"b":5}""", json);
		Assert.NotNull(roundTripped);
		Assert.Equal(8, roundTripped.Sum);
	}

	[GenerateShape(Marshaler = typeof(Marshaler))]
	internal partial class SurrogateBackedType
	{
		private readonly int a;
		private readonly int b;

		internal SurrogateBackedType(int a, int b)
		{
			this.a = a;
			this.b = b;
		}

		internal int Sum => this.a + this.b;

		internal record struct MarshaledType(int A, int B);

		internal class Marshaler : IMarshaler<SurrogateBackedType, MarshaledType?>
		{
			public SurrogateBackedType? Unmarshal(MarshaledType? surrogate)
				=> surrogate.HasValue ? new(surrogate.Value.A, surrogate.Value.B) : null;

			public MarshaledType? Marshal(SurrogateBackedType? value)
				=> value is null ? null : new(value.a, value.b);
		}
	}
}
