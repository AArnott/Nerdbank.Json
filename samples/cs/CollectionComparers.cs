using System.Collections.Generic;
using Nerdbank.Json;
using PolyType;

// <CollectionComparers>
[GenerateShape]
public partial class HttpMessage
{
	// The attribute controls the comparer for the instance the deserializer constructs.
	// The initializer controls the comparer for instances application code constructs.
	[JsonCollectionComparer(typeof(CaseInsensitiveComparer))]
	public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

// A comparer type with a public parameterless constructor, activated through a
// source-generated shape rather than runtime reflection.
public sealed class CaseInsensitiveComparer : IEqualityComparer<string>
{
	public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);

	public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
}

JsonSerializer serializer = new();
HttpMessage message = serializer.Deserialize<HttpMessage>("""{"headers":{"Content-Type":"application/json"}}""")!;

// The reconstructed dictionary uses the case-insensitive comparer.
string contentType = message.Headers["CONTENT-TYPE"];
// </CollectionComparers>
System.Console.WriteLine(contentType);
