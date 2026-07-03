// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1204 // Keep comparer nested near its only caller in this focused helper.
#pragma warning disable SA1600 // Internal helper members are intentionally undocumented.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace Nerdbank.Json;

internal sealed class JsonReferenceEqualityTracker
{
	private readonly Dictionary<object, (int ReferenceId, bool Done)> serializedObjects = new(ReferenceEqualityComparer.Instance);
	private readonly Dictionary<int, object?> deserializedObjects = [];
	private int nextReferenceId = 1;

	internal void WriteObject(ref JsonWriter writer, object value, JsonConverter inner, JsonSerializer serializer)
	{
		if (this.TryGetSerializedObject(value, out int referenceId))
		{
			writer.WriteStartObject();
			writer.WritePropertyName("$ref");
			writer.WriteNumberValue(referenceId);
			writer.WriteEndObject();
			return;
		}

		int assignedReferenceId = this.nextReferenceId++;
		this.serializedObjects.Add(value, (assignedReferenceId, false));

		writer.WriteStartObject();
		writer.WritePropertyName("$id");
		writer.WriteNumberValue(assignedReferenceId);
		writer.WriteValueSeparator();
		writer.WritePropertyName("$value");
		inner.WriteObject(ref writer, value, serializer);
		writer.WriteEndObject();

		this.serializedObjects[value] = (assignedReferenceId, true);
	}

	internal T ReadObject<T>(ref JsonReader reader, JsonConverter<T> inner, JsonSerializer serializer)
	{
		reader.ReadStartObject();
		string firstPropertyName = reader.ReadRequiredString();
		reader.ReadNameSeparator();

		if (firstPropertyName == "$ref")
		{
			int referenceId = ReadReferenceId(ref reader);
			if (!reader.TryReadEndObject())
			{
				throw new FormatException("A reference object may only contain a '$ref' property.");
			}

			if (!this.deserializedObjects.TryGetValue(referenceId, out object? referenced) || referenced is null)
			{
				throw new FormatException($"Reference id '{referenceId}' was not previously defined.");
			}

			return (T)referenced;
		}

		if (firstPropertyName != "$id")
		{
			throw new FormatException("Reference-preserved values must begin with either '$id' or '$ref'.");
		}

		int assignedReferenceId = ReadReferenceId(ref reader);
		if (this.deserializedObjects.ContainsKey(assignedReferenceId))
		{
			throw new FormatException($"Reference id '{assignedReferenceId}' was assigned more than once.");
		}

		reader.ReadValueSeparator();
		string secondPropertyName = reader.ReadRequiredString();
		reader.ReadNameSeparator();
		if (secondPropertyName != "$value")
		{
			throw new FormatException("Reference-preserved values with '$id' must also include a '$value' property.");
		}

		T value = inner.Read(ref reader, serializer) ?? throw new FormatException("Reference-preserved values may not deserialize to null.");
		this.deserializedObjects.Add(assignedReferenceId, value);

		if (!reader.TryReadEndObject())
		{
			throw new FormatException("Reference-preserved values may only contain '$id' and '$value' properties.");
		}

		return value;
	}

	private bool TryGetSerializedObject(object value, out int referenceId)
	{
		if (!this.serializedObjects.TryGetValue(value, out (int ReferenceId, bool Done) slot))
		{
			referenceId = 0;
			return false;
		}

		if (!slot.Done)
		{
			throw new InvalidOperationException("Reference cycles are not supported when reference preservation is enabled.");
		}

		referenceId = slot.ReferenceId;
		return true;
	}

	private static int ReadReferenceId(ref JsonReader reader)
	{
		string token = reader.ReadNumberToken();
		if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int referenceId) || referenceId <= 0)
		{
			throw new FormatException($"'{token}' is not a valid positive reference id.");
		}

		return referenceId;
	}

	private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
	{
		internal static readonly ReferenceEqualityComparer Instance = new();

		public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

		public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
	}
}
