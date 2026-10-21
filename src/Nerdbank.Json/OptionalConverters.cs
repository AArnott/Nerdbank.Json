// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Dynamic;

namespace Nerdbank.Json;

/// <summary>Opt-in converters for dynamic objects and document models.</summary>
public static class OptionalConverters
{
	/// <summary>Adds converters for System.Text.Json document, element, and node values.</summary>
	/// <param name="serializer">The serializer to configure.</param>
	/// <returns>A new serializer with the converters registered.</returns>
	/// <exception cref="ArgumentException">A converter for one of these types is already registered.</exception>
	public static JsonSerializer WithSystemTextJsonConverters(this JsonSerializer serializer)
	{
		Requires.NotNull(serializer);
		return serializer with
		{
			Converters = [
				.. serializer.Converters,
				SystemTextJsonElementConverter.Instance,
				SystemTextJsonNodeConverter.Instance,
				SystemTextJsonDocumentConverter.Instance,
			],
		};
	}

	/// <summary>Adds a converter for values declared as <see cref="object"/>.</summary>
	/// <param name="serializer">The serializer to configure.</param>
	/// <returns>A new serializer with the converter registered.</returns>
	/// <remarks>
	/// Reads JSON into primitives, read-only dictionaries with string-valued object keys, and object arrays.
	/// Non-negative integers are read as <see cref="ulong"/>, negative integers as <see cref="long"/>,
	/// and fractional or exponent-form numbers as <see cref="double"/>.
	/// Other runtime types can be serialized only when their shape is available from the operation's provider.
	/// Use the native <see cref="JsonValue"/> model when exact number-token fidelity is required.
	/// </remarks>
	public static JsonSerializer WithObjectConverter(this JsonSerializer serializer)
	{
		Requires.NotNull(serializer);
		return serializer with { Converters = [.. serializer.Converters, ObjectConverter.Instance] };
	}

	/// <summary>Adds an object converter whose read-only dictionaries also support dynamic member access.</summary>
	/// <param name="serializer">The serializer to configure.</param>
	/// <returns>A new serializer with the converter registered.</returns>
	public static JsonSerializer WithDynamicObjectConverter(this JsonSerializer serializer)
	{
		Requires.NotNull(serializer);
		return serializer with { Converters = [.. serializer.Converters, new ObjectConverter(dynamicObjects: true)] };
	}

	/// <summary>Adds a converter for <see cref="ExpandoObject"/>.</summary>
	/// <param name="serializer">The serializer to configure.</param>
	/// <returns>A new serializer with the converter registered.</returns>
	/// <remarks>Nested objects are read-only dictionaries supporting dynamic member access.</remarks>
	public static JsonSerializer WithExpandoObjectConverter(this JsonSerializer serializer)
	{
		Requires.NotNull(serializer);
		return serializer with { Converters = [.. serializer.Converters, ExpandoObjectConverter.Instance] };
	}

	/// <summary>Adds converters for the native <see cref="JsonValue"/> hierarchy, preserving exact number tokens.</summary>
	/// <param name="serializer">The serializer to configure.</param>
	/// <returns>A new serializer with the converters registered.</returns>
	public static JsonSerializer WithJsonValueConverter(this JsonSerializer serializer)
	{
		Requires.NotNull(serializer);
		return serializer with
		{
			Converters = [
				.. serializer.Converters,
				JsonValueConverter.Instance,
				new JsonDomConverter<JsonObject>(),
				new JsonDomConverter<JsonArray>(),
				new JsonDomConverter<JsonString>(),
				new JsonDomConverter<JsonNumber>(),
				new JsonDomConverter<JsonBoolean>(),
				new JsonDomConverter<JsonNull>(),
			],
		};
	}
}
