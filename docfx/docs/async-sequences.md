# Asynchronous sequence streaming

Building on the [incremental async reader](async-streaming.md), Nerdbank.Json can stream a *sequence*
of values as an <xref:System.Collections.Generic.IAsyncEnumerable`1>, reading only enough of the input
to produce each element on demand.

[!code-csharp[](../../samples/cs/AsyncSequences.cs#AsyncSequences)]

## Framing modes

<xref:Nerdbank.Json.JsonSerializer.DeserializeEnumerableAsync*> accepts <xref:Nerdbank.Json.JsonSerializer.SequenceOptions> to select framing through its <xref:Nerdbank.Json.JsonSerializer.SequenceOptions.Format> property:

* **Newline-delimited JSON (NDJSON / JSON Lines)** is the default, <xref:Nerdbank.Json.JsonSequenceFormat.NDJson>.
  It requires a line terminator between values; two values on one line are rejected with a <xref:System.FormatException>.
* **Top-level JSON array**, <xref:Nerdbank.Json.JsonSequenceFormat.JsonArray>, streams each element of a `[ ... ]` document.
  A top-level `null` yields an empty sequence.
  The dedicated JSON-specific <xref:Nerdbank.Json.JsonSerializer.DeserializeArrayAsync*> API remains available.

For **a sequence inside an envelope**, use <xref:Nerdbank.Json.JsonSerializer.DeserializePathEnumerableAsync*> with <xref:Nerdbank.Json.JsonSerializer.StreamingEnumerationOptions`2> containing a member-access expression such as `e => e.Items`.
The preamble is parsed and skipped incrementally; full enumeration drains and validates the remainder of the envelope and rejects trailing data.
Expression paths can traverse intermediate union or custom-converter representations.
Raw <xref:Nerdbank.Json.JsonPath> overloads are also available for wire-name navigation through plain objects and arrays.

Typed paths throw <xref:Nerdbank.Json.JsonPathNotFoundException> when the path is absent or resolves to `null`, unless <xref:Nerdbank.Json.JsonSerializer.StreamingEnumerationOptions`2.EmptySequenceForUndiscoverablePath> is enabled.
Raw paths use <xref:Nerdbank.Json.MissingPathBehavior> to control missing paths and yield an empty sequence for a selected `null`.
A selected non-array value throws.

## Streaming semantics and ownership

* **No read-ahead beyond framing.** An element is produced as soon as its bytes arrive; the enumerator
  does not read the following separator, closing bracket, or next element until the consumer asks for
  more. Memory is bounded by the largest single element, never the whole sequence or document.
* **Cancellation** is honored via <xref:System.Runtime.CompilerServices.EnumeratorCancellationAttribute>,
  so `WithCancellation` on the enumerator flows through.
* **Early disposal** — breaking out of the loop disposes the enumerator, which releases buffered bytes
  but does not read to the end or validate trailing data.
* **Stream and pipe ownership is explicit.**
  Caller-supplied streams remain open.
  Enumerable and dedicated array APIs leave caller-supplied pipes open, as do raw-path overloads.
  Typed path enumeration completes its input pipe by default; set <xref:Nerdbank.Json.JsonSerializer.StreamingEnumerationOptions`2.LeaveOpen> to retain ownership.
  Internally created pipe adapters are completed when enumeration ends.
* **Reference preservation** is not supported for sequence streaming (a clear
  <xref:System.NotSupportedException> is thrown), because reference metadata wraps every value.

## Serializing a sequence

<xref:Nerdbank.Json.JsonSerializer.SerializeEnumerableAsync*> writes an <xref:System.Collections.Generic.IAsyncEnumerable`1> using the same <xref:Nerdbank.Json.JsonSerializer.SequenceOptions> framing choices, defaulting to NDJSON.
NDJSON output writes a newline after each value and does not indent values across multiple lines.
The dedicated <xref:Nerdbank.Json.JsonSerializer.SerializeArrayAsync*> API writes a top-level JSON array.
Both APIs flush periodically so a long or unbounded source streams with bounded memory and backpressure.
