// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal reference wrapper type is intentionally undocumented.
#pragma warning disable SA1649 // File name is close enough for this focused internal type.

namespace Nerdbank.Json;

/// <summary>
/// Implemented by converters that can materialize an empty instance and then populate it, enabling early
/// registration for <see cref="ReferencePreservationMode.AllowCycles"/> so that back-references resolve.
/// </summary>
/// <typeparam name="T">The type produced by the converter.</typeparam>
internal interface IJsonReferencePreservingConverter<T>
{
	/// <summary>
	/// Creates an empty instance whose members have not yet been populated.
	/// </summary>
	/// <returns>The empty instance.</returns>
	T CreateReferenceInstance();

	/// <summary>
	/// Populates a previously created instance from the reader.
	/// </summary>
	/// <param name="reader">The reader positioned at the value to read into <paramref name="instance"/>.</param>
	/// <param name="instance">The instance to populate.</param>
	/// <param name="context">The active deserialization context.</param>
	void PopulateReference(ref JsonReader reader, ref T instance, SerializationContext context);
}

internal sealed class ReferencePreservingJsonConverter<T>(JsonConverter<T> inner) : JsonConverter<T>
{
	public override void Write(ref JsonWriter writer, T? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		Assumes.NotNull(context.ReferenceTracker);
		context.ReferenceTracker.WriteObject(ref writer, value, inner, context);
	}

	public override T? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		Assumes.NotNull(context.ReferenceTracker);
		return context.ReferenceTracker.ReadObject(ref reader, inner, context);
	}
}
