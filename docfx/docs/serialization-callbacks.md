# Serialization callbacks

Types that need to validate outgoing state or restore computed state after
deserialization may implement <xref:Nerdbank.Json.IJsonSerializationCallbacks>.
The interface has the same four methods as Nerdbank.MessagePack's
`IMessagePackSerializationCallbacks`, and the serializer calls them at the same
relative points the JSON format allows.

On modern .NET each method has an empty default implementation, so an existing
implementer keeps compiling when it only supplies the callbacks it needs.
On .NET Framework and .NET Standard 2.0 the methods are abstract, matching
Nerdbank.MessagePack.

[!code-csharp[](../../samples/cs/SerializationCallbacks.cs#SerializationCallbacks)]

<xref:Nerdbank.Json.IJsonSerializationCallbacks.OnBeforeSerialize> runs after
the serializer has accepted a non-null object and before it reads any serializable
property or extension-data value. Changes made by the callback are therefore
included in the JSON.

<xref:Nerdbank.Json.IJsonSerializationCallbacks.OnAfterSerialize> runs after those
properties and extension data have been written. Changes made by this callback are
not written. It is skipped when serialization of that object fails.

<xref:Nerdbank.Json.IJsonSerializationCallbacks.OnBeforeDeserialize> runs after a
mutable object is constructed and before any property or extension data is assigned.
It is not called for JSON `null`. The default converter also does not call it for a
deserializing constructor, or for a type PolyType constructs with its properties
already assigned, such as a type with `required` or `init` members. That matches
Nerdbank.MessagePack: the callback is only invoked when the converter can observe
the instance before its members are populated.

<xref:Nerdbank.Json.IJsonSerializationCallbacks.OnAfterDeserialize> runs after
the constructor, settable properties, getter-only collections, and extension
data have been applied. It is not called when JSON `null` is deserialized.

Each object occurrence receives its own callbacks. If the same reference appears
twice in the graph, each callback is invoked twice unless reference-preservation
metadata causes one occurrence to be emitted as a reference. Exceptions from
callbacks are propagated to the serializer caller.

Callbacks are discovered with an ordinary interface type test in the generated
object converter and do not require reflection. They are trimming-safe and
NativeAOT-compatible. A custom converter owns the complete representation and
must invoke callbacks itself if its contract requires them.
