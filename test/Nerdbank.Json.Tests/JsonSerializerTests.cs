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
public partial class JsonSerializerTests : TestBase
{
	[Test]
	public void Serialize_StringUsesRfc8259EscapingRules()
		=> this.AssertRoundtrip("ü <tag> \"quoted\" \\ slash\nnext", "\"ü <tag> \\\"quoted\\\" \\\\ slash\\nnext\"");

	[Test]
	public void SerializeDeserialize_PrimitiveScalars()
	{
		this.AssertRoundtrip('A', "\"A\"");
		this.AssertRoundtrip((byte)42, "42");
		this.AssertRoundtrip((sbyte)-42, "-42");
		this.AssertRoundtrip((short)-1234, "-1234");
		this.AssertRoundtrip((ushort)65530, "65530");
		this.AssertRoundtrip(123456, "123456");
		this.AssertRoundtrip(4000000000u, "4000000000");
		this.AssertRoundtrip(-9876543210L, "-9876543210");
		this.AssertRoundtrip(18446744073709551610UL, "18446744073709551610");
		this.AssertRoundtrip(true, "true");
		this.AssertRoundtrip(false, "false");
		this.AssertRoundtrip(1.5f, "1.5");
		this.AssertRoundtrip(1.5d, "1.5");
		this.AssertRoundtrip(79228162514264337593543950335m, "79228162514264337593543950335");
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

		this.AssertRoundtrip(new BigInteger(1234567890123456789L), "1234567890123456789");
		this.AssertRoundtrip(new DateTime(2026, 6, 25, 18, 17, 16, 123, DateTimeKind.Utc).AddTicks(4567), "\"2026-06-25T18:17:16.1234567Z\"");
		this.AssertRoundtrip(new DateTimeOffset(2026, 6, 25, 18, 17, 16, 123, TimeSpan.FromHours(-7)).AddTicks(4567), "\"2026-06-25T18:17:16.1234567-07:00\"");
		this.AssertRoundtrip(new TimeSpan(1, 2, 3, 4, 5).Add(TimeSpan.FromTicks(6)), "\"1.02:03:04.0050006\"");
		this.AssertRoundtrip(Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"), "\"01234567-89ab-cdef-0123-456789abcdef\"");
		this.AssertRoundtrip(new Version(1, 2, 3, 4), "\"1.2.3.4\"");
		this.AssertRoundtrip(new Uri("https://example.com/a?b=c", UriKind.Absolute), "\"https://example.com/a?b=c\"");
		this.AssertRoundtrip(culture, cultureJson);
		this.AssertRoundtrip(Encoding.UTF8, "\"utf-8\"");
		this.AssertRoundtrip(Color.FromArgb(unchecked((int)0xFF336699)), "-13408615");
		this.AssertRoundtrip(new Point(12, -34), "[12,-34]");
	}

	[Test]
	public void SerializeDeserialize_ByteBuffers()
	{
		this.AssertRoundtrip(new byte[] { 1, 2, 3, 4 }, "\"AQIDBA==\"");
		this.AssertRoundtrip(new Memory<byte>([5, 6, 7]), "\"BQYH\"");
		this.AssertRoundtrip(new ReadOnlyMemory<byte>([8, 9, 10]), "\"CAkK\"");
	}

#if NET8_0_OR_GREATER
	[Test]
	public void SerializeDeserialize_ModernBuiltInTypes()
	{
		this.AssertRoundtrip((Half)1.5f, "1.5");
		this.AssertRoundtrip(Int128.Parse("170141183460469231731687303715884105727", CultureInfo.InvariantCulture), "170141183460469231731687303715884105727");
		this.AssertRoundtrip(UInt128.Parse("340282366920938463463374607431768211455", CultureInfo.InvariantCulture), "340282366920938463463374607431768211455");
		this.AssertRoundtrip(DateOnly.ParseExact("2026-06-25", "yyyy-MM-dd", CultureInfo.InvariantCulture), "\"2026-06-25\"");
		this.AssertRoundtrip(TimeOnly.ParseExact("18:17:16.1234567", "HH:mm:ss.fffffff", CultureInfo.InvariantCulture), "\"18:17:16.1234567\"");
		this.AssertRoundtrip(new Rune(0x1F98A), "\"🦊\"");
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

	private void AssertRoundtrip<T>(T value, string expectedJson)
	{
		ITypeShape<T> shape = GetTypeShape<T>();
		string json = this.Serializer.Serialize(value, shape);
		Assert.Equal(expectedJson, json);

		T? roundTripped = this.Serializer.Deserialize(json, shape);
		AssertEqual(value, roundTripped!);
	}
}
