// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Nerdbank.Json;
using PolyType;
using Xunit;

public partial class JsonObjectSerializerTests
{
	[Fact]
	public void Serialize_UnionType_ThrowsNotSupportedException()
	{
		JsonSerializer serializer = new();
		Animal value = new Cat("Milo", 9);

		NotSupportedException exception = Assert.Throws<NotSupportedException>(() => serializer.Serialize(value));
		Assert.Contains("union", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[GenerateShape]
	[DerivedTypeShape(typeof(Cat))]
	internal abstract partial record Animal(string Name);

	[GenerateShape]
	internal partial record Cat(string Name, int Lives) : Animal(Name);
}
