// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1600 // Elements should be documented

using System;
using System.Threading;

namespace Nerdbank.Json;

internal abstract class JsonConverter
{
	internal abstract Type DataType { get; }

	internal abstract void WriteObject(ref JsonWriter writer, object? value, JsonSerializer serializer);

	internal abstract object? ReadObject(ref JsonReader reader, JsonSerializer serializer);
}

internal abstract class JsonConverter<T> : JsonConverter
{
	internal override Type DataType => typeof(T);

	internal abstract void Write(ref JsonWriter writer, T? value, JsonSerializer serializer);

	internal abstract T? Read(ref JsonReader reader, JsonSerializer serializer);

	internal sealed override void WriteObject(ref JsonWriter writer, object? value, JsonSerializer serializer)
		=> this.Write(ref writer, (T?)value, serializer);

	internal sealed override object? ReadObject(ref JsonReader reader, JsonSerializer serializer)
		=> this.Read(ref reader, serializer);
}

internal sealed class BuiltInJsonConverter<T> : JsonConverter<T>
{
	internal override void Write(ref JsonWriter writer, T? value, JsonSerializer serializer)
	{
		if (!BuiltInJsonConverters.TrySerialize(ref writer, value))
		{
			throw new NotSupportedException($"The built-in JSON serializer does not yet support values of type {typeof(T).FullName}.");
		}
	}

	internal override T? Read(ref JsonReader reader, JsonSerializer serializer)
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

	internal void SetInner(JsonConverter<T> inner)
	{
		this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
		this.initialized.Set();
	}

	internal override void Write(ref JsonWriter writer, T? value, JsonSerializer serializer)
	{
		this.initialized.Wait();
		this.inner!.Write(ref writer, value, serializer);
	}

	internal override T? Read(ref JsonReader reader, JsonSerializer serializer)
	{
		this.initialized.Wait();
		return this.inner!.Read(ref reader, serializer);
	}
}
