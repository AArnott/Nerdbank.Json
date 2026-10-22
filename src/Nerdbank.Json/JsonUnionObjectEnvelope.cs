// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal envelope helper.

using System.Globalization;

namespace Nerdbank.Json;

/// <summary>
/// Resolves JSON object property discriminators, including the reserved base-contract alias.
/// </summary>
internal sealed class JsonUnionObjectEnvelope
{
	internal const string BaseAlias = "$base";

	private readonly Dictionary<string, JsonConverter> converters = new(StringComparer.Ordinal);

	internal JsonUnionObjectEnvelope(JsonConverter? baseConverter)
	{
		if (baseConverter is not null)
		{
			this.converters.Add(BaseAlias, baseConverter);
		}
	}

	internal static string GetAlias(int? tag, string? name) => tag?.ToString(CultureInfo.InvariantCulture) ?? name!;

	internal static void ReadEndObject(ref JsonReader reader)
	{
		if (!reader.TryReadEndObject())
		{
			throw new FormatException("A union discriminator object must contain exactly one property.");
		}
	}

	internal void Add(string alias, JsonConverter converter)
	{
		if (alias == BaseAlias || this.converters.ContainsKey(alias))
		{
			throw new NotSupportedException($"The union discriminator '{alias}' is reserved or ambiguous in object format.");
		}

		this.converters.Add(alias, converter);
	}

	internal JsonConverter ReadDiscriminator(ref JsonReader reader)
	{
		reader.ReadStartObject();
		string alias = reader.ReadRequiredString();
		reader.ReadNameSeparator();
		return this.Resolve(alias);
	}

	internal async ValueTask<JsonConverter> ReadDiscriminatorAsync(JsonAsyncReader reader, SerializationContext context)
	{
		await reader.ReadStartObjectAsync(context).ConfigureAwait(false);
		string alias = await reader.ReadStringValueAsync(context).ConfigureAwait(false);
		await reader.ReadNameSeparatorAsync(context).ConfigureAwait(false);
		return this.Resolve(alias);
	}

	private JsonConverter Resolve(string alias) => this.converters.TryGetValue(alias, out JsonConverter? converter)
		? converter
		: throw new FormatException($"Unrecognized union discriminator '{alias}'.");
}
