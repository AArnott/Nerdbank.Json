// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal converter members are intentionally undocumented in this file.

namespace Nerdbank.Json;

/// <summary>
/// Reads and writes the native, dependency-free <see cref="JsonValue"/> document object model, preserving exact JSON
/// number tokens and enforcing depth, member-count, and duplicate-property limits.
/// </summary>
internal sealed class JsonValueConverter : JsonConverter<JsonValue>
{
	internal static readonly JsonValueConverter Instance = new();

	public override bool PreferAsyncSerialization => true;

	public override JsonValue? Read(ref JsonReader reader, SerializationContext context)
	{
		char token = reader.PeekValueToken();
		switch (token)
		{
			case '{':
				return this.ReadJsonObject(ref reader, context);
			case '[':
				return this.ReadArray(ref reader, context);
			case '"':
				return new JsonString(reader.ReadRequiredString());
			case 't':
			case 'f':
				return reader.ReadBoolean() ? JsonBoolean.True : JsonBoolean.False;
			case 'n':
				if (!reader.TryReadNull())
				{
					throw new FormatException("Expected a JSON null literal.");
				}

				return JsonNull.Instance;
			default:
				return new JsonNumber(reader.ReadNumberToken());
		}
	}

	public override void Write(ref JsonWriter writer, JsonValue? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		switch (value.Kind)
		{
			case JsonValueKind.Null:
				writer.WriteNullValue();
				break;
			case JsonValueKind.Boolean:
				writer.WriteBooleanValue(((JsonBoolean)value).Value);
				break;
			case JsonValueKind.String:
				writer.WriteStringValue(((JsonString)value).Value);
				break;
			case JsonValueKind.Number:
				writer.WriteRawValue(((JsonNumber)value).RawToken);
				break;
			case JsonValueKind.Array:
				this.WriteArray(ref writer, (JsonArray)value, context);
				break;
			case JsonValueKind.Object:
				this.WriteJsonObject(ref writer, (JsonObject)value, context);
				break;
		}
	}

	public override async ValueTask<JsonValue?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
	{
		Requires.NotNull(reader);
		int token = await reader.PeekNextByteAsync().ConfigureAwait(false);
		switch (token)
		{
			case '{':
				return await this.ReadJsonObjectAsync(reader, context).ConfigureAwait(false);
			case '[':
				return await this.ReadArrayAsync(reader, context).ConfigureAwait(false);
			default:
				await reader.BufferNextValueAsync(context).ConfigureAwait(false);
				JsonReader sync = reader.CreateBufferedReader();
				JsonValue? scalar = this.Read(ref sync, context);
				reader.ReturnReader(ref sync);
				return scalar;
		}
	}

	public override async ValueTask WriteAsync(JsonAsyncWriter writer, JsonValue? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (value is null || value.Kind == JsonValueKind.Null)
		{
			await writer.WriteNullAsync(context).ConfigureAwait(false);
			return;
		}

		switch (value.Kind)
		{
			case JsonValueKind.Array:
				await this.WriteArrayAsync(writer, (JsonArray)value, context).ConfigureAwait(false);
				break;
			case JsonValueKind.Object:
				await this.WriteJsonObjectAsync(writer, (JsonObject)value, context).ConfigureAwait(false);
				break;
			default:
				JsonWriter syncWriter = writer.CreateWriter();
				this.Write(ref syncWriter, value, context);
				writer.ReturnWriter(ref syncWriter);
				await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
				break;
		}
	}

	public override JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => new();

	private JsonObject ReadJsonObject(ref JsonReader reader, SerializationContext context)
	{
		context.DepthStep();
		int maxMembers = context.Security.MaxObjectMemberCount;
		PropertyCollisionDetection collision = new(StringComparer.Ordinal);
		List<KeyValuePair<string, JsonValue>> members = [];
		reader.ReadStartObject();
		if (!reader.TryReadEndObject())
		{
			while (true)
			{
				string name = reader.ReadRequiredString();
				collision.MarkAsRead(name);
				reader.ReadNameSeparator();
				if (members.Count >= maxMembers)
				{
					throw new FormatException($"The JSON object exceeds the configured maximum of {maxMembers} members.");
				}

				members.Add(new(name, this.Read(ref reader, context)!));
				if (reader.TryReadEndObject())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
		}

		return new JsonObject(members);
	}

	private JsonArray ReadArray(ref JsonReader reader, SerializationContext context)
	{
		context.DepthStep();
		List<JsonValue> items = [];
		reader.ReadStartArray();
		if (!reader.TryReadEndArray())
		{
			while (true)
			{
				items.Add(this.Read(ref reader, context)!);
				if (reader.TryReadEndArray())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
		}

		return new JsonArray([.. items], takeOwnership: true);
	}

	private void WriteArray(ref JsonWriter writer, JsonArray value, SerializationContext context)
	{
		context.DepthStep();
		writer.WriteStartArray();
		for (int i = 0; i < value.Count; i++)
		{
			if (i > 0)
			{
				writer.WriteValueSeparator();
			}

			this.Write(ref writer, value[i], context);
		}

		writer.WriteEndArray();
	}

	private void WriteJsonObject(ref JsonWriter writer, JsonObject value, SerializationContext context)
	{
		context.DepthStep();
		writer.WriteStartObject();
		bool first = true;
		foreach (KeyValuePair<string, JsonValue> member in value)
		{
			if (!first)
			{
				writer.WriteValueSeparator();
			}

			first = false;
			writer.WritePropertyName(member.Key);
			this.Write(ref writer, member.Value, context);
		}

		writer.WriteEndObject();
	}

	private async ValueTask<JsonValue> ReadJsonObjectAsync(JsonAsyncReader reader, SerializationContext context)
	{
		context.DepthStep();
		int maxMembers = context.Security.MaxObjectMemberCount;
		PropertyCollisionDetection collision = new(StringComparer.Ordinal);
		List<KeyValuePair<string, JsonValue>> members = [];
		await reader.ReadStartObjectAsync(context).ConfigureAwait(false);
		if (!await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
		{
			while (true)
			{
				string name = await reader.ReadPropertyNameAsync(context).ConfigureAwait(false);
				collision.MarkAsRead(name);
				if (members.Count >= maxMembers)
				{
					throw new FormatException($"The JSON object exceeds the configured maximum of {maxMembers} members.");
				}

				members.Add(new(name, (await reader.ReadValueAsync(this, context).ConfigureAwait(false))!));
				if (await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
				{
					break;
				}

				await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
			}
		}

		return new JsonObject(members);
	}

	private async ValueTask<JsonValue> ReadArrayAsync(JsonAsyncReader reader, SerializationContext context)
	{
		context.DepthStep();
		List<JsonValue> items = [];
		await reader.ReadStartArrayAsync(context).ConfigureAwait(false);
		if (!await reader.TryReadEndArrayAsync(context).ConfigureAwait(false))
		{
			while (true)
			{
				items.Add((await reader.ReadValueAsync(this, context).ConfigureAwait(false))!);
				if (await reader.TryReadEndArrayAsync(context).ConfigureAwait(false))
				{
					break;
				}

				await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
			}
		}

		return new JsonArray([.. items], takeOwnership: true);
	}

	private async ValueTask WriteArrayAsync(JsonAsyncWriter writer, JsonArray value, SerializationContext context)
	{
		context.DepthStep();
		JsonWriter syncWriter = writer.CreateWriter();
		syncWriter.WriteStartArray();
		writer.ReturnWriter(ref syncWriter);

		for (int i = 0; i < value.Count; i++)
		{
			if (i > 0)
			{
				syncWriter = writer.CreateWriter();
				syncWriter.WriteValueSeparator();
				writer.ReturnWriter(ref syncWriter);
			}

			await this.WriteAsync(writer, value[i], context).ConfigureAwait(false);
		}

		syncWriter = writer.CreateWriter();
		syncWriter.WriteEndArray();
		writer.ReturnWriter(ref syncWriter);
		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}

	private async ValueTask WriteJsonObjectAsync(JsonAsyncWriter writer, JsonObject value, SerializationContext context)
	{
		context.DepthStep();
		JsonWriter syncWriter = writer.CreateWriter();
		syncWriter.WriteStartObject();
		writer.ReturnWriter(ref syncWriter);

		bool first = true;
		foreach (KeyValuePair<string, JsonValue> member in value)
		{
			syncWriter = writer.CreateWriter();
			if (!first)
			{
				syncWriter.WriteValueSeparator();
			}

			syncWriter.WritePropertyName(member.Key);
			writer.ReturnWriter(ref syncWriter);
			first = false;
			await this.WriteAsync(writer, member.Value, context).ConfigureAwait(false);
		}

		syncWriter = writer.CreateWriter();
		syncWriter.WriteEndObject();
		writer.ReturnWriter(ref syncWriter);
		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}
}
