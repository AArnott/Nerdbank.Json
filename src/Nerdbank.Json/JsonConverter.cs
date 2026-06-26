// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Internal helper elements are intentionally undocumented in this file.

using System;
using System.Threading;

namespace Nerdbank.Json;

/// <summary>
/// Base type for JSON converters.
/// </summary>
public abstract class JsonConverter
{
	internal abstract Type DataType { get; }

	internal abstract void WriteObject(ref JsonWriter writer, object? value, JsonSerializer serializer);

	internal abstract object? ReadObject(ref JsonReader reader, JsonSerializer serializer);
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
	/// <param name="serializer">The serializer invoking the converter.</param>
	public abstract void Write(ref JsonWriter writer, T? value, JsonSerializer serializer);

	/// <summary>
	/// Reads a value from JSON.
	/// </summary>
	/// <param name="reader">The reader to consume the JSON value from.</param>
	/// <param name="serializer">The serializer invoking the converter.</param>
	/// <returns>The deserialized value.</returns>
	public abstract T? Read(ref JsonReader reader, JsonSerializer serializer);

	internal sealed override void WriteObject(ref JsonWriter writer, object? value, JsonSerializer serializer)
		=> this.Write(ref writer, (T?)value, serializer);

	internal sealed override object? ReadObject(ref JsonReader reader, JsonSerializer serializer)
		=> this.Read(ref reader, serializer);
}

internal sealed class BuiltInJsonConverter<T> : JsonConverter<T>
{
	public override void Write(ref JsonWriter writer, T? value, JsonSerializer serializer)
	{
		if (!BuiltInJsonConverters.TrySerialize(ref writer, value))
		{
			throw new NotSupportedException($"The built-in JSON serializer does not yet support values of type {typeof(T).FullName}.");
		}
	}

	public override T? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		if (!BuiltInJsonConverters.TryDeserialize(ref reader, out T value))
		{
			throw new NotSupportedException($"The built-in JSON serializer does not yet support values of type {typeof(T).FullName}.");
		}

		return value;
	}
}

internal sealed class DeferredJsonConverter<T> : JsonConverter<T>
{
	private readonly ManualResetEventSlim initialized = new(false);
	private JsonConverter<T>? inner;

	public override void Write(ref JsonWriter writer, T? value, JsonSerializer serializer)
	{
		this.initialized.Wait();
		this.inner!.Write(ref writer, value, serializer);
	}

	public override T? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		this.initialized.Wait();
		return this.inner!.Read(ref reader, serializer);
	}

	internal void SetInner(JsonConverter<T> inner)
	{
		this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
		this.initialized.Set();
	}
}
