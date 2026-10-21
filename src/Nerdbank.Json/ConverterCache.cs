// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1204 // Keep instance/locality-oriented helper ordering in this file.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using PolyType.Utilities;

namespace Nerdbank.Json;

internal sealed class ConverterCache : ITypeShapeFunc
{
	private readonly ConcurrentDictionary<(Type Type, Type Provider), ITypeShape> cachedTypeShapes = new();
	private readonly ConcurrentDictionary<ITypeShape, JsonConverter> nativeAotResolvedConverters = new();
	private readonly JsonSerializerConfiguration configuration;
	private MultiProviderTypeCache? cachedConverters;
	private MultiProviderTypeCache? nativeAotCachedConverters;

	internal ConverterCache(JsonSerializerConfiguration configuration)
	{
		this.configuration = configuration;
	}

	internal bool SerializeEnumValuesByName => this.configuration.SerializeEnumValuesByName;

	/// <summary>Gets the policy determining whether required defaults are always written.</summary>
	internal SerializeDefaultValuesPolicy SerializeDefaultValues => this.configuration.SerializeDefaultValues;

	/// <summary>Gets the representation used for multidimensional arrays.</summary>
	internal MultiDimensionalArrayFormat MultiDimensionalArrayFormat => this.configuration.MultiDimensionalArrayFormat;

	internal JsonNamingPolicy? PropertyNamingPolicy => this.configuration.PropertyNamingPolicy;

	/// <summary>Gets the policy used for serialized dictionary keys.</summary>
	internal JsonNamingPolicy? DictionaryKeyNamingPolicy => this.configuration.DictionaryKeyNamingPolicy;

	internal MessagePack.IComparerProvider? ComparerProvider => this.configuration.ComparerProvider;

	internal bool HasRuntimeConverters => this.configuration.Converters.Count > 0 || this.configuration.ConverterTypes.Count > 0 || this.configuration.ConverterFactories.Length > 0;

	internal StringComparer PropertyNameComparer => this.configuration.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

	internal bool AllowTrailingCommas => this.configuration.AllowTrailingCommas;

	internal JsonCommentHandling ReadCommentHandling => this.configuration.ReadCommentHandling;

	internal ReferencePreservationMode PreserveReferences => this.configuration.PreserveReferences;

	internal DerivedTypeUnionCollection DerivedTypeUnions => this.configuration.DerivedTypeUnions;

	internal bool UseDiscriminatorObjects => this.configuration.UseDiscriminatorObjects;

	private MultiProviderTypeCache CachedConverters => Volatile.Read(ref this.cachedConverters) ?? LazyInitializer.EnsureInitialized(ref this.cachedConverters, () => new()
	{
		DelayedValueFactory = new DelayedJsonConverterFactory(),
		ValueBuilderFactory = ctx => new JsonStandardVisitor(this, ctx, directShapeDispatch: false),
	})!;

	private MultiProviderTypeCache NativeAotCachedConverters => Volatile.Read(ref this.nativeAotCachedConverters) ?? LazyInitializer.EnsureInitialized(ref this.nativeAotCachedConverters, () => new()
	{
		DelayedValueFactory = new DelayedJsonConverterFactory(),
		ValueBuilderFactory = ctx => new JsonStandardVisitor(this, ctx, directShapeDispatch: true),
	})!;

	object? ITypeShapeFunc.Invoke<T>(ITypeShape<T> typeShape, object? state) => this.GetOrAddConverter(typeShape);

#if NET
	internal JsonConverter<T> GetOrAddConverter<T>()
		=> throw new NotSupportedException("Dynamic converter lookup is not supported on .NET. Supply an explicit type shape or witness type.");
#else
	internal JsonConverter<T> GetOrAddConverter<T>()
		=> this.GetOrAddConverter(this.ResolveDynamicTypeShapeOrThrow<T>());
#endif

	internal JsonConverter<T> GetOrAddConverter<T>(ITypeShape<T> shape)
	{
#if NET
		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			return (JsonConverter<T>)this.nativeAotResolvedConverters.GetOrAdd(
				shape,
				static (key, cache) =>
				{
					ITypeShape<T> typedShape = (ITypeShape<T>)key;
					JsonConverter<T> converter = cache.TryGetProfferedConverter(typedShape, visitor: null, out JsonConverter<T>? profferedConverter)
						? profferedConverter
						: (JsonConverter<T>)cache.NativeAotCachedConverters.GetOrAdd(typedShape)!;
					return cache.WrapWithReferencePreservation(converter);
				},
				this);
		}
#endif

		return (JsonConverter<T>)this.CachedConverters.GetOrAdd(shape)!;
	}

	internal JsonConverter GetOrAddConverter(ITypeShape shape)
	{
#if NET
		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			return (JsonConverter)shape.Invoke(this)!;
		}
#endif

		return (JsonConverter)this.CachedConverters.GetOrAdd(shape)!;
	}

	internal JsonConverter GetOrAddConverter(Type type, ITypeShapeProvider provider)
		=> this.GetOrAddConverter(provider.GetTypeShape(type) ?? throw new NotSupportedException($"The type shape provider '{provider.GetType().FullName}' does not provide a shape for '{type.FullName}'."));

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

	internal JsonConverter CreateConverter<T>(ITypeShape<T> shape, TypeShapeVisitor visitor, object? state = null)
	{
		if (this.TryGetProfferedConverter(shape, visitor, out JsonConverter<T>? profferedConverter))
		{
			return this.WrapWithReferencePreservation(profferedConverter);
		}

		object? converter = shape.Accept(visitor, state);
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

	internal bool TryGetProfferedConverter<T>(ITypeShape<T> shape, TypeShapeVisitor? visitor, [NotNullWhen(true)] out JsonConverter<T>? converter)
	{
		if (this.TryGetRuntimeProfferedConverter(shape.Type, shape, out JsonConverter? runtimeConverter) && runtimeConverter is not null)
		{
			converter = (JsonConverter<T>)runtimeConverter;
			return true;
		}

		if (TryGetConverterFromAttribute(shape.Type, shape, attributeProvider: null, out JsonConverter? attributedConverter) && attributedConverter is not null)
		{
			converter = (JsonConverter<T>)attributedConverter;
			return true;
		}

		if (BuiltInJsonConverters.IsSupported(shape.Type))
		{
			converter = new BuiltInJsonConverter<T>();
			return true;
		}

		if (this.configuration.DerivedTypeUnions.TryGetDerivedTypeUnion(shape.Type, out DerivedTypeUnion? unionEntry) && unionEntry is not null)
		{
			visitor ??= new JsonStandardVisitor(this, this.NativeAotCachedConverters.GetScopedCache(shape).CreateGenerationContext(), directShapeDispatch: true);
			converter = RuntimeUnionBuilder.Build(this, shape, unionEntry, visitor);
			return true;
		}

		converter = null;
		return false;
	}

	internal bool ShouldPreserveReferences(Type type) => this.configuration.PreserveReferences != ReferencePreservationMode.Off && RequiresReferencePreservation(type);

	/// <summary>Gets a converter applied directly to a member, if present.</summary>
	/// <param name="shape">The member type shape.</param>
	/// <param name="attributes">The member attributes.</param>
	/// <param name="converter">Receives the converter.</param>
	/// <returns>Whether a member-specific converter was found.</returns>
	internal bool TryGetMemberCustomConverter(ITypeShape shape, IGenericCustomAttributeProvider attributes, out JsonConverter? converter)
		=> TryGetConverterFromAttribute(shape.Type, shape, attributes, out converter);

	internal bool TryGetCustomConverter(ITypeShape shape, out JsonConverter? converter)
	{
		if (this.TryGetRuntimeProfferedConverter(shape.Type, shape, out converter) && converter is not null)
		{
			return true;
		}

		if (TryGetConverterFromAttribute(shape.Type, shape, attributeProvider: null, out converter) && converter is not null)
		{
			return true;
		}

		converter = null;
		return false;
	}

	internal JsonConverter<T> WrapWithReferencePreservation<T>(JsonConverter<T> converter)
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

		ConverterContext context = new(this, shape.Provider);
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

	private static JsonConverter ActivateAssociatedConverterType(Type targetType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type converterType, ITypeShape targetTypeShape)
	{
		if (TryActivateAssociatedConverterType(converterType, targetTypeShape, out JsonConverter? converter))
		{
			return converter!;
		}

		if (!converterType.ContainsGenericParameters)
		{
			JsonConverter result = (JsonConverter)(Activator.CreateInstance(converterType) ?? throw new NotSupportedException($"Converter type '{converterType}' could not be instantiated."));
			if (result.DataType != targetType)
			{
				throw new NotSupportedException($"Converter type '{converterType}' does not convert '{targetType}'.");
			}

			return result;
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
