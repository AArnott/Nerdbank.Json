// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;

[GenerateShapeFor<string>]
[GenerateShapeFor<string[]>]
public partial class JsonStringInterningTests
{
	[Test]
	[Arguments("""["hello","h\u0065llo","\u0068ello"]""", "hello")]
	[Arguments("""["\u0068ello","hello","h\u0065llo"]""", "hello")]
	[Arguments("""["\"\\\/\b\f\n\r\t","\"\\/\b\f\n\r\t"]""", "\"\\/\b\f\n\r\t")]
	[Arguments("""["\u0000","\u0000"]""", "\0")]
	[Arguments("[\"\\u00E9\\uD83D\\uDE00\",\"\u00e9\U0001F600\"]", "\u00e9\U0001F600")]
	[Arguments("""["\uD800","\ud800"]""", "\uD800")]
	public void EquivalentEscapesShareInstances(string json, string expected)
	{
		JsonSerializer serializer = new() { InternStrings = true };
		string[] result = serializer.Deserialize<string[], JsonStringInterningTests>(json)!;
		foreach (string value in result)
		{
			Assert.Equal(expected, value);
			Assert.Same(result[0], value);
		}
	}

	[Test]
	[Arguments(0)]
	[Arguments(4091)]
	[Arguments(4092)]
	[Arguments(4093)]
	[Arguments(4094)]
	[Arguments(4095)]
	[Arguments(100_000)]
	public async Task BufferBoundaries(int prefixLength)
	{
		string expected = new string('a', prefixLength) + "\n\u00e9";
		string token = "\"" + new string('a', prefixLength) + "\\n\u00e9\"";
		string json = "[" + token + "," + token + "]";
		foreach (bool intern in new[] { false, true })
		{
			JsonSerializer serializer = new() { InternStrings = intern };
			string[] result = serializer.Deserialize<string[], JsonStringInterningTests>(json)!;
			Assert.Equal(new[] { expected, expected }, result);
			Assert.Equal(intern, ReferenceEquals(result[0], result[1]));

			using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
			string[] asyncResult = (await serializer.DeserializeAsync(stream, PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<string[]>()!))!;
			Assert.Equal(result, asyncResult);
			Assert.Equal(intern, ReferenceEquals(asyncResult[0], asyncResult[1]));
		}
	}

	[Test]
	[Arguments("\"abc\\")]
	[Arguments("\"abc\\q\"")]
	[Arguments("\"abc\\u12")]
	[Arguments("\"abc\\u12xz\"")]
	[Arguments("\"abc\\n")]
	[Arguments("\"abc\\\"")]
	public void MalformedEscapesStillThrow(string json)
	{
		foreach (bool intern in new[] { false, true })
		{
			JsonSerializer serializer = new() { InternStrings = intern };
			Assert.Throws<FormatException>(() => serializer.Deserialize<string, JsonStringInterningTests>(json));
		}
	}

	[Test]
	public void PooledBufferCanBeReusedAfterInvalidEscape()
	{
		JsonSerializer serializer = new() { InternStrings = true };
		string prefix = new('a', 8192);
		Assert.Throws<FormatException>(() => serializer.Deserialize<string, JsonStringInterningTests>("\"" + prefix + "\\q\""));
		Assert.Equal(prefix + "\n", serializer.Deserialize<string, JsonStringInterningTests>("\"" + prefix + "\\n\""));
	}

	[Test]
	public void MixedUtf8AndUnicodeEscapesRoundTrip()
	{
		Random random = new(1234);
		JsonSerializer serializer = new() { InternStrings = true };
		for (int sample = 0; sample < 100; sample++)
		{
			char[] characters = new char[random.Next(1, 300)];
			for (int i = 0; i < characters.Length; i++)
			{
				characters[i] = (char)random.Next(0, 0xD800);
			}

			string expected = new(characters);
			string canonical = serializer.Serialize<string, JsonStringInterningTests>(expected);
			string escaped = "\"" + string.Concat(characters.Select(c => "\\u" + ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture))) + "\"";
			string[] result = serializer.Deserialize<string[], JsonStringInterningTests>("[" + canonical + "," + escaped + "]")!;
			Assert.Equal(expected, result[0]);
			Assert.Same(result[0], result[1]);

			JsonReader reader = new(Encoding.UTF8.GetBytes(escaped));
			Assert.Equal(expected, reader.ReadRequiredString());
			reader.EnsureFullyConsumed();
		}
	}

#if NET
	[Test]
	[Arguments("hello\\nworld")]
	[Arguments("h\\u0065llo")]
	[Arguments("\\uD83D\\uDE00")]
	[Arguments("\\\"\\\\\\/\\b\\f\\n\\r\\t")]
	[Arguments("")]
	[Arguments("plain")]
	public void SmallInterningCacheHitsDoNotAllocate(string encoded)
	{
		AllocationMeasuringConverter converter = new();
		JsonSerializer serializer = new() { InternStrings = true, Converters = new([converter]) };
		string token = "\"" + encoded + "\"";
		byte[] json = Encoding.UTF8.GetBytes("[" + string.Join(",", Enumerable.Repeat(token, 64)) + "]");
		serializer.Deserialize<string[], JsonStringInterningTests>(json);
		serializer.Deserialize<string[], JsonStringInterningTests>(json);
		Assert.Equal(0L, converter.CacheHitAllocations);
	}

	private sealed class AllocationMeasuringConverter : JsonConverter<string[]>
	{
		internal long CacheHitAllocations { get; private set; }

		public override string[] Read(ref JsonReader reader, SerializationContext context)
		{
			JsonConverter<string> converter = context.GetConverter(
				PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<string>()!);
			reader.ReadStartArray();
			string first = converter.Read(ref reader, context)!;
			long before = GC.GetAllocatedBytesForCurrentThread();
			bool same = true;
			while (!reader.TryReadEndArray())
			{
				reader.ReadValueSeparator();
				same &= ReferenceEquals(first, converter.Read(ref reader, context));
			}

			this.CacheHitAllocations = GC.GetAllocatedBytesForCurrentThread() - before;
			Assert.True(same);
			return [first];
		}

		public override void Write(ref JsonWriter writer, string[]? value, SerializationContext context)
			=> throw new NotSupportedException();
	}
#endif
}
