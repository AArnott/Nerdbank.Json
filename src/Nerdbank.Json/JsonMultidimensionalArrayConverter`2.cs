// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif

namespace Nerdbank.Json;

/// <summary>
/// Serializes and deserializes a rectangular multidimensional array (rank 2 or higher) as nested JSON arrays.
/// </summary>
/// <typeparam name="TArray">The multidimensional array type.</typeparam>
/// <typeparam name="TElement">The type of element stored in the array.</typeparam>
/// <remarks>
/// <para>
/// JSON has no representation for array dimensions, so each dimension is emitted as a nested JSON array:
/// a rank-2 array becomes <c>[[r0c0, r0c1], [r1c0, r1c1]]</c>.
/// </para>
/// <para>
/// Because JSON does not carry dimension metadata, deserialization buffers the elements while it infers the
/// dimension lengths from the nested array structure. Input that is ragged (sibling arrays of differing
/// lengths) or that has inconsistent nesting depth is rejected.
/// </para>
/// </remarks>
internal sealed class JsonMultidimensionalArrayConverter<TArray, TElement> : JsonConverter<TArray>
{
	private readonly JsonConverter<TElement> elementConverter;
	private readonly int rank;

	/// <summary>
	/// Initializes a new instance of the <see cref="JsonMultidimensionalArrayConverter{TArray, TElement}"/> class.
	/// </summary>
	/// <param name="elementConverter">The converter for the array's element type.</param>
	/// <param name="rank">The rank (number of dimensions) of the array.</param>
	internal JsonMultidimensionalArrayConverter(JsonConverter<TElement> elementConverter, int rank)
	{
		this.elementConverter = elementConverter;
		this.rank = rank;
	}

	/// <inheritdoc/>
	public override void Write(ref JsonWriter writer, TArray? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		Array array = (Array)(object)value;
		int[] dimensions = new int[this.rank];
		for (int i = 0; i < this.rank; i++)
		{
			dimensions[i] = array.GetLength(i);
		}

#if NET
		Span<TElement> elements = AsSpan(array);
		int elementIndex = 0;
		this.WriteDimension(ref writer, 0, dimensions, elements, ref elementIndex, context);
#else
		int[] indices = new int[this.rank];
		this.WriteDimension(ref writer, 0, dimensions, array, indices, context);
#endif
	}

	/// <inheritdoc/>
#if NET
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Array.CreateInstance materializes the statically referenced multidimensional array type TArray.")]
#endif
	public override TArray? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return default;
		}

		int[] dimensions = new int[this.rank];
		for (int i = 0; i < this.rank; i++)
		{
			dimensions[i] = -1;
		}

		List<TElement> elements = [];
		this.ReadDimension(ref reader, 0, dimensions, elements, context);

		for (int i = 0; i < this.rank; i++)
		{
			if (dimensions[i] < 0)
			{
				// A dimension whose length could never be observed (an empty outer dimension) is treated as empty.
				dimensions[i] = 0;
			}
		}

		Array array = Array.CreateInstance(typeof(TElement), dimensions);
		if (array.Length != elements.Count)
		{
			throw new JsonSerializationException($"The JSON produced {elements.Count} elements, which does not fill the inferred {DescribeDimensions(dimensions)} array '{typeof(TArray).FullName}'.") { Code = JsonSerializationException.ErrorCode.UnexpectedToken };
		}

		Populate(array, dimensions, elements);
		return (TArray)(object)array;
	}

	private static string DescribeDimensions(int[] dimensions) => string.Join("x", dimensions);

#if NET
	private static Span<TElement> AsSpan(Array array)
		=> MemoryMarshal.CreateSpan(ref Unsafe.As<byte, TElement>(ref MemoryMarshal.GetArrayDataReference(array)), array.Length);

	private static void Populate(Array array, int[] dimensions, List<TElement> elements)
		=> CollectionsMarshal.AsSpan(elements).CopyTo(AsSpan(array));

	private void WriteDimension(ref JsonWriter writer, int depth, int[] dimensions, Span<TElement> elements, ref int elementIndex, SerializationContext context)
	{
		context.DepthStep();
		writer.WriteStartArray();
		int length = dimensions[depth];
		bool innermost = depth == this.rank - 1;
		for (int i = 0; i < length; i++)
		{
			if (i > 0)
			{
				writer.WriteValueSeparator();
			}

			if (innermost)
			{
				this.elementConverter.Write(ref writer, elements[elementIndex++], context);
			}
			else
			{
				this.WriteDimension(ref writer, depth + 1, dimensions, elements, ref elementIndex, context);
			}
		}

		writer.WriteEndArray();
	}
#else
	private static void Populate(Array array, int[] dimensions, List<TElement> elements)
	{
		int[] indices = new int[dimensions.Length];
		for (int flat = 0; flat < elements.Count; flat++)
		{
			array.SetValue(elements[flat], indices);
			IncrementIndices(indices, dimensions);
		}
	}

	private static void IncrementIndices(int[] indices, int[] dimensions)
	{
		for (int d = dimensions.Length - 1; d >= 0; d--)
		{
			if (++indices[d] < dimensions[d])
			{
				break;
			}

			indices[d] = 0;
		}
	}

	private void WriteDimension(ref JsonWriter writer, int depth, int[] dimensions, Array array, int[] indices, SerializationContext context)
	{
		context.DepthStep();
		writer.WriteStartArray();
		int length = dimensions[depth];
		bool innermost = depth == this.rank - 1;
		for (int i = 0; i < length; i++)
		{
			indices[depth] = i;
			if (i > 0)
			{
				writer.WriteValueSeparator();
			}

			if (innermost)
			{
				this.elementConverter.Write(ref writer, (TElement)array.GetValue(indices)!, context);
			}
			else
			{
				this.WriteDimension(ref writer, depth + 1, dimensions, array, indices, context);
			}
		}

		writer.WriteEndArray();
	}
#endif

	private void ReadDimension(ref JsonReader reader, int depth, int[] dimensions, List<TElement> elements, SerializationContext context)
	{
		context.DepthStep();
		reader.ReadStartArray();

		int count = 0;
		bool innermost = depth == this.rank - 1;
		if (!reader.TryReadEndArray())
		{
			while (true)
			{
				if (innermost)
				{
					elements.Add(this.elementConverter.Read(ref reader, context)!);
				}
				else
				{
					if (!reader.IsNextTokenStartArray())
					{
						throw new JsonSerializationException($"Expected a nested JSON array at depth {depth + 1} while deserializing rank-{this.rank} array '{typeof(TArray).FullName}', but found a non-array token. The input is ragged or its nesting depth is inconsistent.") { Code = JsonSerializationException.ErrorCode.UnexpectedToken };
					}

					this.ReadDimension(ref reader, depth + 1, dimensions, elements, context);
				}

				count++;
				if (reader.TryReadEndArray())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
		}

		if (dimensions[depth] < 0)
		{
			dimensions[depth] = count;
		}
		else if (dimensions[depth] != count)
		{
			throw new JsonSerializationException($"Ragged JSON array: at depth {depth} a sibling of rank-{this.rank} array '{typeof(TArray).FullName}' has {count} elements, but {dimensions[depth]} were required to keep the array rectangular.") { Code = JsonSerializationException.ErrorCode.UnexpectedToken };
		}
	}
}
