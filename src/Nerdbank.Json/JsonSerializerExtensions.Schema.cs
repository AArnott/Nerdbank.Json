// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using JsonSchemaObject = System.Text.Json.Nodes.JsonObject;

namespace Nerdbank.Json;

/// <content>Legacy source-generated JSON Schema overloads.</content>
public static partial class JsonSerializerExtensions
{
	/// <summary>
	/// <inheritdoc cref="JsonSerializer.GetJsonSchema(ITypeShape)" path="/summary"/>
	/// </summary>
	/// <typeparam name="T">The self-describing type whose schema should be produced.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <returns><inheritdoc cref="JsonSerializer.GetJsonSchema(ITypeShape)" path="/returns"/></returns>
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> has no type shape created via the <see cref="GenerateShapeAttribute"/> source generator.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="T"/> is decorated with a <see cref="GenerateShapeAttribute"/>.
	/// For non-decorated types, apply <see cref="GenerateShapeForAttribute{T}"/> to a witness type and call <see cref="GetJsonSchema{T, TProvider}(JsonSerializer)"/> instead,
	/// or use <see cref="JsonSerializer.GetJsonSchema{T}(ITypeShapeProvider)"/> for an option that does not require source generation.
	/// </remarks>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Use the JsonSerializer.GetJsonSchema<T>() instance method instead. If using the extension method syntax, check that your type argument actually has a [GenerateShape] attribute or otherwise implements IShapeable<T> to avoid a runtime failure.", error: true)]
#endif
	public static JsonSchemaObject GetJsonSchema<T>(this JsonSerializer self)
		=> Requires.NotNull(self).GetJsonSchema(ResolveTypeShapeOrThrow<T>(self.ConverterCache));

	/// <summary>
	/// <inheritdoc cref="JsonSerializer.GetJsonSchema(ITypeShape, JsonSchemaOptions)" path="/summary"/>
	/// </summary>
	/// <typeparam name="T">The self-describing type whose schema should be produced.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="options">The schema generation options.</param>
	/// <returns><inheritdoc cref="JsonSerializer.GetJsonSchema(ITypeShape, JsonSchemaOptions)" path="/returns"/></returns>
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> has no type shape created via the <see cref="GenerateShapeAttribute"/> source generator.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="T"/> is decorated with a <see cref="GenerateShapeAttribute"/>.
	/// For non-decorated types, apply <see cref="GenerateShapeForAttribute{T}"/> to a witness type and call <see cref="GetJsonSchema{T, TProvider}(JsonSerializer)"/> instead,
	/// or use <see cref="JsonSerializer.GetJsonSchema{T}(ITypeShapeProvider)"/> for an option that does not require source generation.
	/// </remarks>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Use the JsonSerializer.GetJsonSchema<T>(JsonSchemaOptions) instance method instead. If using the extension method syntax, check that your type argument actually has a [GenerateShape] attribute or otherwise implements IShapeable<T> to avoid a runtime failure.", error: true)]
#endif
	public static JsonSchemaObject GetJsonSchema<T>(this JsonSerializer self, JsonSchemaOptions options)
		=> Requires.NotNull(self).GetJsonSchema(ResolveTypeShapeOrThrow<T>(self.ConverterCache), options);

	/// <summary>
	/// <inheritdoc cref="JsonSerializer.GetJsonSchema(ITypeShape)" path="/summary"/>
	/// </summary>
	/// <typeparam name="T">The type whose schema should be produced.</typeparam>
	/// <typeparam name="TProvider">The witness type that provides the shape for <typeparamref name="T"/>.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <returns><inheritdoc cref="JsonSerializer.GetJsonSchema(ITypeShape)" path="/returns"/></returns>
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="TProvider"/> has no <see cref="GenerateShapeForAttribute{T}"/> source generator attribute for <typeparamref name="T"/>.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="TProvider"/> is decorated with a <see cref="GenerateShapeForAttribute{T}"/>.
	/// Use <see cref="JsonSerializer.GetJsonSchema{T}(ITypeShapeProvider)"/> for an option that does not require source generation.
	/// </remarks>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Use the JsonSerializer.GetJsonSchema<T, TProvider>() instance method instead. If using the extension method syntax, check that your type argument actually has a [GenerateShape] attribute or otherwise implements IShapeable<T> to avoid a runtime failure.", error: true)]
#endif
	public static JsonSchemaObject GetJsonSchema<T, TProvider>(this JsonSerializer self)
		=> Requires.NotNull(self).GetJsonSchema(ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache));

	/// <summary>
	/// <inheritdoc cref="JsonSerializer.GetJsonSchema(ITypeShape, JsonSchemaOptions)" path="/summary"/>
	/// </summary>
	/// <typeparam name="T">The type whose schema should be produced.</typeparam>
	/// <typeparam name="TProvider">The witness type that provides the shape for <typeparamref name="T"/>.</typeparam>
	/// <param name="self">The serializer.</param>
	/// <param name="options">The schema generation options.</param>
	/// <returns><inheritdoc cref="JsonSerializer.GetJsonSchema(ITypeShape, JsonSchemaOptions)" path="/returns"/></returns>
	/// <exception cref="NotSupportedException">Thrown if <typeparamref name="TProvider"/> has no <see cref="GenerateShapeForAttribute{T}"/> source generator attribute for <typeparamref name="T"/>.</exception>
	/// <remarks>
	/// This overload should only be used when <typeparamref name="TProvider"/> is decorated with a <see cref="GenerateShapeForAttribute{T}"/>.
	/// Use <see cref="JsonSerializer.GetJsonSchema{T}(ITypeShapeProvider)"/> for an option that does not require source generation.
	/// </remarks>
#if NET8_0
	[RequiresDynamicCode(ResolveDynamicMessage)]
#endif
#if NET
	[EditorBrowsable(EditorBrowsableState.Never)]
	[Obsolete("Use the JsonSerializer.GetJsonSchema<T, TProvider>(JsonSchemaOptions) instance method instead. If using the extension method syntax, check that your type argument actually has a [GenerateShape] attribute or otherwise implements IShapeable<T> to avoid a runtime failure.", error: true)]
#endif
	public static JsonSchemaObject GetJsonSchema<T, TProvider>(this JsonSerializer self, JsonSchemaOptions options)
		=> Requires.NotNull(self).GetJsonSchema(ResolveTypeShapeOrThrow<T, TProvider>(self.ConverterCache), options);
}
