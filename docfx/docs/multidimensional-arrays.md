# Multidimensional arrays

Rectangular multidimensional arrays (rank 2 and higher, such as `int[,]` or
`double[,,]`) serialize to and from nested JSON arrays. Each dimension becomes one
level of JSON array nesting, so a rank-2 array is written as `[[r0c0, r0c1], [r1c0, r1c1]]`.

[!code-csharp[](../../samples/cs/MultidimensionalArrays.cs#MultidimensionalArrays)]

Arbitrary CLR array rank is supported. Elements may be any serializable type, including
nested objects and collections.

## Inferring dimensions

JSON carries no dimension metadata, so deserialization discovers the dimension lengths
from the nested array structure. The elements are buffered while the structure is read,
which requires memory proportional to the number of elements. Once the full structure is
known, the rectangular array is allocated and populated in a single pass.

## Rejected input

Because the target is a rectangular array, the following inputs are rejected with a
<xref:Nerdbank.Json.JsonSerializationException>:

* **Ragged input** — sibling arrays with differing lengths, such as `[[1,2],[3]]`.
* **Inconsistent nesting depth** — a mixture of arrays and scalars at the same level,
  such as `[[1,2],3]`, or input that is not nested deeply enough for the array's rank,
  such as `[1,2]` for a rank-2 array.

A JSON `null` deserializes to a `null` array reference.

## Limits

Each level of array nesting counts against
<xref:Nerdbank.Json.SerializationContext.MaxDepth> and observes the operation's
<xref:Nerdbank.Json.SerializationContext.CancellationToken>. The inferred element count
must match the allocated array, guarding against inconsistent input.
