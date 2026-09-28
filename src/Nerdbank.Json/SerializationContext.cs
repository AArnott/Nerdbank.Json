// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1202 // Keep internal operation-state members near the top of this context type.

using System.Collections.Immutable;

namespace Nerdbank.Json;

/// <summary>
/// Context that flows through the serialization process.
/// </summary>
/// <example>
/// <para>The default values on this struct may be changed and the modified struct applied to <see cref="JsonSerializer.StartingContext"/>
/// in order to serialize with the updated settings.</para>
/// </example>
/// <example>
/// <para>To modify the starting context on an existing serializer, you can use the with keyword to create a new serializer with the updated context.</para>
/// </example>
public record struct SerializationContext
{
	private ImmutableDictionary<object, object?>? specialState = ImmutableDictionary<object, object?>.Empty;
	private ConverterCache? cache;

	/// <summary>
	/// Initializes a new instance of the <see cref="SerializationContext"/> struct.
	/// </summary>
	public SerializationContext()
	{
	}

	/// <summary>
	/// Gets or sets the number of uncommitted bytes that may accumulate in an asynchronous writer's buffer before a
	/// flush to the underlying <see cref="System.IO.Pipelines.PipeWriter"/> is triggered.
	/// </summary>
	/// <value>The default value is 64KB.</value>
	public int UnflushedBytesThreshold { get; set; } = 64 * 1024;

	/// <summary>
	/// Gets or sets the remaining depth of the object graph to serialize or deserialize.
	/// </summary>
	/// <value>The default value is 64.</value>
	/// <remarks>
	/// Exceeding this depth will result in an <see cref="InvalidOperationException"/> being thrown
	/// from <see cref="DepthStep"/>.
	/// </remarks>
	public int MaxDepth { get; set; } = 64;

	/// <summary>
	/// Gets or sets the security settings to apply to (de)serialization.
	/// </summary>
	/// <value>The default value is <see cref="SecuritySettings.UntrustedData"/>.</value>
	public SecuritySettings Security { get; set; } = SecuritySettings.UntrustedData;

	/// <summary>
	/// Gets the converter cache that applies to the serialization operation.
	/// </summary>
	internal ConverterCache Cache
	{
		get => this.cache ?? throw new InvalidOperationException("No serialization operation is in progress.");
		init => this.cache = value;
	}

	/// <summary>
	/// Gets the default-value policy for the active serialization operation.
	/// </summary>
	internal SerializeDefaultValuesPolicy SerializeDefaultValues { get; init; }

	/// <summary>
	/// Gets the default-value policy for the active deserialization operation.
	/// </summary>
	internal DeserializeDefaultValuesPolicy DeserializeDefaultValues { get; init; }

	/// <summary>
	/// Gets the reference tracker active for this serialization operation.
	/// </summary>
	internal JsonReferenceEqualityTracker? ReferenceTracker { get; private init; }

	/// <summary>
	/// Gets the string interning cache for the active deserialization operation.
	/// </summary>
	internal StringInterning? StringInterningCache { get; private init; }

	/// <summary>
	/// Gets a cancellation token that can be used to cancel the serialization operation.
	/// </summary>
	public CancellationToken CancellationToken { get; init; }

	/// <summary>
	/// Gets or sets special state to be exposed to converters during serialization.
	/// </summary>
	/// <param name="key">Any object that can act as a key in a dictionary.</param>
	/// <returns>The value stored under the specified key, or <see langword="null" /> if no value has been stored under that key.</returns>
	/// <remarks>
	/// <para>A key-value pair is removed from the underlying dictionary by assigning a value of <see langword="null" /> for a given key.</para>
	/// <para>
	/// Strings can serve as convenient keys, but may collide with the same string used by another part of the data model for another purpose.
	/// Make your strings sufficiently unique to avoid collisions, or use a <c>static readonly object MyKey = new object()</c> field that you expose
	/// such that all interested parties can access the object for a key that is guaranteed to be unique.
	/// </para>
	/// </remarks>
	/// <example>
	/// To add, modify or remove a key in this state as applied to a <see cref="JsonSerializer.StartingContext"/>,
	/// capture and change the <see cref="SerializationContext"/> as a local variable, then reassign it to the serializer.
	/// </example>
	public object? this[object key]
	{
		get => this.specialState!.TryGetValue(key, out object? value) ? value : null;
		set => this.specialState = value is not null
			? this.specialState!.SetItem(key, value)
			: this.specialState!.Remove(key);
	}

	/// <summary>
	/// Decrements the depth remaining and checks the cancellation token.
	/// </summary>
	/// <remarks>
	/// Converters that (de)serialize nested objects should invoke this once before delegating to nested converters.
	/// </remarks>
	/// <exception cref="InvalidOperationException">Thrown if the depth limit has been exceeded.</exception>
	/// <exception cref="OperationCanceledException">Thrown if <see cref="CancellationToken"/> has been canceled.</exception>
	public void DepthStep()
	{
		this.CancellationToken.ThrowIfCancellationRequested();
		if (--this.MaxDepth < 0)
		{
			throw new InvalidOperationException("Exceeded maximum depth of object graph.");
		}
	}

	/// <summary>
	/// Gets a converter for a specific type shape.
	/// </summary>
	/// <typeparam name="T">The type to convert.</typeparam>
	/// <param name="shape">The type shape describing the type.</param>
	/// <returns>The converter.</returns>
	public JsonConverter<T> GetConverter<T>(ITypeShape<T> shape)
	{
		Requires.NotNull(shape);
		return this.Cache.GetOrAddConverter(shape);
	}

	/// <summary>
	/// Gets a converter for a specific type shape.
	/// </summary>
	/// <param name="shape">The type shape describing the type.</param>
	/// <returns>The converter.</returns>
	public JsonConverter GetConverter(ITypeShape shape)
	{
		Requires.NotNull(shape);
		return this.Cache.GetOrAddConverter(shape);
	}

	/// <summary>
	/// Starts a new serialization or deserialization operation.
	/// </summary>
	/// <param name="owner">The owning serializer.</param>
	/// <param name="cache">The converter cache for the operation.</param>
	/// <param name="cancellationToken">A cancellation token to associate with the operation.</param>
	/// <returns>The initialized context for the operation.</returns>
	internal SerializationContext Start(JsonSerializer owner, ConverterCache cache, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		return this with
		{
			Cache = cache,
			CancellationToken = cancellationToken,
			SerializeDefaultValues = owner.SerializeDefaultValues,
			DeserializeDefaultValues = owner.DeserializeDefaultValues,
			ReferenceTracker = owner.PreserveReferences == ReferencePreservationMode.Off ? null : new JsonReferenceEqualityTracker(owner.PreserveReferences),
			StringInterningCache = owner.InternStrings ? new StringInterning() : null,
		};
	}
}
