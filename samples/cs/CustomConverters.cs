// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Nerdbank.Json;
using PolyType;

public partial class CustomConvertersSamples
{
	// <DeferredRawJson>
	public static string RoundTripRawEnvelope(string json)
	{
		JsonSerializer serializer = new();
		RawEnvelope envelope = serializer.Deserialize<RawEnvelope>(json)!;
		// Deserialization owns these bytes, so they can be retained after the source is released.
		return serializer.Serialize(envelope);
	}

	// </DeferredRawJson>

	// <CustomConverterRegisteredAtRuntime>
	private void CustomConverterRegisteredAtRuntime()
	{
		JsonSerializer serializer = new()
		{
			Converters = ConverterCollection.Create([new FooConverter()]),
		};
	}

	// </CustomConverterRegisteredAtRuntime>

	// <CustomOpenGenericConverterRegisteredAtRuntime>
	private void CustomOpenGenericConverterRegisteredAtRuntime()
	{
		JsonSerializer genericSerializer = new()
		{
			ConverterTypes = ConverterTypeCollection.Create([typeof(EnvelopeConverter<>)]),
		};
	}

	// </CustomOpenGenericConverterRegisteredAtRuntime>

	// <CustomConverterFactory>
	private void CustomConverterFactory()
	{
		JsonSerializer factorySerializer = new()
		{
			ConverterFactories = [new EnvelopeConverterFactory()],
		};
	}

	// </CustomConverterFactory>

	// <RawEnvelopeModel>
	[GenerateShape]
	public partial class RawEnvelope
	{
		public string? Method { get; set; }

		public RawJson Payload { get; set; }
	}

	// </RawEnvelopeModel>
}

// <YourOwnConverter>
// <CustomConverterByAttribute>
[GenerateShape]
[JsonConverter(typeof(FooConverter))]
public partial class Foo
{
	public string? Name { get; set; }
}

// </CustomConverterByAttribute>

public sealed class FooConverter : JsonConverter<Foo>
{
	public override void Write(ref JsonWriter writer, in Foo? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNull();
			return;
		}

		context.DepthStep();
		writer.WriteStartObject();
		writer.WritePropertyName("name");
		writer.Write(value.Name?.ToUpperInvariant());
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
		if (!reader.TryReadEndObject())
		{
			throw new JsonSerializationException("Expected the end of the object.");
		}

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
	public override void Write(ref JsonWriter writer, in Wrapper? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNull();
			return;
		}

		context.DepthStep();
		JsonConverter<Inner> innerConverter = context.GetConverter<Inner>();
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
		JsonConverter<Inner> innerConverter = context.GetConverter<Inner>();
		reader.ReadStartObject();
		reader.ReadRequiredString();
		reader.ReadNameSeparator();
		Inner? value = innerConverter.Read(ref reader, context);
		if (!reader.TryReadEndObject())
		{
			throw new JsonSerializationException("Expected the end of the object.");
		}

		return new Wrapper { Value = value };
	}
}

// </DelegateSubValues>

// <CustomConverterByAttributeOnMember>
[GenerateShape]
public partial class MemberAttributedContainer
{
	[JsonConverter(typeof(FooConverter))]
	public Foo? Value { get; set; }
}

[GenerateShape]
public partial class ParameterAttributedContainer([JsonConverter(typeof(FooConverter))] Foo? value)
{
	public Foo? Value { get; } = value;
}

// </CustomConverterByAttributeOnMember>

[AssociatedTypeShape(typeof(EnvelopeConverter<>))]
public partial class Envelope<T>
{
	public T? Value { get; set; }
}

public sealed class EnvelopeConverter<T> : JsonConverter<Envelope<T>>
	where T : IShapeable<T>
{
	public override void Write(ref JsonWriter writer, in Envelope<T>? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNull();
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
		if (!reader.TryReadEndObject())
		{
			throw new JsonSerializationException("Expected the end of the object.");
		}

		return new Envelope<T> { Value = value };
	}
}

public sealed class EnvelopeConverterFactory : IJsonConverterFactory
{
	public JsonConverter? CreateConverter(Type type, ITypeShape? shape, in ConverterContext context)
	{
		ArgumentNullException.ThrowIfNull(type);
		if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Envelope<>))
		{
			return null;
		}

		Type argument = type.GetGenericArguments()[0];
		Type converterType = typeof(FactoryEnvelopeConverter<>).MakeGenericType(argument);
		return (JsonConverter?)Activator.CreateInstance(converterType, context);
	}

	private sealed class FactoryEnvelopeConverter<T>(ConverterContext context) : JsonConverter<Envelope<T>>
		where T : IShapeable<T>
	{
		private readonly JsonConverter<T> valueConverter = context.GetConverter(T.GetTypeShape());

		public override void Write(ref JsonWriter writer, in Envelope<T>? value, SerializationContext serializationContext)
		{
			if (value is null)
			{
				writer.WriteNull();
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
			if (!reader.TryReadEndObject())
			{
				throw new JsonSerializationException("Expected the end of the object.");
			}

			return new Envelope<T> { Value = value };
		}
	}
}
