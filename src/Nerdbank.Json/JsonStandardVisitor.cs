// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using PolyType.Utilities;

namespace Nerdbank.Json;

internal sealed class JsonStandardVisitor(ConverterCache owner, TypeGenerationContext context) : TypeShapeVisitor, ITypeShapeFunc
{
	private static readonly object ExtensionDataSentinel = new();

	object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state)
		=> owner.CreateConverter(typeShape, this);

	public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null)
		where TEnum : struct
	{
		JsonConverter<TUnderlying> underlyingConverter = this.GetConverter(enumShape.UnderlyingType, attributeProvider: null);
		return new JsonEnumConverter<TEnum, TUnderlying>(underlyingConverter, enumShape.Members, owner.SerializeEnumValuesByName);
	}

	public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state = null)
	{
		JsonConverter<TElement> elementConverter = this.GetConverter(optionalShape.ElementType, attributeProvider: null);
		return new JsonOptionalConverter<TOptional, TElement>(elementConverter, optionalShape.GetDeconstructor(), optionalShape.GetNoneConstructor(), optionalShape.GetSomeConstructor());
	}

	public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null)
	{
		JsonConverter<TElement> elementConverter = this.GetConverter(enumerableShape.ElementType, attributeProvider: null);
		Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetEnumerable();

		return enumerableShape.ConstructionStrategy switch
		{
			CollectionConstructionStrategy.None => new JsonReadOnlyEnumerableConverter<TEnumerable, TElement>(getEnumerable, elementConverter),
			CollectionConstructionStrategy.Mutable => new JsonMutableEnumerableConverter<TEnumerable, TElement>(getEnumerable, elementConverter, enumerableShape.GetAppender(), enumerableShape.GetDefaultConstructor()),
			CollectionConstructionStrategy.Parameterized => new JsonParameterizedEnumerableConverter<TEnumerable, TElement>(getEnumerable, elementConverter, enumerableShape.GetParameterizedConstructor()),
			_ => throw new NotSupportedException($"JSON serialization does not recognize enumerable construction strategy {enumerableShape.ConstructionStrategy} for {enumerableShape.Type.FullName}."),
		};
	}

	public override object? VisitDictionary<TDictionary, TKey, TValue>(IDictionaryTypeShape<TDictionary, TKey, TValue> dictionaryShape, object? state = null)
	{
		if (!JsonDictionaryKeyConverter.IsSupported(typeof(TKey)))
		{
			throw JsonDictionaryKeyConverter.CreateNotSupportedException(typeof(TKey));
		}

		JsonConverter<TValue> valueConverter = this.GetConverter(dictionaryShape.ValueType, attributeProvider: null);
		Func<TDictionary, IReadOnlyDictionary<TKey, TValue>> getReadable = dictionaryShape.GetGetDictionary();

		return dictionaryShape.ConstructionStrategy switch
		{
			CollectionConstructionStrategy.None => new JsonReadOnlyDictionaryConverter<TDictionary, TKey, TValue>(getReadable, valueConverter, owner),
			CollectionConstructionStrategy.Mutable => new JsonMutableDictionaryConverter<TDictionary, TKey, TValue>(getReadable, valueConverter, owner, dictionaryShape.GetInserter(DictionaryInsertionMode.Throw), dictionaryShape.GetDefaultConstructor()),
			CollectionConstructionStrategy.Parameterized => new JsonParameterizedDictionaryConverter<TDictionary, TKey, TValue>(getReadable, valueConverter, owner, dictionaryShape.GetParameterizedConstructor()),
			_ => throw new NotSupportedException($"JSON serialization does not recognize dictionary construction strategy {dictionaryShape.ConstructionStrategy} for {dictionaryShape.Type.FullName}."),
		};
	}

	public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
	{
		List<JsonProperty<T>> properties = new();
		JsonExtensionData<T>? extensionData = null;
		Dictionary<string, string> serializedPropertyNamesByClrName = new(StringComparer.OrdinalIgnoreCase);
		HashSet<string> requiredProperties = new(StringComparer.OrdinalIgnoreCase);
		if (objectShape.Constructor is not null)
		{
			foreach (IParameterShape parameter in objectShape.Constructor.Parameters)
			{
				if (parameter.IsRequired)
				{
					requiredProperties.Add(parameter.Name);
				}
			}
		}

		foreach (IPropertyShape property in objectShape.Properties)
		{
			if (HasExtensionDataAttribute(property.AttributeProvider))
			{
				if (extensionData is not null)
				{
					throw new NotSupportedException($"Type '{typeof(T).FullName}' declares more than one extension data property.");
				}

				extensionData = (JsonExtensionData<T>?)property.Accept(this, ExtensionDataSentinel)
					?? throw new NotSupportedException($"Extension data member '{typeof(T).FullName}.{property.Name}' could not be analyzed.");
				continue;
			}

			serializedPropertyNamesByClrName[property.Name] = owner.GetSerializedPropertyName(property.Name, property.AttributeProvider);
			bool isRequired = requiredProperties.Contains(property.Name) || HasRequiredMemberAttribute(property.MemberInfo);
			if (property.Accept(this, isRequired) is JsonProperty<T> jsonProperty)
			{
				properties.Add(jsonProperty);
			}
		}

		JsonProperty<T>[] jsonProperties = properties.ToArray();
		if (objectShape.Constructor is not null && objectShape.Constructor.Parameters.Count > 0)
		{
			if (objectShape.Constructor.Accept(this, new JsonConstructorVisitorState<T>(jsonProperties, serializedPropertyNamesByClrName, extensionData)) is JsonConverter<T> constructorConverter)
			{
				return constructorConverter;
			}
		}

		Func<T>? factory = objectShape.GetDefaultConstructor();
		if (factory is null)
		{
			throw new NotSupportedException($"Type '{typeof(T).FullName}' does not define a default constructor for property-based deserialization.");
		}

		return new JsonObjectConverter<T>(factory, jsonProperties, owner.PropertyNameComparer, extensionData);
	}

	public override object? VisitConstructor<TDeclaringType, TArgumentState>(IConstructorShape<TDeclaringType, TArgumentState> constructorShape, object? state = null)
	{
		JsonConstructorVisitorState<TDeclaringType> visitorState = (JsonConstructorVisitorState<TDeclaringType>)(state ?? throw new ArgumentNullException(nameof(state)));
		Dictionary<string, JsonConstructorParameter<TArgumentState>> parametersByName = new(owner.PropertyNameComparer);
		List<JsonConstructorParameter<TArgumentState>> parameters = new(constructorShape.Parameters.Count);

		foreach (IParameterShape parameter in constructorShape.Parameters)
		{
			if (!visitorState.TryGetSerializedPropertyName(parameter.Name, out string serializedPropertyName))
			{
				continue;
			}

			if (parameter.Accept(this, new JsonParameterVisitorState(serializedPropertyName)) is JsonConstructorParameter<TArgumentState> jsonParameter)
			{
				parametersByName[jsonParameter.SerializedPropertyName] = jsonParameter;
				parameters.Add(jsonParameter);
			}
		}

		return new JsonObjectWithConstructorConverter<TDeclaringType, TArgumentState>(visitorState.Properties, constructorShape.GetArgumentStateConstructor(), constructorShape.GetParameterizedConstructor(), parameters.ToArray(), parametersByName, visitorState.ExtensionData);
	}

	public override object? VisitParameter<TArgumentState, TParameterType>(IParameterShape<TArgumentState, TParameterType> parameterShape, object? state = null)
	{
		JsonParameterVisitorState visitorState = (JsonParameterVisitorState)(state ?? throw new ArgumentNullException(nameof(state)));
		JsonConverter<TParameterType> converter = this.GetConverter(parameterShape.ParameterType, parameterShape.AttributeProvider);
		bool isNonNullableReferenceType = parameterShape.IsNonNullable && !typeof(TParameterType).IsValueType;
		return new JsonConstructorParameter<TArgumentState, TParameterType>(parameterShape.Name, visitorState.SerializedPropertyName, parameterShape.IsRequired, parameterShape.GetSetter(), converter, isNonNullableReferenceType);
	}

	public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
	{
		if (ReferenceEquals(state, ExtensionDataSentinel))
		{
			if (!typeof(TPropertyType).IsClass)
			{
				throw new NotSupportedException($"Extension data member '{typeof(TDeclaringType).FullName}.{propertyShape.Name}' must be a reference-typed dictionary property.");
			}

			Getter<TDeclaringType, TPropertyType>? extensionGetter = propertyShape.HasGetter ? propertyShape.GetGetter() : null;
			Setter<TDeclaringType, TPropertyType>? extensionSetter = propertyShape.HasSetter ? propertyShape.GetSetter() : null;
			if (extensionGetter is null && extensionSetter is null)
			{
				throw new NotSupportedException($"Extension data member '{typeof(TDeclaringType).FullName}.{propertyShape.Name}' must be readable or writable.");
			}

			if (!typeof(IEnumerable<KeyValuePair<string, string>>).IsAssignableFrom(typeof(TPropertyType)))
			{
				throw new NotSupportedException($"Extension data member '{typeof(TDeclaringType).FullName}.{propertyShape.Name}' must implement IEnumerable<KeyValuePair<string, string>>.");
			}

			if (extensionSetter is null && !typeof(IDictionary<string, string>).IsAssignableFrom(typeof(TPropertyType)))
			{
				throw new NotSupportedException($"Getter-only extension data member '{typeof(TDeclaringType).FullName}.{propertyShape.Name}' must implement IDictionary<string, string>.");
			}

			if (extensionSetter is not null && !typeof(TPropertyType).IsAssignableFrom(typeof(Dictionary<string, string>)))
			{
				throw new NotSupportedException($"Extension data member '{typeof(TDeclaringType).FullName}.{propertyShape.Name}' must be assignable from Dictionary<string, string>.");
			}

			return new JsonExtensionData<TDeclaringType, TPropertyType>(extensionGetter, extensionSetter);
		}

		bool isRequired = state is bool required && required;
		Getter<TDeclaringType, TPropertyType>? getter = propertyShape.HasGetter ? propertyShape.GetGetter() : null;
		Setter<TDeclaringType, TPropertyType>? setter = propertyShape.HasSetter ? propertyShape.GetSetter() : null;
		JsonConverter<TPropertyType> converter = this.GetConverter(propertyShape.PropertyType, propertyShape.AttributeProvider);
		bool deserializeIntoExistingInstance = getter is not null && setter is null && converter is IJsonDeserializeInto<TPropertyType>;
		if (getter is null && setter is null && !deserializeIntoExistingInstance)
		{
			return null;
		}

		string propertyName = owner.GetSerializedPropertyName(propertyShape.Name, propertyShape.AttributeProvider);
		bool isNonNullableReferenceType = !typeof(TPropertyType).IsValueType && (propertyShape.IsSetterNonNullable || (!propertyShape.HasSetter && propertyShape.IsGetterNonNullable));
		return new JsonProperty<TDeclaringType, TPropertyType>(propertyName, propertyShape.Name, getter, setter, converter, deserializeIntoExistingInstance, isRequired, isNonNullableReferenceType);
	}

	public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
	{
		JsonConverter<TSurrogate> surrogateConverter = this.GetConverter(surrogateShape.SurrogateType, attributeProvider: null);
		return new JsonSurrogateConverter<T, TSurrogate>(surrogateShape, surrogateConverter);
	}

	public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null)
	{
		JsonConverter<TUnion> baseConverter = (JsonConverter<TUnion>)unionShape.BaseType.Accept(this)!;
		Getter<TUnion, int> getUnionCaseIndex = unionShape.GetGetUnionCaseIndex();
		Dictionary<int, JsonConverter> deserializersByIntAlias = new(unionShape.UnionCases.Count);
		Dictionary<string, JsonConverter> deserializersByStringAlias = new(unionShape.UnionCases.Count, StringComparer.Ordinal);
		JsonUnionCaseMetadata<TUnion>[] serializers = new JsonUnionCaseMetadata<TUnion>[unionShape.UnionCases.Count];

		for (int i = 0; i < unionShape.UnionCases.Count; i++)
		{
			IUnionCaseShape unionCase = unionShape.UnionCases[i];
			JsonConverter<TUnion> caseConverter = (JsonConverter<TUnion>)unionCase.Accept(this)!;
			if (unionCase.IsTagSpecified)
			{
				deserializersByIntAlias.Add(unionCase.Tag, caseConverter);
				serializers[i] = JsonUnionCaseMetadata<TUnion>.Create(unionCase.Tag, caseConverter);
			}
			else
			{
				deserializersByStringAlias.Add(unionCase.Name, caseConverter);
				serializers[i] = JsonUnionCaseMetadata<TUnion>.Create(unionCase.Name, caseConverter);
			}
		}

		return new JsonUnionConverter<TUnion>(baseConverter, getUnionCaseIndex, serializers, new ReadOnlyDictionary<int, JsonConverter>(deserializersByIntAlias), new ReadOnlyDictionary<string, JsonConverter>(deserializersByStringAlias));
	}

	public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state = null)
	{
		JsonConverter<TUnionCase> caseConverter = (JsonConverter<TUnionCase>)unionCaseShape.UnionCaseType.Accept(this)!;
		return new JsonUnionCaseConverter<TUnionCase, TUnion>(caseConverter, unionCaseShape.Marshaler);
	}

	public override object? VisitFunction<TFunction, TArgumentState, TResult>(IFunctionTypeShape<TFunction, TArgumentState, TResult> functionShape, object? state = null)
		=> throw new NotSupportedException($"JSON serialization does not support delegate types such as {functionShape.Type.FullName}.");

	private static bool HasExtensionDataAttribute(IGenericCustomAttributeProvider attributeProvider)
	{
		using IEnumerator<JsonExtensionDataAttribute> attributes = attributeProvider.GetCustomAttributes<JsonExtensionDataAttribute>(inherit: false).GetEnumerator();
		if (!attributes.MoveNext())
		{
			return false;
		}

		return true;
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

	private JsonConverter<T> GetConverter<T>(ITypeShape<T> shape, IGenericCustomAttributeProvider? attributeProvider)
	{
		if (owner.TryGetConverterFromAttribute(shape.Type, shape, attributeProvider, out JsonConverter? converter) && converter is not null)
		{
			return (JsonConverter<T>)converter;
		}

		return (JsonConverter<T>)context.GetOrAdd(shape)!;
	}
}
