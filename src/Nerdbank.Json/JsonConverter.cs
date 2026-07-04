// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Internal helper elements are intentionally undocumented in this file.

using PolyType.Utilities;

namespace Nerdbank.Json;

/// <summary>
/// Base type for JSON converters.
/// </summary>
public abstract class JsonConverter
{
	internal abstract Type DataType { get; }

	internal abstract void WriteObject(ref JsonWriter writer, object? value, SerializationContext context);

	internal abstract object? ReadObject(ref JsonReader reader, SerializationContext context);
}

/// <summary>
/// Base type for JSON converters that handle a specific .NET type.
/// </summary>
/// <typeparam name="T">The .NET type handled by the converter.</typeparam>
public abstract class JsonConverter<T> : JsonConverter
{
	internal override Type DataType => typeof(T);

	/// <summary>
	/// Writes a value as JSON.
	/// </summary>
	/// <param name="writer">The writer to receive the JSON value.</param>
	/// <param name="value">The value to write.</param>
	/// <param name="context">Context for the serialization operation.</param>
	public abstract void Write(ref JsonWriter writer, T? value, SerializationContext context);

	/// <summary>
	/// Reads a value from JSON.
	/// </summary>
	/// <param name="reader">The reader to consume the JSON value from.</param>
	/// <param name="context">Context for the deserialization operation.</param>
	/// <returns>The deserialized value.</returns>
	public abstract T? Read(ref JsonReader reader, SerializationContext context);

	internal sealed override void WriteObject(ref JsonWriter writer, object? value, SerializationContext context)
		=> this.Write(ref writer, (T?)value, context);

	internal sealed override object? ReadObject(ref JsonReader reader, SerializationContext context)
		=> this.Read(ref reader, context);
}

internal sealed class BuiltInJsonConverter<T> : JsonConverter<T>
{
	public override void Write(ref JsonWriter writer, T? value, SerializationContext context)
	{
		if (BuiltInJsonConverters.RequiresNestedContext(typeof(T)))
		{
			context.DepthStep();
		}
		else
		{
			context.CancellationToken.ThrowIfCancellationRequested();
		}

		if (!BuiltInJsonConverters.TrySerialize(ref writer, value))
		{
			throw new NotSupportedException($"The built-in JSON serializer does not yet support values of type {typeof(T).FullName}.");
		}
	}

	public override T? Read(ref JsonReader reader, SerializationContext context)
	{
		if (BuiltInJsonConverters.RequiresNestedContext(typeof(T)))
		{
			context.DepthStep();
		}
		else
		{
			context.CancellationToken.ThrowIfCancellationRequested();
		}

		if (!BuiltInJsonConverters.TryDeserialize(ref reader, out T value))
		{
			throw new NotSupportedException($"The built-in JSON serializer does not yet support values of type {typeof(T).FullName}.");
		}

		return value;
	}
}

internal sealed class DelayedJsonConverterFactory : IDelayedValueFactory
{
	public DelayedValue Create<T>(ITypeShape<T> typeShape)
		=> new DelayedValue<JsonConverter>(self => new DelayedJsonConverter<T>(self));

	private sealed class DelayedJsonConverter<T>(DelayedValue<JsonConverter> self) : JsonConverter<T>
	{
		public override void Write(ref JsonWriter writer, T? value, SerializationContext context)
			=> ((JsonConverter<T>)self.Result).Write(ref writer, value, context);

		public override T? Read(ref JsonReader reader, SerializationContext context)
			=> ((JsonConverter<T>)self.Result).Read(ref reader, context);
	}
}
