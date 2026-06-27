// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1204 // Keep instance/locality-oriented helper ordering in this file.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace Nerdbank.Json;

internal sealed class JsonConverterCache
{
	private readonly ConcurrentDictionary<Type, JsonConverter> cachedConverters = new();
	private readonly ConcurrentDictionary<ITypeShape, JsonConverter> cachedShapeConverters = new();
	private readonly ConcurrentDictionary<Type, ITypeShape> cachedTypeShapes = new();
	private readonly ConcurrentDictionary<(Type TargetType, Type ProviderType), ITypeShape> cachedWitnessTypeShapes = new();
	private readonly JsonSerializerConfiguration configuration;
	private readonly object shapeCreationLock = new();

	internal JsonConverterCache(JsonSerializerConfiguration configuration)
	{
		this.configuration = configuration;
	}

	internal bool SerializeEnumValuesByName => this.configuration.SerializeEnumValuesByName;

	internal bool HasRuntimeConverters => this.configuration.Converters.Count > 0 || this.configuration.ConverterTypes.Count > 0 || this.configuration.ConverterFactories.Count > 0;

	internal StringComparer PropertyNameComparer => this.configuration.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

	#if NET
	[RequiresDynamicCode("Dynamic converter lookup may require runtime-generated shapes when no explicit type shape is supplied.")]
	#endif
	[RequiresUnreferencedCode("Serializing or deserializing types without generated shapes may require reflection metadata.")]
	internal JsonConverter<T> GetOrAddConverter<T>()
		=> (JsonConverter<T>)this.cachedConverters.GetOrAdd(typeof(T), _ => this.CreateConverter<T>());

	internal JsonConverter<T> GetOrAddConverter<T>(ITypeShape<T> shape)
	{
		if (this.cachedShapeConverters.TryGetValue(shape, out JsonConverter? existingConverter))
		{
			return (JsonConverter<T>)existingConverter;
		}

		lock (this.shapeCreationLock)
		{
			if (this.cachedShapeConverters.TryGetValue(shape, out existingConverter))
			{
				return (JsonConverter<T>)existingConverter;
			}

			DeferredJsonConverter<T> deferredConverter = new();
			this.cachedShapeConverters[shape] = deferredConverter;
			try
			{
				JsonConverter<T> converter = (JsonConverter<T>)this.CreateConverter(shape);
				deferredConverter.SetInner(converter);
				this.cachedShapeConverters[shape] = converter;
				return converter;
			}
			catch
			{
				this.cachedShapeConverters.TryRemove(shape, out _);
				throw;
			}
		}
	}

	#if NET8_0
	[RequiresDynamicCode("Dynamic shape resolution may require runtime-generated code.")]
	#endif
	internal ITypeShape<T> ResolveDynamicTypeShapeOrThrow<T>()
	{
		Type type = typeof(T);
		if (!this.cachedTypeShapes.TryGetValue(type, out ITypeShape? shape))
		{
			shape = this.cachedTypeShapes.GetOrAdd(type, TypeShapeResolver.ResolveDynamicOrThrow<T>());
		}

		return (ITypeShape<T>)shape;
	}

	#if NET8_0
	[RequiresDynamicCode("Dynamic witness shape resolution may require runtime-generated code.")]
	#endif
	internal ITypeShape<T> ResolveDynamicTypeShapeOrThrow<T, TProvider>()
	{
		(Type TargetType, Type ProviderType) key = (typeof(T), typeof(TProvider));
		if (!this.cachedWitnessTypeShapes.TryGetValue(key, out ITypeShape? shape))
		{
			shape = this.cachedWitnessTypeShapes.GetOrAdd(key, TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>());
		}

		return (ITypeShape<T>)shape;
	}

	internal string GetSerializedPropertyName(string name, IGenericCustomAttributeProvider? attributeProvider)
	{
		if (this.configuration.PropertyNamingPolicy is null)
		{
			return name;
		}

		if (attributeProvider?.GetCustomAttributes<PropertyShapeAttribute>(inherit: false) is IReadOnlyList<PropertyShapeAttribute> attributes)
		{
			for (int i = 0; i < attributes.Count; i++)
			{
				if (attributes[i].Name is string overriddenName)
				{
					return overriddenName;
				}
			}
		}

		return this.configuration.PropertyNamingPolicy.ConvertName(name);
	}

	internal string GetSerializedDictionaryKey(string key)
		=> this.configuration.DictionaryKeyNamingPolicy?.ConvertName(key) ?? key;

	internal JsonConverter<T> GetConverter<T>(ITypeShape<T> shape, IGenericCustomAttributeProvider? attributeProvider)
	{
		if (this.TryGetConverterFromAttribute(shape.Type, shape, attributeProvider, out JsonConverter? converter) && converter is not null)
		{
			return this.RequireTypedConverter<T>(converter);
		}

		return this.GetOrAddConverter(shape);
	}

	#if NET
	[RequiresDynamicCode("Dynamic converter creation may require runtime-generated shapes when no explicit type shape is supplied.")]
	#endif
	[RequiresUnreferencedCode("Serializing or deserializing types without generated shapes may require reflection metadata.")]
	private JsonConverter CreateConverter<T>()
	{
		ITypeShape<T>? shape = this.TryResolveDynamicTypeShape<T>();
		if (this.TryGetRuntimeProfferedConverterDynamically(typeof(T), shape, out JsonConverter? runtimeConverter) && runtimeConverter is not null)
		{
			return this.WrapWithReferencePreservation(this.RequireTypedConverter<T>(runtimeConverter));
		}

		if (this.TryGetConverterFromAttributeDynamically(typeof(T), shape, attributeProvider: null, out JsonConverter? attributedConverter) && attributedConverter is not null)
		{
			return this.WrapWithReferencePreservation(this.RequireTypedConverter<T>(attributedConverter));
		}

		if (BuiltInJsonConverters.IsSupported(typeof(T)))
		{
			return this.WrapWithReferencePreservation(new BuiltInJsonConverter<T>());
		}

		if (this.TryCreateCollectionConverter(typeof(T), out JsonConverter? collectionConverter))
		{
			return this.WrapWithReferencePreservation((JsonConverter<T>)collectionConverter!);
		}

		return this.CreateConverter(shape ?? this.ResolveDynamicTypeShapeOrThrow<T>());
	}

	private JsonConverter CreateConverter<T>(ITypeShape<T> shape)
	{
		if (this.TryGetRuntimeProfferedConverter(shape.Type, shape, out JsonConverter? runtimeConverter) && runtimeConverter is not null)
		{
			return this.WrapWithReferencePreservation(this.RequireTypedConverter<T>(runtimeConverter));
		}

		if (this.TryGetConverterFromAttribute(shape.Type, shape, attributeProvider: null, out JsonConverter? attributedConverter) && attributedConverter is not null)
		{
			return this.WrapWithReferencePreservation(this.RequireTypedConverter<T>(attributedConverter));
		}

		if (BuiltInJsonConverters.IsSupported(shape.Type))
		{
			return this.WrapWithReferencePreservation(new BuiltInJsonConverter<T>());
		}

		object? converter = shape.Accept(new JsonStandardVisitor(this), null);
		if (converter is JsonConverter jsonConverter)
		{
			return this.WrapWithReferencePreservation((JsonConverter<T>)jsonConverter);
		}

		throw new NotSupportedException($"JSON serialization does not yet support values of type {shape.Type.FullName}.");
	}

	private JsonConverter<T> WrapWithReferencePreservation<T>(JsonConverter<T> converter)
	{
		if (this.configuration.PreserveReferences == ReferencePreservationMode.Off || !RequiresReferencePreservation(typeof(T)))
		{
			return converter;
		}

		return new ReferencePreservingJsonConverter<T>(converter);
	}

	private static bool RequiresReferencePreservation(Type type) => !type.IsValueType && !BuiltInJsonConverters.IsSupported(type);

	private JsonConverter<T> RequireTypedConverter<T>(JsonConverter converter)
	{
		if (converter is JsonConverter<T> typedConverter)
		{
			return typedConverter;
		}

		throw new InvalidOperationException($"Converter '{converter.GetType().FullName}' was registered for '{typeof(T).FullName}' but does not derive from JsonConverter<{typeof(T).Name}>.");
	}

	private bool TryGetConverterFromAttribute(Type type, ITypeShape typeShape, IGenericCustomAttributeProvider? attributeProvider, out JsonConverter? converter)
	{
		JsonConverterAttribute? attribute = null;
		if (attributeProvider is not null)
		{
			foreach (JsonConverterAttribute candidate in attributeProvider.GetCustomAttributes<JsonConverterAttribute>(inherit: false))
			{
				attribute = candidate;
				break;
			}
		}

		if (attribute is null && type.GetCustomAttributes(typeof(JsonConverterAttribute), inherit: false) is object[] typeAttributes && typeAttributes.Length > 0)
		{
			attribute = (JsonConverterAttribute)typeAttributes[0];
		}

		if (attribute is null)
		{
			converter = null;
			return false;
		}

		converter = ActivateAssociatedConverterType(type, attribute.ConverterType, typeShape);
		return true;
	}

	#if NET
	[RequiresDynamicCode("Dynamic shape resolution may require runtime-generated code.")]
	#endif
	[RequiresUnreferencedCode("Resolving shapes dynamically may require reflection metadata.")]
	private ITypeShape<T>? TryResolveDynamicTypeShape<T>()
	{
		try
		{
			return this.ResolveDynamicTypeShapeOrThrow<T>();
		}
		catch (NotSupportedException)
		{
			return null;
		}
	}

	private bool TryGetRuntimeProfferedConverter(Type type, ITypeShape shape, out JsonConverter? converter)
	{
		if (this.configuration.Converters.TryGetConverter(type, out converter))
		{
			return true;
		}

		if (this.configuration.ConverterTypes.TryGetConverterType(type, out Type? converterType) ||
			(type.IsGenericType && this.configuration.ConverterTypes.TryGetConverterType(type.GetGenericTypeDefinition(), out converterType)))
		{
			converter = ActivateAssociatedConverterType(type, converterType, shape);
			return true;
		}

		JsonConverterFactoryContext context = new(this);
		foreach (IJsonConverterFactory factory in this.configuration.ConverterFactories)
		{
			if ((converter = factory.CreateConverter(type, shape, context)) is not null)
			{
				return true;
			}
		}

		converter = null;
		return false;
	}

	#if NET
	[RequiresDynamicCode("Dynamic converter activation may require native code for reflected generic instantiations.")]
	#endif
	[RequiresUnreferencedCode("Activating converters without associated generated shapes may require reflection metadata.")]
	private bool TryGetConverterFromAttributeDynamically(Type type, ITypeShape? typeShape, IGenericCustomAttributeProvider? attributeProvider, out JsonConverter? converter)
	{
		JsonConverterAttribute? attribute = null;
		if (attributeProvider is not null)
		{
			foreach (JsonConverterAttribute candidate in attributeProvider.GetCustomAttributes<JsonConverterAttribute>(inherit: false))
			{
				attribute = candidate;
				break;
			}
		}

		if (attribute is null && type.GetCustomAttributes(typeof(JsonConverterAttribute), inherit: false) is object[] typeAttributes && typeAttributes.Length > 0)
		{
			attribute = (JsonConverterAttribute)typeAttributes[0];
		}

		if (attribute is null)
		{
			converter = null;
			return false;
		}

		if (typeShape is not null && TryActivateAssociatedConverterType(attribute.ConverterType, typeShape, out converter))
		{
			return true;
		}

		converter = ActivateConverterTypeDynamically(type, attribute.ConverterType);
		return true;
	}

	#if NET
	[RequiresDynamicCode("Dynamic converter activation may require native code for reflected generic instantiations.")]
	#endif
	[RequiresUnreferencedCode("Activating converters without associated generated shapes may require reflection metadata.")]
	private bool TryGetRuntimeProfferedConverterDynamically(Type type, ITypeShape? shape, out JsonConverter? converter)
	{
		if (this.configuration.Converters.TryGetConverter(type, out converter))
		{
			return true;
		}

		if (this.configuration.ConverterTypes.TryGetConverterType(type, out Type? converterType) ||
			(type.IsGenericType && this.configuration.ConverterTypes.TryGetConverterType(type.GetGenericTypeDefinition(), out converterType)))
		{
			if (shape is not null && TryActivateAssociatedConverterType(converterType, shape, out converter))
			{
				return true;
			}

			converter = ActivateConverterTypeDynamically(type, converterType);
			return true;
		}

		JsonConverterFactoryContext context = new(this);
		foreach (IJsonConverterFactory factory in this.configuration.ConverterFactories)
		{
			if ((converter = factory.CreateConverter(type, shape, context)) is not null)
			{
				return true;
			}
		}

		converter = null;
		return false;
	}

	private static JsonConverter ActivateAssociatedConverterType(Type targetType, Type converterType, ITypeShape targetTypeShape)
	{
		if (TryActivateAssociatedConverterType(converterType, targetTypeShape, out JsonConverter? converter))
		{
			return converter!;
		}

		throw new NotSupportedException($"Converter type '{converterType}' registered for '{targetType}' must have an associated generated shape.");
	}

	private static bool TryActivateAssociatedConverterType(Type converterType, ITypeShape targetTypeShape, out JsonConverter? converter)
	{
		if ((targetTypeShape.GetAssociatedTypeShape(converterType) as IObjectTypeShape)?.GetDefaultConstructor() is Func<object> factory)
		{
			converter = (JsonConverter)factory();
			return true;
		}

		converter = null;
		return false;
	}

	#if NET
	[RequiresDynamicCode("Dynamic converter activation may require native code for reflected generic instantiations.")]
	#endif
	[RequiresUnreferencedCode("Activating converters without associated generated shapes may require reflection metadata.")]
	private static JsonConverter ActivateConverterTypeDynamically(Type targetType, Type converterType)
	{
		if (converterType.IsGenericTypeDefinition)
		{
			if (!targetType.IsGenericType)
			{
				throw new InvalidOperationException($"Open generic converter type '{converterType}' cannot be used for non-generic target type '{targetType}'.");
			}

			converterType = converterType.MakeGenericType(targetType.GetGenericArguments());
		}

		return (JsonConverter)Activator.CreateInstance(converterType)!;
	}

	#if NET
	[RequiresDynamicCode("Dynamic collection fallback may require native code for reflected generic instantiations.")]
	#endif
	[RequiresUnreferencedCode("Collection fallback without generated shapes may require reflection metadata.")]
	private bool TryCreateCollectionConverter(Type type, out JsonConverter? converter)
	{
		if (TryGetGenericInterface(type, typeof(IDictionary<,>), out Type[]? dictionaryArguments) && HasDefaultConstructor(type))
		{
			if (!JsonDictionaryKeyConverter.IsSupported(dictionaryArguments![0]))
			{
				converter = null;
				return false;
			}

			MethodInfo factory = typeof(JsonConverterCache).GetMethod(nameof(CreateDictionaryCollectionConverter), BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(type, dictionaryArguments[0], dictionaryArguments[1]);
			converter = (JsonConverter)factory.Invoke(this, null)!;
			return true;
		}

		if (TryGetGenericInterface(type, typeof(ICollection<>), out Type[]? collectionArguments) && HasDefaultConstructor(type))
		{
			MethodInfo factory = typeof(JsonConverterCache).GetMethod(nameof(CreateListCollectionConverter), BindingFlags.Instance | BindingFlags.NonPublic)!.MakeGenericMethod(type, collectionArguments![0]);
			converter = (JsonConverter)factory.Invoke(this, null)!;
			return true;
		}

		converter = null;
		return false;
	}

	[RequiresUnreferencedCode("Determining default constructors without generated shapes may require reflection metadata.")]
	private static bool HasDefaultConstructor(Type type)
		=> !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) is not null;

	[RequiresUnreferencedCode("Inspecting implemented interfaces without generated shapes may require reflection metadata.")]
	private static bool TryGetGenericInterface(Type type, Type genericInterface, out Type[]? genericArguments)
	{
		if (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterface)
		{
			genericArguments = type.GetGenericArguments();
			return true;
		}

		foreach (Type @interface in type.GetInterfaces())
		{
			if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == genericInterface)
			{
				genericArguments = @interface.GetGenericArguments();
				return true;
			}
		}

		genericArguments = null;
		return false;
	}

	#if NET
	[RequiresDynamicCode("Dynamic collection fallback may require native code for reflected generic instantiations.")]
	#endif
	[RequiresUnreferencedCode("Dynamic collection fallback without generated shapes may require reflection metadata.")]
	private JsonConverter CreateListCollectionConverter<TCollection, TElement>()
		where TCollection : IEnumerable<TElement>, ICollection<TElement>, new()
		=> new JsonListConverter<TCollection, TElement>(this.GetOrAddConverter<TElement>(), static () => new TCollection());

	#if NET
	[RequiresDynamicCode("Dynamic collection fallback may require native code for reflected generic instantiations.")]
	#endif
	[RequiresUnreferencedCode("Dynamic dictionary fallback without generated shapes may require reflection metadata.")]
	private JsonConverter CreateDictionaryCollectionConverter<TDictionary, TKey, TValue>()
		where TDictionary : IEnumerable<KeyValuePair<TKey, TValue>>, IDictionary<TKey, TValue>, new()
		where TKey : notnull
		=> new JsonDictionaryCollectionConverter<TDictionary, TKey, TValue>(this, this.GetOrAddConverter<TValue>(), static () => new TDictionary());
}
