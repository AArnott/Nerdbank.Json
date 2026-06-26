// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal reference wrapper type is intentionally undocumented.
#pragma warning disable SA1649 // File name is close enough for this focused internal type.

namespace Nerdbank.Json;

internal sealed class ReferencePreservingJsonConverter<T>(JsonConverter<T> inner) : JsonConverter<T>
{
	public override void Write(ref JsonWriter writer, T? value, JsonSerializer serializer)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		serializer.ReferenceTracker.WriteObject(ref writer, value, inner, serializer);
	}

	public override T? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		return serializer.ReferenceTracker.ReadObject(ref reader, inner, serializer);
	}
}
