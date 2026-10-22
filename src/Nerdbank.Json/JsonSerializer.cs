// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal helper members added for overload forwarding are intentionally undocumented.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Nerdbank.Json;

/// <summary>
/// Serializes .NET values to JSON.
/// </summary>
/// <remarks>
/// <para>
/// This type is immutable and thread-safe.
/// </para>
/// <para>
/// Serialization and deserialization operation failures, including asynchronous sequence enumeration, are reported as
/// <see cref="JsonSerializationException"/>. Existing serialization exceptions retain their instance and error code;
/// other failures retain their original cause as the inner exception. Argument validation and
/// <see cref="OperationCanceledException"/> when the operation's cancellation token is canceled are not wrapped.
/// </para>
/// <para>
/// UTF-8 buffers and caller-owned readers deserialize the first value, leaving subsequent values unread by the parser.
/// String overloads validate a complete JSON document. Asynchronous stream overloads also validate a complete document:
/// their temporary pipe may read ahead, so returning early could discard unread bytes when that pipe is completed.
/// Use a caller-owned <see cref="System.IO.Pipelines.PipeReader"/> to preserve trailing data across asynchronous operations.
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

	/// <summary>
	/// Gets the starting context to begin serializations and deserializations with.
	/// </summary>
	public SerializationContext StartingContext { get; init; } = new();

	/// <inheritdoc cref="JsonSerializerConfiguration.MultiDimensionalArrayFormat"/>
	public MultiDimensionalArrayFormat MultiDimensionalArrayFormat
	{
		get => this.configuration.MultiDimensionalArrayFormat;
		init => this.configuration = this.configuration with { MultiDimensionalArrayFormat = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.PropertyNamingPolicy"/>
	public JsonNamingPolicy? PropertyNamingPolicy
	{
		get => this.configuration.PropertyNamingPolicy;
		init => this.configuration = this.configuration with { PropertyNamingPolicy = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.DictionaryKeyNamingPolicy"/>
	public JsonNamingPolicy? DictionaryKeyNamingPolicy
	{
		get => this.configuration.DictionaryKeyNamingPolicy;
		init => this.configuration = this.configuration with { DictionaryKeyNamingPolicy = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.ComparerProvider"/>
	public MessagePack.IComparerProvider? ComparerProvider
	{
		get => this.configuration.ComparerProvider;
		init => this.configuration = this.configuration with { ComparerProvider = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.Converters"/>
	public ConverterCollection Converters
	{
		get => this.configuration.Converters;
		init => this.configuration = this.configuration with { Converters = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.AllowTrailingCommas"/>
	public bool AllowTrailingCommas
	{
		get => this.configuration.AllowTrailingCommas;
		init => this.configuration = this.configuration with { AllowTrailingCommas = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.ConverterTypes"/>
	public ConverterTypeCollection ConverterTypes
	{
		get => this.configuration.ConverterTypes;
		init => this.configuration = this.configuration with { ConverterTypes = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.ConverterFactories"/>
	public ImmutableArray<IJsonConverterFactory> ConverterFactories
	{
		get => this.configuration.ConverterFactories;
		init => this.configuration = this.configuration with { ConverterFactories = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.ReadCommentHandling"/>
	public JsonCommentHandling ReadCommentHandling
	{
		get => this.configuration.ReadCommentHandling;
		init => this.configuration = this.configuration with { ReadCommentHandling = value };
	}

	/// <summary>
	/// Gets a value indicating whether equal strings should share an instance during deserialization.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The default value is <see langword="false"/>. When enabled, strings with equal values within one
	/// deserialization operation are represented by the same <see cref="string"/> instance.
	/// </para>
	/// <para>
	/// Interned strings are retained only for the operation's lifetime, so this setting does not retain strings
	/// across deserialization operations. It can reduce memory use when input repeats string values, at the cost of
	/// looking up each deserialized string in the operation's cache.
	/// </para>
	/// </remarks>
	public bool InternStrings
	{
		get => this.configuration.InternStrings;
		init => this.configuration = this.configuration with { InternStrings = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.PropertyNameCaseInsensitive"/>
	public bool PropertyNameCaseInsensitive
	{
		get => this.configuration.PropertyNameCaseInsensitive;
		init => this.configuration = this.configuration with { PropertyNameCaseInsensitive = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.WriteIndented"/>
	public bool WriteIndented
	{
		get => this.configuration.WriteIndented;
		init => this.configuration = this.configuration with { WriteIndented = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.SerializeDefaultValues"/>
	public SerializeDefaultValuesPolicy SerializeDefaultValues
	{
		get => this.configuration.SerializeDefaultValues;
		init => this.configuration = this.configuration with { SerializeDefaultValues = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.SerializeEnumValuesByName"/>
	public bool SerializeEnumValuesByName
	{
		get => this.configuration.SerializeEnumValuesByName;
		init => this.configuration = this.configuration with { SerializeEnumValuesByName = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.DeserializeDefaultValues"/>
	public DeserializeDefaultValuesPolicy DeserializeDefaultValues
	{
		get => this.configuration.DeserializeDefaultValues;
		init => this.configuration = this.configuration with { DeserializeDefaultValues = value };
	}

	/// <inheritdoc cref="JsonSerializerConfiguration.PreserveReferences"/>
	public ReferencePreservationMode PreserveReferences
	{
		get => this.configuration.PreserveReferences;
		init => this.configuration = this.configuration with { PreserveReferences = value };
	}

	/// <summary>
	/// Gets the runtime union configuration that adds, replaces, extends, or disables shape-generated unions.
	/// </summary>
	/// <value>The default value is an empty collection.</value>
	public DerivedTypeUnionCollection DerivedTypeUnions
	{
		get => this.configuration.DerivedTypeUnions;
		init => this.configuration = this.configuration with { DerivedTypeUnions = value };
	}

	/// <summary>Gets a value indicating whether unions use single-property object envelopes instead of two-element arrays.</summary>
	/// <value>The default is <see langword="false"/>.</value>
	/// <remarks>
	/// Integer aliases become invariant-culture property names. The reserved <c>$base</c> property identifies a base-contract fallback.
	/// Aliases that collide as property names or use <c>$base</c> are rejected in object mode.
	/// Reference preservation continues to use <c>$id</c>, <c>$ref</c>, and <c>$value</c>.
	/// </remarks>
	public bool UseDiscriminatorObjects
	{
		get => this.configuration.UseDiscriminatorObjects;
		init => this.configuration = this.configuration with { UseDiscriminatorObjects = value };
	}

	/// <summary>
	/// Gets the converter cache derived from this serializer's immutable configuration.
	/// </summary>
	internal ConverterCache ConverterCache => this.configuration.ConverterCache;

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

		SerializationContext context = this.CreateSerializationContext(shape.Provider, cancellationToken);
		try
		{
			this.ConverterCache.GetOrAddConverter(shape).WriteObject(ref writer, value, context);
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException(ex.Message, ex);
		}
	}

	/// <summary>
	/// Serializes an untyped value to JSON using the specified type shape and a caller-supplied starting context.
	/// </summary>
	/// <param name="writer">The writer to serialize the value into.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing the structure of <paramref name="value"/>.</param>
	/// <param name="startingContext">
	/// The context to start this operation with, used instead of <see cref="StartingContext"/>.
	/// Its <see cref="SerializationContext.CancellationToken"/> cancels the operation.
	/// </param>
	/// <exception cref="ArgumentException">Thrown if <paramref name="startingContext"/> belongs to an operation already in progress.</exception>
	[OverloadResolutionPriority(-1)]
	public void SerializeObject(ref JsonWriter writer, object? value, ITypeShape shape, SerializationContext startingContext)
	{
		Requires.NotNull(shape);

		SerializationContext context = this.CreateSerializationContext(startingContext) with { TypeShapeProvider = shape.Provider };
		try
		{
			this.ConverterCache.GetOrAddConverter(shape).WriteObject(ref writer, value, context);
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException(ex.Message, ex);
		}
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
		this.SerializeCore(ref writer, value, shape, this.CreateSerializationContext(cancellationToken));
	}

	/// <summary>
	/// Serializes a value to JSON using the specified type shape and a caller-supplied starting context.
	/// </summary>
	/// <typeparam name="T">The type of value to serialize.</typeparam>
	/// <param name="writer">The writer to serialize the value into.</param>
	/// <param name="value">The value to serialize.</param>
	/// <param name="shape">The type shape describing the structure of <typeparamref name="T"/>.</param>
	/// <param name="startingContext">
	/// The context to start this operation with, used instead of <see cref="StartingContext"/>.
	/// Its <see cref="SerializationContext.CancellationToken"/> cancels the operation.
	/// </param>
	/// <exception cref="ArgumentException">Thrown if <paramref name="startingContext"/> belongs to an operation already in progress.</exception>
	/// <remarks>
	/// Use this overload to supply per-call state to converters via <see cref="SerializationContext.this[object]"/>.
	/// </remarks>
	[OverloadResolutionPriority(-1)]
	public void Serialize<T>(ref JsonWriter writer, in T? value, ITypeShape<T> shape, SerializationContext startingContext)
	{
		Requires.NotNull(shape);
		this.SerializeCore(ref writer, value, shape, this.CreateSerializationContext(startingContext));
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

		SerializationContext context = this.CreateSerializationContext(shape.Provider, cancellationToken);
		try
		{
			return this.ConverterCache.GetOrAddConverter(shape).ReadObject(ref reader, context);
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException(ex.Message, ex);
		}
	}

	/// <summary>
	/// Deserializes an untyped value from JSON using the specified type shape and a caller-supplied starting context.
	/// </summary>
	/// <param name="reader">The reader to deserialize the value from.</param>
	/// <param name="shape">The type shape describing the structure of the value to deserialize.</param>
	/// <param name="startingContext">
	/// The context to start this operation with, used instead of <see cref="StartingContext"/>.
	/// Its <see cref="SerializationContext.CancellationToken"/> cancels the operation.
	/// </param>
	/// <returns>The deserialized value, or <see langword="null"/> if the JSON represents a null value.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="startingContext"/> belongs to an operation already in progress.</exception>
	[OverloadResolutionPriority(-1)]
	public object? DeserializeObject(ref JsonReader reader, ITypeShape shape, SerializationContext startingContext)
	{
		Requires.NotNull(shape);

		SerializationContext context = this.CreateSerializationContext(startingContext) with { TypeShapeProvider = shape.Provider };
		try
		{
			return this.ConverterCache.GetOrAddConverter(shape).ReadObject(ref reader, context);
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException(ex.Message, ex);
		}
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
		return this.DeserializeCore(ref reader, shape, this.CreateSerializationContext(cancellationToken));
	}

	/// <summary>
	/// Deserializes a value from JSON using the specified type shape and a caller-supplied starting context.
	/// </summary>
	/// <typeparam name="T">The type of value to deserialize.</typeparam>
	/// <param name="reader">The reader to deserialize the value from.</param>
	/// <param name="shape">The type shape describing the structure of <typeparamref name="T"/>.</param>
	/// <param name="startingContext">
	/// The context to start this operation with, used instead of <see cref="StartingContext"/>.
	/// Its <see cref="SerializationContext.CancellationToken"/> cancels the operation.
	/// </param>
	/// <returns>The deserialized value, or <see langword="null"/> if the JSON represents a null value.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="startingContext"/> belongs to an operation already in progress.</exception>
	/// <remarks>
	/// Use this overload to supply per-call state to converters via <see cref="SerializationContext.this[object]"/>.
	/// </remarks>
	[OverloadResolutionPriority(-1)]
	public T? Deserialize<T>(ref JsonReader reader, ITypeShape<T> shape, SerializationContext startingContext)
	{
		Requires.NotNull(shape);
		return this.DeserializeCore(ref reader, shape, this.CreateSerializationContext(startingContext));
	}

	/// <summary>Creates an operation context for use by derived serializers.</summary>
	/// <param name="provider">The shape provider for the operation.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The initialized context.</returns>
	protected SerializationContext CreateSerializationContext(ITypeShapeProvider provider, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(provider);
		return this.CreateSerializationContext(cancellationToken) with { TypeShapeProvider = provider };
	}

	private static bool RequiresReferencePreservation(Type type) => !type.IsValueType && !BuiltInJsonConverters.IsSupported(type);

	private static void ThrowIfOperationInProgress(in SerializationContext startingContext)
	{
		if (startingContext.IsOperationInProgress)
		{
			throw new ArgumentException(
				"This context belongs to a serialization operation that is already in progress. Converters must not pass their context to top-level JsonSerializer methods; use SerializationContext.GetConverter to (de)serialize nested values instead.",
				nameof(startingContext));
		}
	}

	private void SerializeCore<T>(ref JsonWriter writer, in T? value, ITypeShape<T> shape, SerializationContext context)
	{
		context = context with { TypeShapeProvider = shape.Provider };
		try
		{
			if (this.CanUseBuiltInFastPath(typeof(T)) && BuiltInJsonConverters.TrySerialize(ref writer, value))
			{
				context.CancellationToken.ThrowIfCancellationRequested();
				return;
			}

			this.ConverterCache.GetOrAddConverter(shape).Write(ref writer, value, context);
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException(ex.Message, ex);
		}
	}

	private T? DeserializeCore<T>(ref JsonReader reader, ITypeShape<T> shape, SerializationContext context)
	{
		context = context with { TypeShapeProvider = shape.Provider };
		try
		{
			if (this.CanUseBuiltInFastPath(typeof(T)) && BuiltInJsonConverters.TryDeserialize(ref reader, context, out T value))
			{
				context.CancellationToken.ThrowIfCancellationRequested();
				return value;
			}

			return this.ConverterCache.GetOrAddConverter(shape).Read(ref reader, context);
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException(ex.Message, ex);
		}
	}

	private static bool ShouldWrapSerializationException(Exception exception, CancellationToken cancellationToken)
		=> exception is not JsonSerializationException && (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested);

	private static void EnsureFullyConsumed(ref JsonReader reader)
	{
		try
		{
			reader.EnsureFullyConsumed();
		}
		catch (FormatException ex)
		{
			throw new JsonSerializationException(ex.Message, ex);
		}
	}

	private bool CanUseBuiltInFastPath(Type type) => !this.ConverterCache.HasRuntimeConverters && (this.PreserveReferences == ReferencePreservationMode.Off || !RequiresReferencePreservation(type));

	private SerializationContext CreateSerializationContext(CancellationToken cancellationToken)
	{
		CancellationToken effectiveCancellationToken = cancellationToken.CanBeCanceled ? cancellationToken : this.StartingContext.CancellationToken;
		return this.StartingContext.Start(this, this.ConverterCache, effectiveCancellationToken);
	}

	private SerializationContext CreateSerializationContext(in SerializationContext startingContext)
	{
		ThrowIfOperationInProgress(startingContext);
		return startingContext.Start(this, this.ConverterCache, startingContext.CancellationToken);
	}
}
