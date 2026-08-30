using Nerdbank.Json;
using PolyType;

// <TargetedDeserialization>
[GenerateShape]
public partial record Weather(Location Location, Reading[] Readings);

[GenerateShape]
public partial record Location(string City, string Country);

[GenerateShape]
public partial record Reading(string Kind, double Value);

JsonSerializer serializer = new();
string json =
	"""
	{"location":{"city":"Seattle","country":"US"},"readings":[{"kind":"temp","value":21.5},{"kind":"humidity","value":0.42}]}
	""";

// Ergonomic expression overload: parsed but never compiled, so it is NativeAOT-safe.
// CLR member names are translated to serialized names automatically.
string city = serializer.DeserializeAt<Weather, string>(json, w => w.Location.City)!;

// Pre-parsed path: build once, reuse on hot paths. Names are the serialized (wire) names.
JsonPath secondReadingValue = JsonPath.Root.Member("readings").Index(1).Member("value");
double humidity = serializer.DeserializeAt<double, Weather>(json, secondReadingValue);

// A missing path can throw or return the default value.
string? missing = serializer.DeserializeAt<Weather, string>(json, w => w.Location.Country, MissingPathBehavior.ReturnDefault);
// </TargetedDeserialization>
System.Console.WriteLine($"{city} {humidity} {missing}");

JsonSerializer pointSerializer = new()
{
	Converters = new ConverterCollection([new PointConverter()]),
};
string pointJson = pointSerializer.Serialize(new Sensor(new Point(3, 7)), Sensor.GetTypeShape());
int y = pointSerializer.DeserializeAt<Sensor, int>(pointJson, s => s.Position.Y);
System.Console.WriteLine($"{pointJson} y={y}");

[GenerateShape]
public partial record Sensor(Point Position);

[GenerateShape]
public partial record Point(int X, int Y);

// <NavigationHook>
// A custom converter that stores a Point as the positional array [x, y] overrides TryNavigate so a
// path such as s => s.Position.Y can traverse *through* the converter's representation. Without the
// override the navigator would only understand plain objects and arrays.
public sealed class PointConverter : JsonConverter<Point>
{
	public override void Write(ref JsonWriter writer, Point? value, SerializationContext context)
	{
		writer.WriteStartArray();
		writer.WriteNumberValue(value!.X);
		writer.WriteValueSeparator();
		writer.WriteNumberValue(value.Y);
		writer.WriteEndArray();
	}

	public override Point? Read(ref JsonReader reader, SerializationContext context)
	{
		reader.ReadStartArray();
		int x = int.Parse(reader.ReadNumberToken(), System.Globalization.CultureInfo.InvariantCulture);
		reader.ReadValueSeparator();
		int y = int.Parse(reader.ReadNumberToken(), System.Globalization.CultureInfo.InvariantCulture);
		reader.ReadEndArray();
		return new Point(x, y);
	}

	public override bool TryNavigate(ref JsonReader reader, in JsonNavigationSegment segment, JsonNavigationOptions options)
	{
		// The segment carries the serialized member name (naming policy already applied).
		int index =
			options.NameComparer.Equals(segment.Name, "x") ? 0 :
			options.NameComparer.Equals(segment.Name, "y") ? 1 : -1;
		if (index < 0)
		{
			return false;
		}

		reader.ReadStartArray();
		if (reader.TryReadEndArray())
		{
			return false;
		}

		for (int i = 0; i < index; i++)
		{
			reader.SkipValue();
			if (reader.TryReadEndArray())
			{
				return false;
			}

			reader.ReadValueSeparator();
		}

		return true;
	}
}
// </NavigationHook>
