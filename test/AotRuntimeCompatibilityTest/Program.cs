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

static ITypeShape<T> GetShape<T, TProvider>()
	where TProvider : IShapeable<T>
	=> TProvider.GetTypeShape();
