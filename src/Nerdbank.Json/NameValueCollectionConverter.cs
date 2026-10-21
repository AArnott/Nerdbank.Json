// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type

using System.Collections.Specialized;

namespace Nerdbank.Json;

/// <summary>Converts name/value collections using the configured string converter.</summary>
[GenerateShapeFor<string>]
internal sealed partial class NameValueCollectionConverter : JsonConverter<NameValueCollection>
{
	private readonly JsonConverter<string> stringConverter;

	/// <summary>Initializes a new instance of the <see cref="NameValueCollectionConverter"/> class.</summary>
	/// <param name="context">The converter creation context.</param>
	internal NameValueCollectionConverter(ConverterContext context)
	{
		this.stringConverter = context.GetConverter<string>(GeneratedTypeShapeProvider);
	}

	/// <inheritdoc/>
	public override NameValueCollection? Read(ref JsonReader reader, SerializationContext context)
	{
		if (reader.TryReadNull())
		{
			return null;
		}

		context.DepthStep();
		NameValueCollection result = new();
		context.ReportObjectConstructed(result);
		PropertyCollisionDetection collision = new(StringComparer.Ordinal);
		reader.ReadStartObject();
		int count = 0;
		if (!reader.TryReadEndObject())
		{
			do
			{
				if (count++ >= context.Security.MaxObjectMemberCount)
				{
					throw new FormatException($"The JSON object exceeds the configured maximum of {context.Security.MaxObjectMemberCount} members.");
				}

				string key = reader.ReadRequiredString(context.StringInterningCache);
				collision.MarkAsRead(key);
				reader.ReadNameSeparator();
				if (reader.TryReadNull())
				{
					result.Add(key, null);
				}
				else if (reader.PeekValueToken() == '[')
				{
					SerializationContext valuesContext = context;
					valuesContext.DepthStep();
					reader.ReadStartArray();
					if (reader.TryReadEndArray())
					{
						result.Add(key, null);
					}
					else
					{
						do
						{
							result.Add(key, this.stringConverter.Read(ref reader, valuesContext));
							if (reader.TryReadEndArray())
							{
								break;
							}

							reader.ReadValueSeparator();
						}
						while (true);
					}
				}
				else
				{
					result.Add(key, this.stringConverter.Read(ref reader, context));
				}

				if (reader.TryReadEndObject())
				{
					break;
				}

				reader.ReadValueSeparator();
			}
			while (true);
		}

		return result;
	}

	/// <inheritdoc/>
	public override void Write(ref JsonWriter writer, in NameValueCollection? value, SerializationContext context)
	{
		if (value is null)
		{
			writer.WriteNull();
			return;
		}

		context.DepthStep();
		writer.WriteStartObject();
		for (int i = 0; i < value.Count; i++)
		{
			context.CancellationToken.ThrowIfCancellationRequested();
			if (i > 0)
			{
				writer.WriteValueSeparator();
			}

			writer.WritePropertyName(value.GetKey(i) ?? throw new NotSupportedException("Null NameValueCollection keys cannot be represented as JSON property names."));
			string[]? values = value.GetValues(i);
			if (values is null)
			{
				writer.WriteNull();
			}
			else if (values.Length == 1)
			{
				this.stringConverter.Write(ref writer, values[0], context);
			}
			else
			{
				SerializationContext valuesContext = context;
				valuesContext.DepthStep();
				writer.WriteStartArray();
				for (int j = 0; j < values.Length; j++)
				{
					if (j > 0)
					{
						writer.WriteValueSeparator();
					}

					this.stringConverter.Write(ref writer, values[j], valuesContext);
				}

				writer.WriteEndArray();
			}
		}

		writer.WriteEndObject();
	}
}

/// <summary>Creates name/value collection converters when explicitly enabled.</summary>
internal sealed class NameValueCollectionConverterFactory : IJsonConverterFactory
{
	/// <inheritdoc/>
	public JsonConverter? CreateConverter(Type type, ITypeShape? shape, in ConverterContext context)
		=> type == typeof(NameValueCollection) ? new NameValueCollectionConverter(context) : null;
}
