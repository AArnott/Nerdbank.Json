// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable IL2091 // DynamicallyAccessedMembers mismatch from Activator.CreateInstance<T>
#pragma warning disable IL2070 // Reflection-based member lookup is intentional for nullability checks.
#pragma warning disable IL2087 // Reflection-based member lookup is intentional for nullability checks.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace Nerdbank.Json;

internal sealed class JsonStandardVisitor(JsonConverterCache owner) : TypeShapeVisitor
{
	public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null)
		where TEnum : struct
	{
		JsonConverter<TUnderlying> underlyingConverter = owner.GetOrAddConverter(enumShape.UnderlyingType);
		return new JsonEnumConverter<TEnum, TUnderlying>(underlyingConverter, enumShape.Members, owner.SerializeEnumValuesByName);
	}

	public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state = null)
	{
		JsonConverter<TElement> elementConverter = owner.GetOrAddConverter(optionalShape.ElementType);
		return new JsonOptionalConverter<TOptional, TElement>(elementConverter, optionalShape.GetDeconstructor(), optionalShape.GetNoneConstructor(), optionalShape.GetSomeConstructor());
	}

	public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null)
	{
		JsonConverter<TElement> elementConverter = owner.GetOrAddConverter(enumerableShape.ElementType);
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

		JsonConverter<TValue> valueConverter = owner.GetOrAddConverter(dictionaryShape.ValueType);
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
			serializedPropertyNamesByClrName[property.Name] = owner.GetSerializedPropertyName(property.Name, property.AttributeProvider);
			bool isRequired = requiredProperties.Contains(property.Name) || IsRequiredMember(typeof(T), property.Name);
			if (property.Accept(this, isRequired) is JsonProperty<T> jsonProperty)
			{
				properties.Add(jsonProperty);
			}
		}

		JsonProperty<T>[] jsonProperties = properties.ToArray();
		if (objectShape.Constructor is not null && objectShape.Constructor.Parameters.Count > 0)
		{
			if (objectShape.Constructor.Accept(this, new JsonConstructorVisitorState<T>(jsonProperties, serializedPropertyNamesByClrName)) is JsonConverter<T> constructorConverter)
			{
				return constructorConverter;
			}
		}

		return new JsonObjectConverter<T>(CreateFactory<T>(), jsonProperties, owner.PropertyNameComparer);
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

		return new JsonObjectWithConstructorConverter<TDeclaringType, TArgumentState>(visitorState.Properties, constructorShape.GetArgumentStateConstructor(), constructorShape.GetParameterizedConstructor(), parameters.ToArray(), parametersByName);
	}

	public override object? VisitParameter<TArgumentState, TParameterType>(IParameterShape<TArgumentState, TParameterType> parameterShape, object? state = null)
	{
		JsonParameterVisitorState visitorState = (JsonParameterVisitorState)(state ?? throw new ArgumentNullException(nameof(state)));
		JsonConverter<TParameterType> converter = owner.GetOrAddConverter(parameterShape.ParameterType);
		bool isNonNullableReferenceType = parameterShape.IsNonNullable && !typeof(TParameterType).IsValueType;
		return new JsonConstructorParameter<TArgumentState, TParameterType>(parameterShape.Name, visitorState.SerializedPropertyName, parameterShape.IsRequired, parameterShape.GetSetter(), converter, isNonNullableReferenceType);
	}

	public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
	{
		bool isRequired = state is bool required && required;
		Getter<TDeclaringType, TPropertyType>? getter = propertyShape.HasGetter ? propertyShape.GetGetter() : null;
		Setter<TDeclaringType, TPropertyType>? setter = propertyShape.HasSetter ? propertyShape.GetSetter() : null;
		JsonConverter<TPropertyType> converter = owner.GetOrAddConverter(propertyShape.PropertyType);
		bool deserializeIntoExistingInstance = getter is not null && setter is null && converter is IJsonDeserializeInto<TPropertyType>;
		if (getter is null && setter is null && !deserializeIntoExistingInstance)
		{
			return null;
		}

		string propertyName = owner.GetSerializedPropertyName(propertyShape.Name, propertyShape.AttributeProvider);
		return new JsonProperty<TDeclaringType, TPropertyType>(propertyName, propertyShape.Name, getter, setter, converter, deserializeIntoExistingInstance, isRequired, IsNonNullableReferenceType(typeof(TDeclaringType), propertyShape.Name, typeof(TPropertyType)));
	}

	public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
	{
		JsonConverter<TSurrogate> surrogateConverter = owner.GetOrAddConverter(surrogateShape.SurrogateType);
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

	private static Func<T> CreateFactory<T>()
	{
		if (typeof(T).IsValueType)
		{
			return static () => default!;
		}

		return static () => Activator.CreateInstance<T>();
	}

	private static bool IsNonNullableReferenceType(Type declaringType, string memberName, Type propertyType)
	{
		if (propertyType.IsValueType)
		{
			return false;
		}

		const BindingFlags PropertyLookup = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		MemberInfo? memberInfo = declaringType.GetProperty(memberName, PropertyLookup) ?? (MemberInfo?)declaringType.GetField(memberName, PropertyLookup);
		if (memberInfo is null)
		{
			return false;
		}

#if NET
		NullabilityInfoContext context = new();
		NullabilityInfo? nullability = memberInfo switch
		{
			PropertyInfo propertyInfo => context.Create(propertyInfo),
			FieldInfo fieldInfo => context.Create(fieldInfo),
			EventInfo eventInfo => context.Create(eventInfo),
			_ => null,
		};

		if (nullability is not null)
		{
			return nullability.ReadState == NullabilityState.NotNull;
		}

		return false;
#else
		if (TryGetNullableFlag(memberInfo.CustomAttributes, out byte propertyFlag))
		{
			return propertyFlag == 1;
		}

		for (Type? currentType = memberInfo.DeclaringType; currentType is not null; currentType = currentType.DeclaringType)
		{
			if (TryGetNullableContextFlag(currentType.CustomAttributes, out byte contextFlag))
			{
				return contextFlag == 1;
			}
		}

		return false;
#endif
	}

	private static bool IsRequiredMember(Type declaringType, string memberName)
	{
		const BindingFlags PropertyLookup = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
		MemberInfo? memberInfo = declaringType.GetProperty(memberName, PropertyLookup) ?? (MemberInfo?)declaringType.GetField(memberName, PropertyLookup);
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

#if !NET
	private static bool TryGetNullableFlag(IEnumerable<CustomAttributeData> attributes, out byte flag)
	{
		foreach (CustomAttributeData attribute in attributes)
		{
			if (attribute.AttributeType.FullName != "System.Runtime.CompilerServices.NullableAttribute" || attribute.ConstructorArguments.Count == 0)
			{
				continue;
			}

			CustomAttributeTypedArgument argument = attribute.ConstructorArguments[0];
			if (argument.ArgumentType == typeof(byte) && argument.Value is byte byteValue)
			{
				flag = byteValue;
				return true;
			}

			if (argument.Value is IReadOnlyCollection<CustomAttributeTypedArgument> values)
			{
				foreach (CustomAttributeTypedArgument value in values)
				{
					if (value.Value is byte item)
					{
						flag = item;
						return true;
					}
				}
			}
		}

		flag = default;
		return false;
	}

	private static bool TryGetNullableContextFlag(IEnumerable<CustomAttributeData> attributes, out byte flag)
	{
		foreach (CustomAttributeData attribute in attributes)
		{
			if (attribute.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute" && attribute.ConstructorArguments.Count > 0 && attribute.ConstructorArguments[0].Value is byte byteValue)
			{
				flag = byteValue;
				return true;
			}
		}

		flag = default;
		return false;
	}
#endif
}
