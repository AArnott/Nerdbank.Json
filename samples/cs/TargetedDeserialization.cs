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
