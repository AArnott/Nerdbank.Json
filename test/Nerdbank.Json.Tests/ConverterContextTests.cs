// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PolyType;
using PolyType.Abstractions;

[GenerateShapeFor<string>]
[GenerateShapeFor<int>]
public partial class ConverterContextTests
{
	[Test]
	public void LookupOverloads()
	{
		ITypeShape<string> shape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<string>()!;
		SerializationContext context = new ContextSerializer().CreateContext(shape.Provider);
		Assert.Same(shape.Provider, context.TypeShapeProvider);
		JsonConverter<string> converter = context.GetConverter(shape);
		Assert.Same(converter, context.GetConverter((ITypeShape)shape));
		Assert.Same(converter, context.GetConverter<string>(null));
		Assert.Same(converter, context.GetConverter<string>(shape.Provider));
		Assert.Same(converter, context.GetConverter(typeof(string)));
		Assert.Same(converter, context.GetConverter<string, ConverterContextTests>());
		Assert.Throws<NotSupportedException>(() => context.GetConverter(typeof(ConverterContextTests)));
		Assert.Throws<InvalidOperationException>(() => default(SerializationContext).GetConverter(shape));
		Assert.Throws<InvalidOperationException>(() => default(ConverterContext).GetConverter(shape));
	}

	[Test]
	public void ConcurrentLookupPreservesConverterIdentity()
	{
		ITypeShape<string> shape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<string>()!;
		for (int attempt = 0; attempt < 100; attempt++)
		{
			ContextSerializer serializer = new() { PropertyNamingPolicy = null };
			JsonConverter<string>[] converters = new JsonConverter<string>[16];
			Parallel.For(0, converters.Length, i =>
			{
				SerializationContext context = serializer.CreateContext(shape.Provider);
				converters[i] = context.GetConverter(shape);
				Assert.Same(converters[i], context.GetConverter((ITypeShape)shape));
				Assert.Same(converters[i], context.GetConverter<string>(shape.Provider));
			});

			foreach (JsonConverter<string> converter in converters)
			{
				Assert.Same(converters[0], converter);
			}
		}
	}

	[Test]
	public void FactoryInvokeAndLookup()
	{
		LookupFactory factory = new();
		JsonSerializer serializer = new() { ConverterFactories = [factory] };
		Assert.Equal("\"abc\"", serializer.Serialize<string, ConverterContextTests>("abc"));
		Assert.True(factory.Invoked);
	}

	private record ContextSerializer : JsonSerializer
	{
		internal SerializationContext CreateContext(ITypeShapeProvider provider) => this.CreateSerializationContext(provider);
	}

	private sealed class LookupFactory : IJsonConverterFactory, ITypeShapeFunc
	{
		internal bool Invoked { get; private set; }

		public JsonConverter? CreateConverter(Type type, ITypeShape? shape, in ConverterContext context)
		{
			if (type != typeof(string))
			{
				return null;
			}

			Assert.Same(shape!.Provider, context.TypeShapeProvider);
			JsonConverter<int> nested = context.GetConverter<int>(null);
			Assert.Same(nested, context.GetConverter(typeof(int)));
			Assert.Same(nested, context.GetConverter(context.TypeShapeProvider.GetTypeShape<int>()!));
			Assert.Same(nested, context.GetConverter((ITypeShape)context.TypeShapeProvider.GetTypeShape<int>()!));
			Assert.Same(nested, context.GetConverter<int, ConverterContextTests>());
			return this.Invoke(shape, context);
		}

		public object? Invoke<T>(ITypeShape<T> typeShape, object? state = null)
		{
			Assert.IsType<ConverterContext>(state);
			this.Invoked = true;
			return new PassThroughStringConverter();
		}
	}

	private sealed class PassThroughStringConverter : JsonConverter<string>
	{
		public override void Write(ref JsonWriter writer, in string? value, SerializationContext context) => writer.WriteStringValue(value);

		public override string? Read(ref JsonReader reader, SerializationContext context) => reader.ReadString();
	}
}
