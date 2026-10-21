// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using System.Globalization;

namespace Nerdbank.Json;

/// <summary>
/// Converts rectangular arrays using <c>[[dimension lengths], [flattened elements]]</c>.
/// </summary>
/// <typeparam name="TArray">The multidimensional array type.</typeparam>
/// <typeparam name="TElement">The array element type.</typeparam>
/// <param name="elementConverter">The converter for each element.</param>
/// <param name="rank">The number of array dimensions.</param>
internal sealed class JsonFlattenedArrayConverter<TArray, TElement>(JsonConverter<TElement> elementConverter, int rank) : JsonConverter<TArray>
{
	/// <inheritdoc/>
	public override bool PreferAsyncSerialization => true;

	/// <inheritdoc/>
	public override void Write(ref JsonWriter writer, TArray? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		context.DepthStep();
		Array array = (Array)(object)value;
		WriteHeader(ref writer, array);
		bool first = true;
		foreach (TElement element in array)
		{
			context.CancellationToken.ThrowIfCancellationRequested();
			if (!first)
			{
				writer.WriteValueSeparator();
			}

			first = false;
			elementConverter.Write(ref writer, element, context);
		}

		writer.WriteEndArray();
		writer.WriteEndArray();
	}

	/// <inheritdoc/>
	public override async ValueTask WriteAsync(JsonAsyncWriter writer, TArray? value, SerializationContext context)
	{
		Requires.NotNull(writer);
		if (value is null)
		{
			await writer.WriteNullAsync(context).ConfigureAwait(false);
			return;
		}

		context.DepthStep();
		Array array = (Array)(object)value;
		JsonWriter syncWriter = writer.CreateWriter();
		WriteHeader(ref syncWriter, array);
		writer.ReturnWriter(ref syncWriter);
		bool first = true;
		foreach (TElement element in array)
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
		syncWriter.WriteEndArray();
		writer.ReturnWriter(ref syncWriter);
		await writer.FlushIfAppropriateAsync(context).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public override TArray? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		context.DepthStep();
		reader.ReadStartArray();
		reader.ReadStartArray();
		int[] dimensions = new int[rank];
		for (int i = 0; i < rank; i++)
		{
			if (i > 0)
			{
				reader.ReadValueSeparator();
			}

			dimensions[i] = ReadLength(ref reader);
		}

		if (!reader.TryReadEndArray())
		{
			throw new JsonSerializationException($"Expected array rank of {rank}.");
		}

		int expected = GetElementCount(dimensions);
		reader.ReadValueSeparator();
		reader.ReadStartArray();
		List<TElement> elements = [];
		if (!reader.TryReadEndArray())
		{
			while (true)
			{
				context.CancellationToken.ThrowIfCancellationRequested();
				ThrowIfTooManyElements(elements.Count, expected);
				elements.Add(elementConverter.Read(ref reader, context)!);
				if (reader.TryReadEndArray())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
		}

		if (!reader.TryReadEndArray())
		{
			throw new JsonSerializationException("Expected array length of 2 for the dimensions and data.");
		}

		return CreateArray(dimensions, elements, expected);
	}

	/// <inheritdoc/>
	public override async ValueTask<TArray?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
	{
		Requires.NotNull(reader);
		if (await reader.TryReadNullAsync(context).ConfigureAwait(false))
		{
			return default;
		}

		context.DepthStep();
		await reader.ReadStartArrayAsync(context).ConfigureAwait(false);
		await reader.ReadStartArrayAsync(context).ConfigureAwait(false);
		int[] dimensions = new int[rank];
		for (int i = 0; i < rank; i++)
		{
			if (i > 0)
			{
				await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
			}

			await reader.BufferNextValueAsync(context).ConfigureAwait(false);
			JsonReader syncReader = reader.CreateBufferedReader();
			dimensions[i] = ReadLength(ref syncReader);
			reader.ReturnReader(ref syncReader);
		}

		if (!await reader.TryReadEndArrayAsync(context).ConfigureAwait(false))
		{
			throw new JsonSerializationException($"Expected array rank of {rank}.");
		}

		int expected = GetElementCount(dimensions);
		await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
		await reader.ReadStartArrayAsync(context).ConfigureAwait(false);
		List<TElement> elements = [];
		if (!await reader.TryReadEndArrayAsync(context).ConfigureAwait(false))
		{
			while (true)
			{
				context.CancellationToken.ThrowIfCancellationRequested();
				ThrowIfTooManyElements(elements.Count, expected);
				elements.Add((await reader.ReadValueAsync(elementConverter, context).ConfigureAwait(false))!);
				if (await reader.TryReadEndArrayAsync(context).ConfigureAwait(false))
				{
					break;
				}

				await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
			}
		}

		if (!await reader.TryReadEndArrayAsync(context).ConfigureAwait(false))
		{
			throw new JsonSerializationException("Expected array length of 2 for the dimensions and data.");
		}

		return CreateArray(dimensions, elements, expected);
	}

	/// <summary>
	/// Reads a non-negative, 32-bit JSON integer dimension length.
	/// </summary>
	/// <param name="reader">The reader.</param>
	/// <returns>The dimension length.</returns>
	private static int ReadLength(ref JsonReader reader)
	{
		if (!int.TryParse(reader.ReadNumberToken(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int length) || length < 0)
		{
			throw new JsonSerializationException("Array dimensions must be non-negative 32-bit integers.");
		}

		return length;
	}

	/// <summary>
	/// Validates dimension arithmetic without allocating storage based on untrusted lengths.
	/// </summary>
	/// <param name="dimensions">The dimension lengths.</param>
	/// <returns>The expected element count.</returns>
	private static int GetElementCount(int[] dimensions)
	{
		long count = 1;
		try
		{
			foreach (int length in dimensions)
			{
				count = checked(count * length);
			}
		}
		catch (OverflowException ex)
		{
			throw new JsonSerializationException("Array dimensions are too large.", ex);
		}

		if (count > int.MaxValue)
		{
			throw new JsonSerializationException("Array dimensions are too large.");
		}

		return (int)count;
	}

	/// <summary>
	/// Rejects extra elements before deserializing them.
	/// </summary>
	/// <param name="actual">The number of elements already read.</param>
	/// <param name="expected">The expected number of elements.</param>
	private static void ThrowIfTooManyElements(int actual, int expected)
	{
		if (actual >= expected)
		{
			throw new JsonSerializationException($"Expected {expected} elements but found additional data.");
		}
	}

	/// <summary>
	/// Allocates the result only after the complete shape and element count have been validated.
	/// </summary>
	/// <param name="dimensions">The dimension lengths.</param>
	/// <param name="elements">The deserialized elements.</param>
	/// <param name="expected">The expected element count.</param>
	/// <returns>The populated array.</returns>
#if NET
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Array.CreateInstance materializes the statically referenced multidimensional array type TArray.")]
#endif
	private static TArray CreateArray(int[] dimensions, List<TElement> elements, int expected)
	{
		if (elements.Count != expected)
		{
			throw new JsonSerializationException($"Expected {expected} elements but found {elements.Count}.");
		}

		Array array = Array.CreateInstance(typeof(TElement), dimensions);
		int[] indices = new int[dimensions.Length];
		foreach (TElement element in elements)
		{
			array.SetValue(element, indices);
			for (int d = dimensions.Length - 1; d >= 0; d--)
			{
				if (++indices[d] < dimensions[d])
				{
					break;
				}

				indices[d] = 0;
			}
		}

		return (TArray)(object)array;
	}

	/// <summary>
	/// Writes dimension lengths and starts the flattened element array.
	/// </summary>
	/// <param name="writer">The writer.</param>
	/// <param name="array">The array to describe.</param>
	private static void WriteHeader(ref JsonWriter writer, Array array)
	{
		writer.WriteStartArray();
		writer.WriteStartArray();
		for (int i = 0; i < array.Rank; i++)
		{
			if (i > 0)
			{
				writer.WriteValueSeparator();
			}

			writer.WriteNumberValue(array.GetLength(i));
		}

		writer.WriteEndArray();
		writer.WriteValueSeparator();
		writer.WriteStartArray();
	}
}
