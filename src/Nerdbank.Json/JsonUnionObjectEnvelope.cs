// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal envelope helper.

using System.Globalization;
using System.Text;

namespace Nerdbank.Json;

internal interface IJsonUnionObjectConverter
{
	bool SupportsInlineObject { get; }

	void WriteObjectProperties(ref JsonWriter writer, object value, SerializationContext context, bool first);

	ValueTask WriteObjectPropertiesAsync(JsonAsyncWriter writer, object value, SerializationContext context, bool first);

	object? ReadObjectWithMetadata(ref JsonReader reader, SerializationContext context);

	object? ReadObjectWithMetadataAfterDiscriminator(ref JsonReader reader, SerializationContext context);

	ValueTask<object?> ReadObjectWithMetadataAsync(JsonAsyncReader reader, SerializationContext context);
}

/// <summary>
/// Resolves JSON object property discriminators, including the reserved base-contract alias.
/// </summary>
internal sealed class JsonUnionObjectEnvelope
{
	internal const string BaseAlias = "$base";
	internal const string DiscriminatorPropertyName = "$type";
	internal const string ValuePropertyName = "$value";

	private readonly Dictionary<string, JsonConverter> converters = new(StringComparer.Ordinal);
	private readonly List<(byte[] Utf8Alias, JsonConverter Converter)> utf8Converters = [];

	internal JsonUnionObjectEnvelope(JsonConverter? baseConverter)
	{
		if (baseConverter is not null)
		{
			this.converters.Add(BaseAlias, baseConverter);
			this.utf8Converters.Add((Encoding.UTF8.GetBytes(BaseAlias), baseConverter));
		}
	}

	internal static string GetAlias(int? tag, string? name) => tag?.ToString(CultureInfo.InvariantCulture) ?? name!;

	internal static void Write(ref JsonWriter writer, string alias, JsonConverter converter, object? value, SerializationContext context)
	{
		writer.WriteStartObject();
		writer.WritePropertyName(DiscriminatorPropertyName);
		writer.Write(alias);
		if (converter is IJsonUnionObjectConverter { SupportsInlineObject: true } objectConverter && value is not null)
		{
			objectConverter.WriteObjectProperties(ref writer, value, context, first: false);
		}
		else
		{
			writer.WriteValueSeparator();
			writer.WritePropertyName(ValuePropertyName);
			converter.WriteObject(ref writer, value, context);
		}

		writer.WriteEndObject();
	}

	internal static async ValueTask WriteAsync(JsonAsyncWriter writer, string alias, JsonConverter converter, object? value, SerializationContext context)
	{
		JsonWriter syncWriter = writer.CreateWriter();
		syncWriter.WriteStartObject();
		syncWriter.WritePropertyName(DiscriminatorPropertyName);
		syncWriter.Write(alias);
		if (converter is IJsonUnionObjectConverter { SupportsInlineObject: true } objectConverter && value is not null)
		{
			writer.ReturnWriter(ref syncWriter);
			await objectConverter.WriteObjectPropertiesAsync(writer, value, context, first: false).ConfigureAwait(false);
		}
		else
		{
			syncWriter.WriteValueSeparator();
			syncWriter.WritePropertyName(ValuePropertyName);
			writer.ReturnWriter(ref syncWriter);
			await converter.WriteObjectAsync(writer, value, context).ConfigureAwait(false);
		}

		syncWriter = writer.CreateWriter();
		syncWriter.WriteEndObject();
		writer.ReturnWriter(ref syncWriter);
		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}

	internal void Add(string alias, JsonConverter converter)
	{
		if (alias == BaseAlias || this.converters.ContainsKey(alias))
		{
			throw new NotSupportedException($"The union discriminator '{alias}' is reserved or ambiguous in object format.");
		}

		this.converters.Add(alias, converter);
		this.utf8Converters.Add((Encoding.UTF8.GetBytes(alias), converter));
	}

	internal JsonConverter ReadDiscriminator(ref JsonReader reader, bool allowOutOfOrderMetadataProperties, SerializationContext context)
	{
		JsonReader peek = reader.CreatePeekReader();
		return this.ReadDiscriminatorCore(ref peek, allowOutOfOrderMetadataProperties, context);
	}

	internal JsonConverter ReadDiscriminator(ref JsonReader reader, bool allowOutOfOrderMetadataProperties, SerializationContext context, out bool discriminatorConsumed)
	{
		discriminatorConsumed = false;
		if (!allowOutOfOrderMetadataProperties)
		{
			JsonReader original = reader;
			JsonConverter converter = this.ReadFirstPropertyDiscriminator(ref reader);
			if (converter is IJsonUnionObjectConverter { SupportsInlineObject: true })
			{
				discriminatorConsumed = true;
				return converter;
			}

			reader = original;
		}

		return this.ReadDiscriminator(ref reader, allowOutOfOrderMetadataProperties, context);
	}

	internal async ValueTask<JsonConverter> ReadDiscriminatorAsync(JsonAsyncReader reader, bool allowOutOfOrderMetadataProperties, SerializationContext context)
	{
		await reader.BufferNextValueAsync(context).ConfigureAwait(false);
		JsonReader buffered = reader.CreateBufferedReader();
		JsonReader peek = buffered.CreatePeekReader();
		try
		{
			return this.ReadDiscriminatorCore(ref peek, allowOutOfOrderMetadataProperties, context);
		}
		finally
		{
			reader.ReturnReader(ref buffered);
		}
	}

	internal static object? ReadValue(ref JsonReader reader, JsonConverter converter, SerializationContext context)
	{
		reader.ReadStartObject();
		bool foundType = false;
		bool foundValue = false;
		object? result = null;
		if (!reader.TryReadEndObject())
		{
			while (true)
			{
				string propertyName = reader.ReadRequiredString();
				reader.ReadNameSeparator();
				if (propertyName == DiscriminatorPropertyName)
				{
					if (foundType)
					{
						throw new FormatException("A union discriminator object may contain only one '$type' property.");
					}

					_ = reader.ReadRequiredString();
					foundType = true;
				}
				else if (propertyName == ValuePropertyName)
				{
					if (foundValue)
					{
						throw new FormatException("A union discriminator object may contain only one '$value' property.");
					}

					result = converter.ReadObject(ref reader, context);
					foundValue = true;
				}
				else
				{
					throw new FormatException($"A union case represented by '$value' cannot contain the property '{propertyName}'.");
				}

				if (reader.TryReadEndObject())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
		}

		if (!foundType || !foundValue)
		{
			throw new FormatException("A union discriminator object must contain '$type' and '$value' properties.");
		}

		return result;
	}

	internal static void ReadValueStart(ref JsonReader reader, SerializationContext context)
	{
		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			throw new FormatException("A union discriminator object is missing the '$value' property.");
		}

		while (true)
		{
			string propertyName = reader.ReadRequiredString();
			reader.ReadNameSeparator();
			if (propertyName == ValuePropertyName)
			{
				return;
			}

			if (propertyName == DiscriminatorPropertyName)
			{
				_ = reader.ReadRequiredString();
			}
			else
			{
				throw new FormatException($"A union case represented by '$value' cannot contain the property '{propertyName}'.");
			}

			if (reader.TryReadEndObject())
			{
				break;
			}

			reader.ReadValueSeparator();
		}
	}

	internal static async ValueTask<object?> ReadValueAsync(JsonAsyncReader reader, JsonConverter converter, SerializationContext context)
	{
		await reader.ReadStartObjectAsync(context).ConfigureAwait(false);
		bool foundType = false;
		bool foundValue = false;
		object? result = null;
		if (!await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
		{
			while (true)
			{
				string propertyName = await reader.ReadPropertyNameAsync(context).ConfigureAwait(false);
				if (propertyName == DiscriminatorPropertyName)
				{
					if (foundType)
					{
						throw new FormatException("A union discriminator object may contain only one '$type' property.");
					}

					_ = await reader.ReadStringValueAsync(context).ConfigureAwait(false);
					foundType = true;
				}
				else if (propertyName == ValuePropertyName)
				{
					if (foundValue)
					{
						throw new FormatException("A union discriminator object may contain only one '$value' property.");
					}

					result = await converter.ReadObjectAsync(reader, context).ConfigureAwait(false);
					foundValue = true;
				}
				else
				{
					throw new FormatException($"A union case represented by '$value' cannot contain the property '{propertyName}'.");
				}

				if (await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
				{
					break;
				}

				await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
			}
		}

		if (!foundType || !foundValue)
		{
			throw new FormatException("A union discriminator object must contain '$type' and '$value' properties.");
		}

		return result;
	}

	internal static async ValueTask ReadValueStartAsync(JsonAsyncReader reader, SerializationContext context)
	{
		await reader.ReadStartObjectAsync(context).ConfigureAwait(false);
		if (await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
		{
			throw new FormatException("A union discriminator object is missing the '$value' property.");
		}

		while (true)
		{
			string propertyName = await reader.ReadPropertyNameAsync(context).ConfigureAwait(false);
			if (propertyName == ValuePropertyName)
			{
				return;
			}

			if (propertyName == DiscriminatorPropertyName)
			{
				_ = await reader.ReadStringValueAsync(context).ConfigureAwait(false);
			}
			else
			{
				throw new FormatException($"A union case represented by '$value' cannot contain the property '{propertyName}'.");
			}

			if (await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
			{
				break;
			}

			await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
		}
	}

	private JsonConverter ReadDiscriminatorCore(ref JsonReader reader, bool allowOutOfOrderMetadataProperties, SerializationContext context)
	{
		reader.ReadStartObject();
		int propertyIndex = 0;
		JsonConverter? converter = null;
		if (!reader.TryReadEndObject())
		{
			while (true)
			{
				bool isDiscriminator;
				if (propertyIndex == 0 && reader.TryReadUnescapedUtf8StringToken(out ReadOnlySpan<byte> utf8PropertyName, out _, out _))
				{
					if (utf8PropertyName.IsEmpty)
					{
						isDiscriminator = reader.ReadRequiredString() == DiscriminatorPropertyName;
					}
					else
					{
						isDiscriminator = utf8PropertyName.SequenceEqual("\"$type\""u8);
						if (!isDiscriminator && !allowOutOfOrderMetadataProperties)
						{
							throw new FormatException("The '$type' metadata property must appear first.");
						}
					}
				}
				else
				{
					isDiscriminator = reader.ReadRequiredString() == DiscriminatorPropertyName;
				}

				reader.ReadNameSeparator();
				if (isDiscriminator)
				{
					if (converter is not null)
					{
						throw new FormatException("A union discriminator object may contain only one '$type' property.");
					}

					if (propertyIndex != 0 && !allowOutOfOrderMetadataProperties)
					{
						throw new FormatException("The '$type' metadata property must appear first.");
					}

					converter = this.Resolve(ref reader);
					if (propertyIndex == 0)
					{
						return converter;
					}
				}
				else
				{
					reader.Skip(context);
				}

				propertyIndex++;
				if (reader.TryReadEndObject())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
		}

		return converter is not null
			? converter
			: throw new FormatException("The union discriminator object is missing the '$type' property.");
	}

	private JsonConverter ReadFirstPropertyDiscriminator(ref JsonReader reader)
	{
		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			throw new FormatException("The union discriminator object is missing the '$type' property.");
		}

		bool isDiscriminator;
		if (reader.TryReadUnescapedUtf8StringToken(out ReadOnlySpan<byte> propertyName, out _, out _))
		{
			isDiscriminator = propertyName.IsEmpty
				? reader.ReadRequiredString() == DiscriminatorPropertyName
				: propertyName.SequenceEqual("\"$type\""u8);
		}
		else
		{
			isDiscriminator = reader.ReadRequiredString() == DiscriminatorPropertyName;
		}

		if (!isDiscriminator)
		{
			throw new FormatException("The '$type' metadata property must appear first.");
		}

		reader.ReadNameSeparator();
		return this.Resolve(ref reader);
	}

	private JsonConverter Resolve(string alias) => this.converters.TryGetValue(alias, out JsonConverter? converter)
		? converter
		: throw new FormatException($"Unrecognized union discriminator '{alias}'.");

	private JsonConverter Resolve(ref JsonReader reader)
	{
		if (!reader.TryReadStringSpan(out ReadOnlySpan<byte> alias))
		{
			return this.Resolve(reader.ReadRequiredString());
		}

		foreach ((byte[] utf8Alias, JsonConverter converter) in this.utf8Converters)
		{
			if (alias.SequenceEqual(utf8Alias))
			{
				return converter;
			}
		}

		throw new FormatException($"Unrecognized union discriminator '{Encoding.UTF8.GetString(alias)}'.");
	}
}
