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
	private MessagePack.IComparerProvider? comparerProvider = MessagePack.SecureComparerProvider.Default;
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

	/// <summary>
	/// Gets the runtime-registered converters that take precedence over built-in and shape-based converters.
	/// </summary>
	internal ConverterCollection Converters
	{
		get => this.converters;
		init
		{
			this.converters = value ?? throw new ArgumentNullException(nameof(value));
			this.converterCache = null;
		}
	}

	/// <summary>
	/// Gets the runtime-registered converter types that take precedence after <see cref="Converters"/>.
	/// </summary>
	internal JsonConverterTypeCollection ConverterTypes
	{
		get => this.converterTypes;
		init
		{
			this.converterTypes = value ?? throw new ArgumentNullException(nameof(value));
			this.converterCache = null;
		}
	}

	/// <summary>
	/// Gets the runtime-registered converter factories that are consulted after <see cref="Converters"/> and <see cref="ConverterTypes"/>.
	/// </summary>
	internal IReadOnlyList<IJsonConverterFactory> ConverterFactories
	{
		get => this.converterFactories;
		init
		{
			this.converterFactories = value;
			this.converterCache = null;
		}
	}

	/// <summary>
	/// Gets a value indicating whether a trailing comma is permitted before the end of a JSON array or object during deserialization.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="false"/>.
	/// </remarks>
	internal bool AllowTrailingCommas
	{
		get => this.allowTrailingCommas;
		init => this.allowTrailingCommas = value;
	}

	/// <summary>
	/// Gets the provider of <see cref="IEqualityComparer{T}"/> and <see cref="IComparer{T}"/> instances
	/// to use when instantiating collections that support them.
	/// </summary>
	/// <value>
	/// The default value is an instance of <see cref="MessagePack.SecureComparerProvider"/>,
	/// which provides hash collision resistance for improved security when deserializing untrusted data.
	/// </value>
	/// <remarks>
	/// This property may be cleared from its secure default for improved performance when deserializing trusted data.
	/// </remarks>
	internal MessagePack.IComparerProvider? ComparerProvider
	{
		get => this.comparerProvider;
		init
		{
			this.comparerProvider = value;
			this.converterCache = null;
		}
	}

	/// <summary>
	/// Gets the transformation applied to object property names and enum value names during serialization and deserialization.
	/// </summary>
	/// <remarks>
	/// The default value is <see cref="JsonNamingPolicy.CamelCase"/>.
	/// Set this property to <see langword="null"/> to preserve declared property names and enum value names.
	/// Enum value names are transformed only when <see cref="SerializeEnumValuesByName"/> is <see langword="true"/>.
	/// </remarks>
	internal JsonNamingPolicy? PropertyNamingPolicy
	{
		get => this.propertyNamingPolicy;
		init
		{
			this.propertyNamingPolicy = value;
			this.converterCache = null;
		}
	}

	/// <summary>
	/// Gets a value indicating whether JSON object property names are matched case-insensitively during deserialization.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="false"/>.
	/// </remarks>
	internal bool PropertyNameCaseInsensitive
	{
		get => this.propertyNameCaseInsensitive;
		init
		{
			this.propertyNameCaseInsensitive = value;
			this.converterCache = null;
		}
	}

	/// <summary>
	/// Gets the transformation applied to dictionary keys during serialization and deserialization.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="null"/>, which preserves dictionary keys as declared.
	/// </remarks>
	internal JsonNamingPolicy? DictionaryKeyNamingPolicy
	{
		get => this.dictionaryKeyNamingPolicy;
		init
		{
			this.dictionaryKeyNamingPolicy = value;
			this.converterCache = null;
		}
	}

	/// <summary>
	/// Gets the policy that determines whether properties with default values are serialized.
	/// </summary>
	/// <remarks>
	/// The default value is <see cref="SerializeDefaultValuesPolicy.Always"/>.
	/// </remarks>
	internal SerializeDefaultValuesPolicy SerializeDefaultValues
	{
		get => this.serializeDefaultValues;
		init
		{
			this.serializeDefaultValues = value;
			this.converterCache = null;
		}
	}

	/// <summary>
	/// Gets a value indicating whether enum values should be serialized by name rather than by numeric value when possible.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="false"/>.
	/// When enabled, <see cref="PropertyNamingPolicy"/> is applied to enum value names.
	/// </remarks>
	internal bool SerializeEnumValuesByName
	{
		get => this.serializeEnumValuesByName;
		init
		{
			this.serializeEnumValuesByName = value;
			this.converterCache = null;
		}
	}

	/// <summary>
	/// Gets the policy that determines how deserialization handles missing or <see langword="null"/> values.
	/// </summary>
	/// <remarks>
	/// The default value is <see cref="DeserializeDefaultValuesPolicy.Default"/>.
	/// </remarks>
	internal DeserializeDefaultValuesPolicy DeserializeDefaultValues
	{
		get => this.deserializeDefaultValues;
		init
		{
			this.deserializeDefaultValues = value;
			this.converterCache = null;
		}
	}

	/// <summary>
	/// Gets the mode that preserves reference equality during serialization and deserialization.
	/// </summary>
	internal ReferencePreservationMode PreserveReferences
	{
		get => this.preserveReferences;
		init
		{
			this.preserveReferences = value;
			this.converterCache = null;
		}
	}

	/// <summary>
	/// Gets a value indicating whether serialized JSON should be formatted with indentation and line breaks.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="false"/>.
	/// </remarks>
	internal bool WriteIndented
	{
		get => this.writeIndented;
		init => this.writeIndented = value;
	}

	/// <summary>
	/// Gets the policy for handling JSON comments during deserialization.
	/// </summary>
	/// <remarks>
	/// The default value is <see cref="JsonCommentHandling.Disallow"/>.
	/// </remarks>
	internal JsonCommentHandling ReadCommentHandling
	{
		get => this.readCommentHandling;
		init => this.readCommentHandling = value;
	}
}
