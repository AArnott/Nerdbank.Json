using Nerdbank.Json;
using PolyType;
using PolyType.Abstractions;

// <RuntimeUnions>
[GenerateShape]
public partial record Drawing;

[GenerateShape]
public partial record Circle(double Radius) : Drawing;

[GenerateShape]
public partial record Square(double Side) : Drawing;

static ITypeShape<T> ShapeOf<T>()
	where T : IShapeable<T> => T.GetTypeShape();

// Configure a union at runtime by supplying explicit, source-generated case shapes.
JsonSerializer serializer = new()
{
	Unions = JsonUnionConfiguration.Default.WithUnion(
		JsonUnion<Drawing>.Create()
			.AddCase<Circle>("circle", ShapeOf<Circle>())
			.AddCase<Square>("square", ShapeOf<Square>())),
};

Drawing value = new Circle(2.5);

// Serializes using the default two-element envelope: ["circle",{"radius":2.5}].
string json = serializer.Serialize<Drawing>(value);
Drawing roundTripped = serializer.Deserialize<Drawing>(json)!;
// </RuntimeUnions>

// <DuckTypingUnions>
// The experimental duck-typing strategy omits the discriminator and selects a case from the
// unique set of required property names present in the JSON object.
JsonSerializer duckSerializer = new()
{
	Unions = JsonUnionConfiguration.Default.WithUnion(
		JsonUnion<Drawing>.Create()
			.AddCase<Circle>("circle", ShapeOf<Circle>())
			.AddCase<Square>("square", ShapeOf<Square>())
			.UseDuckTyping()),
};

// Serializes as the bare object {"radius":2.5}; deserialization matches the 'radius' property to Circle.
string duckJson = duckSerializer.Serialize<Drawing>(new Circle(2.5));
Drawing duckResult = duckSerializer.Deserialize<Drawing>(duckJson)!;
// </DuckTypingUnions>
System.Console.WriteLine($"{json} {roundTripped} {duckJson} {duckResult}");
