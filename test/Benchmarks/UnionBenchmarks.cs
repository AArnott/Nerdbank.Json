// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using PolyType.SourceGenerator;

namespace Benchmarks;

/// <summary>Measures native and discriminator-based union serialization and deserialization.</summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public partial class UnionBenchmarks
{
	private readonly JsonSerializer serializer = new()
	{
		DerivedTypeUnions = [new DerivedShapeMapping<Pet> { { "dog", TypeShapeProvider_Benchmarks.Default.GetTypeShape<Dog>()! } }],
	};

	private readonly ArrayBufferWriter<byte> buffer = new();
	private readonly ITypeShape<ScalarOrWidget> scalarUnionShape = TypeShapeProvider_Benchmarks.Default.GetTypeShape<ScalarOrWidget>() ?? throw new InvalidOperationException("No generated shape for ScalarOrWidget.");
	private readonly ITypeShape<Pet> petShape = TypeShapeProvider_Benchmarks.Default.GetTypeShape<Pet>() ?? throw new InvalidOperationException("No generated shape for Pet.");
	private readonly ScalarOrWidget scalarValue = new(42);
	private readonly ScalarOrWidget objectValue = new(new Widget { Name = "benchmark", Count = 7 });
	private readonly Pet petValue = new Dog { Name = "benchmark", Count = 7 };
	private byte[] scalarJson = [];
	private byte[] objectJson = [];
	private byte[] petJson = [];

	/// <summary>Validates that the benchmark payloads deserialize before measurement.</summary>
	[GlobalSetup]
	public void Setup()
	{
		this.scalarJson = Encoding.UTF8.GetBytes(this.serializer.Serialize(this.scalarValue, this.scalarUnionShape));
		this.objectJson = Encoding.UTF8.GetBytes(this.serializer.Serialize(this.objectValue, this.scalarUnionShape));
		this.petJson = Encoding.UTF8.GetBytes(this.serializer.Serialize(this.petValue, this.petShape));

		if (this.serializer.Deserialize(this.scalarJson, this.scalarUnionShape).Value is not int ||
			this.serializer.Deserialize(this.objectJson, this.scalarUnionShape).Value is not Widget ||
			this.serializer.Deserialize(this.petJson, this.petShape) is not Dog)
		{
			throw new InvalidOperationException("A union benchmark payload did not deserialize to its expected case.");
		}
	}

	/// <summary>Serializes a native integer union case.</summary>
	[Benchmark]
	[BenchmarkCategory("CSharpNative")]
	public int SerializeNative()
		=> this.Serialize(this.scalarValue, this.scalarUnionShape);

	/// <summary>Deserializes a native integer union case.</summary>
	[Benchmark]
	[BenchmarkCategory("CSharpNative")]
	public ScalarOrWidget DeserializeNative()
		=> this.serializer.Deserialize(this.scalarJson, this.scalarUnionShape);

	/// <summary>Serializes an object union case with a discriminator.</summary>
	[Benchmark]
	[BenchmarkCategory("CSharpTaggedObject")]
	public int SerializeTaggedObject()
		=> this.Serialize(this.objectValue, this.scalarUnionShape);

	/// <summary>Deserializes an object union case with a discriminator.</summary>
	[Benchmark]
	[BenchmarkCategory("CSharpTaggedObject")]
	public ScalarOrWidget DeserializeTaggedObject()
		=> this.serializer.Deserialize(this.objectJson, this.scalarUnionShape);

	/// <summary>Serializes a runtime-derived-type union case with a discriminator.</summary>
	[Benchmark]
	[BenchmarkCategory("RuntimeTaggedObject")]
	public int SerializeRuntimeTaggedObject()
		=> this.Serialize(this.petValue, this.petShape);

	/// <summary>Deserializes a runtime-derived-type union case with a discriminator.</summary>
	[Benchmark]
	[BenchmarkCategory("RuntimeTaggedObject")]
	public Pet? DeserializeRuntimeTaggedObject()
		=> this.serializer.Deserialize(this.petJson, this.petShape);

	private int Serialize<T>(T value, ITypeShape<T> shape)
	{
		this.serializer.Serialize(this.buffer, value, shape);
		int length = this.buffer.WrittenCount;
		this.buffer.Clear();
		return length;
	}

	[Union, GenerateShape]
	public readonly partial struct ScalarOrWidget
	{
		private readonly int number;
		private readonly Widget? widget;
		private readonly bool isWidget;

		public ScalarOrWidget(int value) => this.number = value;

		public ScalarOrWidget(Widget value)
		{
			this.widget = value;
			this.isWidget = true;
		}

		public object? Value => this.isWidget ? this.widget : this.number;

		public bool TryGetValue(out int value)
		{
			value = this.number;
			return !this.isWidget;
		}

		public bool TryGetValue([NotNullWhen(true)] out Widget? value)
		{
			value = this.widget;
			return this.isWidget;
		}
	}

	[GenerateShape]
	public sealed partial class Widget
	{
		public string Name { get; set; } = string.Empty;

		public int Count { get; set; }
	}

	[GenerateShape]
	public abstract partial class Pet
	{
		public string Name { get; set; } = string.Empty;

		public int Count { get; set; }
	}

	[GenerateShape]
	public sealed partial class Dog : Pet
	{
	}
}
