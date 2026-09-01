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
	private readonly bool allowCycles;
	private HashSet<int>? inProgressUnregisteredIds;
	private int nextReferenceId = 1;

	internal JsonReferenceEqualityTracker(ReferencePreservationMode mode)
	{
		this.allowCycles = mode == ReferencePreservationMode.AllowCycles;
	}

	internal void WriteObject(ref JsonWriter writer, object value, JsonConverter inner, SerializationContext context)
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
		inner.WriteObject(ref writer, value, context);
		writer.WriteEndObject();

		this.serializedObjects[value] = (assignedReferenceId, true);
	}

	internal T ReadObject<T>(ref JsonReader reader, JsonConverter<T> inner, SerializationContext context)
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

			if (this.deserializedObjects.TryGetValue(referenceId, out object? referenced) && referenced is not null)
			{
				return (T)referenced;
			}

			if (this.inProgressUnregisteredIds is not null && this.inProgressUnregisteredIds.Contains(referenceId))
			{
				throw new FormatException($"Reference id '{referenceId}' refers to an immutable or constructor-bound object that is still being constructed. Such objects cannot participate in a reference cycle.");
			}

			throw new FormatException($"Reference id '{referenceId}' was not previously defined.");
		}

		if (firstPropertyName != "$id")
		{
			throw new FormatException("Reference-preserved values must begin with either '$id' or '$ref'.");
		}

		int assignedReferenceId = ReadReferenceId(ref reader);
		if (this.deserializedObjects.ContainsKey(assignedReferenceId) ||
			(this.inProgressUnregisteredIds is not null && this.inProgressUnregisteredIds.Contains(assignedReferenceId)))
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

		T value;
		if (this.allowCycles && inner is IJsonReferencePreservingConverter<T> earlyRegistration && !reader.IsNextTokenNull())
		{
			// Register the object before its members are populated so that a back-reference resolves to it.
			value = earlyRegistration.CreateReferenceInstance();
			this.deserializedObjects.Add(assignedReferenceId, value);
			earlyRegistration.PopulateReference(ref reader, ref value, context);
		}
		else if (this.allowCycles)
		{
			// The inner converter cannot register an instance before populating it (for example, immutable
			// constructor-bound objects). Track the id so a cyclic back-reference produces a clear error.
			(this.inProgressUnregisteredIds ??= []).Add(assignedReferenceId);
			try
			{
				value = inner.Read(ref reader, context) ?? throw new FormatException("Reference-preserved values may not deserialize to null.");
			}
			finally
			{
				this.inProgressUnregisteredIds.Remove(assignedReferenceId);
			}

			this.deserializedObjects.Add(assignedReferenceId, value);
		}
		else
		{
			value = inner.Read(ref reader, context) ?? throw new FormatException("Reference-preserved values may not deserialize to null.");
			this.deserializedObjects.Add(assignedReferenceId, value);
		}

		if (!reader.TryReadEndObject())
		{
			throw new FormatException("Reference-preserved values may only contain '$id' and '$value' properties.");
		}

		return value;
	}

	internal async ValueTask WriteObjectAsync(JsonAsyncWriter writer, object value, JsonConverter inner, SerializationContext context)
	{
		if (this.TryGetSerializedObject(value, out int referenceId))
		{
			JsonWriter refWriter = writer.CreateWriter();
			refWriter.WriteStartObject();
			refWriter.WritePropertyName("$ref");
			refWriter.WriteNumberValue(referenceId);
			refWriter.WriteEndObject();
			writer.ReturnWriter(ref refWriter);
			await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
			return;
		}

		int assignedReferenceId = this.nextReferenceId++;
		this.serializedObjects.Add(value, (assignedReferenceId, false));

		JsonWriter headerWriter = writer.CreateWriter();
		headerWriter.WriteStartObject();
		headerWriter.WritePropertyName("$id");
		headerWriter.WriteNumberValue(assignedReferenceId);
		headerWriter.WriteValueSeparator();
		headerWriter.WritePropertyName("$value");
		writer.ReturnWriter(ref headerWriter);

		await inner.WriteObjectAsync(writer, value, context).ConfigureAwait(false);

		JsonWriter footerWriter = writer.CreateWriter();
		footerWriter.WriteEndObject();
		writer.ReturnWriter(ref footerWriter);

		this.serializedObjects[value] = (assignedReferenceId, true);
		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}

	internal async ValueTask<T> ReadObjectAsync<T>(JsonAsyncReader reader, JsonConverter<T> inner, SerializationContext context)
	{
		await reader.ReadStartObjectAsync(context).ConfigureAwait(false);
		string firstPropertyName = await reader.ReadPropertyNameAsync(context).ConfigureAwait(false);

		if (firstPropertyName == "$ref")
		{
			int referenceId = await ReadReferenceIdAsync(reader, context).ConfigureAwait(false);
			if (!await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
			{
				throw new FormatException("A reference object may only contain a '$ref' property.");
			}

			if (this.deserializedObjects.TryGetValue(referenceId, out object? referenced) && referenced is not null)
			{
				return (T)referenced;
			}

			if (this.inProgressUnregisteredIds is not null && this.inProgressUnregisteredIds.Contains(referenceId))
			{
				throw new FormatException($"Reference id '{referenceId}' refers to an immutable or constructor-bound object that is still being constructed. Such objects cannot participate in a reference cycle.");
			}

			throw new FormatException($"Reference id '{referenceId}' was not previously defined.");
		}

		if (firstPropertyName != "$id")
		{
			throw new FormatException("Reference-preserved values must begin with either '$id' or '$ref'.");
		}

		int assignedReferenceId = await ReadReferenceIdAsync(reader, context).ConfigureAwait(false);
		if (this.deserializedObjects.ContainsKey(assignedReferenceId) ||
			(this.inProgressUnregisteredIds is not null && this.inProgressUnregisteredIds.Contains(assignedReferenceId)))
		{
			throw new FormatException($"Reference id '{assignedReferenceId}' was assigned more than once.");
		}

		await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
		string secondPropertyName = await reader.ReadPropertyNameAsync(context).ConfigureAwait(false);
		if (secondPropertyName != "$value")
		{
			throw new FormatException("Reference-preserved values with '$id' must also include a '$value' property.");
		}

		bool nextIsNull = await reader.PeekNextByteAsync().ConfigureAwait(false) == 'n';

		T value;
		if (this.allowCycles && inner is IJsonReferencePreservingConverter<T> earlyRegistration && !nextIsNull)
		{
			// Register the object before its members are populated so that a back-reference resolves to it.
			value = earlyRegistration.CreateReferenceInstance();
			this.deserializedObjects.Add(assignedReferenceId, value);
			value = await earlyRegistration.PopulateReferenceAsync(reader, value, context).ConfigureAwait(false);
			this.deserializedObjects[assignedReferenceId] = value;
		}
		else if (this.allowCycles)
		{
			(this.inProgressUnregisteredIds ??= []).Add(assignedReferenceId);
			try
			{
				value = await inner.ReadAsync(reader, context).ConfigureAwait(false) ?? throw new FormatException("Reference-preserved values may not deserialize to null.");
			}
			finally
			{
				this.inProgressUnregisteredIds.Remove(assignedReferenceId);
			}

			this.deserializedObjects.Add(assignedReferenceId, value);
		}
		else
		{
			value = await inner.ReadAsync(reader, context).ConfigureAwait(false) ?? throw new FormatException("Reference-preserved values may not deserialize to null.");
			this.deserializedObjects.Add(assignedReferenceId, value);
		}

		if (!await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
		{
			throw new FormatException("Reference-preserved values may only contain '$id' and '$value' properties.");
		}

		return value;
	}

	private static async ValueTask<int> ReadReferenceIdAsync(JsonAsyncReader reader, SerializationContext context)
	{
		await reader.BufferNextValueAsync(context).ConfigureAwait(false);
		JsonReader sync = reader.CreateBufferedReader();
		string token = sync.ReadNumberToken();
		reader.ReturnReader(ref sync);
		if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int referenceId) || referenceId <= 0)
		{
			throw new FormatException($"'{token}' is not a valid positive reference id.");
		}

		return referenceId;
	}

	private bool TryGetSerializedObject(object value, out int referenceId)
	{
		if (!this.serializedObjects.TryGetValue(value, out (int ReferenceId, bool Done) slot))
		{
			referenceId = 0;
			return false;
		}

		if (!slot.Done && !this.allowCycles)
		{
			throw new InvalidOperationException("Reference cycles are not supported when reference preservation is set to RejectCycles. Use AllowCycles to serialize cyclic graphs.");
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
