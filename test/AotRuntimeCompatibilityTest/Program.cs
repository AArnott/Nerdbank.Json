// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Dynamic;
using Nerdbank.Json;
using PolyType;

Payload value = new() { Number = 42 };
string json = new JsonSerializer().Serialize(value, GetShape<Payload, Payload>());
if (json != """{"number":42}""")
{
	throw new InvalidOperationException($"Unexpected JSON: {json}");
}

JsonSerializer serializer = new();
RawJson raw = serializer.Deserialize(" [1.2300e+02,\"\\u0061\"]", GetShape<RawJson, RawJson>());
if (!raw.IsOwned || serializer.Serialize(raw, GetShape<RawJson, RawJson>()) != """[1.2300e+02,"\u0061"]""")
{
	throw new InvalidOperationException("Owned raw JSON did not retain exact token spelling.");
}

LowLevelSegment first = new("\"é"u8.ToArray());
LowLevelSegment last = first.Append("😀\\n\""u8.ToArray());
ReadOnlySequence<byte> segmented = new(first, 0, last, last.Memory.Length);
JsonReader utf8Reader = new(segmented);
if (utf8Reader.NextJsonType != JsonValueKind.String
	|| !utf8Reader.ReadStringSpan().SequenceEqual("é😀\n"u8)
	|| !utf8Reader.End
	|| JsonPrimitives.TryRead("-123"u8, out long scalar, out int consumed) != JsonPrimitives.DecodeResult.Success
	|| scalar != -123 || consumed != 4
	|| JsonPrimitives.TryRead("1e+"u8, out double _, out _, isFinalBlock: false) != JsonPrimitives.DecodeResult.InsufficientBuffer)
{
	throw new InvalidOperationException("The low-level segmented reader or scalar primitives failed.");
}

ArrayBufferWriter<byte> utf8Output = new();
JsonWriter utf8Writer = new(utf8Output);
utf8Writer.WriteStartArray();
utf8Writer.WriteString("é😀\n"u8);
utf8Writer.WriteValueSeparator();
utf8Writer.Write(raw);
utf8Writer.WriteEndArray();
utf8Writer.Flush();
Span<byte> primitiveOutput = stackalloc byte[32];
if (!utf8Output.WrittenSpan.SequenceEqual("""["é😀\n",[1.2300e+02,"\u0061"]]"""u8)
	|| !JsonPrimitives.TryWrite(primitiveOutput, scalar, out int written)
	|| !primitiveOutput[..written].SequenceEqual("-123"u8))
{
	throw new InvalidOperationException("The UTF-8 string, raw JSON, or primitive writer failed.");
}

AddressBook original = new([new("Ada", 36), new("Grace", 45)], new("Ada", 36));
string addressBookJson = serializer.Serialize(original, GetShape<AddressBook, AddressBook>());
if (addressBookJson != """{"people":[{"name":"Ada","age":36},{"name":"Grace","age":45}],"favorite":{"name":"Ada","age":36}}""")
{
	throw new InvalidOperationException($"Unexpected address book JSON: {addressBookJson}");
}

AddressBook restored = serializer.Deserialize(addressBookJson, GetShape<AddressBook, AddressBook>())
	?? throw new InvalidOperationException("The address book was null.");
if (!original.People.SequenceEqual(restored.People) || original.Favorite != restored.Favorite || serializer.Serialize(restored) != addressBookJson)
{
	throw new InvalidOperationException("The nested records and array did not round-trip.");
}

Console.WriteLine(json);
Console.WriteLine(addressBookJson);

ITypeShape<AttributedPayload> attributedShape = GetShape<AttributedPayload, AttributedPayload>();
ITypeShape<GenericAttributedPayload<int>> genericIntShape = GetShape<GenericAttributedPayload<int>, AttributedPayloadWitness>();
ITypeShape<GenericAttributedPayload<string>> genericStringShape = GetShape<GenericAttributedPayload<string>, AttributedPayloadWitness>();
if (attributedShape.GetAssociatedTypeShape(typeof(AttributedPayloadConverter)) is null ||
	genericIntShape.GetAssociatedTypeShape(typeof(GenericAttributedPayloadConverter<>))?.Type != typeof(GenericAttributedPayloadConverter<int>) ||
	genericStringShape.GetAssociatedTypeShape(typeof(GenericAttributedPayloadConverter<>))?.Type != typeof(GenericAttributedPayloadConverter<string>) ||
	serializer.Serialize(new AttributedPayload(41), attributedShape) != "42" ||
	serializer.Deserialize("42", attributedShape)?.Number != 41 ||
	serializer.Serialize(new GenericAttributedPayload<int>(41), genericIntShape) != "42" ||
	serializer.Deserialize("42", genericIntShape)?.Number != 41 ||
	serializer.Serialize(new GenericAttributedPayload<string>(7), genericStringShape) != "8" ||
	serializer.Deserialize("8", genericStringShape)?.Number != 7)
{
	throw new InvalidOperationException("Automatically associated converter constructors did not round-trip.");
}

JsonSerializer dynamicSerializer = new JsonSerializer().WithDynamicObjectConverter();
object dynamicValue = dynamicSerializer.Deserialize<object, UntypedWitness>("""{"name":"Ada","nested":{"active":true}}""")
	?? throw new InvalidOperationException("The dynamic dictionary was null.");
if (dynamicValue is not IDynamicMetaObjectProvider ||
	dynamicValue is not IReadOnlyDictionary<object, object?> dynamicMap ||
	!Equals(dynamicMap["name"], "Ada") ||
	dynamicMap["nested"] is not IReadOnlyDictionary<object, object?> nested ||
	!Equals(nested["active"], true) ||
	dynamicSerializer.Serialize<object, UntypedWitness>(dynamicValue) != """{"name":"Ada","nested":{"active":true}}""")
{
	throw new InvalidOperationException("The dynamic dictionary did not round-trip through its native dictionary interface.");
}

static ITypeShape<T> GetShape<T, TProvider>()
	where TProvider : IShapeable<T>
	=> TProvider.GetTypeShape();

[GenerateShapeFor<object>]
internal partial class UntypedWitness;
