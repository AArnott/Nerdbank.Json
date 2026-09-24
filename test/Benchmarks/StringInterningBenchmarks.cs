// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

namespace Benchmarks;

/// <summary>
/// Measures string decoding with and without operation-scoped interning.
/// </summary>
[MemoryDiagnoser]
[GenerateShapeFor<string[]>]
public partial class StringInterningBenchmarks
{
	private JsonSerializer serializer = new();
	private byte[] json = [];

	/// <summary>
	/// Gets or sets the string representation to decode.
	/// </summary>
	[Params("Plain", "Escaped", "Unicode", "LargeEscaped")]
	public string Representation { get; set; } = "Plain";

	/// <summary>
	/// Gets or sets a value indicating whether strings should be interned.
	/// </summary>
	[Params(false, true)]
	public bool InternStrings { get; set; }

	/// <summary>
	/// Prepares and validates an array containing 64 equal strings.
	/// </summary>
	[GlobalSetup]
	public void Setup()
	{
		this.serializer = new() { InternStrings = this.InternStrings };
		(string token, string expected) = this.Representation switch
		{
			"Plain" => ("\"hello world\"", "hello world"),
			"Escaped" => ("\"hello\\nworld\\t\\\"quoted\\\"\"", "hello\nworld\t\"quoted\""),
			"Unicode" => ("\"h\\u0065llo \\uD83D\\uDE00\"", "hello \U0001F600"),
			"LargeEscaped" => ("\"" + new string('a', 8192) + "\\n\"", new string('a', 8192) + "\n"),
			_ => throw new InvalidOperationException("Unknown representation."),
		};
		this.json = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat(token, 64)) + "]");
		string[] result = this.Deserialize()!;
		if (result.Length != 64 || result.Any(value => value != expected)
			|| (this.InternStrings && !ReferenceEquals(result[0], result[63])))
		{
			throw new InvalidOperationException("Incorrect benchmark result.");
		}
	}

	/// <summary>
	/// Deserializes the UTF-8 payload.
	/// </summary>
	/// <returns>The decoded strings.</returns>
	[Benchmark]
	public string[]? Deserialize() => this.serializer.Deserialize<string[], StringInterningBenchmarks>(this.json);
}
