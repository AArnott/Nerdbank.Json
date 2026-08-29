using Nerdbank.Json;
using PolyType;

// <ReferenceCycles>
[GenerateShape]
public partial class Employee
{
	public string? Name { get; set; }

	// A manager may (indirectly) reference this same employee, forming a cycle.
	public Employee? Manager { get; set; }

	public List<Employee> Reports { get; set; } = [];
}

JsonSerializer serializer = new()
{
	PreserveReferences = ReferencePreservationMode.AllowCycles,
};

Employee ceo = new() { Name = "Ada" };
Employee report = new() { Name = "Grace", Manager = ceo };
ceo.Reports.Add(report);

// Serializes the cycle using $id/$ref metadata.
string json = serializer.Serialize(ceo);

Employee roundTripped = serializer.Deserialize<Employee>(json)!;

// The cycle is restored: the report's manager is the same instance as the root.
bool restored = ReferenceEquals(roundTripped, roundTripped.Reports[0].Manager);
// </ReferenceCycles>
System.Console.WriteLine(restored);
