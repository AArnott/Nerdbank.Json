// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json.AspNetCore;

/// <summary>
/// The media types handled by the Nerdbank.Json MVC formatters.
/// </summary>
internal static class MediaTypeNames
{
	/// <summary>The <c>application/json</c> media type.</summary>
	internal const string ApplicationJson = "application/json";

	/// <summary>The <c>text/json</c> media type.</summary>
	internal const string TextJson = "text/json";

	/// <summary>The structured-suffix wildcard that matches any <c>application/*+json</c> media type.</summary>
	internal const string ApplicationAnyJsonSuffix = "application/*+json";
}
