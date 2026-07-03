// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1204 // Keep instance/locality-oriented helper ordering in this file.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using PolyType.Utilities;

namespace Nerdbank.Json;

internal sealed class ConverterCache
{
	private readonly ConcurrentDictionary<(Type Type, Type Provider), ITypeShape> cachedTypeShapes = new();
	private readonly JsonSerializerConfiguration configuration;
	private object? lastConverter;
	private MultiProviderTypeCache? cachedConverters;

	internal ConverterCache(JsonSerializerConfiguration configuration)
	{
		this.configuration = configuration;
	}

	internal bool SerializeEnumValuesByName => this.configuration.SerializeEnumValuesByName;

	internal JsonNamingPolicy? PropertyNamingPolicy => this.configuration.PropertyNamingPolicy;

	internal IComparerProvider? ComparerProvider => this.configuration.ComparerProvider;

	internal bool HasRuntimeConverters => this.configuration.Converters.Count > 0 || this.configuration.ConverterTypes.Count > 0 || this.configuration.ConverterFactories.Count > 0;

	internal StringComparer PropertyNameComparer => this.configuration.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

	private MultiProviderTypeCache CachedConverters
	{
		get
		{
			this.cachedConverters ??= new()
			{
				DelayedValueFactory = new DelayedJsonConverterFactory(),
				ValueBuilderFactory = ctx => new JsonStandardVisitor(this, ctx),
			};

			return this.cachedConverters;
		}
	}

#if NET
	internal JsonConverter<T> GetOrAddConverter<T>()
		=> throw new NotSupportedException("Dynamic converter lookup is not supported on .NET. Supply an explicit type shape or witness type.");
#else
	internal JsonConverter<T> GetOrAddConverter<T>()
		=> this.GetOrAddConverter(this.ResolveDynamicTypeShapeOrThrow<T>());
#endif

	internal JsonConverter<T> GetOrAddConverter<T>(ITypeShape<T> shape)
		=> (JsonConverter<T>)(this.lastConverter is JsonConverter<T> lastConverter ? lastConverter : (this.lastConverter = this.CachedConverters.GetOrAdd(shape)!));

	internal JsonConverter GetOrAddConverter(ITypeShape shape)
		=> (JsonConverter)this.CachedConverters.GetOrAdd(shape)!;

#if NET8_0
	[RequiresDynamicCode("Dynamic shape resolution may require runtime-generated code.")]
#endif
	internal ITypeShape<T> ResolveDynamicTypeShapeOrThrow<T>()
	{
		Type type = typeof(T);
		(Type, Type) key = (type, type);
		if (!this.cachedTypeShapes.TryGetValue(key, out ITypeShape? shape))
		{
			shape = this.cachedTypeShapes.GetOrAdd(key, TypeShapeResolver.ResolveDynamicOrThrow<T>());
		}

		return (ITypeShape<T>)shape;
	}

#if NET8_0
	[RequiresDynamicCode("Dynamic witness shape resolution may require runtime-generated code.")]
#endif
	internal ITypeShape<T> ResolveDynamicTypeShapeOrThrow<T, TProvider>()
	{
		(Type, Type) key = (typeof(T), typeof(TProvider));
		if (!this.cachedTypeShapes.TryGetValue(key, out ITypeShape? shape))
		{
			shape = this.cachedTypeShapes.GetOrAdd(key, TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>());
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
		if (TryGetConverterFromAttribute(shape.Type, shape, attributeProvider, out JsonConverter? converter) && converter is not null)
		{
			return (JsonConverter<T>)converter;
		}

		return this.GetOrAddConverter(shape);
	}

	internal JsonConverter CreateConverter<T>(ITypeShape<T> shape, TypeShapeVisitor visitor)
	{
		if (this.TryGetRuntimeProfferedConverter(shape.Type, shape, out JsonConverter? runtimeConverter) && runtimeConverter is not null)
		{
			return this.WrapWithReferencePreservation((JsonConverter<T>)runtimeConverter);
		}

		if (TryGetConverterFromAttribute(shape.Type, shape, attributeProvider: null, out JsonConverter? attributedConverter) && attributedConverter is not null)
		{
			return this.WrapWithReferencePreservation((JsonConverter<T>)attributedConverter);
		}

		if (BuiltInJsonConverters.IsSupported(shape.Type))
		{
			return this.WrapWithReferencePreservation(new BuiltInJsonConverter<T>());
		}

		object? converter = shape.Accept(visitor, null);
		if (converter is JsonConverter jsonConverter)
		{
			return this.WrapWithReferencePreservation((JsonConverter<T>)jsonConverter);
		}

		throw new NotSupportedException($"JSON serialization does not yet support values of type {shape.Type.FullName}.");
	}

	internal static bool TryGetConverterFromAttribute(Type type, ITypeShape typeShape, IGenericCustomAttributeProvider? attributeProvider, out JsonConverter? converter)
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

	private JsonConverter<T> WrapWithReferencePreservation<T>(JsonConverter<T> converter)
	{
		if (this.configuration.PreserveReferences == ReferencePreservationMode.Off || !RequiresReferencePreservation(typeof(T)))
		{
			return converter;
		}

		return new ReferencePreservingJsonConverter<T>(converter);
	}

	private static bool RequiresReferencePreservation(Type type) => !type.IsValueType && !BuiltInJsonConverters.IsSupported(type);

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
}
