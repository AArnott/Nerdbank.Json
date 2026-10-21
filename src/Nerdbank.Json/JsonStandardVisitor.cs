// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using PolyType.Utilities;

namespace Nerdbank.Json;

internal sealed class JsonStandardVisitor(ConverterCache owner, TypeGenerationContext context, bool directShapeDispatch) : TypeShapeVisitor, ITypeShapeFunc
{
	object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state)
	{
		// Work around https://github.com/dotnet/runtime/issues/134698 by keeping the NativeAOT dispatch direct.
		return directShapeDispatch ? typeShape.Accept(this, state) : owner.CreateConverter(typeShape, this);
	}

	public override object? VisitEnum<TEnum, TUnderlying>(IEnumTypeShape<TEnum, TUnderlying> enumShape, object? state = null)
		where TEnum : struct
	{
		JsonConverter<TUnderlying> underlyingConverter = this.GetConverter(enumShape.UnderlyingType, attributeProvider: null);
		return new JsonEnumConverter<TEnum, TUnderlying>(underlyingConverter, enumShape.Members, owner.SerializeEnumValuesByName, owner.PropertyNamingPolicy);
	}

	public override object? VisitOptional<TOptional, TElement>(IOptionalTypeShape<TOptional, TElement> optionalShape, object? state = null)
	{
		JsonConverter<TElement> elementConverter = this.GetConverter(optionalShape.ElementType, attributeProvider: null);
		return new JsonOptionalConverter<TOptional, TElement>(elementConverter, optionalShape.GetDeconstructor(), optionalShape.GetNoneConstructor(), optionalShape.GetSomeConstructor());
	}

	public override object? VisitEnumerable<TEnumerable, TElement>(IEnumerableTypeShape<TEnumerable, TElement> enumerableShape, object? state = null)
	{
		JsonConverter<TElement> elementConverter = this.GetConverter(enumerableShape.ElementType, attributeProvider: null);

		if (enumerableShape.IsAsyncEnumerable)
		{
			this.GetCollectionOptions(enumerableShape, enumerableShape.ElementType, enumerableShape.SupportedComparer, state as MemberComparerInfluence);
			return new JsonAsyncEnumerableConverter<TEnumerable, TElement>(elementConverter);
		}

		if (enumerableShape.Type.IsArray && enumerableShape.Rank > 1)
		{
			return new JsonMultidimensionalArrayConverter<TEnumerable, TElement>(elementConverter, enumerableShape.Rank);
		}

		Func<TEnumerable, IEnumerable<TElement>> getEnumerable = enumerableShape.GetGetEnumerable();
		CollectionConstructionOptions<TElement> constructionOptions = this.GetCollectionOptions(enumerableShape, enumerableShape.ElementType, enumerableShape.SupportedComparer, state as MemberComparerInfluence);

		return enumerableShape.ConstructionStrategy switch
		{
			CollectionConstructionStrategy.None => new JsonReadOnlyEnumerableConverter<TEnumerable, TElement>(getEnumerable, elementConverter),
			CollectionConstructionStrategy.Mutable => new JsonMutableEnumerableConverter<TEnumerable, TElement>(getEnumerable, elementConverter, enumerableShape.GetAppender(), enumerableShape.GetDefaultConstructor(), constructionOptions),
			CollectionConstructionStrategy.Parameterized => new JsonParameterizedEnumerableConverter<TEnumerable, TElement>(getEnumerable, elementConverter, enumerableShape.GetParameterizedConstructor(), constructionOptions),
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
		CollectionConstructionOptions<TKey> constructionOptions = this.GetCollectionOptions(dictionaryShape, dictionaryShape.KeyType, dictionaryShape.SupportedComparer, state as MemberComparerInfluence);

		return dictionaryShape.ConstructionStrategy switch
		{
			CollectionConstructionStrategy.None => new JsonReadOnlyDictionaryConverter<TDictionary, TKey, TValue>(getReadable, valueConverter, owner),
			CollectionConstructionStrategy.Mutable => new JsonMutableDictionaryConverter<TDictionary, TKey, TValue>(getReadable, valueConverter, owner, dictionaryShape.GetInserter(DictionaryInsertionMode.Throw), dictionaryShape.GetDefaultConstructor(), constructionOptions),
			CollectionConstructionStrategy.Parameterized => new JsonParameterizedDictionaryConverter<TDictionary, TKey, TValue>(getReadable, valueConverter, owner, dictionaryShape.GetParameterizedConstructor(), constructionOptions),
			_ => throw new NotSupportedException($"JSON serialization does not recognize dictionary construction strategy {dictionaryShape.ConstructionStrategy} for {dictionaryShape.Type.FullName}."),
		};
	}

	public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
	{
		List<JsonProperty<T>> properties = [];
		JsonExtensionData<T>? extensionData = null;
		Dictionary<string, string> serializedPropertyNamesByClrName = new(StringComparer.OrdinalIgnoreCase);
		foreach (IPropertyShape property in objectShape.Properties)
		{
			if (property is IPropertyShape<T, UnusedDataPacket> unusedDataProperty)
			{
				if (extensionData is not null)
				{
					throw new JsonSerializationException($"Type '{typeof(T).FullName}' declares more than one member of type '{typeof(UnusedDataPacket).FullName}'.");
				}

				extensionData = new JsonExtensionData<T>(
					unusedDataProperty.HasGetter ? unusedDataProperty.GetGetter() : null,
					unusedDataProperty.HasSetter ? unusedDataProperty.GetSetter() : null);
				continue;
			}

			serializedPropertyNamesByClrName[property.Name] = owner.GetSerializedPropertyName(property.Name, property.AttributeProvider);
			IParameterShape? matchingParameter = objectShape.Constructor?.Parameters.FirstOrDefault(p => p.Name == property.Name);
			if (matchingParameter is null && objectShape.Constructor is { } constructor)
			{
				IParameterShape[] candidates = constructor.Parameters.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Name, property.Name)).ToArray();
				if (candidates.Length == 1)
				{
					matchingParameter = candidates[0];
				}
			}

			if (property.Accept(this, matchingParameter) is JsonProperty<T> jsonProperty)
			{
				properties.Add(jsonProperty);
			}
		}

		JsonProperty<T>[] jsonProperties = [.. properties];
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
		var visitorState = (JsonConstructorVisitorState<TDeclaringType>)(state ?? throw new ArgumentNullException(nameof(state)));
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

		return new JsonObjectWithConstructorConverter<TDeclaringType, TArgumentState>(visitorState.Properties, constructorShape.GetArgumentStateConstructor(), constructorShape.GetParameterizedConstructor(), [.. parameters], parametersByName, owner.PropertyNameComparer, visitorState.ExtensionData);
	}

	public override object? VisitParameter<TArgumentState, TParameterType>(IParameterShape<TArgumentState, TParameterType> parameterShape, object? state = null)
	{
		var visitorState = (JsonParameterVisitorState)(state ?? throw new ArgumentNullException(nameof(state)));
		JsonConverter<TParameterType> converter = this.GetConverter(parameterShape.ParameterType, parameterShape.AttributeProvider);
		bool isNonNullableReferenceType = parameterShape.IsNonNullable && !typeof(TParameterType).IsValueType;
		return new JsonConstructorParameter<TArgumentState, TParameterType>(parameterShape.Name, visitorState.SerializedPropertyName, parameterShape.IsRequired, parameterShape.GetSetter(), converter, isNonNullableReferenceType);
	}

	public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
	{
		IParameterShape? constructorParameter = state as IParameterShape;
		bool isRequired = constructorParameter?.IsRequired
			?? propertyShape.AttributeProvider?.GetCustomAttributes<PropertyShapeAttribute>(inherit: true).FirstOrDefault()?.IsRequired
			?? HasRequiredMemberAttribute(propertyShape.MemberInfo);
		Getter<TDeclaringType, TPropertyType>? getter = propertyShape.HasGetter ? propertyShape.GetGetter() : null;
		Setter<TDeclaringType, TPropertyType>? setter = propertyShape.HasSetter ? propertyShape.GetSetter() : null;
		JsonConverter<TPropertyType> converter = this.GetConverter(propertyShape.PropertyType, propertyShape.AttributeProvider);
		bool deserializeIntoExistingInstance = getter is not null && setter is null && converter is IJsonDeserializeInto<TPropertyType>;
		if (setter is null && !deserializeIntoExistingInstance && constructorParameter is null)
		{
			return null;
		}

		string propertyName = owner.GetSerializedPropertyName(propertyShape.Name, propertyShape.AttributeProvider);
		bool isNonNullableReferenceType = !typeof(TPropertyType).IsValueType && (propertyShape.IsSetterNonNullable || (!propertyShape.HasSetter && propertyShape.IsGetterNonNullable));
		TPropertyType? defaultValue = default;
		if (constructorParameter?.HasDefaultValue is true)
		{
			defaultValue = (TPropertyType?)constructorParameter.DefaultValue;
		}
		else if (propertyShape.AttributeProvider?.GetCustomAttributes<DefaultValueAttribute>(inherit: true).FirstOrDefault() is { Value: TPropertyType attributeDefault })
		{
			defaultValue = attributeDefault;
		}

		return new JsonProperty<TDeclaringType, TPropertyType>(propertyName, propertyShape.Name, getter, setter, converter, deserializeIntoExistingInstance, isRequired, isNonNullableReferenceType, defaultValue);
	}

	public override object? VisitSurrogate<T, TSurrogate>(ISurrogateTypeShape<T, TSurrogate> surrogateShape, object? state = null)
	{
		JsonConverter<TSurrogate> surrogateConverter = this.GetConverter(surrogateShape.SurrogateType, attributeProvider: null);
		return new JsonSurrogateConverter<T, TSurrogate>(surrogateShape, surrogateConverter);
	}

	public override object? VisitUnion<TUnion>(IUnionTypeShape<TUnion> unionShape, object? state = null)
	{
		ITypeShape baseType = unionShape.BaseType;
		JsonConverter<TUnion>? baseConverter = RuntimeUnionBuilder.IsConstructible(baseType as IObjectTypeShape)
			? (JsonConverter<TUnion>)baseType.Accept(this)!
			: null;
		Getter<TUnion, int> getUnionCaseIndex = unionShape.GetGetUnionCaseIndex();
		Dictionary<int, JsonConverter> deserializersByIntAlias = new(unionShape.UnionCases.Count);
		Dictionary<string, JsonConverter> deserializersByStringAlias = new(unionShape.UnionCases.Count, StringComparer.Ordinal);
		var serializers = new JsonUnionCaseMetadata<TUnion>[unionShape.UnionCases.Count];

		for (int i = 0; i < unionShape.UnionCases.Count; i++)
		{
			IUnionCaseShape unionCase = unionShape.UnionCases[i];
			var caseConverter = (JsonConverter<TUnion>)unionCase.Accept(this)!;
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

		return new JsonUnionConverter<TUnion>(baseConverter, getUnionCaseIndex, serializers, new ReadOnlyDictionary<int, JsonConverter>(deserializersByIntAlias), new ReadOnlyDictionary<string, JsonConverter>(deserializersByStringAlias), owner.UseDiscriminatorObjects);
	}

	public override object? VisitUnionCase<TUnionCase, TUnion>(IUnionCaseShape<TUnionCase, TUnion> unionCaseShape, object? state = null)
	{
		var caseConverter = (JsonConverter<TUnionCase>)unionCaseShape.UnionCaseType.Accept(this)!;
		return new JsonUnionCaseConverter<TUnionCase, TUnion>(caseConverter, unionCaseShape.Marshaler);
	}

	public override object? VisitFunction<TFunction, TArgumentState, TResult>(IFunctionTypeShape<TFunction, TArgumentState, TResult> functionShape, object? state = null)
		=> throw new NotSupportedException($"JSON serialization does not support delegate types such as {functionShape.Type.FullName}.");

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

	private static bool TryGetCollectionComparerAttribute(IGenericCustomAttributeProvider? attributeProvider, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out UseComparerAttribute? comparerAttribute)
	{
		if (attributeProvider is not null)
		{
			foreach (UseComparerAttribute attribute in attributeProvider.GetCustomAttributes<UseComparerAttribute>(inherit: false))
			{
				comparerAttribute = attribute;
				return true;
			}
		}

		comparerAttribute = null;
		return false;
	}

	private static IComparer<TKey> AsComparer<TKey>(object comparer, ITypeShape collectionShape, Type comparerType)
		=> comparer as IComparer<TKey> ?? throw new NotSupportedException($"The comparer type '{comparerType.FullName}' specified by {nameof(UseComparerAttribute)} must implement IComparer<{typeof(TKey).FullName}> for collection type '{collectionShape.Type.FullName}'.");

	private static IEqualityComparer<TKey> AsEqualityComparer<TKey>(object comparer, ITypeShape collectionShape, Type comparerType)
		=> comparer as IEqualityComparer<TKey> ?? throw new NotSupportedException($"The comparer type '{comparerType.FullName}' specified by {nameof(UseComparerAttribute)} must implement IEqualityComparer<{typeof(TKey).FullName}> for collection type '{collectionShape.Type.FullName}'.");

	private JsonConverter<T> GetConverter<T>(ITypeShape<T> shape, IGenericCustomAttributeProvider? attributeProvider)
	{
		if (ConverterCache.TryGetConverterFromAttribute(shape.Type, shape, attributeProvider, out JsonConverter? converter) && converter is not null)
		{
			return (JsonConverter<T>)converter;
		}

		if (TryGetCollectionComparerAttribute(attributeProvider, out UseComparerAttribute? comparerAttribute))
		{
			if (shape.Kind is not (TypeShapeKind.Enumerable or TypeShapeKind.Dictionary))
			{
				throw new NotSupportedException($"{nameof(UseComparerAttribute)} can only be applied to dictionary or set members, but it was applied to a member of type '{shape.Type.FullName}'.");
			}

			// Member-specific comparers bypass the per-type converter cache so that two members of the same
			// collection type can carry different comparers.
			return (JsonConverter<T>)owner.CreateConverter(shape, this, new MemberComparerInfluence(comparerAttribute));
		}

		if (directShapeDispatch)
		{
			JsonConverter<T> result = owner.TryGetProfferedConverter(shape, this, out JsonConverter<T>? profferedConverter)
				? profferedConverter
				: (JsonConverter<T>)context.GetOrAdd(shape)!;
			return owner.WrapWithReferencePreservation(result);
		}

		return (JsonConverter<T>)context.GetOrAdd(shape)!;
	}

	private CollectionConstructionOptions<TKey> GetCollectionOptions<TKey>(ITypeShape collectionShape, ITypeShape<TKey> keyShape, CollectionComparerOptions requiredComparer, MemberComparerInfluence? memberInfluence)
	{
		if (memberInfluence is not null)
		{
			if (requiredComparer == CollectionComparerOptions.None)
			{
				throw new NotSupportedException($"The collection type '{collectionShape.Type.FullName}' does not accept a custom comparer, so {nameof(UseComparerAttribute)} cannot be applied to a member of this type.");
			}

			object comparer = memberInfluence.ActivateComparer(collectionShape);
			return requiredComparer switch
			{
				CollectionComparerOptions.Comparer => new CollectionConstructionOptions<TKey> { Comparer = AsComparer<TKey>(comparer, collectionShape, memberInfluence.ComparerType) },
				CollectionComparerOptions.EqualityComparer => new CollectionConstructionOptions<TKey> { EqualityComparer = AsEqualityComparer<TKey>(comparer, collectionShape, memberInfluence.ComparerType) },
				_ => throw new NotSupportedException($"JSON serialization does not recognize collection comparer option {requiredComparer}."),
			};
		}

		if (owner.ComparerProvider is null)
		{
			return default;
		}

		return requiredComparer switch
		{
			CollectionComparerOptions.None => default,
			CollectionComparerOptions.Comparer => new CollectionConstructionOptions<TKey> { Comparer = owner.ComparerProvider.GetComparer(keyShape) },
			CollectionComparerOptions.EqualityComparer => new CollectionConstructionOptions<TKey> { EqualityComparer = owner.ComparerProvider.GetEqualityComparer(keyShape) },
			_ => throw new NotSupportedException($"JSON serialization does not recognize collection comparer option {requiredComparer}."),
		};
	}

	/// <summary>
	/// Captures a member-specified comparer so a member-specific collection converter can be constructed.
	/// </summary>
	private sealed class MemberComparerInfluence(UseComparerAttribute attribute)
	{
		internal Type ComparerType => attribute.ComparerType;

		/// <summary>
		/// Activates the member-specified comparer using the source-generated shape associated with the collection type.
		/// </summary>
		/// <param name="collectionShape">The shape of the collection type that declares the comparer as an associated type.</param>
		/// <returns>The activated comparer instance.</returns>
		internal object ActivateComparer(ITypeShape collectionShape)
		{
			MethodInfo? propertyGetter = null;
			if (attribute.MemberName is not null)
			{
				PropertyInfo? property = attribute.ComparerType.GetProperty(attribute.MemberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
				if (property is not { GetMethod: { IsPublic: true } getter } || property.GetIndexParameters().Length != 0)
				{
					throw new NotSupportedException($"Unable to find public property '{attribute.MemberName}' on type '{attribute.ComparerType.FullName}' with a public parameterless getter.");
				}

				if (getter.IsStatic)
				{
					return getter.Invoke(null, null) ?? throw this.CreateNullPropertyValueError();
				}

				propertyGetter = getter;
			}

			if ((collectionShape.GetAssociatedTypeShape(attribute.ComparerType) as IObjectTypeShape)?.GetDefaultConstructor() is Func<object> factory)
			{
				object instance = factory() ?? throw new NotSupportedException($"The comparer type '{attribute.ComparerType.FullName}' specified by {nameof(UseComparerAttribute)} produced a null instance.");
				return propertyGetter is null ? instance : propertyGetter.Invoke(instance, null) ?? throw this.CreateNullPropertyValueError();
			}

			throw new NotSupportedException($"The comparer type '{attribute.ComparerType.FullName}' specified by {nameof(UseComparerAttribute)} must declare a public parameterless constructor and have a source-generated shape associated with collection type '{collectionShape.Type.FullName}'.");
		}

		private NotSupportedException CreateNullPropertyValueError() => new($"{attribute.ComparerType.FullName}.{attribute.MemberName} produced a null value.");
	}
}
