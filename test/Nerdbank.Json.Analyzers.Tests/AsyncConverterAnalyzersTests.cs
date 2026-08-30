// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using VerifyCS = Nerdbank.Json.Analyzers.Tests.Verifier.AnalyzerVerifier<Nerdbank.Json.Analyzers.AsyncConverterAnalyzers>;

public class AsyncConverterAnalyzersTests
{
	[Fact]
	public async Task PreferAsyncSerialization_NotOverridden_Reports()
	{
		string source = /* lang=c#-test */ """
			using System.Threading.Tasks;
			using Nerdbank.Json;

			class {|NBJson037:MyConverter|} : JsonConverter<string>
			{
				public override string? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
				public override async ValueTask WriteAsync(JsonAsyncWriter writer, string? value, SerializationContext context) => await Task.Yield();
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task PreferAsyncSerialization_Overridden_NoDiagnostic()
	{
		string source = /* lang=c#-test */ """
			using System.Threading.Tasks;
			using Nerdbank.Json;

			class MyConverter : JsonConverter<string>
			{
				public override bool PreferAsyncSerialization => true;
				public override string? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
				public override async ValueTask WriteAsync(JsonAsyncWriter writer, string? value, SerializationContext context) => await Task.Yield();
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task SyncOnlyConverter_NoDiagnostic()
	{
		string source = /* lang=c#-test */ """
			using Nerdbank.Json;

			class MyConverter : JsonConverter<string>
			{
				public override string? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task AbstractAsyncConverter_NoPreferDiagnostic()
	{
		string source = /* lang=c#-test */ """
			using System.Threading.Tasks;
			using Nerdbank.Json;

			abstract class MyConverter : JsonConverter<string>
			{
				public override string? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
				public override async ValueTask WriteAsync(JsonAsyncWriter writer, string? value, SerializationContext context) => await Task.Yield();
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task NonConverter_Ignored()
	{
		string source = /* lang=c#-test */ """
			using System.Threading.Tasks;
			using Nerdbank.Json;

			class NotAConverter
			{
				public async ValueTask WriteAsync(JsonAsyncWriter writer) => await Task.Yield();
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task Writer_NotReturnedBeforeExit_Reports()
	{
		string source = /* lang=c#-test */ """
			using System.Threading.Tasks;
			using Nerdbank.Json;

			class MyConverter : JsonConverter<string>
			{
				public override bool PreferAsyncSerialization => true;
				public override string? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
				public override ValueTask WriteAsync(JsonAsyncWriter writer, string? value, SerializationContext context)
				{
					JsonWriter w = writer.CreateWriter();
					w.WriteNullValue();
					{|NBJson033:return|} default;
				}
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task Writer_ReturnedBeforeExit_NoDiagnostic()
	{
		string source = /* lang=c#-test */ """
			using System.Threading.Tasks;
			using Nerdbank.Json;

			class MyConverter : JsonConverter<string>
			{
				public override bool PreferAsyncSerialization => true;
				public override string? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
				public override ValueTask WriteAsync(JsonAsyncWriter writer, string? value, SerializationContext context)
				{
					JsonWriter w = writer.CreateWriter();
					w.WriteNullValue();
					writer.ReturnWriter(ref w);
					return default;
				}
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task Writer_AwaitWhileHeld_Reports()
	{
		string source = /* lang=c#-test */ """
			using System.Threading.Tasks;
			using Nerdbank.Json;

			class MyConverter : JsonConverter<string>
			{
				public override bool PreferAsyncSerialization => true;
				public override string? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
				public override async ValueTask WriteAsync(JsonAsyncWriter writer, string? value, SerializationContext context)
				{
					JsonWriter w = writer.CreateWriter();
					w.WriteNullValue();
					{|NBJson033:await|} Task.Yield();
				}
			}
			""";

		// The rental is held across the await (reported at the await) and is still held when the method exits.
		await VerifyCS.VerifyAnalyzerAsync(
			source,
			VerifyCS.Diagnostic("NBJson033").WithSpan(14, 2, 14, 3));
	}

	[Fact]
	public async Task Writer_ReusedAfterReturn_Reports()
	{
		string source = /* lang=c#-test */ """
			using System.Threading.Tasks;
			using Nerdbank.Json;

			class MyConverter : JsonConverter<string>
			{
				public override bool PreferAsyncSerialization => true;
				public override string? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
				public override ValueTask WriteAsync(JsonAsyncWriter writer, string? value, SerializationContext context)
				{
					JsonWriter w = writer.CreateWriter();
					writer.ReturnWriter(ref w);
					{|NBJson034:w|}.WriteNullValue();
					return default;
				}
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task Reader_NotReturnedBeforeExit_Reports()
	{
		string source = /* lang=c#-test */ """
			using System.Threading.Tasks;
			using Nerdbank.Json;

			class MyConverter : JsonConverter<string>
			{
				public override bool PreferAsyncSerialization => true;
				public override string? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
				public override ValueTask<string?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
				{
					JsonReader r = reader.CreateBufferedReader();
					r.ReadNumberToken();
					{|NBJson035:return|} default;
				}
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task Reader_ReturnedBeforeExit_NoDiagnostic()
	{
		string source = /* lang=c#-test */ """
			using System.Threading.Tasks;
			using Nerdbank.Json;

			class MyConverter : JsonConverter<string>
			{
				public override bool PreferAsyncSerialization => true;
				public override string? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
				public override async ValueTask<string?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
				{
					await reader.BufferNextValueAsync(context);
					JsonReader r = reader.CreateBufferedReader();
					string token = r.ReadNumberToken();
					reader.ReturnReader(ref r);
					return token;
				}
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task Reader_ReusedAfterReturn_Reports()
	{
		string source = /* lang=c#-test */ """
			using System.Threading.Tasks;
			using Nerdbank.Json;

			class MyConverter : JsonConverter<string>
			{
				public override bool PreferAsyncSerialization => true;
				public override string? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
				public override ValueTask<string?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
				{
					JsonReader r = reader.CreateBufferedReader();
					reader.ReturnReader(ref r);
					return new ValueTask<string?>({|NBJson036:r|}.ReadNumberToken());
				}
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task Reader_OtherMethodWhileHeld_Reports()
	{
		string source = /* lang=c#-test */ """
			using System.Threading.Tasks;
			using Nerdbank.Json;

			class MyConverter : JsonConverter<string>
			{
				public override bool PreferAsyncSerialization => true;
				public override string? Read(ref JsonReader reader, SerializationContext context) => null;
				public override void Write(ref JsonWriter writer, string? value, SerializationContext context) { }
				public override ValueTask<string?> ReadAsync(JsonAsyncReader reader, SerializationContext context)
				{
					JsonReader r = reader.CreateBufferedReader();
					{|NBJson035:reader.BufferNextValueAsync(context)|};
					reader.ReturnReader(ref r);
					return default;
				}
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}
}
