using System;
using Nerdbank.Json;
using PolyType;

partial class CustomConvertersSamples
{
	// <CustomConverterRegisteredAtRuntime>
	void CustomConverterRegisteredAtRuntime()
	{
		JsonSerializer serializer = new()
		{
			Converters = new ConverterCollection([new FooConverter()]),
		};
	}
	// </CustomConverterRegisteredAtRuntime>

	// <CustomOpenGenericConverterRegisteredAtRuntime>
	void CustomOpenGenericConverterRegisteredAtRuntime()
	{
		JsonSerializer genericSerializer = new()
		{
			ConverterTypes = new JsonConverterTypeCollection([typeof(EnvelopeConverter<>)]),
		};
	}
	// </CustomOpenGenericConverterRegisteredAtRuntime>

	// <CustomConverterFactory>
	void CustomConverterFactory()
	{
		JsonSerializer factorySerializer = new()
		{
			ConverterFactories = [new EnvelopeConverterFactory()],
		};
	}
	// </CustomConverterFactory>
}

// <YourOwnConverter>
[GenerateShape]
public partial class Foo
{
	public string? Name { get; set; }
}

public sealed class FooConverter : JsonConverter<Foo>
{
	public override void Write(ref JsonWriter writer, Foo? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		context.DepthStep();
		writer.WriteStartObject();
		writer.WritePropertyName("name");
		writer.WriteStringValue(value.Name?.ToUpperInvariant());
		writer.WriteEndObject();
	}

	public override Foo? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return null;
		}

		context.DepthStep();
		reader.ReadStartObject();
		string propertyName = reader.ReadRequiredString();
		reader.ReadNameSeparator();
		string? name = reader.ReadString();
		reader.ReadEndObject();

		if (!string.Equals(propertyName, "name", StringComparison.Ordinal))
		{
			throw new JsonSerializationException($"Unexpected property '{propertyName}'.")
			{
				Code = JsonSerializationException.ErrorCode.UnexpectedToken,
			};
		}

		return new Foo { Name = name?.ToLowerInvariant() };
	}
}
// </YourOwnConverter>

// <DelegateSubValues>
[GenerateShape]
public partial class Wrapper
{
	public Inner? Value { get; set; }
}

[GenerateShape]
public partial class Inner
{
	public string? Name { get; set; }
}

public sealed class WrapperConverter : JsonConverter<Wrapper>
{
	public override void Write(ref JsonWriter writer, Wrapper? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		context.DepthStep();
		JsonConverter<Inner> innerConverter = context.GetConverter(Inner.GetTypeShape());
		writer.WriteStartObject();
		writer.WritePropertyName("value");
		innerConverter.Write(ref writer, value.Value, context);
		writer.WriteEndObject();
	}

	public override Wrapper? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return null;
		}

		context.DepthStep();
		JsonConverter<Inner> innerConverter = context.GetConverter(Inner.GetTypeShape());
		reader.ReadStartObject();
		reader.ReadRequiredString();
		reader.ReadNameSeparator();
		Inner? value = innerConverter.Read(ref reader, context);
		reader.ReadEndObject();
		return new Wrapper { Value = value };
	}
}
// </DelegateSubValues>

// <CustomConverterByAttribute>
[GenerateShape]
[JsonConverter(typeof(FooConverter))]
public partial class FooByAttribute
{
	public string? Name { get; set; }
}
// </CustomConverterByAttribute>

// <CustomConverterByAttributeOnMember>
[GenerateShape]
public partial class MemberAttributedContainer
{
	[JsonConverter(typeof(FooConverter))]
	public Foo? Value { get; set; }
}

[GenerateShape]
public partial class ParameterAttributedContainer([property: JsonConverter(typeof(FooConverter))] Foo? value)
{
	public Foo? Value { get; } = value;
}
// </CustomConverterByAttributeOnMember>

[GenerateShape]
public partial class Envelope<T>
{
	public T? Value { get; set; }
}

public sealed class EnvelopeConverter<T> : JsonConverter<Envelope<T>>
	where T : IShapeable<T>
{
	public override void Write(ref JsonWriter writer, Envelope<T>? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNullValue();
			return;
		}

		context.DepthStep();
		writer.WriteStartObject();
		writer.WritePropertyName("value");
		context.GetConverter(T.GetTypeShape()).Write(ref writer, value.Value, context);
		writer.WriteEndObject();
	}

	public override Envelope<T>? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return null;
		}

		context.DepthStep();
		reader.ReadStartObject();
		reader.ReadRequiredString();
		reader.ReadNameSeparator();
		T? value = context.GetConverter(T.GetTypeShape()).Read(ref reader, context);
		reader.ReadEndObject();
		return new Envelope<T> { Value = value };
	}
}

public sealed class EnvelopeConverterFactory : IJsonConverterFactory
{
	public JsonConverter? CreateConverter(Type type, ITypeShape? shape, in JsonConverterFactoryContext context)
	{
		if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Envelope<>))
		{
			return null;
		}

		Type argument = type.GetGenericArguments()[0];
		Type converterType = typeof(FactoryEnvelopeConverter<>).MakeGenericType(argument);
		return (JsonConverter?)Activator.CreateInstance(converterType, context);
	}

	private sealed class FactoryEnvelopeConverter<T>(JsonConverterFactoryContext context) : JsonConverter<Envelope<T>>
		where T : IShapeable<T>
	{
		private readonly JsonConverter<T> valueConverter = context.GetConverter(T.GetTypeShape());

		public override void Write(ref JsonWriter writer, Envelope<T>? value, SerializationContext serializationContext)
		{
			if (value is null)
			{
				writer.WriteNullValue();
				return;
			}

			serializationContext.DepthStep();
			writer.WriteStartObject();
			writer.WritePropertyName("value");
			this.valueConverter.Write(ref writer, value.Value, serializationContext);
			writer.WriteEndObject();
		}

		public override Envelope<T>? Read(ref JsonReader reader, SerializationContext serializationContext)
		{
			if (reader.TryReadNull())
			{
				return null;
			}

			serializationContext.DepthStep();
			reader.ReadStartObject();
			reader.ReadRequiredString();
			reader.ReadNameSeparator();
			T? value = this.valueConverter.Read(ref reader, serializationContext);
			reader.ReadEndObject();
			return new Envelope<T> { Value = value };
		}
	}
}
