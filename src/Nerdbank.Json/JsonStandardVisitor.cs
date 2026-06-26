// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable IL2091 // DynamicallyAccessedMembers mismatch from Activator.CreateInstance<T>

using System;
using System.Collections.Generic;

namespace Nerdbank.Json;

internal sealed class JsonStandardVisitor(JsonConverterCache owner) : TypeShapeVisitor
{
	public override object? VisitObject<T>(IObjectTypeShape<T> objectShape, object? state = null)
	{
		List<JsonProperty<T>> properties = new();
		foreach (IPropertyShape property in objectShape.Properties)
		{
			if (property.Accept(this) is JsonProperty<T> jsonProperty)
			{
				properties.Add(jsonProperty);
			}
		}

		return new JsonObjectConverter<T>(CreateFactory<T>(), properties.ToArray());
	}

	public override object? VisitProperty<TDeclaringType, TPropertyType>(IPropertyShape<TDeclaringType, TPropertyType> propertyShape, object? state = null)
	{
		Getter<TDeclaringType, TPropertyType>? getter = propertyShape.HasGetter ? propertyShape.GetGetter() : null;
		Setter<TDeclaringType, TPropertyType>? setter = propertyShape.HasSetter ? propertyShape.GetSetter() : null;
		if (getter is null && setter is null)
		{
			return null;
		}

		string propertyName = owner.GetSerializedPropertyName(propertyShape.Name, propertyShape.AttributeProvider);
		JsonConverter<TPropertyType> converter = owner.GetOrAddConverter(propertyShape.PropertyType);
		return new JsonProperty<TDeclaringType, TPropertyType>(propertyName, getter, setter, converter);
	}

	private static Func<T> CreateFactory<T>()
	{
		if (typeof(T).IsValueType)
		{
			return static () => default!;
		}

		return static () => Activator.CreateInstance<T>();
	}
}
