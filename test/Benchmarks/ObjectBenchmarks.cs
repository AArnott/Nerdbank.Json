// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // Benchmark context is kept adjacent to the benchmark types it serves.

using System.Text.Json.Serialization;
using NerdbankJsonSerializer = Nerdbank.Json.JsonSerializer;
using STJ = System.Text.Json.JsonSerializer;

namespace Benchmarks;

/// <summary>
/// Benchmarks object serialization and deserialization scenarios that are sensitive to property-name handling.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public partial class ObjectBenchmarks
{
	private readonly NerdbankJsonSerializer serializer = new();
	private readonly ArrayBufferWriter<byte> buffer = new();
	private readonly ITypeShape<SmallModel> smallShape = PolyType.SourceGenerator.TypeShapeProvider_Benchmarks.Default.GetTypeShape<SmallModel>() ?? throw new InvalidOperationException("No generated type shape found for SmallModel.");
	private readonly ITypeShape<WideModel> wideShape = PolyType.SourceGenerator.TypeShapeProvider_Benchmarks.Default.GetTypeShape<WideModel>() ?? throw new InvalidOperationException("No generated type shape found for WideModel.");
	private readonly SmallModel small = new()
	{
		Id = 42,
		FirstName = "Ada",
		LastName = "Lovelace",
		City = "London",
		CountryCode = "GB",
	};

	private readonly WideModel wide = new()
	{
		PrimaryIdentifier = 42,
		DisplayName = "serialization benchmark",
		ShippingStreetAddress = "123 Benchmark Avenue",
		ShippingCityName = "Redmond",
		ShippingPostalCode = "98052",
		BillingStreetAddress = "1 Performance Way",
		BillingCityName = "Seattle",
		BillingPostalCode = "98101",
		PreferredLocaleName = "en-US",
		PreferredCurrencyCode = "USD",
		IsSubscriptionActive = true,
		ItemCountInCart = 9,
		MostRecentOrderTotal = 1234.56m,
		MostRecentOrderNumber = "SO-000042",
		MarketingCampaignSource = "newsletter",
		FavoriteProductCategory = "books",
	};

	private byte[] smallJson = [];
	private byte[] wideJson = [];

	/// <summary>
	/// Creates the canonical UTF-8 payloads used by deserialization benchmarks.
	/// </summary>
	[GlobalSetup]
	public void Setup()
	{
		this.smallJson = STJ.SerializeToUtf8Bytes(this.small, BenchmarkJsonContext.Default.SmallModel);
		this.wideJson = STJ.SerializeToUtf8Bytes(this.wide, BenchmarkJsonContext.Default.WideModel);
	}

	/// <summary>
	/// Measures Nerdbank.Json serialization throughput for a small object.
	/// </summary>
	/// <returns>The number of UTF-8 bytes produced.</returns>
	[Benchmark]
	[BenchmarkCategory("Serialize", "Small")]
	public int SerializeSmall_NerdbankJson()
		=> this.SerializeWithNerdbank(this.small, this.smallShape);

	/// <summary>
	/// Measures System.Text.Json serialization throughput for a small object.
	/// </summary>
	/// <returns>The number of UTF-8 bytes produced.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Serialize", "Small")]
	public int SerializeSmall_SystemTextJson()
		=> this.SerializeWithSystemTextJson(this.small, BenchmarkJsonContext.Default.SmallModel);

	/// <summary>
	/// Measures Nerdbank.Json deserialization throughput for a small object.
	/// </summary>
	/// <returns>The deserialized value.</returns>
	[Benchmark]
	[BenchmarkCategory("Deserialize", "Small")]
	public SmallModel? DeserializeSmall_NerdbankJson()
		=> this.serializer.Deserialize(this.smallJson, this.smallShape);

	/// <summary>
	/// Measures System.Text.Json deserialization throughput for a small object.
	/// </summary>
	/// <returns>The deserialized value.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Deserialize", "Small")]
	public SmallModel? DeserializeSmall_SystemTextJson()
		=> STJ.Deserialize(this.smallJson, BenchmarkJsonContext.Default.SmallModel);

	/// <summary>
	/// Measures Nerdbank.Json serialization throughput for a wide object with many property names.
	/// </summary>
	/// <returns>The number of UTF-8 bytes produced.</returns>
	[Benchmark]
	[BenchmarkCategory("Serialize", "Wide")]
	public int SerializeWide_NerdbankJson()
		=> this.SerializeWithNerdbank(this.wide, this.wideShape);

	/// <summary>
	/// Measures System.Text.Json serialization throughput for a wide object with many property names.
	/// </summary>
	/// <returns>The number of UTF-8 bytes produced.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Serialize", "Wide")]
	public int SerializeWide_SystemTextJson()
		=> this.SerializeWithSystemTextJson(this.wide, BenchmarkJsonContext.Default.WideModel);

	/// <summary>
	/// Measures Nerdbank.Json deserialization throughput for a wide object with many property names.
	/// </summary>
	/// <returns>The deserialized value.</returns>
	[Benchmark]
	[BenchmarkCategory("Deserialize", "Wide")]
	public WideModel? DeserializeWide_NerdbankJson()
		=> this.serializer.Deserialize(this.wideJson, this.wideShape);

	/// <summary>
	/// Measures System.Text.Json deserialization throughput for a wide object with many property names.
	/// </summary>
	/// <returns>The deserialized value.</returns>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Deserialize", "Wide")]
	public WideModel? DeserializeWide_SystemTextJson()
		=> STJ.Deserialize(this.wideJson, BenchmarkJsonContext.Default.WideModel);

	private int SerializeWithNerdbank<T>(T value, ITypeShape<T> shape)
	{
		this.serializer.Serialize(this.buffer, value, shape);
		int written = this.buffer.WrittenCount;
		this.buffer.Clear();
		return written;
	}

	private int SerializeWithSystemTextJson<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
	{
		using System.Text.Json.Utf8JsonWriter writer = new(this.buffer);
		STJ.Serialize(writer, value, typeInfo);
		writer.Flush();
		int written = this.buffer.WrittenCount;
		this.buffer.Clear();
		return written;
	}

	[GenerateShape]
	public sealed partial class SmallModel
	{
		public int Id { get; set; }

		public string FirstName { get; set; } = string.Empty;

		public string LastName { get; set; } = string.Empty;

		public string City { get; set; } = string.Empty;

		public string CountryCode { get; set; } = string.Empty;
	}

	[GenerateShape]
	public sealed partial class WideModel
	{
		public int PrimaryIdentifier { get; set; }

		public string DisplayName { get; set; } = string.Empty;

		public string ShippingStreetAddress { get; set; } = string.Empty;

		public string ShippingCityName { get; set; } = string.Empty;

		public string ShippingPostalCode { get; set; } = string.Empty;

		public string BillingStreetAddress { get; set; } = string.Empty;

		public string BillingCityName { get; set; } = string.Empty;

		public string BillingPostalCode { get; set; } = string.Empty;

		public string PreferredLocaleName { get; set; } = string.Empty;

		public string PreferredCurrencyCode { get; set; } = string.Empty;

		public bool IsSubscriptionActive { get; set; }

		public int ItemCountInCart { get; set; }

		public decimal MostRecentOrderTotal { get; set; }

		public string MostRecentOrderNumber { get; set; } = string.Empty;

		public string MarketingCampaignSource { get; set; } = string.Empty;

		public string FavoriteProductCategory { get; set; } = string.Empty;
	}
}

[JsonSerializable(typeof(ObjectBenchmarks.SmallModel))]
[JsonSerializable(typeof(ObjectBenchmarks.WideModel))]
internal sealed partial class BenchmarkJsonContext : JsonSerializerContext
{
}