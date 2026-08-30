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
a raw extension-data value, or a single constructor argument is materialized at a time — never the
enclosing document.

## Behavior and ownership

* **Stream ownership is explicit.** The `Stream`, `PipeReader`, and `PipeWriter` you pass are never
  disposed or completed by the serializer. You remain responsible for their lifetime.
* **Cancellation** is honored throughout; a canceled token surfaces as an
  <xref:System.OperationCanceledException>.
* **Trailing data** after the top-level value is rejected with a <xref:System.FormatException>, just
  like the synchronous API. Trailing whitespace (and comments, when
  <xref:Nerdbank.Json.JsonCommentHandling.Skip> is configured) is allowed.
* **Reference preservation** streams incrementally for built-in mutable objects, collections, and
  dictionaries, so an optional `$id`/`$value` envelope does not reintroduce whole-document buffering.
  Early registration still enables `AllowCycles` back-references. A custom converter that relies on the
  default asynchronous read is a clear, bounded fallback: only that one preserved value is buffered.
* **Exact synchronous semantics are preserved:** duplicate-property detection, required and
  non-nullable validation, extension data, lifecycle callbacks (including early cycle registration
  ordering), comments, and trailing commas all behave identically to the synchronous API.

## Writing a streaming custom converter

Custom converters that may handle very large values can override
<xref:Nerdbank.Json.JsonConverter`1.WriteAsync*> and <xref:Nerdbank.Json.JsonConverter`1.ReadAsync*>
and set <xref:Nerdbank.Json.JsonConverter.PreferAsyncSerialization> to `true`. Use
<xref:Nerdbank.Json.JsonAsyncWriter> and <xref:Nerdbank.Json.JsonAsyncReader> to create short-lived
synchronous readers/writers for fragments; a synchronous reader or writer obtained from those types
must be returned before the next asynchronous operation, and must never be held across an `await`.
