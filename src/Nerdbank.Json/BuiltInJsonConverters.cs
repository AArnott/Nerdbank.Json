// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Nerdbank.Json;

internal static class BuiltInJsonConverters
{
	internal static bool RequiresNestedContext(Type type)
	{
		return type == typeof(Point);
	}

	internal static bool IsSupported(Type type)
	{
		return type == typeof(string)
			|| type == typeof(char)
			|| type == typeof(bool)
			|| type == typeof(byte)
			|| type == typeof(sbyte)
			|| type == typeof(short)
			|| type == typeof(ushort)
			|| type == typeof(int)
			|| type == typeof(uint)
			|| type == typeof(long)
			|| type == typeof(ulong)
			|| type == typeof(float)
			|| type == typeof(double)
			|| type == typeof(decimal)
			|| type == typeof(BigInteger)
			|| type == typeof(DateTime)
			|| type == typeof(DateTimeOffset)
			|| type == typeof(TimeSpan)
			|| type == typeof(Guid)
			|| type == typeof(Version)
			|| type == typeof(Uri)
			|| type == typeof(CultureInfo)
			|| type == typeof(Encoding)
			|| type == typeof(byte[])
			|| type == typeof(Memory<byte>)
			|| type == typeof(ReadOnlyMemory<byte>)
			|| type == typeof(Color)
			|| type == typeof(Point)
#if NET8_0_OR_GREATER
			|| type == typeof(Half)
			|| type == typeof(Int128)
			|| type == typeof(UInt128)
			|| type == typeof(DateOnly)
			|| type == typeof(TimeOnly)
			|| type == typeof(Rune)
#endif
			;
	}

	internal static bool TrySerialize<T>(ref JsonWriter writer, T value)
	{
		Type type = typeof(T);
		object? boxed = value;

		if (type == typeof(string))
		{
			writer.WriteStringValue((string?)boxed);
			return true;
		}

		if (type == typeof(char))
		{
			writer.WriteStringValue(new string((char)boxed!, 1));
			return true;
		}

		if (type == typeof(bool))
		{
			writer.WriteBooleanValue((bool)boxed!);
			return true;
		}

		if (type == typeof(byte))
		{
			writer.WriteNumberValue((byte)boxed!);
			return true;
		}

		if (type == typeof(sbyte))
		{
			writer.WriteNumberValue((sbyte)boxed!);
			return true;
		}

		if (type == typeof(short))
		{
			writer.WriteNumberValue((short)boxed!);
			return true;
		}

		if (type == typeof(ushort))
		{
			writer.WriteNumberValue((ushort)boxed!);
			return true;
		}

		if (type == typeof(int))
		{
			writer.WriteNumberValue((int)boxed!);
			return true;
		}

		if (type == typeof(uint))
		{
			writer.WriteNumberValue((uint)boxed!);
			return true;
		}

		if (type == typeof(long))
		{
			writer.WriteNumberValue((long)boxed!);
			return true;
		}

		if (type == typeof(ulong))
		{
			writer.WriteNumberValue((ulong)boxed!);
			return true;
		}

		if (type == typeof(float))
		{
			writer.WriteNumberValue((float)boxed!);
			return true;
		}

		if (type == typeof(double))
		{
			writer.WriteNumberValue((double)boxed!);
			return true;
		}

		if (type == typeof(decimal))
		{
			writer.WriteNumberValue((decimal)boxed!);
			return true;
		}

		if (type == typeof(BigInteger))
		{
			writer.WriteRawValue(((BigInteger)boxed!).ToString(CultureInfo.InvariantCulture));
			return true;
		}

		if (type == typeof(DateTime))
		{
			writer.WriteStringValue(((DateTime)boxed!).ToString("O", CultureInfo.InvariantCulture));
			return true;
		}

		if (type == typeof(DateTimeOffset))
		{
			writer.WriteStringValue(((DateTimeOffset)boxed!).ToString("O", CultureInfo.InvariantCulture));
			return true;
		}

		if (type == typeof(TimeSpan))
		{
			writer.WriteStringValue(((TimeSpan)boxed!).ToString("c", CultureInfo.InvariantCulture));
			return true;
		}

		if (type == typeof(Guid))
		{
			writer.WriteStringValue(((Guid)boxed!).ToString("D", CultureInfo.InvariantCulture));
			return true;
		}

		if (type == typeof(Version))
		{
			writer.WriteStringValue(((Version?)boxed)?.ToString());
			return true;
		}

		if (type == typeof(Uri))
		{
			writer.WriteStringValue(((Uri?)boxed)?.OriginalString);
			return true;
		}

		if (type == typeof(CultureInfo))
		{
			writer.WriteStringValue(((CultureInfo?)boxed)?.Name);
			return true;
		}

		if (type == typeof(Encoding))
		{
			writer.WriteStringValue(((Encoding?)boxed)?.WebName);
			return true;
		}

		if (type == typeof(byte[]))
		{
			writer.WriteBase64StringValue((byte[]?)boxed);
			return true;
		}

		if (type == typeof(Memory<byte>))
		{
			writer.WriteBase64StringValue(((Memory<byte>)boxed!).Span);
			return true;
		}

		if (type == typeof(ReadOnlyMemory<byte>))
		{
			writer.WriteBase64StringValue(((ReadOnlyMemory<byte>)boxed!).Span);
			return true;
		}

		if (type == typeof(Color))
		{
			writer.WriteNumberValue(((Color)boxed!).ToArgb());
			return true;
		}

		if (type == typeof(Point))
		{
			var point = (Point)boxed!;
			writer.WriteStartArray();
			writer.WriteNumberValue(point.X);
			writer.WriteValueSeparator();
			writer.WriteNumberValue(point.Y);
			writer.WriteEndArray();
			return true;
		}

#if NET8_0_OR_GREATER
		if (type == typeof(Half))
		{
			writer.WriteRawValue(((Half)boxed!).ToString(CultureInfo.InvariantCulture));
			return true;
		}

		if (type == typeof(Int128))
		{
			writer.WriteRawValue(((Int128)boxed!).ToString(CultureInfo.InvariantCulture));
			return true;
		}

		if (type == typeof(UInt128))
		{
			writer.WriteRawValue(((UInt128)boxed!).ToString(CultureInfo.InvariantCulture));
			return true;
		}

		if (type == typeof(DateOnly))
		{
			writer.WriteStringValue(((DateOnly)boxed!).ToString("O", CultureInfo.InvariantCulture));
			return true;
		}

		if (type == typeof(TimeOnly))
		{
			writer.WriteStringValue(((TimeOnly)boxed!).ToString("O", CultureInfo.InvariantCulture));
			return true;
		}

		if (type == typeof(Rune))
		{
			writer.WriteStringValue(((Rune)boxed!).ToString());
			return true;
		}
#endif

		return false;
	}

	internal static bool TryDeserialize<T>(ref JsonReader reader, out T value)
	{
		Type type = typeof(T);
		object? result;

		if (type == typeof(string))
		{
			result = reader.ReadString();
			value = (T?)result!;
			return true;
		}

		if (type == typeof(char))
		{
			result = reader.ReadChar();
			value = (T)result;
			return true;
		}

		if (type == typeof(bool))
		{
			result = reader.ReadBoolean();
			value = (T)result;
			return true;
		}

		if (type == typeof(byte))
		{
			result = reader.ReadByteValue();
			value = (T)result;
			return true;
		}

		if (type == typeof(sbyte))
		{
			result = reader.ReadSByteValue();
			value = (T)result;
			return true;
		}

		if (type == typeof(short))
		{
			result = reader.ReadInt16Value();
			value = (T)result;
			return true;
		}

		if (type == typeof(ushort))
		{
			result = reader.ReadUInt16Value();
			value = (T)result;
			return true;
		}

		if (type == typeof(int))
		{
			result = reader.ReadInt32Value();
			value = (T)result;
			return true;
		}

		if (type == typeof(uint))
		{
			result = reader.ReadUInt32Value();
			value = (T)result;
			return true;
		}

		if (type == typeof(long))
		{
			result = reader.ReadInt64Value();
			value = (T)result;
			return true;
		}

		if (type == typeof(ulong))
		{
			result = reader.ReadUInt64Value();
			value = (T)result;
			return true;
		}

		if (type == typeof(float))
		{
			result = reader.ReadSingleValue();
			value = (T)result;
			return true;
		}

		if (type == typeof(double))
		{
			result = reader.ReadDoubleValue();
			value = (T)result;
			return true;
		}

		if (type == typeof(decimal))
		{
			result = reader.ReadDecimalValue();
			value = (T)result;
			return true;
		}

		if (type == typeof(BigInteger))
		{
			result = BigInteger.Parse(reader.ReadNumberToken(), CultureInfo.InvariantCulture);
			value = (T)result;
			return true;
		}

		if (type == typeof(DateTime))
		{
			result = DateTime.ParseExact(reader.ReadRequiredString(), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			value = (T)result;
			return true;
		}

		if (type == typeof(DateTimeOffset))
		{
			result = DateTimeOffset.ParseExact(reader.ReadRequiredString(), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			value = (T)result;
			return true;
		}

		if (type == typeof(TimeSpan))
		{
			result = TimeSpan.ParseExact(reader.ReadRequiredString(), "c", CultureInfo.InvariantCulture);
			value = (T)result;
			return true;
		}

		if (type == typeof(Guid))
		{
			result = Guid.ParseExact(reader.ReadRequiredString(), "D");
			value = (T)result;
			return true;
		}

		if (type == typeof(Version))
		{
			result = reader.TryReadNull() ? null : new Version(reader.ReadRequiredString());
			value = (T?)result!;
			return true;
		}

		if (type == typeof(Uri))
		{
			result = reader.TryReadNull() ? null : new Uri(reader.ReadRequiredString(), UriKind.RelativeOrAbsolute);
			value = (T?)result!;
			return true;
		}

		if (type == typeof(CultureInfo))
		{
			result = reader.TryReadNull() ? null : CultureInfo.GetCultureInfo(reader.ReadRequiredString());
			value = (T?)result!;
			return true;
		}

		if (type == typeof(Encoding))
		{
			result = reader.TryReadNull() ? null : Encoding.GetEncoding(reader.ReadRequiredString());
			value = (T?)result!;
			return true;
		}

		if (type == typeof(byte[]))
		{
			result = reader.ReadBase64Bytes();
			value = (T?)result!;
			return true;
		}

		if (type == typeof(Memory<byte>))
		{
			result = new Memory<byte>(reader.ReadRequiredBase64Bytes());
			value = (T)result;
			return true;
		}

		if (type == typeof(ReadOnlyMemory<byte>))
		{
			result = new ReadOnlyMemory<byte>(reader.ReadRequiredBase64Bytes());
			value = (T)result;
			return true;
		}

		if (type == typeof(Color))
		{
			result = Color.FromArgb(reader.ReadInt32Value());
			value = (T)result;
			return true;
		}

		if (type == typeof(Point))
		{
			reader.ReadStartArray();
			int x = reader.ReadInt32Value();
			reader.ReadValueSeparator();
			int y = reader.ReadInt32Value();
			reader.ReadEndArray();
			result = new Point(x, y);
			value = (T)result;
			return true;
		}

#if NET8_0_OR_GREATER
		if (type == typeof(Half))
		{
			result = Half.Parse(reader.ReadNumberToken(), CultureInfo.InvariantCulture);
			value = (T)result;
			return true;
		}

		if (type == typeof(Int128))
		{
			result = Int128.Parse(reader.ReadNumberToken(), CultureInfo.InvariantCulture);
			value = (T)result;
			return true;
		}

		if (type == typeof(UInt128))
		{
			result = UInt128.Parse(reader.ReadNumberToken(), CultureInfo.InvariantCulture);
			value = (T)result;
			return true;
		}

		if (type == typeof(DateOnly))
		{
			result = DateOnly.ParseExact(reader.ReadRequiredString(), "O", CultureInfo.InvariantCulture);
			value = (T)result;
			return true;
		}

		if (type == typeof(TimeOnly))
		{
			result = TimeOnly.ParseExact(reader.ReadRequiredString(), "O", CultureInfo.InvariantCulture);
			value = (T)result;
			return true;
		}

		if (type == typeof(Rune))
		{
			string runeText = reader.ReadRequiredString();
			result = Rune.GetRuneAt(runeText, 0);
			value = (T)result;
			return true;
		}
#endif

		value = default!;
		return false;
	}
}
