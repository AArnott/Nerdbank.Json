using Nerdbank.Json;
using PolyType;

// <SimpleObject>
[GenerateShape]
public partial class Person
{
	public string? Name { get; set; }

	public int Age { get; set; }
}
// </SimpleObject>

// <SimpleObjectRoundtrip>
JsonSerializer serializer = new();

string json = serializer.Serialize(new Person { Name = "Ada", Age = 37 });
Person person = serializer.Deserialize<Person>(json);
// </SimpleObjectRoundtrip>
