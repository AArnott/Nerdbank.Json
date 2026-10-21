// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using JsonSchemaArray = System.Text.Json.Nodes.JsonArray;
using JsonSchemaNode = System.Text.Json.Nodes.JsonNode;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Internal helper elements are intentionally undocumented in this file.

using System.Globalization;
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
	public virtual JsonSchemaObject? GetJsonSchema(JsonSchemaContext context, ITypeShape typeShape) => null;

	/// <summary>Skips to the value of a logical property in this converter's representation.</summary>
	/// <param name="reader">The reader positioned at this converter's value.</param>
	/// <param name="propertyShape">The logical property to select.</param>
	/// <param name="context">The serialization context.</param>
	/// <returns>Whether the property was found, with the reader ready to read its value.</returns>
	public virtual bool SkipToPropertyValue(ref JsonReader reader, IPropertyShape propertyShape, SerializationContext context)
	{
		Requires.NotNull(propertyShape);
		context.CancellationToken.ThrowIfCancellationRequested();
		JsonNavigationSegment segment = CreatePropertySegment(propertyShape, context);
		return this.TryNavigate(ref reader, in segment, GetNavigationOptions(context), context);
	}

	/// <summary>Skips to the value at a logical array index or dictionary key.</summary>
	/// <param name="reader">The reader positioned at this converter's value.</param>
	/// <param name="index">The array index or dictionary key.</param>
	/// <param name="context">The serialization context.</param>
	/// <returns>Whether the value was found, with the reader ready to read it.</returns>
	public virtual bool SkipToIndexValue(ref JsonReader reader, object? index, SerializationContext context)
	{
		context.CancellationToken.ThrowIfCancellationRequested();
		JsonNavigationSegment segment = CreateIndexSegment(index, reader.PeekValueToken() == '[', context);
		return this.TryNavigate(ref reader, in segment, GetNavigationOptions(context), context);
	}

	/// <summary>Asynchronously skips to the value of a logical property without buffering its enclosing value.</summary>
	/// <param name="reader">The asynchronous reader.</param>
	/// <param name="propertyShape">The logical property to select.</param>
	/// <param name="context">The serialization context.</param>
	/// <returns>Whether the property was found, with the reader ready to read its value.</returns>
	/// <remarks>Use the reader's structural methods to enter containers so envelope draining can account for them.</remarks>
	public virtual async ValueTask<bool> SkipToPropertyValueAsync(JsonAsyncReader reader, IPropertyShape propertyShape, SerializationContext context)
	{
		Requires.NotNull(reader);
		Requires.NotNull(propertyShape);
		context.CancellationToken.ThrowIfCancellationRequested();
		return await this.TryNavigateAsync(reader, CreatePropertySegment(propertyShape, context), GetNavigationOptions(context), context).ConfigureAwait(false) >= 0;
	}

	/// <summary>Asynchronously skips to a logical array index or dictionary key.</summary>
	/// <param name="reader">The asynchronous reader.</param>
	/// <param name="index">The array index or dictionary key.</param>
	/// <param name="context">The serialization context.</param>
	/// <returns>Whether the value was found, with the reader ready to read it.</returns>
	/// <remarks>Use the reader's structural methods to enter containers so envelope draining can account for them.</remarks>
	public virtual async ValueTask<bool> SkipToIndexValueAsync(JsonAsyncReader reader, object? index, SerializationContext context)
	{
		Requires.NotNull(reader);
		context.CancellationToken.ThrowIfCancellationRequested();
		JsonNavigationSegment segment = CreateIndexSegment(index, await reader.PeekNextByteAsync().ConfigureAwait(false) == '[', context);
		return await this.TryNavigateAsync(reader, segment, GetNavigationOptions(context), context).ConfigureAwait(false) >= 0;
	}

	/// <summary>Writes an untyped value.</summary>
	/// <param name="writer">The destination writer.</param>
	/// <param name="value">The value to write.</param>
	/// <param name="context">The operation context.</param>
	public abstract void WriteObject(ref JsonWriter writer, object? value, SerializationContext context);

	/// <summary>Reads an untyped value.</summary>
	/// <param name="reader">The source reader.</param>
	/// <param name="context">The operation context.</param>
	/// <returns>The deserialized value.</returns>
	public abstract object? ReadObject(ref JsonReader reader, SerializationContext context);

	/// <summary>Writes an untyped value asynchronously.</summary>
	/// <param name="writer">The destination writer.</param>
	/// <param name="value">The value to write.</param>
	/// <param name="context">The operation context.</param>
	/// <returns>A task tracking the write.</returns>
	public abstract ValueTask WriteObjectAsync(JsonAsyncWriter writer, object? value, SerializationContext context);

	/// <summary>Reads an untyped value asynchronously.</summary>
	/// <param name="reader">The source reader.</param>
	/// <param name="context">The operation context.</param>
	/// <returns>The deserialized value.</returns>
	public abstract ValueTask<object?> ReadObjectAsync(JsonAsyncReader reader, SerializationContext context);

	/// <summary>
	/// Advances the reader from this converter's value to the child value selected by a targeted-deserialization segment.
	/// </summary>
	/// <param name="reader">The reader positioned at this converter's JSON value.</param>
	/// <param name="segment">The member or index to navigate into.</param>
	/// <param name="options">The navigation options (name comparer and case sensitivity).</param>
	/// <param name="context">The serialization context.</param>
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
	internal virtual bool TryNavigate(ref JsonReader reader, in JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
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
	internal virtual ValueTask<int> TryNavigateAsync(JsonAsyncReader reader, JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
		=> JsonAsyncTargetedNavigator.NavigateRawTokenAsync(reader, segment, options, context);

	/// <summary>Dispatches a parsed expression segment to the public converter navigation hook.</summary>
	/// <param name="reader">The reader at the current value.</param>
	/// <param name="segment">The logical expression segment.</param>
	/// <param name="context">The active serialization context.</param>
	/// <returns>Whether the selected value was found.</returns>
	internal bool SkipToValue(ref JsonReader reader, in JsonNavigationSegment segment, SerializationContext context)
		=> segment.PropertyShape is { } property
			? this.SkipToPropertyValue(ref reader, property, context)
			: this.SkipToIndexValue(ref reader, segment.Key, context);

	/// <summary>Dispatches a navigation hook while accounting for containers entered by a custom converter.</summary>
	/// <param name="reader">The asynchronous reader.</param>
	/// <param name="segment">The logical expression segment.</param>
	/// <param name="context">The active serialization context.</param>
	/// <returns>The open container count, or a negative value if the path is missing.</returns>
	internal async ValueTask<int> SkipToValueAsync(JsonAsyncReader reader, JsonNavigationSegment segment, SerializationContext context)
	{
		int depth = reader.NavigationDepth;
		bool found = segment.PropertyShape is { } property
			? await this.SkipToPropertyValueAsync(reader, property, context).ConfigureAwait(false)
			: await this.SkipToIndexValueAsync(reader, segment.Key, context).ConfigureAwait(false);
		return found ? reader.NavigationDepth - depth : -1;
	}

	/// <summary>Gets this converter without its reference-preservation wrapper.</summary>
	/// <returns>The converter for the underlying value representation.</returns>
	internal virtual JsonConverter UnwrapReferencePreservation() => this;

	private static JsonNavigationSegment CreatePropertySegment(IPropertyShape propertyShape, SerializationContext context)
		=> new(JsonPathSegment.ForMember(context.Cache.GetSerializedPropertyName(propertyShape.Name, propertyShape.AttributeProvider), propertyShape));

	private static JsonNavigationSegment CreateIndexSegment(object? index, bool isArray, SerializationContext context)
	{
		if (index is null)
		{
			throw new ArgumentNullException(nameof(index));
		}

		if (isArray)
		{
			if (index is not int value || value < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(index), index, "JSON array navigation requires a non-negative integer index.");
			}

			return new(JsonPathSegment.ForIndex(value));
		}

		string key = index is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : index.ToString()!;
		key = context.Cache.DictionaryKeyNamingPolicy?.ConvertName(key) ?? key;
		return new(JsonPathSegment.ForMember(key, key: index));
	}

	private static JsonNavigationOptions GetNavigationOptions(SerializationContext context)
		=> new(context.Cache.PropertyNameComparer, context.Cache.PropertyNameComparer.Equals("a", "A"));
}

/// <summary>
/// Base type for JSON converters that handle a specific .NET type.
/// </summary>
/// <typeparam name="T">The .NET type handled by the converter.</typeparam>
public abstract partial class JsonConverter<T> : JsonConverter
{
	internal override Type DataType => typeof(T);

	/// <summary>
	/// Writes a value as JSON.
	/// </summary>
	/// <param name="writer">The writer to receive the JSON value.</param>
	/// <param name="value">The value to write.</param>
	/// <param name="context">Context for the serialization operation.</param>
	public abstract void Write(ref JsonWriter writer, in T? value, SerializationContext context);

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

	/// <inheritdoc/>
	public sealed override void WriteObject(ref JsonWriter writer, object? value, SerializationContext context)
		=> this.Write(ref writer, (T?)value, context);

	/// <inheritdoc/>
	public sealed override object? ReadObject(ref JsonReader reader, SerializationContext context)
		=> this.Read(ref reader, context);

	/// <inheritdoc/>
	public sealed override ValueTask WriteObjectAsync(JsonAsyncWriter writer, object? value, SerializationContext context)
		=> this.WriteAsync(writer, (T?)value, context);

	/// <inheritdoc/>
	public sealed override async ValueTask<object?> ReadObjectAsync(JsonAsyncReader reader, SerializationContext context)
		=> await this.ReadAsync(reader, context).ConfigureAwait(false);
}

internal sealed class BuiltInJsonConverter<T> : JsonConverter<T>
{
	public override void Write(ref JsonWriter writer, in T? value, SerializationContext context)
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

		if (!BuiltInJsonConverters.TryDeserialize(ref reader, context, out T value))
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
		public override void Write(ref JsonWriter writer, in T? value, SerializationContext context)
			=> ((JsonConverter<T>)self.Result).Write(ref writer, value, context);

		public override T? Read(ref JsonReader reader, SerializationContext context)
			=> ((JsonConverter<T>)self.Result).Read(ref reader, context);
	}
}
