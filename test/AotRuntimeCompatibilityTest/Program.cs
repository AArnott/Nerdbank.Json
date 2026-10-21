// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Json;
using PolyType;

Payload value = new() { Number = 42 };
string json = new JsonSerializer().Serialize(value, GetShape<Payload, Payload>());
if (json != """{"number":42}""")
{
	throw new InvalidOperationException($"Unexpected JSON: {json}");
}

JsonSerializer serializer = new();
AddressBook original = new([new("Ada", 36), new("Grace", 45)], new("Ada", 36));
string addressBookJson = serializer.Serialize(original, GetShape<AddressBook, AddressBook>());
if (addressBookJson != """{"people":[{"name":"Ada","age":36},{"name":"Grace","age":45}],"favorite":{"name":"Ada","age":36}}""")
{
	throw new InvalidOperationException($"Unexpected address book JSON: {addressBookJson}");
}

AddressBook restored = serializer.Deserialize(addressBookJson, GetShape<AddressBook, AddressBook>())
	?? throw new InvalidOperationException("The address book was null.");
if (!original.People.SequenceEqual(restored.People) || original.Favorite != restored.Favorite || serializer.Serialize(restored) != addressBookJson)
{
	throw new InvalidOperationException("The nested records and array did not round-trip.");
}

Console.WriteLine(json);
Console.WriteLine(addressBookJson);

static ITypeShape<T> GetShape<T, TProvider>()
	where TProvider : IShapeable<T>
	=> TProvider.GetTypeShape();
