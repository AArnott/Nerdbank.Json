// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Json.Analyzers;
using VerifyCS = Nerdbank.Json.Analyzers.Tests.Verifier.AnalyzerVerifier<Nerdbank.Json.Analyzers.JsonShapeUsageAnalyzer>;

public class JsonShapeUsageAnalyzerTests
{
	[Fact]
	public async Task NoIssuesForSupportedShapes()
	{
		string source = /* lang=c#-test */ """
			#nullable enable

			using System;
			using System.Collections.Generic;
			using PolyType;

			[GenerateShape]
			internal partial class Model
			{
				public string Name { get; set; } = string.Empty;
				public Dictionary<int, string> Values { get; set; } = new();
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task ReportsDelegateMember()
	{
		string source = /* lang=c#-test */ """
			#nullable enable

			using System;
			using PolyType;

			[GenerateShape]
			internal partial class Model
			{
				public Action? Callback { get; set; }
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source, VerifyCS.Diagnostic("NBJson001").WithSpan(9, 17, 9, 25).WithArguments("Callback", "Action?"));
	}

	[Fact]
	public async Task ReportsUnsupportedDictionaryKeyMember()
	{
		string source = /* lang=c#-test */ """
			#nullable enable

			using System.Collections.Generic;
			using PolyType;

			[GenerateShape]
			internal partial class Model
			{
				public Dictionary<ComplexKey, int> Values { get; set; } = new();
			}

			[GenerateShape]
			internal partial class ComplexKey
			{
				public string Name { get; set; } = string.Empty;
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source, VerifyCS.Diagnostic("NBJson002").WithSpan(9, 37, 9, 43).WithArguments("Values", "ComplexKey"));
	}

	[Fact]
	public async Task ReportsUnsupportedDictionaryKeyWitnessTarget()
	{
		string source = /* lang=c#-test */ """
			#nullable enable

			using System.Collections.Generic;
			using PolyType;

			[GenerateShapeFor<Dictionary<ComplexKey, int>>]
			internal partial class Witness
			{
			}

			[GenerateShape]
			internal partial class ComplexKey
			{
				public string Name { get; set; } = string.Empty;
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source, VerifyCS.Diagnostic("NBJson002").WithSpan(6, 2, 6, 47).WithArguments("Dictionary<ComplexKey, int>", "ComplexKey"));
	}
}
