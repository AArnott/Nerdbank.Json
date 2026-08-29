// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal elements are intentionally undocumented in this file.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Nerdbank.Json;

/// <summary>
/// Produces JSON Schema fragments for shape-generated types (objects, collections, dictionaries, enums, unions).
/// </summary>
internal sealed class JsonSchemaVisitor : TypeShapeVisitor
{
	private readonly ConverterCache owner;
	private readonly JsonSchemaContext context;

	internal JsonSchemaVisitor(ConverterCache owner, JsonSchemaContext context)
	{
		this.owner = owner;
		this.context = context;
	}

	public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
	{
		List<KeyValuePair<string, JsonSchema>> properties = [];
		List<string> required = [];
		bool hasExtensionData = false;

		HashSet<string> requiredConstructorParameters = new(StringComparer.Ordinal);
		if (objectShape.Constructor is { } constructor)
		{
			foreach (IParameterShape parameter in constructor.Parameters)
			{
				if (parameter.IsRequired)
				{
					requiredConstructorParameters.Add(parameter.Name);
				}
			}
		}

		foreach (IPropertyShape property in objectShape.Properties)
		{
			if (HasExtensionDataAttribute(property.AttributeProvider))
			{
				hasExtensionData = true;
				continue;
			}

			if (!property.HasGetter && !property.HasSetter)
			{
				continue;
			}

			string serializedName = this.owner.GetSerializedPropertyName(property.Name, property.AttributeProvider);
			JsonSchema propertySchema = this.context.GetSchema(property.PropertyType);
			if (IsMemberNullable(property))
			{
				propertySchema = propertySchema.MakeNullable();
			}

			properties.Add(new(serializedName, propertySchema));

			if (requiredConstructorParameters.Contains(property.Name) || HasRequiredMemberAttribute(property.MemberInfo))
			{
				required.Add(serializedName);
			}
		}

		JsonSchema schema = new JsonSchema().Set("type", "object");
		if (properties.Count > 0)
		{
			schema.SetMap("properties", properties);
		}

		if (required.Count > 0)
		{
			schema.SetStrings("required", required);
		}

		schema.Set("additionalProperties", hasExtensionData);
		return schema;
	}

	public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null)
	{
		JsonSchema element = this.context.GetSchema(enumerableShape.ElementType);
		if (enumerableShape.Type.IsArray && enumerableShape.Rank > 1)
		{
			JsonSchema nested = element;
			for (int i = 0; i < enumerableShape.Rank; i++)
			{
				nested = new JsonSchema().Set("type", "array").Set("items", nested);
			}

			return nested;
		}

		return new JsonSchema().Set("type", "array").Set("items", element);
	}

	public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null)
		=> new JsonSchema().Set("type", "object").Set("additionalProperties", this.context.GetSchema(dictionaryShape.ValueType));

	public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null)
	{
		if (this.owner.SerializeEnumValuesByName)
		{
			List<string> names = enumShape.Members.Keys
				.Select(name => this.owner.PropertyNamingPolicy?.ConvertName(name) ?? name)
				.Distinct(StringComparer.Ordinal)
				.ToList();
			return new JsonSchema().Set("type", "string").SetStrings("enum", names);
		}

		return this.context.GetSchema(enumShape.UnderlyingType);
	}

	public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state = null)
		=> this.context.GetSchema(optionalShape.ElementType).MakeNullable();

	public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
		=> this.context.GetSchema(surrogateShape.SurrogateType);

	public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null)
	{
		List<(object? Alias, JsonSchema CaseSchema)> cases = [];
		foreach (IUnionCaseShape unionCase in unionShape.UnionCases)
		{
			object alias = unionCase.IsTagSpecified ? unionCase.Tag : unionCase.Name;
			cases.Add((alias, this.context.GetSchema(unionCase.UnionCaseType)));
		}

		JsonSchema baseSchema = (JsonSchema)unionShape.BaseType.Accept(this)!;
		return this.context.CreateUnionEnvelope(cases, baseSchema);
	}

	public override object? VisitFunction<TFunction, TArgumentState, TResult>(IFunctionTypeShape<TFunction, TArgumentState, TResult> functionShape, object? state = null)
		=> throw new NotSupportedException($"JSON schema generation does not support delegate types such as {functionShape.Type.FullName}.");

	private static bool IsMemberNullable(IPropertyShape property)
	{
		Type propertyType = property.PropertyType.Type;
		if (Nullable.GetUnderlyingType(propertyType) is not null)
		{
			return true;
		}

		if (propertyType.IsValueType)
		{
			return false;
		}

		bool isNonNullableReference = property.HasSetter ? property.IsSetterNonNullable : property.IsGetterNonNullable;
		return !isNonNullableReference;
	}

	private static bool HasExtensionDataAttribute(IGenericCustomAttributeProvider attributeProvider)
	{
		using IEnumerator<JsonExtensionDataAttribute> attributes = attributeProvider.GetCustomAttributes<JsonExtensionDataAttribute>(inherit: false).GetEnumerator();
		return attributes.MoveNext();
	}

	private static bool HasRequiredMemberAttribute(MemberInfo? memberInfo)
	{
		if (memberInfo is null)
		{
			return false;
		}

		foreach (CustomAttributeData attribute in memberInfo.CustomAttributes)
		{
			if (attribute.AttributeType.FullName == "System.Runtime.CompilerServices.RequiredMemberAttribute")
			{
				return true;
			}
		}

		return false;
	}
}
