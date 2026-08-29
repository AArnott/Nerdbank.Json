// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal schema helpers are intentionally undocumented in this file.

using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Nerdbank.Json;

/// <summary>
/// Produces JSON Schema fragments for the built-in scalar and simple types.
/// </summary>
internal static class JsonSchemaScalars
{
	internal static bool TryGetScalarSchema(Type type, out JsonSchema schema)
	{
		schema = new JsonSchema();
#if NET8_0_OR_GREATER
		if (type == typeof(Half))
		{
			schema.Set("type", "number");
			return true;
		}

		if (type == typeof(Int128) || type == typeof(UInt128))
		{
			schema.Set("type", "integer");
			return true;
		}

		if (type == typeof(DateOnly))
		{
			schema.Set("type", "string").Set("format", "date");
			return true;
		}

		if (type == typeof(TimeOnly))
		{
			schema.Set("type", "string").Set("format", "time");
			return true;
		}

		if (type == typeof(Rune))
		{
			schema.Set("type", "string").Set("minLength", 1L);
			return true;
		}
#endif

		if (type == typeof(string) || type == typeof(CultureInfo) || type == typeof(Encoding) || type == typeof(Version))
		{
			schema.Set("type", "string");
		}
		else if (type == typeof(char))
		{
			schema.Set("type", "string").Set("minLength", 1L);
		}
		else if (type == typeof(bool))
		{
			schema.Set("type", "boolean");
		}
		else if (type == typeof(byte))
		{
			IntegerSchema(schema, 0, byte.MaxValue);
		}
		else if (type == typeof(sbyte))
		{
			IntegerSchema(schema, sbyte.MinValue, sbyte.MaxValue);
		}
		else if (type == typeof(short))
		{
			IntegerSchema(schema, short.MinValue, short.MaxValue);
		}
		else if (type == typeof(ushort))
		{
			IntegerSchema(schema, ushort.MinValue, ushort.MaxValue);
		}
		else if (type == typeof(int))
		{
			IntegerSchema(schema, int.MinValue, int.MaxValue);
		}
		else if (type == typeof(uint))
		{
			IntegerSchema(schema, uint.MinValue, uint.MaxValue);
		}
		else if (type == typeof(long) || type == typeof(ulong) || type == typeof(BigInteger) || type == typeof(Color))
		{
			schema.Set("type", "integer");
		}
		else if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
		{
			schema.Set("type", "number");
		}
		else if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
		{
			schema.Set("type", "string").Set("format", "date-time");
		}
		else if (type == typeof(TimeSpan))
		{
			schema.Set("type", "string").Set("$comment", "A .NET TimeSpan formatted as [-][d.]hh:mm:ss[.fffffff].");
		}
		else if (type == typeof(Guid))
		{
			schema.Set("type", "string").Set("format", "uuid");
		}
		else if (type == typeof(Uri))
		{
			schema.Set("type", "string").Set("format", "uri-reference");
		}
		else if (type == typeof(byte[]) || type == typeof(Memory<byte>) || type == typeof(ReadOnlyMemory<byte>))
		{
			schema.Set("type", "string").Set("contentEncoding", "base64");
		}
		else if (type == typeof(Point))
		{
			schema.Set("type", "array")
				.SetSchemas("prefixItems", [new JsonSchema().Set("type", "integer"), new JsonSchema().Set("type", "integer")])
				.Set("minItems", 2L)
				.Set("maxItems", 2L)
				.Set("items", new JsonSchema().Set("type", "integer"));
		}
		else
		{
			return false;
		}

		return true;
	}

	private static void IntegerSchema(JsonSchema schema, long min, long max)
	{
		schema.Set("type", "integer").Set("minimum", min).Set("maximum", max);
	}
}
