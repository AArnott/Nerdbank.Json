// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1202 // Keep internal operation-state members near the top of this context type.

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
	private ConverterCache? cache;

	/// <summary>
	/// Initializes a new instance of the <see cref="SerializationContext"/> struct.
	/// </summary>
	public SerializationContext()
	{
	}

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
	/// Gets a cancellation token that can be used to cancel the serialization operation.
	/// </summary>
	public CancellationToken CancellationToken { get; init; }

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
			ReferenceTracker = owner.PreserveReferences == ReferencePreservationMode.Off ? null : new JsonReferenceEqualityTracker(),
		};
	}
}
