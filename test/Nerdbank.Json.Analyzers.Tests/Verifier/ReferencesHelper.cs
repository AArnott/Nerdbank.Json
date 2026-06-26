// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using PolyType;

namespace Nerdbank.Json.Analyzers.Tests.Verifier;

internal static class ReferencesHelper
{
	internal static IEnumerable<MetadataReference> GetReferences()
	{
		yield return MetadataReference.CreateFromFile(typeof(JsonSerializer).Assembly.Location);
		yield return MetadataReference.CreateFromFile(typeof(GenerateShapeAttribute).Assembly.Location);
	}
}
