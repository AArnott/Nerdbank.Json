// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // Keep the nested record regression models together.

using PolyType;

[GenerateShape]
internal partial record AddressBook(Person[] People, Person? Favorite);

internal partial record Person(string Name, int Age);
