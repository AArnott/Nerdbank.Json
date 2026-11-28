// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PolyType;
using PolyType.Abstractions;

/// <summary>
/// Verifies that serializer instances and their connection-specific resources are collectible
/// while the generated shape provider and the represented runtime types remain alive.
/// </summary>
[GenerateShapeFor<Message>]
[GenerateShapeFor<Ping>]
[GenerateShapeFor<Pong>]
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
		for (int attempt = 0; attempt < 10 && (serializer.IsAlive || connection.IsAlive); attempt++)
		{
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
			await Task.Delay(10);
		}

		// Keep the generated shape provider and the represented runtime types alive throughout.
		GC.KeepAlive(Provider);
		GC.KeepAlive(typeof(Ping));
		GC.KeepAlive(typeof(Pong));
		GC.KeepAlive(typeof(Token));

		Assert.False(connection.IsAlive, "The connection-specific converter factory state is still reachable.");
		Assert.False(serializer.IsAlive, "The serializer is still reachable.");
	}

	public abstract record Message;

	public record Ping(string Text, Token Token) : Message;

	public record Pong(string Text, Token Token);

	public record Token(int Value);

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
}
