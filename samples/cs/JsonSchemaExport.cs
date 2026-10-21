
using JsonSchemaArray = System.Text.Json.Nodes.JsonArray;
using JsonSchemaNode = System.Text.Json.Nodes.JsonNode;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;
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
string schema = serializer.GetJsonSchema<Product>().ToJsonString();
// </JsonSchemaExport>

// <JsonSchemaDraft7>
string draft7Schema = serializer.GetJsonSchema<Product>(
	new JsonSchemaOptions { Dialect = JsonSchemaDialect.Draft7 }).ToJsonString();
// </JsonSchemaDraft7>

// <JsonSchemaCustomConverter>
// A custom converter can describe its own representation by overriding GetJsonSchema.
public sealed class TemperatureConverter : JsonConverter<Temperature>
{
	public override void Write(ref JsonWriter writer, in Temperature? value, SerializationContext context)
		=> writer.Write(value?.Celsius ?? 0);

	public override Temperature? Read(ref JsonReader reader, SerializationContext context)
		=> new(double.Parse(reader.ReadNumberToken()));

	public override JsonSchemaObject? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape)
		=> new() { ["type"] = "number", ["description"] = "Temperature in degrees Celsius." };
}

[GenerateShape]
public partial record Temperature(double Celsius);
// </JsonSchemaCustomConverter>
System.Console.WriteLine(schema);
