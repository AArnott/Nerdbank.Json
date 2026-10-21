// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json.Analyzers;

/// <summary>
/// Metadata names shared by semantic analyzer rules.
/// </summary>
internal static class KnownNames
{
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
}
