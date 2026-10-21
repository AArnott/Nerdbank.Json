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
	[NotInParallel]
	[Arguments(false)]
	[Arguments(true)]
	public void AlternatingProvidersPreserveConverterIdentity(bool equalShapes)
	{
		StringShapeProvider firstProvider = new(equalShapes);
		StringShapeProvider secondProvider = new(equalShapes);
		ContextSerializer serializer = new() { ConverterFactories = [new LookupFactory()] };
		SerializationContext context = serializer.CreateContext(firstProvider);
		JsonConverter<string> first = context.GetConverter(firstProvider.Shape);
		JsonConverter<string> second = context.GetConverter(secondProvider.Shape);
		Assert.NotSame(first, second);
		Assert.Same(firstProvider, Assert.IsType<PassThroughStringConverter>(first).Provider);
		Assert.Same(secondProvider, Assert.IsType<PassThroughStringConverter>(second).Provider);
		Parallel.For(0, 1000, i =>
		{
			Assert.Same(first, context.GetConverter(firstProvider.Shape));
			Assert.Same(second, context.GetConverter((ITypeShape)secondProvider.Shape));
			Assert.Same(first, context.GetConverter<string>(firstProvider));
			Assert.Same(second, context.GetConverter<string>(secondProvider));
		});

#if NET
		for (int i = 0; i < 1000; i++)
		{
			context.GetConverter(firstProvider.Shape);
			context.GetConverter(secondProvider.Shape);
		}

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (int i = 0; i < 1000; i++)
		{
			context.GetConverter(firstProvider.Shape);
			context.GetConverter(secondProvider.Shape);
		}

		Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
#endif
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

	private sealed class StringShapeProvider : ITypeShapeProvider
	{
		internal StringShapeProvider(bool equalShapes = false)
		{
			this.Shape = equalShapes
				? new EqualStringShape { Provider = this }
				: new PolyType.SourceGenModel.SourceGenObjectTypeShape<string> { Provider = this };
		}

		internal ITypeShape<string> Shape { get; }

		public ITypeShape? GetTypeShape(Type type)
			=> type == typeof(string) ? this.Shape : PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape(type);

		private sealed class EqualStringShape : PolyType.SourceGenModel.SourceGenTypeShape<string>
		{
			public override TypeShapeKind Kind => TypeShapeKind.Object;

			public override object? Accept(TypeShapeVisitor visitor, object? state = null)
				=> throw new NotSupportedException("The registered factory supplies the converter.");

			public override bool Equals(object? obj) => obj is EqualStringShape;

			public override int GetHashCode() => 1;
		}
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
			return new PassThroughStringConverter(typeShape.Provider);
		}
	}

	private sealed class PassThroughStringConverter(ITypeShapeProvider provider) : JsonConverter<string>
	{
		internal ITypeShapeProvider Provider { get; } = provider;

		public override void Write(ref JsonWriter writer, in string? value, SerializationContext context) => writer.Write(value);

		public override string? Read(ref JsonReader reader, SerializationContext context) => reader.ReadString();
	}
}
