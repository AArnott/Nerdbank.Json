// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

public partial record JsonSerializer
{
#if NET
	/// <inheritdoc cref="GetJsonSchema{T}(ITypeShape{T})"/>
	public string GetJsonSchema<T>()
		where T : IShapeable<T> => this.GetJsonSchema(T.GetTypeShape());

	/// <summary>
	/// Produces a JSON Schema document that describes how this serializer represents <typeparamref name="T"/>,
	/// using a witness type for the shape.
	/// </summary>
	/// <typeparam name="T">The type to describe.</typeparam>
	/// <typeparam name="TProvider">A witness type that provides the shape for <typeparamref name="T"/>.</typeparam>
	/// <returns>The JSON Schema document as a JSON string.</returns>
	public string GetJsonSchema<T, TProvider>()
		where TProvider : IShapeable<T> => this.GetJsonSchema(TProvider.GetTypeShape());
#endif

	/// <summary>
	/// Produces a JSON Schema (draft 2020-12) document that describes how this serializer represents the given type.
	/// </summary>
	/// <typeparam name="T">The type to describe.</typeparam>
	/// <param name="shape">The shape of the type.</param>
	/// <returns>The JSON Schema document as a JSON string.</returns>
	/// <remarks>
	/// The schema reflects the effective configuration, including the property naming policy, enum-by-name serialization,
	/// reference preservation, and runtime union configuration. Custom converters that do not override
	/// <see cref="JsonConverter.GetJsonSchema(JsonSchemaContext, ITypeShape)"/> produce a permissive schema annotated with
	/// a conspicuous comment rather than false precision.
	/// </remarks>
	public string GetJsonSchema<T>(ITypeShape<T> shape)
	{
		Requires.NotNull(shape);
		return this.SerializeSchema(new JsonSchemaContext(this.ConverterCache).GenerateDocument(shape));
	}

	/// <inheritdoc cref="GetJsonSchema{T}(ITypeShape{T})"/>
	/// <param name="typeShape">The shape of the type.</param>
	public string GetJsonSchema(ITypeShape typeShape)
	{
		Requires.NotNull(typeShape);
		return this.SerializeSchema(new JsonSchemaContext(this.ConverterCache).GenerateDocument(typeShape));
	}

	private string SerializeSchema(JsonSchema document)
	{
		JsonWriter writer = new(SequencePool<byte>.Shared, new byte[4096]) { WriteIndented = this.WriteIndented };
		document.Write(ref writer);
		return writer.FlushAndGetString();
	}
}
