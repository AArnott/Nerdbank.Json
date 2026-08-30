// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type

using PolyType;

namespace Nerdbank.Json.AspNetCore.Tests;

[GenerateShape]
public partial record Person(string Name, int Age);

[GenerateShape]
public partial record Bag(int[] Numbers);

/// <summary>A type intentionally not annotated for shape generation, used to test provider misses.</summary>
public sealed class Unregistered
{
	public int X { get; set; }
}
