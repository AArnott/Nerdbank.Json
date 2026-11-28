// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
#endif
using System.Runtime.CompilerServices;
using PolyType;
using PolyType.Abstractions;
#if NET
using PolyType.SourceGenModel;
#endif

/// <summary>
/// Verifies that serializer instances and their connection-specific resources are collectible
/// while the generated shape provider and the represented runtime types remain alive.
/// </summary>
[GenerateShapeFor<Message>]
[GenerateShapeFor<Ping>]
[GenerateShapeFor<Pong>]
[GenerateShapeFor<RecursiveMessage>]
public partial class SerializerLifetimeTests
{
	private static readonly ITypeShapeProvider Provider = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default;

	[Test]
	[NotInParallel]
	public async Task SerializerWithConnectionSpecificFactoryIsCollectible()
	{
		(WeakReference serializer, WeakReference connection) = UseSerializer(derivedTypeUnion: false);
		await AssertCollectedAsync(serializer, connection);
	}

	[Test]
	[NotInParallel]
	public async Task SerializerWithDerivedTypeUnionAndConnectionSpecificFactoryIsCollectible()
	{
		(WeakReference serializer, WeakReference connection) = UseSerializer(derivedTypeUnion: true);
		await AssertCollectedAsync(serializer, connection);
	}

#if NET
	[Test]
	[NotInParallel]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Collectible types are tested only when runtime code generation is supported.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This helper is used only in untrimmed managed runs.")]
	public async Task CollectibleProviderAndTypeAreReleasedWithSerializer()
	{
		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			Skip.Test("Collectible runtime types require runtime code generation.");
		}

		(WeakReference provider, WeakReference type) = UseCollectibleSerializer();
		await CollectAsync(provider, type);
		Assert.False(provider.IsAlive, "The collectible shape provider is still reachable.");
		Assert.False(type.IsAlive, "The collectible runtime type is still reachable.");
	}

	[Test]
	[NotInParallel]
	[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Collectible types are tested only when runtime code generation is supported.")]
	[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "This helper is used only in untrimmed managed runs.")]
	public async Task FailedConversionDoesNotRetainCollectibleProviderOrType()
	{
		if (!RuntimeFeature.IsDynamicCodeSupported)
		{
			Skip.Test("Collectible runtime types require runtime code generation.");
		}

		JsonSerializer serializer = new() { PropertyNamingPolicy = null };
		(WeakReference provider, WeakReference type) = UseCollectibleShape(serializer, failConversion: true);
		await CollectAsync(provider, type);
		Assert.False(provider.IsAlive, "An unsuccessful conversion retained its shape provider.");
		Assert.False(type.IsAlive, "An unsuccessful conversion retained its runtime type.");
		GC.KeepAlive(serializer);
	}
#endif

	[Test]
	[NotInParallel]
	public void RecursiveConvertersSurviveCollectionWhileSerializerIsAlive()
	{
		JsonSerializer serializer = new() { ConverterFactories = [new ConnectionConverterFactory(new Connection())] };
		ITypeShape<RecursiveMessage> shape = Provider.GetTypeShape<RecursiveMessage>()!;
		RecursiveMessage message = new(new Token(1), new(new Token(2), null));
		string json = serializer.Serialize(message, shape);
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
		Assert.Equal(message, serializer.Deserialize(json, shape));
		Assert.Equal(json, serializer.Serialize(message, shape));
	}

#if NET
	[RequiresDynamicCode("Creates a collectible runtime type and its generic shape.")]
	[RequiresUnreferencedCode("Creates a generic shape through reflection.")]
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static (WeakReference Provider, WeakReference Type) UseCollectibleSerializer()
	{
		JsonSerializer serializer = new() { ConverterFactories = [new NullConverterFactory()] };
		return UseCollectibleShape(serializer);
	}

	[RequiresDynamicCode("Creates a collectible runtime type and its generic shape.")]
	[RequiresUnreferencedCode("Creates a generic shape through reflection.")]
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static (WeakReference Provider, WeakReference Type) UseCollectibleShape(JsonSerializer serializer, bool failConversion = false)
	{
		AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("CollectibleSerializerType"), AssemblyBuilderAccess.RunAndCollect);
		Type type = assembly.DefineDynamicModule("Main").DefineType("CollectibleType", TypeAttributes.Public).CreateType()!;
		TemporaryProvider provider = new();
		provider.Shape = (ITypeShape)typeof(SerializerLifetimeTests).GetMethod(nameof(CreateCollectibleShape), BindingFlags.NonPublic | BindingFlags.Static)!
			.MakeGenericMethod(type).Invoke(null, [provider])!;
		if (failConversion)
		{
			Assert.Throws<JsonSerializationException>(() => serializer.SerializeObject(null, provider.Shape));
		}
		else
		{
			Assert.Equal("null", serializer.SerializeObject(null, provider.Shape));
		}

		// Exercise a different shape so that the last-converter optimization is not the sole root.
		Assert.Equal("\"hi\"", serializer.Serialize("hi", Provider.GetTypeShape<string>()!));
		return (new WeakReference(provider), new WeakReference(type));
	}

	private static ITypeShape CreateCollectibleShape<T>(ITypeShapeProvider provider)
		=> new SourceGenObjectTypeShape<T> { Provider = provider };
#endif

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static (WeakReference Serializer, WeakReference Connection) UseSerializer(bool derivedTypeUnion)
	{
		Connection connection = new();
		JsonSerializer serializer = new()
		{
			ConverterFactories = [new ConnectionConverterFactory(connection)],
			DerivedTypeUnions = derivedTypeUnion ? [new DerivedShapeMapping<Message> { { "ping", Provider.GetTypeShape<Ping>()! } }] : [],
		};

		string json = derivedTypeUnion
			? serializer.Serialize<Message>(new Ping("hi", new Token(1)), Provider.GetTypeShape<Message>()!)
			: serializer.Serialize(new Ping("hi", new Token(1)), Provider.GetTypeShape<Ping>()!);
		Pong? pong = serializer.Deserialize(json.Replace("hi", "there"), Provider.GetTypeShape<Pong>()!);
		Assert.Equal("there", pong?.Text);
		Assert.Equal(1, connection.TokensWritten);
		Assert.Equal(1, connection.TokensRead);

		return (new WeakReference(serializer), new WeakReference(connection));
	}

	private static async Task AssertCollectedAsync(WeakReference serializer, WeakReference connection)
	{
		await CollectAsync(serializer, connection);

		// Keep the generated shape provider and the represented runtime types alive throughout.
		GC.KeepAlive(Provider);
		GC.KeepAlive(typeof(Ping));
		GC.KeepAlive(typeof(Pong));
		GC.KeepAlive(typeof(Token));

		Assert.False(connection.IsAlive, "The connection-specific converter factory state is still reachable.");
		Assert.False(serializer.IsAlive, "The serializer is still reachable.");
	}

	private static async Task CollectAsync(WeakReference first, WeakReference second)
	{
		for (int attempt = 0; attempt < 10 && (first.IsAlive || second.IsAlive); attempt++)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			await Task.Delay(10);
		}
	}

	public abstract record Message;

	public record Ping(string Text, Token Token) : Message;

	public record Pong(string Text, Token Token);

	public record Token(int Value);

	public record RecursiveMessage(Token Token, RecursiveMessage? Next);

	private sealed class Connection
	{
		internal int TokensWritten { get; set; }

		internal int TokensRead { get; set; }
	}

	private sealed class ConnectionConverterFactory(Connection connection) : IJsonConverterFactory
	{
		public JsonConverter? CreateConverter(Type type, ITypeShape? shape, in ConverterContext context)
			=> type == typeof(Token) ? new TokenConverter(connection) : null;
	}

	private sealed class TokenConverter(Connection connection) : JsonConverter<Token>
	{
		public override void Write(ref JsonWriter writer, in Token? value, SerializationContext context)
		{
			connection.TokensWritten++;
			writer.Write(value!.Value);
		}

		public override Token? Read(ref JsonReader reader, SerializationContext context)
		{
			connection.TokensRead++;
			return new Token(reader.ReadInt32());
		}
	}

#if NET
	private sealed class TemporaryProvider : ITypeShapeProvider
	{
		internal ITypeShape Shape { get; set; } = null!;

		public ITypeShape? GetTypeShape(Type type) => type == this.Shape.Type ? this.Shape : null;
	}

	private sealed class NullConverterFactory : IJsonConverterFactory, ITypeShapeFunc
	{
		public JsonConverter? CreateConverter(Type type, ITypeShape? shape, in ConverterContext context)
			=> type.Assembly.IsDynamic ? (JsonConverter)shape!.Invoke(this)! : null;

		public object? Invoke<T>(ITypeShape<T> typeShape, object? state = null) => new NullConverter<T>();
	}

	private sealed class NullConverter<T> : JsonConverter<T>
	{
		public override void Write(ref JsonWriter writer, in T? value, SerializationContext context) => writer.WriteNull();

		public override T? Read(ref JsonReader reader, SerializationContext context)
		{
			reader.ReadNull();
			return default;
		}
	}
#endif
}
