// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Nerdbank.Json.Analyzers;
using VerifyCS = Nerdbank.Json.Analyzers.Tests.Verifier.AnalyzerVerifier<Nerdbank.Json.Analyzers.JsonCollectionComparerAnalyzer>;

public class JsonCollectionComparerAnalyzerTests
{
	[Fact]
	public async Task Dictionary_WithCompatibleEqualityComparer_NoDiagnostic()
	{
		string source = /* lang=c#-test */ """
			#nullable enable

			using System;
			using System.Collections.Generic;
			using Nerdbank.Json;
			using PolyType;

			[GenerateShape]
			partial class Model
			{
				[JsonCollectionComparer(typeof(CaseInsensitive))]
				public Dictionary<string, int> Values { get; set; } = new();
			}

			sealed class CaseInsensitive : IEqualityComparer<string>
			{
				public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);
				public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task HashSet_WithCompatibleEqualityComparer_NoDiagnostic()
	{
		string source = /* lang=c#-test */ """
			#nullable enable

			using System;
			using System.Collections.Generic;
			using Nerdbank.Json;
			using PolyType;

			[GenerateShape]
			partial class Model
			{
				[JsonCollectionComparer(typeof(CaseInsensitive))]
				public HashSet<string> Values { get; set; } = new();
			}

			sealed class CaseInsensitive : IEqualityComparer<string>
			{
				public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);
				public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task SortedDictionary_WithCompatibleOrderComparer_NoDiagnostic()
	{
		string source = /* lang=c#-test */ """
			#nullable enable

			using System;
			using System.Collections.Generic;
			using Nerdbank.Json;
			using PolyType;

			[GenerateShape]
			partial class Model
			{
				[JsonCollectionComparer(typeof(Descending))]
				public SortedDictionary<string, int> Values { get; set; } = new();
			}

			sealed class Descending : IComparer<string>
			{
				public int Compare(string? x, string? y) => StringComparer.Ordinal.Compare(y, x);
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task ConstructorParameter_WithCompatibleComparer_NoDiagnostic()
	{
		string source = /* lang=c#-test */ """
			#nullable enable

			using System;
			using System.Collections.Generic;
			using Nerdbank.Json;
			using PolyType;

			[GenerateShape]
			partial class Model([JsonCollectionComparer(typeof(CaseInsensitive))] Dictionary<string, int> values)
			{
				public Dictionary<string, int> Values { get; } = values;
			}

			sealed class CaseInsensitive : IEqualityComparer<string>
			{
				public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);
				public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task Attribute_OnNonCollectionMember_ReportsDiagnostic()
	{
		string source = /* lang=c#-test */ """
			#nullable enable

			using System;
			using System.Collections.Generic;
			using Nerdbank.Json;
			using PolyType;

			[GenerateShape]
			partial class Model
			{
				[{|NBJson003:JsonCollectionComparer(typeof(CaseInsensitive))|}]
				public int Count { get; set; }
			}

			sealed class CaseInsensitive : IEqualityComparer<string>
			{
				public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);
				public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task Comparer_ForWrongElementType_ReportsDiagnostic()
	{
		string source = /* lang=c#-test */ """
			#nullable enable

			using System;
			using System.Collections.Generic;
			using Nerdbank.Json;
			using PolyType;

			[GenerateShape]
			partial class Model
			{
				[{|NBJson003:JsonCollectionComparer(typeof(IntComparer))|}]
				public Dictionary<string, int> Values { get; set; } = new();
			}

			sealed class IntComparer : IEqualityComparer<int>
			{
				public bool Equals(int x, int y) => x == y;
				public int GetHashCode(int obj) => obj;
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task Comparer_WithoutParameterlessConstructor_ReportsDiagnostic()
	{
		string source = /* lang=c#-test */ """
			#nullable enable

			using System;
			using System.Collections.Generic;
			using Nerdbank.Json;
			using PolyType;

			[GenerateShape]
			partial class Model
			{
				[{|NBJson003:JsonCollectionComparer(typeof(NeedsArgs))|}]
				public Dictionary<string, int> Values { get; set; } = new();
			}

			sealed class NeedsArgs : IEqualityComparer<string>
			{
				public NeedsArgs(bool ignoreCase) { }
				public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);
				public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
			}
			""";

		await VerifyCS.VerifyAnalyzerAsync(source);
	}
}
