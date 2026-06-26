// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Json.Analyzers;
using VerifyCS = Nerdbank.Json.Analyzers.Tests.Verifier.AnalyzerVerifier<Nerdbank.Json.Analyzers.RefParametersForRefStructsAnalyzer>;

public class RefParametersForRefStructsAnalyzerTests
{
	[Fact]
	public async Task MethodWithRefJsonWriterParameter()
	{
		string source = /* lang=c#-test */ """
			using Nerdbank.Json;

			class Test
			{
				public void Write(ref JsonWriter writer)
				{
				}
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task MethodWithJsonWriterParameterMissingRef()
	{
		string source = /* lang=c#-test */ """
			using Nerdbank.Json;

			class Test
			{
				public void Write({|NBJson050:JsonWriter|} writer)
				{
				}
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}
}
