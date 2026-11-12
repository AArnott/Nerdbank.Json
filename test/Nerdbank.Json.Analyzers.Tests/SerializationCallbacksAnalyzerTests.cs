// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Json.Analyzers;
using VerifyCS = Nerdbank.Json.Analyzers.Tests.Verifier.AnalyzerVerifier<Nerdbank.Json.Analyzers.SerializationCallbacksAnalyzer>;

public class SerializationCallbacksAnalyzerTests
{
	[Test]
	public Task Class_MayImplementCallbacks()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;

			class Reading : IJsonSerializationCallbacks
			{
			}
			""");

	[Test]
	public Task RecordClass_MayImplementCallbacks()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;

			record class Reading : IJsonSerializationCallbacks;
			""");

	[Test]
	public Task Struct_ImplementingCallbacks_IsReported()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;

			struct Reading : {|NBJson004:IJsonSerializationCallbacks|}
			{
			}
			""");

	[Test]
	public Task RecordStruct_ImplementingCallbacks_IsReported()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;

			record struct Reading : {|NBJson004:IJsonSerializationCallbacks|};
			""");

	[Test]
	public Task Struct_ImplementingDerivedInterface_IsReported()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			using Nerdbank.Json;

			interface IReadingCallbacks : IJsonSerializationCallbacks
			{
			}

			struct Reading : {|NBJson004:IReadingCallbacks|}
			{
			}
			""");

	[Test]
	public Task Struct_WithoutCallbacks_IsNotReported()
		=> VerifyCS.VerifyAnalyzerAsync(
			/* lang=c#-test */ """
			interface IMarker
			{
			}

			struct Reading : IMarker
			{
			}
			""");
}
