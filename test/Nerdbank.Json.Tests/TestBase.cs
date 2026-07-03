// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Nerdbank.MessagePack;
using PolyType.Abstractions;

public abstract class TestBase
{
	protected JsonSerializer Serializer { get; set; } = new();

	protected string? LastSerializedJson { get; set; }

	protected static void AssertStructuralEqual<T>(T expected, T actual, IEqualityComparer<T>? equalityComparer = null)
#if NET
		where T : IShapeable<T> => AssertStructuralEqual<T>(expected, actual, T.GetTypeShape(), equalityComparer);
#else
		=> AssertStructuralEqual(expected, actual, TypeShapeResolver.ResolveDynamicOrThrow<T>(), equalityComparer);
#endif

	protected static void AssertStructuralEqual<T>(T expected, T actual, ITypeShape<T> shape, IEqualityComparer<T>? equalityComparer = null)
	{
		equalityComparer ??= StructuralEqualityComparer.GetDefault<T>(shape);
		Assert.Equal(expected, actual, equalityComparer);
	}

	[MemberNotNull(nameof(LastSerializedJson))]
	protected T? Roundtrip<T>(T value)
#if NET
		where T : IShapeable<T> => this.Roundtrip(value, T.GetTypeShape());
#else
		=> this.Roundtrip(value, TypeShapeResolver.ResolveDynamicOrThrow<T>());
#endif

	[MemberNotNull(nameof(LastSerializedJson))]
	protected T? Roundtrip<T, TProvider>(T value)
#if NET
		where TProvider : IShapeable<T> => this.Roundtrip(value, TProvider.GetTypeShape());
#else
		=> this.Roundtrip(value, TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>());
#endif

	[MemberNotNull(nameof(LastSerializedJson))]
	protected T? Roundtrip<T>(T value, ITypeShape<T> shape)
	{
		string json = this.Serializer.Serialize(value, shape);
		this.LastSerializedJson = json;
		TestContext.Current?.OutputWriter.WriteLine(json);
		return this.Serializer.Deserialize(json, shape);
	}

	[MemberNotNull(nameof(LastSerializedJson))]
	protected T? AssertRoundtrip<T>(T value, IEqualityComparer<T>? equalityComparer = null)
#if NET
		where T : IShapeable<T> => this.AssertRoundtrip(value, T.GetTypeShape(), equalityComparer);
#else
		=> this.AssertRoundtrip(value, TypeShapeResolver.ResolveDynamicOrThrow<T>(), equalityComparer);
#endif

	[MemberNotNull(nameof(LastSerializedJson))]
	protected T? AssertRoundtrip<T, TProvider>(T value, IEqualityComparer<T>? equalityComparer = null)
#if NET
		where TProvider : IShapeable<T> => this.AssertRoundtrip(value, TProvider.GetTypeShape(), equalityComparer);
#else
		=> this.AssertRoundtrip(value, TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>(), equalityComparer);
#endif

	[MemberNotNull(nameof(LastSerializedJson))]
	protected T? AssertRoundtrip<T>(T value, ITypeShape<T> shape, IEqualityComparer<T>? equalityComparer = null)
	{
		T result = this.Roundtrip(value, shape)!;
		AssertStructuralEqual(value, result, shape, equalityComparer);
		return result;
	}

	[MemberNotNull(nameof(LastSerializedJson))]
	protected T? AssertRoundtrip<T>(T value, [StringSyntax(StringSyntaxAttribute.Json)] string expectedJson, IEqualityComparer<T>? equalityComparer = null)
#if NET
		where T : IShapeable<T> => this.AssertRoundtrip(value, T.GetTypeShape(), expectedJson, equalityComparer);
#else
		=> this.AssertRoundtrip(value, TypeShapeResolver.ResolveDynamicOrThrow<T>(), expectedJson, equalityComparer);
#endif

	[MemberNotNull(nameof(LastSerializedJson))]
	protected T? AssertRoundtrip<T, TProvider>(T value, [StringSyntax(StringSyntaxAttribute.Json)] string expectedJson, IEqualityComparer<T>? equalityComparer = null)
#if NET
		where TProvider : IShapeable<T> => this.AssertRoundtrip(value, TProvider.GetTypeShape(), expectedJson, equalityComparer);
#else
		=> this.AssertRoundtrip(value, TypeShapeResolver.ResolveDynamicOrThrow<T, TProvider>(), expectedJson, equalityComparer);
#endif

	[MemberNotNull(nameof(LastSerializedJson))]
	protected T? AssertRoundtrip<T>(T value, ITypeShape<T> shape, [StringSyntax(StringSyntaxAttribute.Json)] string expectedJson, IEqualityComparer<T>? equalityComparer = null)
	{
		string json = this.Serializer.Serialize(value, shape);
		this.LastSerializedJson = json;
		Console.WriteLine(json);
		Assert.Equal(expectedJson, json);

		T result = this.Serializer.Deserialize(json, shape)!;
		AssertStructuralEqual(value, result, shape, equalityComparer);
		return result;
	}

	protected T? AssertDeserializesTo<T>([StringSyntax(StringSyntaxAttribute.Json)] string json, T expected, ITypeShape<T> shape, IEqualityComparer<T>? equalityComparer = null)
	{
		T? actual = this.Serializer.Deserialize<T>(json, shape);
		AssertStructuralEqual(expected, actual!, shape, equalityComparer);
		return actual;
	}

	protected T? AssertDeserializesTo<T, TWitness>([StringSyntax(StringSyntaxAttribute.Json)] string json, T expected, ITypeShape<T> shape, IEqualityComparer<T>? equalityComparer = null)
	{
		T actual = this.Serializer.Deserialize(json, shape)!;
		AssertStructuralEqual(expected, actual, shape, equalityComparer);
		return actual;
	}
}
