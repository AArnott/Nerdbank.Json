using System.Collections.Generic;
using System.Dynamic;
using Nerdbank.Json;
using PolyType;

// <UntypedJson>
public static class UntypedJsonSample
{
	// Parse arbitrary JSON into the native, dependency-free DOM. No shape, no reflection,
	// and System.Text.Json is never rooted.
	public static void ParseIntoDom(JsonSerializer serializer)
	{
		JsonValue? value = serializer.DeserializeJsonValue("""{"name":"Ada","scores":[10,9.5,8]}""");

		var root = (JsonObject)value!;
		var name = (JsonString)root["name"];
		var scores = (JsonArray)root["scores"];

		System.Console.WriteLine(name.Value);            // Ada
		System.Console.WriteLine(scores.Count);          // 3

		// JSON number tokens are preserved exactly (e.g. "9.5" stays "9.5").
		System.Console.WriteLine(((JsonNumber)scores[1]).RawToken);

		string json = serializer.SerializeJsonValue(value);
		System.Console.WriteLine(json);
	}

	// Opt in to converters for object / ExpandoObject / JsonValue members.
	public static void ObjectAndExpando()
	{
		JsonSerializer serializer = new JsonSerializer().WithUntypedConverters();

		// A member typed as `object` deserializes to a boxed JsonValue.
		object? boxed = serializer.Deserialize<object, Witness>("""[1,2,3]""");
		System.Console.WriteLine(((JsonArray)boxed!).Count);

		// ExpandoObject round-trips as a JSON object (note: reconstruction is O(n^2)).
		ExpandoObject expando = serializer.Deserialize<ExpandoObject, Witness>("""{"a":1,"b":true}""")!;
		IDictionary<string, object?> members = expando;
		System.Console.WriteLine(members.Count);
	}

	[GenerateShapeFor<object>]
	[GenerateShapeFor<ExpandoObject>]
	private partial class Witness;
}
// </UntypedJson>
