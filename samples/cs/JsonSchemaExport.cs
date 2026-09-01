using Nerdbank.Json;
using PolyType;
using PolyType.Abstractions;

// <JsonSchemaExport>
[GenerateShape]
public partial record Product(string Name, decimal Price)
{
	public string? Description { get; set; }
}

JsonSerializer serializer = new();

// Produces a JSON Schema (draft 2020-12) document describing the wire representation.
string schema = serializer.GetJsonSchema<Product>();
// </JsonSchemaExport>

// <JsonSchemaCustomConverter>
// A custom converter can describe its own representation by overriding GetJsonSchema.
public sealed class TemperatureConverter : JsonConverter<Temperature>
{
	public override void Write(ref JsonWriter writer, Temperature? value, SerializationContext context)
		=> writer.WriteNumberValue(value?.Celsius ?? 0);

	public override Temperature? Read(ref JsonReader reader, SerializationContext context)
		=> new(double.Parse(reader.ReadNumberToken()));

	public override JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape)
		=> new JsonSchema().Set("type", "number").Set("description", "Temperature in degrees Celsius.");
}

[GenerateShape]
public partial record Temperature(double Celsius);
// </JsonSchemaCustomConverter>
System.Console.WriteLine(schema);
