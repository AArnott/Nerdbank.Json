// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

namespace Nerdbank.Json;

internal record JsonSerializerConfiguration
{
	internal static readonly JsonSerializerConfiguration Default = new();

	private JsonConverterCache? converterCache;
	private JsonNamingPolicy? dictionaryKeyNamingPolicy;
	private JsonNamingPolicy? propertyNamingPolicy = JsonNamingPolicy.CamelCase;
	private ReferencePreservationMode preserveReferences;
	private SerializeDefaultValuesPolicy serializeDefaultValues = SerializeDefaultValuesPolicy.Always;

	internal JsonConverterCache ConverterCache => this.converterCache ??= new(this);

	internal JsonNamingPolicy? PropertyNamingPolicy
	{
		get => this.propertyNamingPolicy;
		init
		{
			this.propertyNamingPolicy = value;
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

	internal ReferencePreservationMode PreserveReferences
	{
		get => this.preserveReferences;
		init
		{
			this.preserveReferences = value;
			this.converterCache = null;
		}
	}
}
