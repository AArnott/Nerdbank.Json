// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Nerdbank.Json;
using PolyType;
using Xunit;

public partial class JsonObjectSerializerTests
{
	[Test]
	public void Serialize_Delegate_ThrowsNotSupportedException()
	{
		JsonSerializer serializer = new();
		FunctionContainer value = new() { Callback = static () => { } };

		NotSupportedException exception = Assert.Throws<NotSupportedException>(() => serializer.Serialize(value));
		Assert.Contains("delegate", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[GenerateShape]
	internal partial class FunctionContainer
	{
		public Action? Callback { get; set; }
	}
}
