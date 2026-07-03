// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

namespace Nerdbank.Json;

internal record JsonSerializerConfiguration
{
	internal static readonly JsonSerializerConfiguration Default = new();

	private ConverterCache? converterCache;
	private ConverterCollection converters = new();
	private IReadOnlyList<IJsonConverterFactory> converterFactories = [];
	private JsonConverterTypeCollection converterTypes = new();
	private bool allowTrailingCommas;
	private DeserializeDefaultValuesPolicy deserializeDefaultValues;
	private JsonNamingPolicy? dictionaryKeyNamingPolicy;
	private bool propertyNameCaseInsensitive;
	private JsonNamingPolicy? propertyNamingPolicy = JsonNamingPolicy.CamelCase;
	private JsonCommentHandling readCommentHandling;
	private ReferencePreservationMode preserveReferences;
	private bool serializeEnumValuesByName;
	private SerializeDefaultValuesPolicy serializeDefaultValues = SerializeDefaultValuesPolicy.Always;
	private bool writeIndented;

	internal ConverterCache ConverterCache => this.converterCache ??= new(this);

	internal ConverterCollection Converters
	{
		get => this.converters;
		init
		{
			this.converters = value ?? throw new ArgumentNullException(nameof(value));
			this.converterCache = null;
		}
	}

	internal JsonConverterTypeCollection ConverterTypes
	{
		get => this.converterTypes;
		init
		{
			this.converterTypes = value ?? throw new ArgumentNullException(nameof(value));
			this.converterCache = null;
		}
	}

	internal IReadOnlyList<IJsonConverterFactory> ConverterFactories
	{
		get => this.converterFactories;
		init
		{
			this.converterFactories = value;
			this.converterCache = null;
		}
	}

	internal bool AllowTrailingCommas
	{
		get => this.allowTrailingCommas;
		init => this.allowTrailingCommas = value;
	}

	internal JsonNamingPolicy? PropertyNamingPolicy
	{
		get => this.propertyNamingPolicy;
		init
		{
			this.propertyNamingPolicy = value;
			this.converterCache = null;
		}
	}

	internal bool PropertyNameCaseInsensitive
	{
		get => this.propertyNameCaseInsensitive;
		init
		{
			this.propertyNameCaseInsensitive = value;
			this.converterCache = null;
		}
	}

	internal JsonNamingPolicy? DictionaryKeyNamingPolicy
	{
		get => this.dictionaryKeyNamingPolicy;
		init
		{
			this.dictionaryKeyNamingPolicy = value;
			this.converterCache = null;
		}
	}

	internal SerializeDefaultValuesPolicy SerializeDefaultValues
	{
		get => this.serializeDefaultValues;
		init
		{
			this.serializeDefaultValues = value;
			this.converterCache = null;
		}
	}

	internal bool SerializeEnumValuesByName
	{
		get => this.serializeEnumValuesByName;
		init
		{
			this.serializeEnumValuesByName = value;
			this.converterCache = null;
		}
	}

	internal DeserializeDefaultValuesPolicy DeserializeDefaultValues
	{
		get => this.deserializeDefaultValues;
		init
		{
			this.deserializeDefaultValues = value;
			this.converterCache = null;
		}
	}

	internal ReferencePreservationMode PreserveReferences
	{
		get => this.preserveReferences;
		init
		{
			this.preserveReferences = value;
			this.converterCache = null;
		}
	}

	internal bool WriteIndented
	{
		get => this.writeIndented;
		init => this.writeIndented = value;
	}

	internal JsonCommentHandling ReadCommentHandling
	{
		get => this.readCommentHandling;
		init => this.readCommentHandling = value;
	}
}
