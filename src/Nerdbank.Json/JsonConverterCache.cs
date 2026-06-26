// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable IL3050 // Dynamic shape resolution is the intentional fallback API for now.
#pragma warning disable IL2070 // Reflection-based collection fallback is intentional for unsupported root collection shapes.
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1204 // Keep instance/locality-oriented helper ordering in this file.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Nerdbank.Json;

internal sealed class JsonConverterCache
{
	private readonly ConcurrentDictionary<Type, JsonConverter> cachedConverters = new();
	private readonly ConcurrentDictionary<ITypeShape, JsonConverter> cachedShapeConverters = new();
	private readonly ConcurrentDictionary<Type, ITypeShape> cachedTypeShapes = new();
	private readonly ConcurrentDictionary<(Type TargetType, Type ProviderType), ITypeShape> cachedWitnessTypeShapes = new();
	private readonly JsonSerializerConfiguration configuration;

	internal JsonConverterCache(JsonSerializerConfiguration configuration)
	{
		this.configuration = configuration;
	}

	internal JsonConverter<T> GetOrAddConverter<T>()
		=> (JsonConverter<T>)this.cachedConverters.GetOrAdd(typeof(T), _ => this.CreateConverter<T>());

	internal JsonConverter<T> GetOrAddConverter<T>(ITypeShape<T> shape)
		=> (JsonConverter<T>)this.cachedShapeConverters.GetOrAdd(shape, _ => this.CreateConverter(shape));

	internal ITypeShape<T> ResolveDynamicTypeShapeOrThrow<T>()
	{
		Type type = typeof(T);
		if (!this.cachedTypeShapes.TryGetValue(type, out ITypeShape? shape))
		{
			shape = this.cachedTypeShapes.GetOrAdd(type, TypeShapeResolver.ResolveDynamicOrThrow<T>());
		}

		return (ITypeShape<T>)shape;
	}

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

	private JsonConverter CreateConverter<T>()
	{
		if (BuiltInJsonConverters.IsSupported(typeof(T)))
		{
			return new BuiltInJsonConverter<T>();
		}

		if (this.TryCreateCollectionConverter(typeof(T), out JsonConverter? collectionConverter))
		{
			return collectionConverter!;
		}

		return this.CreateConverter(this.ResolveDynamicTypeShapeOrThrow<T>());
	}

	private JsonConverter CreateConverter<T>(ITypeShape<T> shape)
	{
		if (BuiltInJsonConverters.IsSupported(shape.Type))
		{
			return new BuiltInJsonConverter<T>();
		}

		object? converter = shape.Accept(new JsonStandardVisitor(this), null);
		if (converter is JsonConverter jsonConverter)
		{
			return jsonConverter;
		}

		throw new NotSupportedException($"JSON serialization does not yet support values of type {shape.Type.FullName}.");
	}

	private bool TryCreateCollectionConverter(Type type, out JsonConverter? converter)
	{
		if (TryGetGenericInterface(type, typeof(IDictionary<,>), out Type[]? dictionaryArguments) && HasDefaultConstructor(type))
		{
			if (!JsonDictionaryKeyConverter.IsSupported(dictionaryArguments![0]))
			{
				converter = null;
				return false;
			}

			Type converterType = typeof(JsonDictionaryCollectionConverter<,,>).MakeGenericType(type, dictionaryArguments[0], dictionaryArguments[1]);
			converter = (JsonConverter)Activator.CreateInstance(converterType, this)!;
			return true;
		}

		if (TryGetGenericInterface(type, typeof(ICollection<>), out Type[]? collectionArguments) && HasDefaultConstructor(type))
		{
			Type converterType = typeof(JsonListConverter<,>).MakeGenericType(type, collectionArguments![0]);
			converter = (JsonConverter)Activator.CreateInstance(converterType, this)!;
			return true;
		}

		converter = null;
		return false;
	}

	private static bool HasDefaultConstructor(Type type)
		=> !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) is not null;

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
}
