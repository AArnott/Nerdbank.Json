# Asynchronous streaming

Nerdbank.Json can serialize to and deserialize from streams and pipes **incrementally**, without
buffering the entire JSON document in memory. This keeps memory bounded for large object graphs,
applies cooperative backpressure, and supports cancellation.

[!code-csharp[](../../samples/cs/AsyncStreaming.cs#AsyncStreaming)]

## API surface

* <xref:Nerdbank.Json.JsonSerializer.SerializeAsync*> and
  <xref:Nerdbank.Json.JsonSerializer.DeserializeAsync*> accept either a
  <xref:System.IO.Stream> or a <xref:System.IO.Pipelines.PipeReader>/<xref:System.IO.Pipelines.PipeWriter>.
* On .NET, source-generated `IShapeable<T>` and witness (`TProvider`) convenience overloads are
  available; on .NET Standard and .NET Framework the shape-accepting overloads are used.

## How it works

Reading uses an incremental UTF-8 scanner that frames one JSON value at a time. Because it is a
resumable, byte-level state machine, JSON tokens may be split at **any** buffer boundary — inside a
multi-byte UTF-8 sequence, an escaped string, a `\uXXXX` escape, a number or exponent, a literal, a
comment, or between delimiters — and are reassembled correctly. Only enough bytes to decode the value
currently being read are held in memory.

Writing periodically flushes to the pipe once the buffered bytes exceed
<xref:Nerdbank.Json.SerializationContext.UnflushedBytesThreshold> (64&nbsp;KB by default), so a large
array or map is emitted in fragments rather than being fully materialized first.

## Streaming granularity

Built-in collection, dictionary, and object converters override the asynchronous read/write hooks so
that arrays, maps, and objects stream member-by-member with bounded memory. A top-level array of large
elements is read one element at a time; a large object is read one property at a time — including
objects that are constructed from constructor parameters — so processing begins before the closing
brace arrives. Only an individual value that a custom converter reads with the default buffering hook,
a raw retained unknown-member value, or a single constructor argument is materialized at a time — never the
enclosing document.

### Asynchronous enumerable contracts

<xref:System.Collections.Generic.IAsyncEnumerable`1> values are supported both at the root and within ordinary shaped models when using <xref:Nerdbank.Json.JsonSerializer.SerializeAsync*>.
Each element is written before the next is requested, without materializing the sequence or requiring its count in advance.
Cancellation is passed to the enumerator, and pipe backpressure pauses enumeration.
Element converters retain their normal synchronous or asynchronous dispatch.

Synchronous serialization rejects asynchronous-only enumerable contracts and directs callers to the asynchronous API.
Deserialization accepts a JSON `null` but cannot construct an asynchronous enumerable from an array; use a supported collection contract instead.

## Behavior and ownership

* **Stream ownership is explicit.** The `Stream`, `PipeReader`, and `PipeWriter` you pass are never
  disposed or completed by the serializer. You remain responsible for their lifetime.
* **Cancellation** is honored throughout; a canceled token surfaces as an
  <xref:System.OperationCanceledException>.
* **Caller-owned pipes** deserialize the first value and leave prefetched bytes unread and unexamined for the next operation.
  Scalars require a token delimiter or end of input, and invalid scalar prefixes such as `truefalse` and `01` are rejected.
* **Asynchronous streams** remain strict single-document inputs and reject trailing data.
  Their temporary pipe can prefetch beyond the value, so completing it after a first-value read could discard bytes needed by a subsequent operation.
  Use a caller-owned pipe for repeated first-value reads.
* **String inputs** also require a complete document.
  UTF-8 buffer and sequence overloads return the first value.
  Synchronous stream overloads return the first value after buffering the stream to its end; they do not preserve a position for the next document.
* **Operation failures** surface as <xref:Nerdbank.Json.JsonSerializationException>, retaining the causal exception.
  Existing serialization exceptions retain their instance and error code.
  Argument validation remains an argument error, and actual operation cancellation is not wrapped.
* **Reference preservation** streams incrementally for built-in mutable objects, collections, and
  dictionaries, so an optional `$id`/`$value` envelope does not reintroduce whole-document buffering.
  Early registration still enables `AllowCycles` back-references. A custom converter that relies on the
  default asynchronous read is a clear, bounded fallback: only that one preserved value is buffered.
* **Exact synchronous semantics are preserved:** duplicate-property detection, required and
  non-nullable validation, retained unknown members, lifecycle callbacks (including early cycle registration
  ordering), comments, and trailing commas all behave identically to the synchronous API.

## Writing a streaming custom converter

Custom converters that may handle very large values can override
<xref:Nerdbank.Json.JsonConverter`1.WriteAsync*> and <xref:Nerdbank.Json.JsonConverter`1.ReadAsync*>
and set <xref:Nerdbank.Json.JsonConverter.PreferAsyncSerialization> to `true`. Use
<xref:Nerdbank.Json.JsonAsyncWriter> and <xref:Nerdbank.Json.JsonAsyncReader> to create short-lived
synchronous readers/writers for fragments; a synchronous reader or writer obtained from those types
must be returned before the next asynchronous operation, and must never be held across an `await`.
