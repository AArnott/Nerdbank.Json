# Retaining unknown object members

Declare a field or property of type <xref:Nerdbank.Json.UnusedDataPacket> to retain unknown JSON object members when reading data from a newer version of a contract.
The serializer recognizes the member by its type, not its name or a JSON-specific attribute.
Use <xref:PolyType.PropertyShapeAttribute> to include a private field or property in the type shape.
For example, a shaped contract can declare `[PropertyShape] private UnusedDataPacket? unusedData;` to retain data without exposing it as part of the public model.
Only one packet member is allowed on a type.

The packet starts out null and is assigned once, after all object members have been read, only if unknown members were encountered.
Retained values are written back as raw JSON, preserving numeric spellings and nested values without converting them through a DOM.
The packet member itself is never emitted as a JSON property.
Packets are specific to JSON and cannot be exchanged with MessagePack packets.

A writable packet member is needed to retain data during deserialization.
A getter-only packet member is supported for serialization of an existing packet, but unknown members are skipped during deserialization, matching MessagePack.
A setter-only member can capture data but cannot write it back.
Constructor-bound contracts and asynchronous serialization follow the same rules.
Deserialization callbacks run after the packet is assigned.
Duplicate member names are rejected using the serializer's configured property-name comparison, just as for ordinary object members.
JSON reference metadata continues to be handled by the reference-preserving envelope rather than the packet.
