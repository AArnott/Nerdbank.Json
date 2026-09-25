// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
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
		// Each branch below reinterprets `value` (of static type T) as the concrete runtime type
		// via Unsafe.As, rather than boxing it to `object` and unboxing/casting it back down.
		// This is safe because the runtime JIT-compiles a specialized instantiation of this method
		// for every value-type T, so `typeof(T) == typeof(X)` is a compile-time-constant check within
		// that instantiation: when it is true, T and X are the same type and share the same layout.
		// Unlike a box-then-unbox pattern (which allocates on the heap for every call), this technique
		// never allocates.
		if (typeof(T) == typeof(string))
		{
			writer.WriteStringValue((string?)(object?)value);
			return true;
		}

		if (typeof(T) == typeof(char))
		{
			writer.WriteStringValue(new string(Unsafe.As<T, char>(ref value), 1));
			return true;
		}

		if (typeof(T) == typeof(bool))
		{
			writer.WriteBooleanValue(Unsafe.As<T, bool>(ref value));
			return true;
		}

		if (typeof(T) == typeof(byte))
		{
			writer.WriteNumberValue(Unsafe.As<T, byte>(ref value));
			return true;
		}

		if (typeof(T) == typeof(sbyte))
		{
			writer.WriteNumberValue(Unsafe.As<T, sbyte>(ref value));
			return true;
		}

		if (typeof(T) == typeof(short))
		{
			writer.WriteNumberValue(Unsafe.As<T, short>(ref value));
			return true;
		}

		if (typeof(T) == typeof(ushort))
		{
			writer.WriteNumberValue(Unsafe.As<T, ushort>(ref value));
			return true;
		}

		if (typeof(T) == typeof(int))
		{
			writer.WriteNumberValue(Unsafe.As<T, int>(ref value));
			return true;
		}

		if (typeof(T) == typeof(uint))
		{
			writer.WriteNumberValue(Unsafe.As<T, uint>(ref value));
			return true;
		}

		if (typeof(T) == typeof(long))
		{
			writer.WriteNumberValue(Unsafe.As<T, long>(ref value));
			return true;
		}

		if (typeof(T) == typeof(ulong))
		{
			writer.WriteNumberValue(Unsafe.As<T, ulong>(ref value));
			return true;
		}

		if (typeof(T) == typeof(float))
		{
			writer.WriteNumberValue(Unsafe.As<T, float>(ref value));
			return true;
		}

		if (typeof(T) == typeof(double))
		{
			writer.WriteNumberValue(Unsafe.As<T, double>(ref value));
			return true;
		}

		if (typeof(T) == typeof(decimal))
		{
			writer.WriteNumberValue(Unsafe.As<T, decimal>(ref value));
			return true;
		}

		if (typeof(T) == typeof(BigInteger))
		{
			writer.WriteRawValue(Unsafe.As<T, BigInteger>(ref value).ToString(CultureInfo.InvariantCulture));
			return true;
		}

		if (typeof(T) == typeof(DateTime))
		{
			writer.WriteStringValue(Unsafe.As<T, DateTime>(ref value).ToString("O", CultureInfo.InvariantCulture));
			return true;
		}

		if (typeof(T) == typeof(DateTimeOffset))
		{
#if NET8_0_OR_GREATER
			writer.WriteAsciiFormattedString(Unsafe.As<T, DateTimeOffset>(ref value), maxLength: 33, format: "O");
#else
			writer.WriteStringValue(Unsafe.As<T, DateTimeOffset>(ref value).ToString("O", CultureInfo.InvariantCulture));
#endif
			return true;
		}

		if (typeof(T) == typeof(TimeSpan))
		{
			writer.WriteStringValue(Unsafe.As<T, TimeSpan>(ref value).ToString("c", CultureInfo.InvariantCulture));
			return true;
		}

		if (typeof(T) == typeof(Guid))
		{
#if NET8_0_OR_GREATER
			writer.WriteAsciiFormattedString(Unsafe.As<T, Guid>(ref value), maxLength: 36, format: "D");
#else
			writer.WriteStringValue(Unsafe.As<T, Guid>(ref value).ToString("D", CultureInfo.InvariantCulture));
#endif
			return true;
		}

		if (typeof(T) == typeof(Version))
		{
			writer.WriteStringValue(((Version?)(object?)value)?.ToString());
			return true;
		}

		if (typeof(T) == typeof(Uri))
		{
			writer.WriteStringValue(((Uri?)(object?)value)?.OriginalString);
			return true;
		}

		if (typeof(T) == typeof(CultureInfo))
		{
			writer.WriteStringValue(((CultureInfo?)(object?)value)?.Name);
			return true;
		}

		if (typeof(T) == typeof(Encoding))
		{
			writer.WriteStringValue(((Encoding?)(object?)value)?.WebName);
			return true;
		}

		if (typeof(T) == typeof(byte[]))
		{
			writer.WriteBase64StringValue((byte[]?)(object?)value);
			return true;
		}

		if (typeof(T) == typeof(Memory<byte>))
		{
			writer.WriteBase64StringValue(Unsafe.As<T, Memory<byte>>(ref value).Span);
			return true;
		}

		if (typeof(T) == typeof(ReadOnlyMemory<byte>))
		{
			writer.WriteBase64StringValue(Unsafe.As<T, ReadOnlyMemory<byte>>(ref value).Span);
			return true;
		}

		if (typeof(T) == typeof(Color))
		{
			writer.WriteNumberValue(Unsafe.As<T, Color>(ref value).ToArgb());
			return true;
		}

		if (typeof(T) == typeof(Point))
		{
			Point point = Unsafe.As<T, Point>(ref value);
			writer.WriteStartArray();
			writer.WriteNumberValue(point.X);
			writer.WriteValueSeparator();
			writer.WriteNumberValue(point.Y);
			writer.WriteEndArray();
			return true;
		}

#if NET8_0_OR_GREATER
		if (typeof(T) == typeof(Half))
		{
			writer.WriteRawValue(Unsafe.As<T, Half>(ref value).ToString(CultureInfo.InvariantCulture));
			return true;
		}

		if (typeof(T) == typeof(Int128))
		{
			writer.WriteRawValue(Unsafe.As<T, Int128>(ref value).ToString(CultureInfo.InvariantCulture));
			return true;
		}

		if (typeof(T) == typeof(UInt128))
		{
			writer.WriteRawValue(Unsafe.As<T, UInt128>(ref value).ToString(CultureInfo.InvariantCulture));
			return true;
		}

		if (typeof(T) == typeof(DateOnly))
		{
			writer.WriteStringValue(Unsafe.As<T, DateOnly>(ref value).ToString("O", CultureInfo.InvariantCulture));
			return true;
		}

		if (typeof(T) == typeof(TimeOnly))
		{
			writer.WriteStringValue(Unsafe.As<T, TimeOnly>(ref value).ToString("O", CultureInfo.InvariantCulture));
			return true;
		}

		if (typeof(T) == typeof(Rune))
		{
			writer.WriteStringValue(Unsafe.As<T, Rune>(ref value).ToString());
			return true;
		}
#endif

		return false;
	}

	internal static bool TryDeserialize<T>(ref JsonReader reader, SerializationContext context, out T value)
	{
		// See the remarks on TrySerialize regarding the use of Unsafe.As instead of a box/unbox
		// pattern to avoid a heap allocation for every value-type property read.
		// `value` must be definitely assigned before its address can be taken below.
		value = default!;

		if (typeof(T) == typeof(string))
		{
			object? result = reader.ReadString(context.StringInterningCache);
			value = (T?)result!;
			return true;
		}

		if (typeof(T) == typeof(char))
		{
			Unsafe.As<T, char>(ref value) = reader.ReadChar();
			return true;
		}

		if (typeof(T) == typeof(bool))
		{
			Unsafe.As<T, bool>(ref value) = reader.ReadBoolean();
			return true;
		}

		if (typeof(T) == typeof(byte))
		{
			Unsafe.As<T, byte>(ref value) = reader.ReadByteValue();
			return true;
		}

		if (typeof(T) == typeof(sbyte))
		{
			Unsafe.As<T, sbyte>(ref value) = reader.ReadSByteValue();
			return true;
		}

		if (typeof(T) == typeof(short))
		{
			Unsafe.As<T, short>(ref value) = reader.ReadInt16Value();
			return true;
		}

		if (typeof(T) == typeof(ushort))
		{
			Unsafe.As<T, ushort>(ref value) = reader.ReadUInt16Value();
			return true;
		}

		if (typeof(T) == typeof(int))
		{
			Unsafe.As<T, int>(ref value) = reader.ReadInt32Value();
			return true;
		}

		if (typeof(T) == typeof(uint))
		{
			Unsafe.As<T, uint>(ref value) = reader.ReadUInt32Value();
			return true;
		}

		if (typeof(T) == typeof(long))
		{
			Unsafe.As<T, long>(ref value) = reader.ReadInt64Value();
			return true;
		}

		if (typeof(T) == typeof(ulong))
		{
			Unsafe.As<T, ulong>(ref value) = reader.ReadUInt64Value();
			return true;
		}

		if (typeof(T) == typeof(float))
		{
			Unsafe.As<T, float>(ref value) = reader.ReadSingleValue();
			return true;
		}

		if (typeof(T) == typeof(double))
		{
			Unsafe.As<T, double>(ref value) = reader.ReadDoubleValue();
			return true;
		}

		if (typeof(T) == typeof(decimal))
		{
			Unsafe.As<T, decimal>(ref value) = reader.ReadDecimalValue();
			return true;
		}

		if (typeof(T) == typeof(BigInteger))
		{
			Unsafe.As<T, BigInteger>(ref value) = BigInteger.Parse(reader.ReadNumberToken(), CultureInfo.InvariantCulture);
			return true;
		}

		if (typeof(T) == typeof(DateTime))
		{
			Unsafe.As<T, DateTime>(ref value) = DateTime.ParseExact(reader.ReadRequiredString(), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			return true;
		}

		if (typeof(T) == typeof(DateTimeOffset))
		{
			Unsafe.As<T, DateTimeOffset>(ref value) = DateTimeOffset.ParseExact(reader.ReadRequiredString(), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			return true;
		}

		if (typeof(T) == typeof(TimeSpan))
		{
			Unsafe.As<T, TimeSpan>(ref value) = TimeSpan.ParseExact(reader.ReadRequiredString(), "c", CultureInfo.InvariantCulture);
			return true;
		}

		if (typeof(T) == typeof(Guid))
		{
			Unsafe.As<T, Guid>(ref value) = Guid.ParseExact(reader.ReadRequiredString(), "D");
			return true;
		}

		if (typeof(T) == typeof(Version))
		{
			object? result = reader.TryReadNull() ? null : new Version(reader.ReadRequiredString());
			value = (T?)result!;
			return true;
		}

		if (typeof(T) == typeof(Uri))
		{
			object? result = reader.TryReadNull() ? null : new Uri(reader.ReadRequiredString(), UriKind.RelativeOrAbsolute);
			value = (T?)result!;
			return true;
		}

		if (typeof(T) == typeof(CultureInfo))
		{
			object? result = reader.TryReadNull() ? null : CultureInfo.GetCultureInfo(reader.ReadRequiredString());
			value = (T?)result!;
			return true;
		}

		if (typeof(T) == typeof(Encoding))
		{
			object? result = reader.TryReadNull() ? null : Encoding.GetEncoding(reader.ReadRequiredString());
			value = (T?)result!;
			return true;
		}

		if (typeof(T) == typeof(byte[]))
		{
			object? result = reader.ReadBase64Bytes();
			value = (T?)result!;
			return true;
		}

		if (typeof(T) == typeof(Memory<byte>))
		{
			Unsafe.As<T, Memory<byte>>(ref value) = new Memory<byte>(reader.ReadRequiredBase64Bytes());
			return true;
		}

		if (typeof(T) == typeof(ReadOnlyMemory<byte>))
		{
			Unsafe.As<T, ReadOnlyMemory<byte>>(ref value) = new ReadOnlyMemory<byte>(reader.ReadRequiredBase64Bytes());
			return true;
		}

		if (typeof(T) == typeof(Color))
		{
			Unsafe.As<T, Color>(ref value) = Color.FromArgb(reader.ReadInt32Value());
			return true;
		}

		if (typeof(T) == typeof(Point))
		{
			reader.ReadStartArray();
			int x = reader.ReadInt32Value();
			reader.ReadValueSeparator();
			int y = reader.ReadInt32Value();
			reader.ReadEndArray();
			Unsafe.As<T, Point>(ref value) = new Point(x, y);
			return true;
		}

#if NET8_0_OR_GREATER
		if (typeof(T) == typeof(Half))
		{
			Unsafe.As<T, Half>(ref value) = Half.Parse(reader.ReadNumberToken(), CultureInfo.InvariantCulture);
			return true;
		}

		if (typeof(T) == typeof(Int128))
		{
			Unsafe.As<T, Int128>(ref value) = Int128.Parse(reader.ReadNumberToken(), CultureInfo.InvariantCulture);
			return true;
		}

		if (typeof(T) == typeof(UInt128))
		{
			Unsafe.As<T, UInt128>(ref value) = UInt128.Parse(reader.ReadNumberToken(), CultureInfo.InvariantCulture);
			return true;
		}

		if (typeof(T) == typeof(DateOnly))
		{
			Unsafe.As<T, DateOnly>(ref value) = DateOnly.ParseExact(reader.ReadRequiredString(), "O", CultureInfo.InvariantCulture);
			return true;
		}

		if (typeof(T) == typeof(TimeOnly))
		{
			Unsafe.As<T, TimeOnly>(ref value) = TimeOnly.ParseExact(reader.ReadRequiredString(), "O", CultureInfo.InvariantCulture);
			return true;
		}

		if (typeof(T) == typeof(Rune))
		{
			string runeText = reader.ReadRequiredString();
			Unsafe.As<T, Rune>(ref value) = Rune.GetRuneAt(runeText, 0);
			return true;
		}
#endif

		value = default!;
		return false;
	}
}
