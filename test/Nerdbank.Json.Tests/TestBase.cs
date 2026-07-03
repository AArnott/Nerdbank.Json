// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using Nerdbank.MessagePack;

public abstract class TestBase
{
	public JsonSerializer Serializer = new();

	public string? LastSerializedJson { get; set; }

	public T? Roundtrip<T>(T value)
#if NET
		where T : IShapeable<T>
#endif
	{
		string json = this.Serializer.Serialize(value);
		this.LastSerializedJson = json;
		Console.WriteLine(json);
		return this.Serializer.Deserialize<T>(json);
	}

	public T? Roundtrip<T, TWitness>(T value, JsonSerializer? serializer = null)
	{
		serializer ??= this.Serializer;
		ITypeShape<T> shape = GetTypeShape<T>();
		string json = serializer.Serialize(value, shape);
		this.LastSerializedJson = json;
		Console.WriteLine(json);
		return serializer.Deserialize(json, shape);
	}

	public T? AssertRoundtrip<T>(T value, IEqualityComparer<T>? equalityComparer = null)
#if NET
		where T : IShapeable<T>
#endif
	{
		T? result = this.Roundtrip(value);
		AssertStructuralEqual(value, result!, this.LastSerializedJson!, equalityComparer);
		return result;
	}

	public T? AssertRoundtrip<T>(T value, [StringSyntax(StringSyntaxAttribute.Json)] string expectedJson, JsonSerializer? serializer = null, IEqualityComparer<T>? equalityComparer = null)
#if NET
		where T : IShapeable<T>
#endif
	{
		serializer ??= this.Serializer;
		string json = serializer.Serialize(value);
		this.LastSerializedJson = json;
		Console.WriteLine(json);
		Assert.Equal(expectedJson, json);

		T? result = serializer.Deserialize<T>(json);
		AssertStructuralEqual(value, result!, json, equalityComparer);
		return result;
	}

	public T? AssertRoundtrip<T, TWitness>(T value, [StringSyntax(StringSyntaxAttribute.Json)] string expectedJson, JsonSerializer? serializer = null, IEqualityComparer<T>? equalityComparer = null)
	{
		serializer ??= this.Serializer;
		ITypeShape<T> shape = GetTypeShape<T>();
		string json = serializer.Serialize(value, shape);
		this.LastSerializedJson = json;
		Console.WriteLine(json);
		Assert.Equal(expectedJson, json);

		T? result = serializer.Deserialize(json, shape);
		AssertStructuralEqual(value, result!, json, equalityComparer);
		return result;
	}

	protected static T? AssertRoundtrip<T>([StringSyntax(StringSyntaxAttribute.Json)] string json, JsonSerializer serializer, T expected, IEqualityComparer<T>? equalityComparer = null)
	{
		T? actual = serializer.Deserialize(json, GetTypeShape<T>());
		AssertStructuralEqual(expected, actual!, json, equalityComparer);
		return actual;
	}

	protected static T? AssertDeserializesTo<T>([StringSyntax(StringSyntaxAttribute.Json)] string json, T expected, JsonSerializer? serializer = null, IEqualityComparer<T>? equalityComparer = null)
#if NET
		where T : IShapeable<T>
#endif
	{
		serializer ??= new JsonSerializer();
		T? actual = serializer.Deserialize<T>(json);
		AssertStructuralEqual(expected, actual!, json, equalityComparer);
		return actual;
	}

	protected static T? AssertDeserializesTo<T, TWitness>([StringSyntax(StringSyntaxAttribute.Json)] string json, T expected, JsonSerializer? serializer = null, IEqualityComparer<T>? equalityComparer = null)
	{
		serializer ??= new JsonSerializer();
		T? actual = serializer.Deserialize(json, GetTypeShape<T>());
		AssertStructuralEqual(expected, actual!, json, equalityComparer);
		return actual;
	}

	protected static void AssertStructuralEqual<T>(T expected, T actual, [StringSyntax(StringSyntaxAttribute.Json)] string json, IEqualityComparer<T>? equalityComparer = null)
		=> Assert.True((equalityComparer ?? GetStructuralEqualityComparer<T>()).Equals(expected, actual), $"Round-trip mismatch for serialized JSON: {json}");

	protected static void AssertEqual<T>(T expected, T actual, IEqualityComparer<T>? equalityComparer = null)
	{
		if (equalityComparer is not null)
		{
			Assert.True(equalityComparer.Equals(expected, actual));
			return;
		}

		if (expected is byte[] expectedBytes && actual is byte[] actualBytes)
		{
			Assert.Equal(expectedBytes, actualBytes);
			return;
		}

		if (expected is Memory<byte> expectedMemory && actual is Memory<byte> actualMemory)
		{
			Assert.True(expectedMemory.Span.SequenceEqual(actualMemory.Span));
			return;
		}

		if (expected is ReadOnlyMemory<byte> expectedReadOnlyMemory && actual is ReadOnlyMemory<byte> actualReadOnlyMemory)
		{
			Assert.True(expectedReadOnlyMemory.Span.SequenceEqual(actualReadOnlyMemory.Span));
			return;
		}

		Assert.Equal(expected, actual);
	}

	protected static IEqualityComparer<T> GetStructuralEqualityComparer<T>()
	{
		ITypeShape<T>? shape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<T>();
		return shape is null ? EqualityComparer<T>.Default : StructuralEqualityComparer.GetDefault(shape);
	}

	protected static ITypeShape<T> GetTypeShape<T>()
		=> PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<T>() ?? throw new InvalidOperationException($"No generated type shape found for {typeof(T)}.");
}
