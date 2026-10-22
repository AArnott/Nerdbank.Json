# Multidimensional arrays

Rectangular multidimensional arrays (rank 2 and higher, such as `int[,]` or `double[,,]`) serialize to and from nested JSON arrays by default.
Each dimension becomes one level of JSON array nesting, so a rank-2 array is written as `[[r0c0, r0c1], [r1c0, r1c1]]`.
Select the representation with <xref:Nerdbank.Json.JsonSerializer.MultiDimensionalArrayFormat>.

[!code-csharp[](../../samples/cs/MultidimensionalArrays.cs#MultidimensionalArrays)]

Arbitrary CLR array rank is supported. Elements may be any serializable type, including
nested objects and collections.

## Inferring dimensions

The default <xref:Nerdbank.Json.MultiDimensionalArrayFormat.Nested> representation carries no dimension metadata, so deserialization discovers the dimension lengths from the nested array structure.
The elements are buffered while the structure is read, which requires memory proportional to the number of elements.
Once the full structure is known, the rectangular array is allocated and populated in a single pass.
When a dimension is empty, lengths of dimensions nested inside it cannot be inferred and deserialize as zero.

## Flat format

<xref:Nerdbank.Json.MultiDimensionalArrayFormat.Flat> mirrors MessagePack's representation using JSON numbers for dimension lengths: `[[length0,length1,...],[element0,element1,...]]`.
Elements are written in CLR row-major order, with the last dimension varying fastest.
For example, a two-row, three-column matrix is written as `[[2,3],[1,2,3,4,5,6]]`.
This representation preserves all dimension lengths even when any dimension is empty.
Lower bounds are not encoded; arrays with nonzero lower bounds serialize their elements in row-major order and deserialize with zero lower bounds.

Dimension lengths must be non-negative 32-bit JSON integers, and their product must fit in a supported CLR array length.
The dimensions array must contain exactly as many lengths as the target array's rank.
The data array must contain exactly the product of those lengths, and the outer array must contain exactly the dimensions and data arrays.
Invalid lengths, arithmetic overflow, and mismatched counts are rejected before allocating the rectangular array.
Reading buffers only actual elements, never preallocating storage from untrusted dimension lengths.
Asynchronous operations stream the data array and honor asynchronous element converters.

Schema export uses a two-item tuple with an exactly rank-sized array of non-negative integer lengths and a flat array of element schemas.
The relationship between the product of the lengths and the data count is enforced by the serializer, not expressible by these JSON Schema constraints.

## Rejected input

Because the target is a rectangular array, the following inputs are rejected with a
<xref:Nerdbank.Json.JsonSerializationException>:

* **Ragged input** — sibling arrays with differing lengths, such as `[[1,2],[3]]`.
* **Inconsistent nesting depth** — a mixture of arrays and scalars at the same level,
  such as `[[1,2],3]`, or input that is not nested deeply enough for the array's rank,
  such as `[1,2]` for a rank-2 array.

A JSON `null` deserializes to a `null` array reference.

## Limits

In nested format, each level of array nesting counts against <xref:Nerdbank.Json.SerializationContext.MaxDepth>.
In flat format, the array consumes one logical depth step, matching MessagePack; the dimensions and data containers do not add logical depth steps.
Element converters consume additional depth as usual.
Both formats observe the operation's <xref:Nerdbank.Json.SerializationContext.CancellationToken>.
Reference preservation continues to wrap the array in the native JSON `$id`/`$ref`/`$value` representation, independently of the selected array format.
