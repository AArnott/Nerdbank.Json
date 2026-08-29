# Serialization callbacks

Types that need to validate outgoing state or restore computed state after
deserialization may implement <xref:Nerdbank.Json.IJsonSerializationCallbacks>.

[!code-csharp[](../../samples/cs/SerializationCallbacks.cs#SerializationCallbacks)]

<xref:Nerdbank.Json.IJsonSerializationCallbacks.OnBeforeSerialize> runs after
the serializer has accepted a non-null object and before it reads any serializable
property or extension-data value. Changes made by the callback are therefore
included in the JSON.

<xref:Nerdbank.Json.IJsonSerializationCallbacks.OnAfterDeserialize> runs after
the constructor, settable properties, getter-only collections, and extension
data have been applied. It is not called when JSON `null` is deserialized.

Each object occurrence receives its own callback. If the same reference appears
twice in the graph, it is called twice unless reference-preservation metadata
causes one occurrence to be emitted as a reference. Exceptions from callbacks
are propagated to the serializer caller.

Callbacks are discovered with an ordinary interface type test in the generated
object converter and do not require reflection. They are trimming-safe and
NativeAOT-compatible. A custom converter owns the complete representation and
must invoke callbacks itself if its contract requires them.
