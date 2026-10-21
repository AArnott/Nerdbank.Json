// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json.Analyzers;

/// <summary>
/// Metadata names shared by semantic analyzer rules.
/// </summary>
internal static class KnownNames
{
	/// <summary>The member shape customization attribute.</summary>
	internal const string PropertyShapeAttribute = "PolyType.PropertyShapeAttribute";

	/// <summary>The type shape customization attribute.</summary>
	internal const string TypeShapeAttribute = "PolyType.TypeShapeAttribute";

	/// <summary>The constructor selection attribute.</summary>
	internal const string ConstructorShapeAttribute = "PolyType.ConstructorShapeAttribute";

	/// <summary>The constructor parameter customization attribute.</summary>
	internal const string ParameterShapeAttribute = "PolyType.ParameterShapeAttribute";

	/// <summary>The declarative union case attribute.</summary>
	internal const string DerivedTypeShapeAttribute = "PolyType.DerivedTypeShapeAttribute";

	/// <summary>The surrogate marshaler interface.</summary>
	internal const string Marshaler = "PolyType.IMarshaler`2";

	/// <summary>The reflection shape provider.</summary>
	internal const string ReflectionShapeProvider = "PolyType.ReflectionProvider.ReflectionTypeShapeProvider";

	/// <summary>The generic type shape interface.</summary>
	internal const string GenericTypeShape = "PolyType.ITypeShape`1";

	/// <summary>The shape provider interface.</summary>
	internal const string TypeShapeProvider = "PolyType.ITypeShapeProvider";

	/// <summary>The default reflection provider property.</summary>
	internal const string DefaultProvider = "Default";

	/// <summary>The custom converter attribute.</summary>
	internal const string ConverterAttribute = "Nerdbank.Json.JsonConverterAttribute";

	/// <summary>The generic converter base.</summary>
	internal const string GenericConverter = "Nerdbank.Json.JsonConverter`1";

	/// <summary>The non-generic converter base.</summary>
	internal const string Converter = "Nerdbank.Json.JsonConverter";

	/// <summary>The top-level serializer.</summary>
	internal const string Serializer = "Nerdbank.Json.JsonSerializer";

	/// <summary>The schema customization method.</summary>
	internal const string GetJsonSchema = "GetJsonSchema";

	/// <summary>The mutable synchronous JSON reader.</summary>
	internal const string Reader = "Nerdbank.Json.JsonReader";

	/// <summary>The mutable synchronous JSON writer.</summary>
	internal const string Writer = "Nerdbank.Json.JsonWriter";

	/// <summary>The marker for APIs with modern .NET alternatives.</summary>
	internal const string PreferDotNetAlternative = "Nerdbank.Json.PreferDotNetAlternativeApiAttribute";

	/// <summary>The runtime capability catalog.</summary>
	internal const string RuntimeFeature = "System.Runtime.CompilerServices.RuntimeFeature";

	/// <summary>The capability required by static abstract shape APIs.</summary>
	internal const string VirtualStaticsInInterfaces = "VirtualStaticsInInterfaces";

	/// <summary>The dictionary key types supported on every library target.</summary>
	internal static readonly string[] DictionaryKeyTypes =
	[
		"System.String", "System.Char", "System.Boolean", "System.Byte", "System.SByte",
		"System.Int16", "System.UInt16", "System.Int32", "System.UInt32", "System.Int64",
		"System.UInt64", "System.Single", "System.Double", "System.Decimal",
		"System.Numerics.BigInteger", "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", "System.Guid",
	];

	/// <summary>The additional dictionary key types supported by the modern library asset.</summary>
	internal static readonly string[] ModernDictionaryKeyTypes =
	[
		"System.Half", "System.Int128", "System.UInt128", "System.DateOnly", "System.TimeOnly", "System.Text.Rune",
	];
}
