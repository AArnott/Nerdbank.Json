// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Text;

[GenerateShapeFor<char>]
[GenerateShapeFor<byte>]
[GenerateShapeFor<sbyte>]
[GenerateShapeFor<short>]
[GenerateShapeFor<ushort>]
[GenerateShapeFor<int>]
[GenerateShapeFor<uint>]
[GenerateShapeFor<long>]
[GenerateShapeFor<ulong>]
[GenerateShapeFor<bool>]
[GenerateShapeFor<float>]
[GenerateShapeFor<double>]
[GenerateShapeFor<decimal>]
[GenerateShapeFor<BigInteger>]
[GenerateShapeFor<DateTime>]
[GenerateShapeFor<DateTimeOffset>]
[GenerateShapeFor<TimeSpan>]
[GenerateShapeFor<Guid>]
[GenerateShapeFor<Version>]
[GenerateShapeFor<Uri>]
[GenerateShapeFor<CultureInfo>]
[GenerateShapeFor<Encoding>]
[GenerateShapeFor<Color>]
[GenerateShapeFor<Point>]
[GenerateShapeFor<byte[]>]
[GenerateShapeFor<Memory<byte>>]
[GenerateShapeFor<ReadOnlyMemory<byte>>]
[GenerateShapeFor<string>]
#if NET8_0_OR_GREATER
[GenerateShapeFor<Half>]
[GenerateShapeFor<Int128>]
[GenerateShapeFor<UInt128>]
[GenerateShapeFor<DateOnly>]
[GenerateShapeFor<TimeOnly>]
[GenerateShapeFor<Rune>]
#endif
public partial class JsonSerializerTests
{
	[Test]
	public void Serialize_StringUsesRfc8259EscapingRules()
		=> AssertRoundtrip("ü <tag> \"quoted\" \\ slash\nnext", "\"ü <tag> \\\"quoted\\\" \\\\ slash\\nnext\"");

	[Test]
	public void SerializeDeserialize_PrimitiveScalars()
	{
		AssertRoundtrip('A', "\"A\"");
		AssertRoundtrip((byte)42, "42");
		AssertRoundtrip((sbyte)-42, "-42");
		AssertRoundtrip((short)-1234, "-1234");
		AssertRoundtrip((ushort)65530, "65530");
		AssertRoundtrip(123456, "123456");
		AssertRoundtrip(4000000000u, "4000000000");
		AssertRoundtrip(-9876543210L, "-9876543210");
		AssertRoundtrip(18446744073709551610UL, "18446744073709551610");
		AssertRoundtrip(true, "true");
		AssertRoundtrip(false, "false");
		AssertRoundtrip(1.5f, "1.5");
		AssertRoundtrip(1.5d, "1.5");
		AssertRoundtrip(79228162514264337593543950335m, "79228162514264337593543950335");
	}

	[Test]
	public void SerializeDeserialize_CommonBclTypes()
	{
		CultureInfo culture;
		string cultureJson;
		try
		{
			culture = CultureInfo.GetCultureInfo("fr-FR");
			cultureJson = "\"fr-FR\"";
		}
		catch (CultureNotFoundException)
		{
			culture = CultureInfo.InvariantCulture;
			cultureJson = "\"\"";
		}

		AssertRoundtrip(new BigInteger(1234567890123456789L), "1234567890123456789");
		AssertRoundtrip(new DateTime(2026, 6, 25, 18, 17, 16, 123, DateTimeKind.Utc).AddTicks(4567), "\"2026-06-25T18:17:16.1234567Z\"");
		AssertRoundtrip(new DateTimeOffset(2026, 6, 25, 18, 17, 16, 123, TimeSpan.FromHours(-7)).AddTicks(4567), "\"2026-06-25T18:17:16.1234567-07:00\"");
		AssertRoundtrip(new TimeSpan(1, 2, 3, 4, 5).Add(TimeSpan.FromTicks(6)), "\"1.02:03:04.0050006\"");
		AssertRoundtrip(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), "\"01234567-89ab-cdef-0123-456789abcdef\"");
		AssertRoundtrip(new Version(1, 2, 3, 4), "\"1.2.3.4\"");
		AssertRoundtrip(new Uri("https://example.com/a?b=c", UriKind.Absolute), "\"https://example.com/a?b=c\"");
		AssertRoundtrip(culture, cultureJson);
		AssertRoundtrip(Encoding.UTF8, "\"utf-8\"");
		AssertRoundtrip(Color.FromArgb(unchecked((int)0xFF336699)), "-13408615");
		AssertRoundtrip(new Point(12, -34), "[12,-34]");
	}

	[Test]
	public void SerializeDeserialize_ByteBuffers()
	{
		AssertRoundtrip(new byte[] { 1, 2, 3, 4 }, "\"AQIDBA==\"");
		AssertRoundtrip(new Memory<byte>([5, 6, 7]), "\"BQYH\"");
		AssertRoundtrip(new ReadOnlyMemory<byte>([8, 9, 10]), "\"CAkK\"");
	}

#if NET8_0_OR_GREATER
	[Test]
	public void SerializeDeserialize_ModernBuiltInTypes()
	{
		AssertRoundtrip((Half)1.5f, "1.5");
		AssertRoundtrip(Int128.Parse("170141183460469231731687303715884105727", CultureInfo.InvariantCulture), "170141183460469231731687303715884105727");
		AssertRoundtrip(UInt128.Parse("340282366920938463463374607431768211455", CultureInfo.InvariantCulture), "340282366920938463463374607431768211455");
		AssertRoundtrip(DateOnly.ParseExact("2026-06-25", "yyyy-MM-dd", CultureInfo.InvariantCulture), "\"2026-06-25\"");
		AssertRoundtrip(TimeOnly.ParseExact("18:17:16.1234567", "HH:mm:ss.fffffff", CultureInfo.InvariantCulture), "\"18:17:16.1234567\"");
		AssertRoundtrip(new Rune(0x1F98A), "\"🦊\"");
	}
#endif

	[Test]
	public void Deserialize_NullReferenceTypes()
	{
		JsonSerializer serializer = new();

		Assert.Null(serializer.Deserialize<string?, JsonSerializerTests>("null"));
		Assert.Null(serializer.Deserialize<byte[]?, JsonSerializerTests>("null"));
		Assert.Null(serializer.Deserialize<Version?, JsonSerializerTests>("null"));
		Assert.Null(serializer.Deserialize<Uri?, JsonSerializerTests>("null"));
	}

	[Test]
	public void Deserialize_Point_AllowsTrailingComma_WhenEnabled()
	{
		JsonSerializer serializer = new() { AllowTrailingCommas = true };

		Point value = serializer.Deserialize<Point, JsonSerializerTests>("[12,-34,]");

		Assert.Equal(new Point(12, -34), value);
	}

	[Test]
	public void Deserialize_Point_TrailingComma_ThrowsByDefault()
	{
		JsonSerializer serializer = new();

		Assert.Throws<FormatException>(() => serializer.Deserialize<Point, JsonSerializerTests>("[12,-34,]"));
	}

	[Test]
	public void Deserialize_Point_SkipsComments_WhenEnabled()
	{
		JsonSerializer serializer = new() { ReadCommentHandling = JsonCommentHandling.Skip };

		Point value = serializer.Deserialize<Point, JsonSerializerTests>("[/* x */12, // y\r\n -34]");

		Assert.Equal(new Point(12, -34), value);
	}

	[Test]
	public void Deserialize_Point_Comments_ThrowByDefault()
	{
		JsonSerializer serializer = new();

		Assert.Throws<FormatException>(() => serializer.Deserialize<Point, JsonSerializerTests>("[/* x */12,-34]"));
	}

	private static void AssertRoundtrip<T>(T value, string expectedJson)
	{
		JsonSerializer serializer = new();
		ITypeShape<T> shape = PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_Tests.Default.GetTypeShape<T>() ?? throw new InvalidOperationException($"No generated type shape found for {typeof(T)}.");

		string json = serializer.Serialize(value, shape);
		Assert.Equal(expectedJson, json);

		T? roundTripped = serializer.Deserialize(json, shape);
		AssertEqual(value, roundTripped);
	}

	private static void AssertEqual<T>(T expected, T actual)
	{
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
}
