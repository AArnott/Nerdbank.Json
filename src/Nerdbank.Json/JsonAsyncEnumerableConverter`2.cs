// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Writes an asynchronous enumerable incrementally as a JSON array.
/// </summary>
/// <typeparam name="TEnumerable">The shaped enumerable type.</typeparam>
/// <typeparam name="TElement">The element type.</typeparam>
/// <param name="elementConverter">The converter for each element.</param>
internal sealed class JsonAsyncEnumerableConverter<TEnumerable, TElement>(JsonConverter<TElement> elementConverter) : JsonConverter<TEnumerable>
{
	/// <inheritdoc/>
	public override bool PreferAsyncSerialization => true;

	/// <inheritdoc/>
	public override void Write(ref JsonWriter writer, in TEnumerable? value, SerializationContext context)
		=> throw new NotSupportedException($"The type {typeof(TEnumerable)} implements IAsyncEnumerable<T> and requires asynchronous serialization. Use SerializeAsync instead.");

	/// <inheritdoc/>
	public override async ValueTask WriteAsync(JsonAsyncWriter writer, TEnumerable? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (value is null)
		{
			await writer.WriteNullAsync(context).ConfigureAwait(false);
			return;
		}

		context.DepthStep();
		JsonWriter syncWriter = writer.CreateWriter();
		syncWriter.WriteStartArray();
		writer.ReturnWriter(ref syncWriter);

		bool first = true;
		await foreach (TElement element in ((IAsyncEnumerable<TElement>)value).WithCancellation(context.CancellationToken).ConfigureAwait(false))
		{
			context.CancellationToken.ThrowIfCancellationRequested();
			if (!first)
			{
				syncWriter = writer.CreateWriter();
				syncWriter.WriteValueSeparator();
				writer.ReturnWriter(ref syncWriter);
			}

			first = false;
			await writer.WriteValueAsync(elementConverter, element, context).ConfigureAwait(false);
		}

		syncWriter = writer.CreateWriter();
		syncWriter.WriteEndArray();
		writer.ReturnWriter(ref syncWriter);
		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public override TEnumerable? Read(ref JsonReader reader, SerializationContext context)
		=> reader.TryReadNull() ? default : throw CreateReadNotSupportedException();

	/// <inheritdoc/>
	public override async ValueTask<TEnumerable?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
	{
		Requires.NotNull(reader);
		return await reader.TryReadNullAsync(context).ConfigureAwait(false) ? default : throw CreateReadNotSupportedException();
	}

	/// <summary>
	/// Creates an error describing the unsupported read contract.
	/// </summary>
	/// <returns>The error to throw.</returns>
	private static NotSupportedException CreateReadNotSupportedException()
		=> new($"JSON deserialization cannot construct asynchronous enumerable type {typeof(TEnumerable)}. Deserialize into a supported collection instead.");
}
