# Asynchronous sequence streaming

Building on the [incremental async reader](async-streaming.md), Nerdbank.Json can stream a *sequence*
of values as an <xref:System.Collections.Generic.IAsyncEnumerable`1>, reading only enough of the input
to produce each element on demand.

[!code-csharp[](../../samples/cs/AsyncSequences.cs#AsyncSequences)]

## Framing modes

The three framings are separate, clearly named methods rather than a mode flag:

* **Top-level JSON array** — <xref:Nerdbank.Json.JsonSerializer.DeserializeArrayAsync*> streams each
  element of a `[ ... ]` document. A top-level `null` yields an empty sequence.
* **Newline-delimited JSON (NDJSON / JSON Lines)** — <xref:Nerdbank.Json.JsonSerializer.DeserializeNewlineDelimitedAsync*>
  streams one value per line. Because adjacent top-level JSON values are otherwise ambiguous, this mode
  is explicit and requires a line terminator between values; two values on one line are rejected with a
  <xref:System.FormatException>.
* **A sequence inside an envelope** — <xref:Nerdbank.Json.JsonSerializer.DeserializeArrayAtAsync*> selects
  an array nested inside an object or array using either a pre-parsed <xref:Nerdbank.Json.JsonPath> (wire
  names) or a member-access expression (`e => e.Items`). The preamble is parsed and skipped
  incrementally; after the sequence completes the remainder of the envelope is drained and validated, and
  trailing data after the document is rejected. Expression paths can traverse *through* intermediate union
  or custom-converter representations; pre-parsed wire paths traverse plain objects and arrays only and
  report clearly when converter metadata is required.

Missing paths are controlled by <xref:Nerdbank.Json.MissingPathBehavior>: throw a
<xref:Nerdbank.Json.JsonPathNotFoundException> (the default) or yield an empty sequence. A path that
resolves to `null` yields an empty sequence; a path that resolves to a non-array value throws.

## Streaming semantics and ownership

* **No read-ahead beyond framing.** An element is produced as soon as its bytes arrive; the enumerator
  does not read the following separator, closing bracket, or next element until the consumer asks for
  more. Memory is bounded by the largest single element, never the whole sequence or document.
* **Cancellation** is honored via <xref:System.Runtime.CompilerServices.EnumeratorCancellationAttribute>,
  so `WithCancellation` on the enumerator flows through.
* **Early disposal** — breaking out of the loop disposes the enumerator, which releases buffered bytes
  but does not read to the end or validate trailing data.
* **Stream and pipe ownership is explicit.** The <xref:System.IO.Stream>, <xref:System.IO.Pipelines.PipeReader>,
  and <xref:System.IO.Pipelines.PipeWriter> you pass are never disposed or completed by the serializer.
* **Reference preservation** is not supported for sequence streaming (a clear
  <xref:System.NotSupportedException> is thrown), because reference metadata wraps every value.

## Serializing a sequence

<xref:Nerdbank.Json.JsonSerializer.SerializeArrayAsync*> writes an
<xref:System.Collections.Generic.IAsyncEnumerable`1> as a top-level JSON array, and
<xref:Nerdbank.Json.JsonSerializer.SerializeNewlineDelimitedAsync*> writes it as newline-delimited JSON,
both flushing periodically so a long or unbounded source streams with bounded memory and backpressure.
