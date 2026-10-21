// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>Serializes raw JSON without losing token spelling or borrowing recyclable input buffers.</summary>
internal sealed class RawJsonConverter : JsonConverter<RawJson>
{
	/// <summary>Gets the shared converter.</summary>
	internal static readonly RawJsonConverter Instance = new();

	/// <inheritdoc/>
	public override RawJson Read(ref JsonReader reader, SerializationContext context) => reader.ReadRaw(context).ToOwned();

	/// <inheritdoc/>
	public override void Write(ref JsonWriter writer, in RawJson value, SerializationContext context)
	{
		JsonReader validation = new(value.Json);
		validation.Skip(context);
		validation.EnsureFullyConsumed();
		context.CancellationToken.ThrowIfCancellationRequested();
		writer.WriteRaw(value.Json);
	}
}
