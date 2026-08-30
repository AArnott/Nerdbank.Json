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
	/// <summary>
	/// Gets a value indicating whether this converter's asynchronous methods should be preferred over its synchronous
	/// methods when a graph is being (de)serialized asynchronously.
	/// </summary>
	/// <remarks>
	/// The default is <see langword="false"/>, which means the default asynchronous methods buffer a whole value and
	/// delegate to the synchronous methods. Converters that may (de)serialize very large values should override the
	/// asynchronous methods, set this to <see langword="true"/>, and stream fragments so that memory use stays bounded.
	/// </remarks>
	public virtual bool PreferAsyncSerialization => false;

	internal abstract Type DataType { get; }

	/// <summary>
	/// Produces a JSON Schema fragment that describes how this converter represents its value, or
	/// <see langword="null"/> if this converter cannot describe its representation.
	/// </summary>
	/// <param name="context">The schema-generation context, which resolves nested schemas and shared definitions.</param>
	/// <param name="typeShape">The shape of the type this converter handles.</param>
	/// <returns>
	/// A schema fragment, or <see langword="null"/> to indicate the representation is undocumented. When
	/// <see langword="null"/> is returned, the exporter emits a permissive schema annotated with a conspicuous
	/// comment rather than pretending to know the representation.
	/// </returns>
	public virtual JsonSchema? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => null;

	/// <summary>
	/// Advances the reader from this converter's value to the child value selected by a targeted-deserialization segment.
	/// </summary>
	/// <param name="reader">The reader positioned at this converter's JSON value.</param>
	/// <param name="segment">The member or index to navigate into.</param>
	/// <param name="options">The navigation options (name comparer and case sensitivity).</param>
	/// <returns>
	/// <see langword="true"/> with the reader positioned at the selected child value; otherwise <see langword="false"/>
	/// when the child is absent (a missing path).
	/// </returns>
	/// <remarks>
	/// The default implementation navigates a self-describing JSON object or array using raw token skipping. Converters
	/// whose representation is not a plain object or array (such as unions that emit a <c>[discriminator, payload]</c>
	/// envelope, or custom converters that remap members) should override this method to translate the segment into a
	/// navigation over their representation, typically by delegating to the converter of the underlying value.
	/// </remarks>
	public virtual bool TryNavigate(ref JsonReader reader, in JsonNavigationSegment segment, JsonNavigationOptions options)
		=> JsonTargetedNavigator.NavigateRawToken(ref reader, in segment, options);

	/// <summary>
	/// Asynchronously advances the reader from this converter's value toward the child selected by a targeted-navigation
	/// segment, without buffering the enclosing document.
	/// </summary>
	/// <param name="reader">The asynchronous reader positioned at this converter's JSON value.</param>
	/// <param name="segment">The member or index to navigate into.</param>
	/// <param name="options">The navigation options (name comparer and case sensitivity).</param>
	/// <param name="context">The serialization context.</param>
	/// <returns>
	/// A task whose result is the number of JSON containers this converter left open on the path to the selected child
	/// (so the caller can later consume them), or a negative value when the child is absent (a missing path).
	/// </returns>
	/// <remarks>
	/// The default implementation navigates a self-describing JSON object or array using the asynchronous reader.
	/// Converters whose representation is a union envelope or a custom shape should override this method to translate the
	/// segment into a navigation over their representation, typically by delegating to the converter of the underlying
	/// value and adding the number of containers they themselves opened.
	/// </remarks>
	public virtual ValueTask<int> TryNavigateAsync(JsonAsyncReader reader, JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
		=> JsonAsyncTargetedNavigator.NavigateRawTokenAsync(reader, segment, options, context);

	internal abstract void WriteObject(ref JsonWriter writer, object? value, SerializationContext context);

	internal abstract object? ReadObject(ref JsonReader reader, SerializationContext context);

	internal abstract ValueTask WriteObjectAsync(JsonAsyncWriter writer, object? value, SerializationContext context);

	internal abstract ValueTask<object?> ReadObjectAsync(JsonAsyncReader reader, SerializationContext context);
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

	/// <summary>
	/// Writes a value as JSON to an asynchronous, incremental writer.
	/// </summary>
	/// <param name="writer">The asynchronous writer.</param>
	/// <param name="value">The value to write.</param>
	/// <param name="context">Context for the serialization operation.</param>
	/// <returns>A task tracking the asynchronous write.</returns>
	/// <remarks>
	/// The default implementation writes the whole value synchronously and then flushes the pipe if the buffer is large.
	/// Override this only when a value may be very large; write fragments and periodically call
	/// <see cref="JsonAsyncWriter.FlushIfAppropriateAsync"/> to keep memory bounded.
	/// </remarks>
	public virtual ValueTask WriteAsync(JsonAsyncWriter writer, T? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		context.CancellationToken.ThrowIfCancellationRequested();

		JsonWriter syncWriter = writer.CreateWriter();
		this.Write(ref syncWriter, value, context);
		writer.ReturnWriter(ref syncWriter);
		return writer.FlushIfAppropriateAsync(context);
	}

	/// <summary>
	/// Reads a value from an asynchronous, incremental reader.
	/// </summary>
	/// <param name="reader">The asynchronous reader.</param>
	/// <param name="context">Context for the deserialization operation.</param>
	/// <returns>A task whose result is the deserialized value.</returns>
	/// <remarks>
	/// The default implementation buffers the next complete JSON value and delegates to <see cref="Read"/>. Override
	/// this only when a value may be very large; read fragments and periodically await more data so memory stays bounded.
	/// </remarks>
	public virtual async ValueTask<T?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
	{
		Requires.NotNull(reader);
		context.CancellationToken.ThrowIfCancellationRequested();

		await reader.BufferNextValueAsync(context).ConfigureAwait(false);
		JsonReader syncReader = reader.CreateBufferedReader();
		T? result = this.Read(ref syncReader, context);
		reader.ReturnReader(ref syncReader);
		return result;
	}

	internal sealed override void WriteObject(ref JsonWriter writer, object? value, SerializationContext context)
		=> this.Write(ref writer, (T?)value, context);

	internal sealed override object? ReadObject(ref JsonReader reader, SerializationContext context)
		=> this.Read(ref reader, context);

	internal sealed override ValueTask WriteObjectAsync(JsonAsyncWriter writer, object? value, SerializationContext context)
		=> this.WriteAsync(writer, (T?)value, context);

	internal sealed override async ValueTask<object?> ReadObjectAsync(JsonAsyncReader reader, SerializationContext context)
		=> await this.ReadAsync(reader, context).ConfigureAwait(false);
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
