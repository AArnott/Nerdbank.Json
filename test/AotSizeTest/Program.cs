// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // Keep the minimal size fixture in one file.

using Nerdbank.Json;
using PolyType;

JsonSerializer serializer = new();
AddressBook original = new([new("Ada", 36), new("Grace", 45)]);
string json = serializer.Serialize(original);
AddressBook restored = serializer.Deserialize<AddressBook>(json)
	?? throw new InvalidOperationException("The address book was null.");

if (!original.People.SequenceEqual(restored.People) || serializer.Serialize(restored) != json)
{
	throw new InvalidOperationException("The address book did not round-trip.");
}

Console.WriteLine(json);

[GenerateShape]
internal partial record AddressBook(Person[] People);

internal partial record Person(string Name, int Age);
