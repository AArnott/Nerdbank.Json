// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // Benchmark models and source-generated context are kept adjacent to their workloads.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NerdbankJsonSerializer = Nerdbank.Json.JsonSerializer;
using STJ = System.Text.Json.JsonSerializer;

namespace Benchmarks;

/// <summary>
/// Compares serialization of nested, repeated, and text-heavy models beyond the flat ObjectBenchmarks fixtures.
/// The text scenarios compare equivalent JSON values, but the serializers' default escaping produces
/// different payload sizes; interpret their throughput ratios with that difference in mind.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public partial class SerializationWorkloadBenchmarks
{
	private readonly NerdbankJsonSerializer serializer = new();
	private readonly ArrayBufferWriter<byte> buffer = new();
	private readonly ITypeShape<Order> orderShape = PolyType.SourceGenerator.TypeShapeProvider_Benchmarks.Default.GetTypeShape<Order>() ?? throw new InvalidOperationException("No generated shape for Order.");
	private readonly ITypeShape<SensorBatch> batchShape = PolyType.SourceGenerator.TypeShapeProvider_Benchmarks.Default.GetTypeShape<SensorBatch>() ?? throw new InvalidOperationException("No generated shape for SensorBatch.");
	private readonly ITypeShape<LogBatch> logBatchShape = PolyType.SourceGenerator.TypeShapeProvider_Benchmarks.Default.GetTypeShape<LogBatch>() ?? throw new InvalidOperationException("No generated shape for LogBatch.");
	private readonly ITypeShape<TextDocument> documentShape = PolyType.SourceGenerator.TypeShapeProvider_Benchmarks.Default.GetTypeShape<TextDocument>() ?? throw new InvalidOperationException("No generated shape for TextDocument.");
	private readonly ITypeShape<ChainNode> chainShape = PolyType.SourceGenerator.TypeShapeProvider_Benchmarks.Default.GetTypeShape<ChainNode>() ?? throw new InvalidOperationException("No generated shape for ChainNode.");
	private readonly Order order = new()
	{
		OrderId = new Guid("c2fb865c-0682-494d-a98a-1866fc81f246"),
		PlacedAt = new DateTimeOffset(2025, 5, 6, 12, 30, 0, TimeSpan.Zero).AddTicks(1234567),
		Customer = new Customer
		{
			Name = "Ada Lovelace",
			IsMember = true,
			Address = new Address { Street = "12 Example St", City = "London", PostalCode = "SW1A 1AA" },
		},
		Items = Enumerable.Range(0, 12).Select(i => new LineItem
		{
			Sku = $"ITEM-{i:D4}",
			Quantity = (i % 4) + 1,
			UnitPrice = 12.50m + i,
			Discount = i % 3 == 0 ? null : 1.25m,
		}).ToArray(),
		Notes = null,
	};

	private readonly SensorBatch batch = new()
	{
		DeviceId = "sensor-042",
		Samples = Enumerable.Range(0, 128).Select(i => new Sample
		{
			Sequence = i,
			Timestamp = new DateTimeOffset(2025, 5, 6, 12, 30, 0, TimeSpan.Zero).AddTicks(1234567).AddSeconds(i),
			Temperature = 18.5 + ((i % 17) * 0.125),
			Humidity = 40.0 + ((i % 13) * 0.25),
			Valid = i % 11 != 0,
		}).ToArray(),
	};

	private readonly LogBatch logBatch = new()
	{
		Service = "payments",
		Entries = Enumerable.Range(0, 128).Select(i => new LogEntry
		{
			Sequence = i,
			OccurredAt = new DateTime(2025, 5, 6, 12, 30, 0, DateTimeKind.Utc).AddTicks((i * TimeSpan.TicksPerSecond) + 1234567),
			Duration = TimeSpan.FromTicks((i + 1) * 1234567),
			Message = i % 11 == 0 ? "Retry succeeded" : "Request completed",
		}).ToArray(),
	};

	private readonly TextDocument document = new()
	{
		Id = 42,
		Title = "Incident report",
		Body = string.Concat(Enumerable.Repeat("The \"primary\" service reported a warning.\nPath: C:\\logs\\service\\\tStatus: reviewed.\n", 32)),
		Tags = ["incident", "reviewed", "service"],
	};

	private readonly TextDocument unicodeDocument = new()
	{
		Id = 43,
		Title = "Regional incident report",
		Body = string.Concat(Enumerable.Repeat("Locations: caf\u00e9, M\u00fcnchen, \u6771\u4eac, \U0001F600. \"Reviewed\" and resolved.\n", 32)),
		Tags = ["regional", "reviewed", "service"],
	};

	private readonly ChainNode chain = CreateChain(12);

	/// <summary>
	/// Checks that both serializers produce valid, equivalent JSON for every fixture before measurement.
	/// </summary>
	[GlobalSetup]
	public void Setup()
	{
		this.Validate(this.order, this.orderShape, SerializationWorkloadJsonContext.Default.Order);
		this.Validate(this.batch, this.batchShape, SerializationWorkloadJsonContext.Default.SensorBatch);
		this.Validate(this.logBatch, this.logBatchShape, SerializationWorkloadJsonContext.Default.LogBatch);
		this.Validate(this.document, this.documentShape, SerializationWorkloadJsonContext.Default.TextDocument);
		this.Validate(this.unicodeDocument, this.documentShape, SerializationWorkloadJsonContext.Default.TextDocument);
		this.Validate(this.chain, this.chainShape, SerializationWorkloadJsonContext.Default.ChainNode);
	}

	/// <summary>Serializes a nested order with 12 line items, nullable values, and dates.</summary>
	/// <returns>The number of UTF-8 bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("Order")]
	public int Order_NerdbankJson() => this.SerializeWithNerdbank(this.order, this.orderShape);

	/// <summary>Serializes the same nested order with System.Text.Json.</summary>
	/// <returns>The number of UTF-8 bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Order")]
	public int Order_SystemTextJson() => this.SerializeWithSystemTextJson(this.order, SerializationWorkloadJsonContext.Default.Order);

	/// <summary>Serializes 128 repeated sensor readings with numeric and timestamp fields.</summary>
	/// <returns>The number of UTF-8 bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("SensorBatch")]
	public int SensorBatch_NerdbankJson() => this.SerializeWithNerdbank(this.batch, this.batchShape);

	/// <summary>Serializes the same sensor readings with System.Text.Json.</summary>
	/// <returns>The number of UTF-8 bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("SensorBatch")]
	public int SensorBatch_SystemTextJson() => this.SerializeWithSystemTextJson(this.batch, SerializationWorkloadJsonContext.Default.SensorBatch);

	/// <summary>Serializes 128 log entries with UTC timestamps, variable durations, and messages.</summary>
	/// <returns>The number of UTF-8 bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("LogBatch")]
	public int LogBatch_NerdbankJson() => this.SerializeWithNerdbank(this.logBatch, this.logBatchShape);

	/// <summary>Serializes the same log entries with System.Text.Json.</summary>
	/// <returns>The number of UTF-8 bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("LogBatch")]
	public int LogBatch_SystemTextJson() => this.SerializeWithSystemTextJson(this.logBatch, SerializationWorkloadJsonContext.Default.LogBatch);

	/// <summary>Serializes a multi-kilobyte ASCII document with quotes, slashes, and control characters.</summary>
	/// <returns>The number of UTF-8 bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("TextDocument")]
	public int TextDocument_NerdbankJson() => this.SerializeWithNerdbank(this.document, this.documentShape);

	/// <summary>Serializes the same document with System.Text.Json.</summary>
	/// <returns>The number of UTF-8 bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("TextDocument")]
	public int TextDocument_SystemTextJson() => this.SerializeWithSystemTextJson(this.document, SerializationWorkloadJsonContext.Default.TextDocument);

	/// <summary>Serializes a multi-kilobyte Unicode document, including non-ASCII text and surrogate pairs.</summary>
	/// <returns>The number of UTF-8 bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("UnicodeDocument")]
	public int UnicodeDocument_NerdbankJson() => this.SerializeWithNerdbank(this.unicodeDocument, this.documentShape);

	/// <summary>Serializes the same Unicode document with System.Text.Json's default encoder.</summary>
	/// <returns>The number of UTF-8 bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("UnicodeDocument")]
	public int UnicodeDocument_SystemTextJson() => this.SerializeWithSystemTextJson(this.unicodeDocument, SerializationWorkloadJsonContext.Default.TextDocument);

	/// <summary>Serializes a 12-level object graph, exceeding the writer's inline container-stack capacity.</summary>
	/// <returns>The number of UTF-8 bytes written.</returns>
	[Benchmark]
	[BenchmarkCategory("DeepChain")]
	public int DeepChain_NerdbankJson() => this.SerializeWithNerdbank(this.chain, this.chainShape);

	/// <summary>Serializes the same 12-level graph with System.Text.Json.</summary>
	/// <returns>The number of UTF-8 bytes written.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("DeepChain")]
	public int DeepChain_SystemTextJson() => this.SerializeWithSystemTextJson(this.chain, SerializationWorkloadJsonContext.Default.ChainNode);

	private static ChainNode CreateChain(int depth)
	{
		ChainNode root = new() { Value = 0 };
		ChainNode current = root;
		for (int i = 1; i < depth; i++)
		{
			current.Next = new ChainNode { Value = i };
			current = current.Next;
		}

		return root;
	}

	private int SerializeWithNerdbank<T>(T value, ITypeShape<T> shape)
	{
		this.serializer.Serialize(this.buffer, value, shape);
		int written = this.buffer.WrittenCount;
		this.buffer.Clear();
		return written;
	}

	private int SerializeWithSystemTextJson<T>(T value, JsonTypeInfo<T> typeInfo)
	{
		using Utf8JsonWriter writer = new(this.buffer);
		STJ.Serialize(writer, value, typeInfo);
		writer.Flush();
		int written = this.buffer.WrittenCount;
		this.buffer.Clear();
		return written;
	}

	private void Validate<T>(T value, ITypeShape<T> shape, JsonTypeInfo<T> typeInfo)
	{
		this.serializer.Serialize(this.buffer, value, shape);
		byte[] nerdbank = this.buffer.WrittenSpan.ToArray();
		this.buffer.Clear();
		using (Utf8JsonWriter writer = new(this.buffer))
		{
			STJ.Serialize(writer, value, typeInfo);
			writer.Flush();
		}

		byte[] systemTextJson = this.buffer.WrittenSpan.ToArray();
		this.buffer.Clear();

		using JsonDocument first = JsonDocument.Parse(nerdbank);
		using JsonDocument second = JsonDocument.Parse(systemTextJson);
		if (nerdbank.Length == 0 || systemTextJson.Length == 0 || !JsonElement.DeepEquals(first.RootElement, second.RootElement))
		{
			throw new InvalidOperationException($"Serializer outputs differ for {typeof(T).Name}: {System.Text.Encoding.UTF8.GetString(nerdbank)} versus {System.Text.Encoding.UTF8.GetString(systemTextJson)}.");
		}

		Console.WriteLine($"{typeof(T).Name}: Nerdbank.Json {nerdbank.Length} bytes, System.Text.Json {systemTextJson.Length} bytes.");
	}

	[GenerateShape]
	public sealed partial class Order
	{
		public Guid OrderId { get; set; }

		public DateTimeOffset PlacedAt { get; set; }

		public Customer Customer { get; set; } = new();

		public LineItem[] Items { get; set; } = [];

		public string? Notes { get; set; }
	}

	public sealed class Customer
	{
		public string Name { get; set; } = string.Empty;

		public bool IsMember { get; set; }

		public Address Address { get; set; } = new();
	}

	public sealed class Address
	{
		public string Street { get; set; } = string.Empty;

		public string City { get; set; } = string.Empty;

		public string PostalCode { get; set; } = string.Empty;
	}

	public sealed class LineItem
	{
		public string Sku { get; set; } = string.Empty;

		public int Quantity { get; set; }

		public decimal UnitPrice { get; set; }

		public decimal? Discount { get; set; }
	}

	[GenerateShape]
	public sealed partial class SensorBatch
	{
		public string DeviceId { get; set; } = string.Empty;

		public Sample[] Samples { get; set; } = [];
	}

	public sealed class Sample
	{
		public int Sequence { get; set; }

		public DateTimeOffset Timestamp { get; set; }

		public double Temperature { get; set; }

		public double Humidity { get; set; }

		public bool Valid { get; set; }
	}

	[GenerateShape]
	public sealed partial class LogBatch
	{
		public string Service { get; set; } = string.Empty;

		public LogEntry[] Entries { get; set; } = [];
	}

	public sealed class LogEntry
	{
		public int Sequence { get; set; }

		public DateTime OccurredAt { get; set; }

		public TimeSpan Duration { get; set; }

		public string Message { get; set; } = string.Empty;
	}

	[GenerateShape]
	public sealed partial class TextDocument
	{
		public int Id { get; set; }

		public string Title { get; set; } = string.Empty;

		public string Body { get; set; } = string.Empty;

		public string[] Tags { get; set; } = [];
	}

	[GenerateShape]
	public sealed partial class ChainNode
	{
		public int Value { get; set; }

		public ChainNode? Next { get; set; }
	}
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SerializationWorkloadBenchmarks.Order))]
[JsonSerializable(typeof(SerializationWorkloadBenchmarks.SensorBatch))]
[JsonSerializable(typeof(SerializationWorkloadBenchmarks.LogBatch))]
[JsonSerializable(typeof(SerializationWorkloadBenchmarks.TextDocument))]
[JsonSerializable(typeof(SerializationWorkloadBenchmarks.ChainNode))]
internal sealed partial class SerializationWorkloadJsonContext : JsonSerializerContext
{
}
