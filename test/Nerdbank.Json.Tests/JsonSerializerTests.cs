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
		=> this.AssertRoundtrip<string, JsonSerializerTests>(
			"ü <tag> \"quoted\" \\ slash\nnext",
			"\"ü <tag> \\\"quoted\\\" \\\\ slash\\nnext\"");

	[Test]
	public void SerializeDeserialize_PrimitiveScalars()
	{
		this.AssertRoundtrip<char, JsonSerializerTests>('A', "\"A\"");
		this.AssertRoundtrip<byte, JsonSerializerTests>((byte)42, "42");
		this.AssertRoundtrip<sbyte, JsonSerializerTests>((sbyte)-42, "-42");
		this.AssertRoundtrip<short, JsonSerializerTests>((short)-1234, "-1234");
		this.AssertRoundtrip<ushort, JsonSerializerTests>((ushort)65530, "65530");
		this.AssertRoundtrip<int, JsonSerializerTests>(123456, "123456");
		this.AssertRoundtrip<uint, JsonSerializerTests>(4000000000u, "4000000000");
		this.AssertRoundtrip<long, JsonSerializerTests>(-9876543210L, "-9876543210");
		this.AssertRoundtrip<ulong, JsonSerializerTests>(18446744073709551610UL, "18446744073709551610");
		this.AssertRoundtrip<bool, JsonSerializerTests>(true, "true");
		this.AssertRoundtrip<bool, JsonSerializerTests>(false, "false");
		this.AssertRoundtrip<float, JsonSerializerTests>(1.5f, "1.5");
		this.AssertRoundtrip<double, JsonSerializerTests>(1.5d, "1.5");
		this.AssertRoundtrip<decimal, JsonSerializerTests>(79228162514264337593543950335m, "79228162514264337593543950335");
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

		this.AssertRoundtrip<BigInteger, JsonSerializerTests>(new BigInteger(1234567890123456789L), "1234567890123456789", EqualityComparer<BigInteger>.Default);
		this.AssertRoundtrip<DateTime, JsonSerializerTests>(
			new DateTime(2026, 6, 25, 18, 17, 16, 123, DateTimeKind.Utc).AddTicks(4567),
			"\"2026-06-25T18:17:16.1234567Z\"",
			EqualityComparer<DateTime>.Default);
		this.AssertRoundtrip<DateTimeOffset, JsonSerializerTests>(
			new DateTimeOffset(2026, 6, 25, 18, 17, 16, 123, TimeSpan.FromHours(-7)).AddTicks(4567),
			"\"2026-06-25T18:17:16.1234567-07:00\"",
			EqualityComparer<DateTimeOffset>.Default);
		this.AssertRoundtrip<TimeSpan, JsonSerializerTests>(
			new TimeSpan(1, 2, 3, 4, 5).Add(TimeSpan.FromTicks(6)),
			"\"1.02:03:04.0050006\"",
			EqualityComparer<TimeSpan>.Default);
		this.AssertRoundtrip<Guid, JsonSerializerTests>(
			Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
			"\"01234567-89ab-cdef-0123-456789abcdef\"",
			EqualityComparer<Guid>.Default);
		this.AssertRoundtrip<Version, JsonSerializerTests>(
			new Version(1, 2, 3, 4),
			"\"1.2.3.4\"",
			EqualityComparer<Version>.Default);
		this.AssertRoundtrip<Uri, JsonSerializerTests>(
			new Uri("https://example.com/a?b=c", UriKind.Absolute),
			"\"https://example.com/a?b=c\"",
			EqualityComparer<Uri>.Default);
		this.AssertRoundtrip<CultureInfo, JsonSerializerTests>(culture, cultureJson, EqualityComparer<CultureInfo>.Default);
		this.AssertRoundtrip<Encoding, JsonSerializerTests>(Encoding.UTF8, "\"utf-8\"", EqualityComparer<Encoding>.Default);
		this.AssertRoundtrip<Color, JsonSerializerTests>(Color.FromArgb(unchecked((int)0xFF336699)), "-13408615", EqualityComparer<Color>.Default);
		this.AssertRoundtrip<Point, JsonSerializerTests>(new Point(12, -34), "[12,-34]", EqualityComparer<Point>.Default);
	}

	[Test]
	public void SerializeDeserialize_ByteBuffers()
	{
		this.AssertRoundtrip<byte[], JsonSerializerTests>(new byte[] { 1, 2, 3, 4 }, "\"AQIDBA==\"");
		this.AssertRoundtrip<Memory<byte>, JsonSerializerTests>(new Memory<byte>(new byte[] { 5, 6, 7 }), "\"BQYH\"");
		this.AssertRoundtrip<ReadOnlyMemory<byte>, JsonSerializerTests>(new ReadOnlyMemory<byte>(new byte[] { 8, 9, 10 }), "\"CAkK\"");
	}

#if NET8_0_OR_GREATER
	[Test]
	public void SerializeDeserialize_ModernBuiltInTypes()
	{
		this.AssertRoundtrip<Half, JsonSerializerTests>((Half)1.5f, "1.5");
		this.AssertRoundtrip<Int128, JsonSerializerTests>(Int128.Parse("170141183460469231731687303715884105727", CultureInfo.InvariantCulture), "170141183460469231731687303715884105727");
		this.AssertRoundtrip<UInt128, JsonSerializerTests>(UInt128.Parse("340282366920938463463374607431768211455", CultureInfo.InvariantCulture), "340282366920938463463374607431768211455");
		this.AssertRoundtrip<DateOnly, JsonSerializerTests>(
			DateOnly.ParseExact("2026-06-25", "yyyy-MM-dd", CultureInfo.InvariantCulture),
			"\"2026-06-25\"");
		this.AssertRoundtrip<TimeOnly, JsonSerializerTests>(
			TimeOnly.ParseExact("18:17:16.1234567", "HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
			"\"18:17:16.1234567\"");
		this.AssertRoundtrip<Rune, JsonSerializerTests>(
			new Rune(0x1F98A),
			"\"🦊\"");
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

		Assert.Throws<FormatException>(() => serializer.Deserialize<Point, JsonSerializerTests>("""[12,-34,]"""));
	}

	[Test]
	public void Deserialize_Point_SkipsComments_WhenEnabled()
	{
		JsonSerializer serializer = new() { ReadCommentHandling = JsonCommentHandling.Skip };

		Point value = serializer.Deserialize<Point, JsonSerializerTests>(
			"""
			[/* x */12, // y
			 -34]
			""");

		Assert.Equal(new Point(12, -34), value);
	}

	[Test]
	public void Deserialize_Point_Comments_ThrowByDefault()
	{
		JsonSerializer serializer = new();

		Assert.Throws<FormatException>(() => serializer.Deserialize<Point, JsonSerializerTests>("""[/* x */12,-34]"""));
	}

	[Test]
	public void Serialize_NestedBuiltInComplexType_ExceedingMaxDepth_Throws()
	{
		this.Serializer = this.Serializer with
		{
			StartingContext = this.Serializer.StartingContext with { MaxDepth = 1 },
		};

		PointContainer value = new() { Location = new Point(12, -34) };

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => this.Serializer.Serialize(value));
		Assert.Contains("Exceeded maximum depth", exception.Message);
	}

	[Test]
	public void Deserialize_NestedBuiltInComplexType_ExceedingMaxDepth_Throws()
	{
		this.Serializer = this.Serializer with
		{
			StartingContext = this.Serializer.StartingContext with { MaxDepth = 1 },
		};

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => this.Serializer.Deserialize<PointContainer>("""{"location":[12,-34]}"""));
		Assert.Contains("Exceeded maximum depth", exception.Message);
	}

	[GenerateShape]
	internal partial class PointContainer
	{
		public Point Location { get; set; }
	}
}
