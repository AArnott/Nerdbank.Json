// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CS1591, SA1600, SA1601 // Documentation samples do not require API documentation.
#pragma warning disable SA1512, SA1513, SA1515 // Documentation region markers.
#pragma warning disable SA1649 // Documentation samples share a file.

using Nerdbank.Json;
using PolyType;

namespace CSharpUnions;

// <CSharpUnion>
[GenerateShape]
public partial union Result(int, string?);
// </CSharpUnion>

public static class Examples
{
	// <CSharpUnionUsage>
	public static Result Roundtrip()
	{
		JsonSerializer serializer = new();
		string json = serializer.Serialize(new Result(42)); // ["Int32",42]
		return serializer.Deserialize<Result>(json);
	}
	// </CSharpUnionUsage>

	// <CSharpUnionAliases>
	public static JsonSerializer CreateSerializer()
	{
		UnionCaseMapping mapping = new(typeof(Result))
		{
			{ "number", typeof(int) },
			{ "text", typeof(string) },
		};
		return new JsonSerializer { UnionTypeAliasProvider = new UnionCaseMappingCollection([mapping]) };
	}
	// </CSharpUnionAliases>

	// <CSharpUnionAliasDelegate>
	public static JsonSerializer CreateSerializerWithDelegate()
	{
		return new JsonSerializer
		{
			UnionTypeAliasProvider = new DelegateUnionTypeAliasProvider((unionType, caseType) =>
				unionType == typeof(Result) && caseType == typeof(int) ? new DerivedTypeIdentifier(1) : (DerivedTypeIdentifier?)null),
		};
	}
	// </CSharpUnionAliasDelegate>
}
