// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal helper members added for overload forwarding are intentionally undocumented.

namespace Nerdbank.Json;

/// <summary>
/// Serializes .NET values to JSON.
/// </summary>
/// <remarks>
/// <para>
/// This type is immutable and thread-safe.
/// </para>
/// <para>
/// When targeting .NET Standard 2.0 or .NET Framework, some important methods are available only as extension methods,
/// so make sure to have a <c><![CDATA[using Nerdbank.Json;]]></c> directive in your code file to see these.
/// </para>
/// </remarks>
public partial record JsonSerializer
{
#if NET
	internal const string PreferTypeConstrainedInstanceOverloads = "Use a non-extension overload that constrains the generic T : IShapeable<T>, or use an overload that explicitly supplies a witness type or ITypeShape<T>.";
#endif

	private JsonSerializerConfiguration configuration = JsonSerializerConfiguration.Default;
	[ThreadStatic]
	private static JsonReferenceEqualityTracker? currentReferenceTracker;

	/// <summary>
	/// Gets the transformation applied to object property names and enum value names during serialization and deserialization.
	/// </summary>
	/// <remarks>
	/// The default value is <see cref="JsonNamingPolicy.CamelCase"/>.
	/// Set this property to <see langword="null"/> to preserve declared property names and enum value names.
	/// Enum value names are transformed only when <see cref="SerializeEnumValuesByName"/> is <see langword="true"/>.
	/// </remarks>
	public JsonNamingPolicy? PropertyNamingPolicy
	{
		get => this.configuration.PropertyNamingPolicy;
		init => this.configuration = this.configuration with { PropertyNamingPolicy = value };
	}

	/// <summary>
	/// Gets the transformation applied to dictionary keys during serialization and deserialization.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="null"/>, which preserves dictionary keys as declared.
	/// </remarks>
	public JsonNamingPolicy? DictionaryKeyNamingPolicy
	{
		get => this.configuration.DictionaryKeyNamingPolicy;
		init => this.configuration = this.configuration with { DictionaryKeyNamingPolicy = value };
	}

	/// <summary>
	/// Gets the provider of <see cref="IEqualityComparer{T}"/> and <see cref="IComparer{T}"/> instances
	/// to use when instantiating collections that support them.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="null"/>, which uses collection defaults.
	/// </remarks>
	public IComparerProvider? ComparerProvider
	{
		get => this.configuration.ComparerProvider;
		init => this.configuration = this.configuration with { ComparerProvider = value };
	}

	/// <summary>
	/// Gets the runtime-registered converters that take precedence over built-in and shape-based converters.
	/// </summary>
	public ConverterCollection Converters
	{
		get => this.configuration.Converters;
		init => this.configuration = this.configuration with { Converters = value };
	}

	/// <summary>
	/// Gets a value indicating whether a trailing comma is permitted before the end of a JSON array or object during deserialization.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="false"/>.
	/// </remarks>
	public bool AllowTrailingCommas
	{
		get => this.configuration.AllowTrailingCommas;
		init => this.configuration = this.configuration with { AllowTrailingCommas = value };
	}

	/// <summary>
	/// Gets the runtime-registered converter types that take precedence after <see cref="Converters"/>.
	/// </summary>
	public JsonConverterTypeCollection ConverterTypes
	{
		get => this.configuration.ConverterTypes;
		init => this.configuration = this.configuration with { ConverterTypes = value };
	}

	/// <summary>
	/// Gets the runtime-registered converter factories that are consulted after <see cref="Converters"/> and <see cref="ConverterTypes"/>.
	/// </summary>
	public IReadOnlyList<IJsonConverterFactory> ConverterFactories
	{
		get => this.configuration.ConverterFactories;
		init => this.configuration = this.configuration with { ConverterFactories = value };
	}

	/// <summary>
	/// Gets the policy for handling JSON comments during deserialization.
	/// </summary>
	/// <remarks>
	/// The default value is <see cref="JsonCommentHandling.Disallow"/>.
	/// </remarks>
	public JsonCommentHandling ReadCommentHandling
	{
		get => this.configuration.ReadCommentHandling;
		init => this.configuration = this.configuration with { ReadCommentHandling = value };
	}

	/// <summary>
	/// Gets a value indicating whether JSON object property names are matched case-insensitively during deserialization.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="false"/>.
	/// </remarks>
	public bool PropertyNameCaseInsensitive
	{
		get => this.configuration.PropertyNameCaseInsensitive;
		init => this.configuration = this.configuration with { PropertyNameCaseInsensitive = value };
	}

	/// <summary>
	/// Gets a value indicating whether serialized JSON should be formatted with indentation and line breaks.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="false"/>.
	/// </remarks>
	public bool WriteIndented
	{
		get => this.configuration.WriteIndented;
		init => this.configuration = this.configuration with { WriteIndented = value };
	}

	/// <summary>
	/// Gets the policy that determines whether properties with default values are serialized.
	/// </summary>
	/// <remarks>
	/// The default value is <see cref="SerializeDefaultValuesPolicy.Always"/>.
	/// </remarks>
	public SerializeDefaultValuesPolicy SerializeDefaultValues
	{
		get => this.configuration.SerializeDefaultValues;
		init => this.configuration = this.configuration with { SerializeDefaultValues = value };
	}

	/// <summary>
	/// Gets a value indicating whether enum values should be serialized by name rather than by numeric value when possible.
	/// </summary>
	/// <remarks>
	/// The default value is <see langword="false"/>.
	/// When enabled, <see cref="PropertyNamingPolicy"/> is applied to enum value names.
	/// </remarks>
	public bool SerializeEnumValuesByName
	{
		get => this.configuration.SerializeEnumValuesByName;
		init => this.configuration = this.configuration with { SerializeEnumValuesByName = value };
	}

	/// <summary>
	/// Gets the policy that determines how deserialization handles missing or <see langword="null"/> values.
	/// </summary>
	/// <remarks>
	/// The default value is <see cref="DeserializeDefaultValuesPolicy.Default"/>.
	/// </remarks>
	public DeserializeDefaultValuesPolicy DeserializeDefaultValues
	{
		get => this.configuration.DeserializeDefaultValues;
		init => this.configuration = this.configuration with { DeserializeDefaultValues = value };
	}

	/// <summary>
	/// Gets the mode that preserves reference equality during serialization and deserialization.
	/// </summary>
	public ReferencePreservationMode PreserveReferences
	{
		get => this.configuration.PreserveReferences;
		init => this.configuration = this.configuration with { PreserveReferences = value };
	}

	/// <summary>
	/// Gets the converter cache derived from this serializer's immutable configuration.
	/// </summary>
	internal ConverterCache ConverterCache => this.configuration.ConverterCache;

	internal JsonReferenceEqualityTracker ReferenceTracker => currentReferenceTracker ?? throw new InvalidOperationException("Reference tracking is only available within an active serialization or deserialization operation.");

	/// <summary>
	/// Serializes an untyped value to JSON using the specified type shape.
	/// </summary>
	/// <param name="writer">The writer to serialize the value into.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing the structure of <paramref name="value"/>.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	public void SerializeObject(ref JsonWriter writer, object? value, ITypeShape shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(shape);

		this.ConverterCache.GetOrAddConverter(shape).WriteObject(ref writer, value, this);
	}

	/// <summary>
	/// Serializes a value to JSON using the specified type shape.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="writer">The writer to serialize the value into.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing the structure of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	public void Serialize<T>(ref JsonWriter writer, in T? value, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(shape);

		if (this.CanUseBuiltInFastPath(typeof(T)) && BuiltInJsonConverters.TrySerialize(ref writer, value))
		{
			return;
		}

		this.ConverterCache.GetOrAddConverter(shape).Write(ref writer, value, this);
	}

	/// <summary>
	/// Deserializes an untyped value from JSON using the specified type shape.
	/// </summary>
	/// <param name="reader">The reader to deserialize the value from.</param>
	/// <param name="shape">The type shape describing the structure of the value to deserialize.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The deserialized value, or <see langword="null"/> if the JSON represents a null value.</returns>
	public object? DeserializeObject(ref JsonReader reader, ITypeShape shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(shape);

		return this.ConverterCache.GetOrAddConverter(shape).ReadObject(ref reader, this);
	}

	/// <summary>
	/// Deserializes a value from JSON using the specified type shape.
	/// </summary>
	/// <typeparam name="T">The type of value to deserialize.</typeparam>
	/// <param name="reader">The reader to deserialize the value from.</param>
	/// <param name="shape">The type shape describing the structure of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The deserialized value, or <see langword="null"/> if the JSON represents a null value.</returns>
	public T? Deserialize<T>(ref JsonReader reader, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(shape);

		if (this.CanUseBuiltInFastPath(typeof(T)) && BuiltInJsonConverters.TryDeserialize(ref reader, out T value))
		{
			return value;
		}

		return this.ConverterCache.GetOrAddConverter(shape).Read(ref reader, this);
	}

	private bool CanUseBuiltInFastPath(Type type) => !this.ConverterCache.HasRuntimeConverters && (this.PreserveReferences == ReferencePreservationMode.Off || !RequiresReferencePreservation(type));

	private static bool RequiresReferencePreservation(Type type) => !type.IsValueType && !BuiltInJsonConverters.IsSupported(type);
}
