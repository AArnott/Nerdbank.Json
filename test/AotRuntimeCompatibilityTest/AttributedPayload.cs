// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // Keep the attributed converter regression models and constructors together.

using Nerdbank.Json;
using PolyType;

[GenerateShape]
[JsonConverter(typeof(AttributedPayloadConverter))]
internal partial record AttributedPayload(int Number);

internal sealed class AttributedPayloadConverter : JsonConverter<AttributedPayload>
{
	public override AttributedPayload Read(ref JsonReader reader, SerializationContext context) => new(reader.ReadInt32() - 1);

	public override void Write(ref JsonWriter writer, in AttributedPayload? value, SerializationContext context) => writer.Write(value!.Number + 1);
}

[JsonConverter(typeof(GenericAttributedPayloadConverter<>))]
internal record GenericAttributedPayload<T>(int Number);

internal sealed class GenericAttributedPayloadConverter<T> : JsonConverter<GenericAttributedPayload<T>>
{
	public override GenericAttributedPayload<T> Read(ref JsonReader reader, SerializationContext context) => new(reader.ReadInt32() - 1);

	public override void Write(ref JsonWriter writer, in GenericAttributedPayload<T>? value, SerializationContext context) => writer.Write(value!.Number + 1);
}

[GenerateShapeFor<GenericAttributedPayload<int>>]
[GenerateShapeFor<GenericAttributedPayload<string>>]
internal partial class AttributedPayloadWitness;
