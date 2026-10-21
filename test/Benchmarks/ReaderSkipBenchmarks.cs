// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Nerdbank.Json;

namespace Benchmarks;

/// <summary>
/// Measures context-aware skipping of nested containers, long strings, and long numeric tokens.
/// </summary>
[MemoryDiagnoser]
public class ReaderSkipBenchmarks
{
	private byte[] json = null!;
	private CancellationTokenSource? cancellation;
	private SerializationContext context;

	/// <summary>Gets or sets the skipped token distribution.</summary>
	[Params("Containers", "String", "Number")]
	public string Scenario { get; set; } = "Containers";

	/// <summary>Gets or sets the number of repeated entries or token characters.</summary>
	[Params(16, 4096)]
	public int Size { get; set; }

	/// <summary>Gets or sets a value indicating whether the active context supports cancellation.</summary>
	[Params(false, true)]
	public bool Cancellable { get; set; }

	/// <summary>Prepares and validates the input outside the measured operation.</summary>
	[GlobalSetup]
	public void Setup()
	{
		string text = this.Scenario switch
		{
			"Containers" => "[" + string.Join(",", Enumerable.Repeat("""{"a":[1,true,null,{}]}""", this.Size)) + "]",
			"String" => "\"" + string.Concat(Enumerable.Repeat("text\\\"more", this.Size)) + "\"",
			"Number" => "0." + new string('1', this.Size),
			_ => throw new InvalidOperationException("Unknown skip scenario."),
		};
		this.json = Encoding.UTF8.GetBytes(text);
		this.cancellation = this.Cancellable ? new CancellationTokenSource() : null;
		this.context = new SerializationContext { CancellationToken = this.cancellation?.Token ?? default };
		if (this.Skip() != this.json.Length)
		{
			throw new InvalidOperationException("Skip did not consume exactly one complete value.");
		}
	}

	/// <summary>Releases the optional cancellation source.</summary>
	[GlobalCleanup]
	public void Cleanup() => this.cancellation?.Dispose();

	/// <summary>Skips one complete value without materializing strings or object graphs.</summary>
	/// <returns>The number of bytes consumed.</returns>
	[Benchmark]
	public long Skip()
	{
		JsonReader reader = new(this.json.AsSpan());
		reader.Skip(this.context);
		return reader.Consumed;
	}
}
