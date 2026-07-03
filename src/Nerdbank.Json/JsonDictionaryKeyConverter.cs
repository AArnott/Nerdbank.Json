// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

using System.Globalization;
using System.Numerics;
using System.Text;

namespace Nerdbank.Json;

internal static class JsonDictionaryKeyConverter
{
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
			|| type.IsEnum
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

	internal static string FormatKey<TKey>(TKey key)
	{
		if (key is null)
		{
			throw new NotSupportedException("JSON serialization does not support null dictionary keys.");
		}

		Type type = typeof(TKey);
		object boxed = key;

		if (type == typeof(string))
		{
			return (string)boxed;
		}

		if (type == typeof(char))
		{
			return new string((char)boxed, 1);
		}

		if (type == typeof(bool))
		{
			return ((bool)boxed) ? "true" : "false";
		}

		if (type == typeof(byte))
		{
			return ((byte)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(sbyte))
		{
			return ((sbyte)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(short))
		{
			return ((short)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(ushort))
		{
			return ((ushort)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(int))
		{
			return ((int)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(uint))
		{
			return ((uint)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(long))
		{
			return ((long)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(ulong))
		{
			return ((ulong)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(float))
		{
			return ((float)boxed).ToString("R", CultureInfo.InvariantCulture);
		}

		if (type == typeof(double))
		{
			return ((double)boxed).ToString("R", CultureInfo.InvariantCulture);
		}

		if (type == typeof(decimal))
		{
			return ((decimal)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(BigInteger))
		{
			return ((BigInteger)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(DateTime))
		{
			return ((DateTime)boxed).ToString("O", CultureInfo.InvariantCulture);
		}

		if (type == typeof(DateTimeOffset))
		{
			return ((DateTimeOffset)boxed).ToString("O", CultureInfo.InvariantCulture);
		}

		if (type == typeof(TimeSpan))
		{
			return ((TimeSpan)boxed).ToString("c", CultureInfo.InvariantCulture);
		}

		if (type == typeof(Guid))
		{
			return ((Guid)boxed).ToString("D");
		}

		if (type.IsEnum)
		{
			return boxed.ToString()!;
		}

#if NET8_0_OR_GREATER
		if (type == typeof(Half))
		{
			return ((Half)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(Int128))
		{
			return ((Int128)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(UInt128))
		{
			return ((UInt128)boxed).ToString(CultureInfo.InvariantCulture);
		}

		if (type == typeof(DateOnly))
		{
			return ((DateOnly)boxed).ToString("O", CultureInfo.InvariantCulture);
		}

		if (type == typeof(TimeOnly))
		{
			return ((TimeOnly)boxed).ToString("O", CultureInfo.InvariantCulture);
		}

		if (type == typeof(Rune))
		{
			return ((Rune)boxed).ToString();
		}
#endif

		throw CreateNotSupportedException(type);
	}

	internal static TKey ParseKey<TKey>(string key)
	{
		Type type = typeof(TKey);
		object result;

		if (type == typeof(string))
		{
			result = key;
			return (TKey)result;
		}

		if (type == typeof(char))
		{
			if (key.Length != 1)
			{
				throw new FormatException("Expected a single-character dictionary key.");
			}

			result = key[0];
			return (TKey)result;
		}

		if (type == typeof(bool))
		{
			result = bool.Parse(key);
			return (TKey)result;
		}

		if (type == typeof(byte))
		{
			result = byte.Parse(key, NumberStyles.Integer, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(sbyte))
		{
			result = sbyte.Parse(key, NumberStyles.Integer, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(short))
		{
			result = short.Parse(key, NumberStyles.Integer, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(ushort))
		{
			result = ushort.Parse(key, NumberStyles.Integer, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(int))
		{
			result = int.Parse(key, NumberStyles.Integer, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(uint))
		{
			result = uint.Parse(key, NumberStyles.Integer, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(long))
		{
			result = long.Parse(key, NumberStyles.Integer, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(ulong))
		{
			result = ulong.Parse(key, NumberStyles.Integer, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(float))
		{
			result = float.Parse(key, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(double))
		{
			result = double.Parse(key, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(decimal))
		{
			result = decimal.Parse(key, NumberStyles.Float, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(BigInteger))
		{
			result = BigInteger.Parse(key, NumberStyles.Integer, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(DateTime))
		{
			result = DateTime.ParseExact(key, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			return (TKey)result;
		}

		if (type == typeof(DateTimeOffset))
		{
			result = DateTimeOffset.ParseExact(key, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
			return (TKey)result;
		}

		if (type == typeof(TimeSpan))
		{
			result = TimeSpan.ParseExact(key, "c", CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(Guid))
		{
			result = Guid.ParseExact(key, "D");
			return (TKey)result;
		}

		if (type.IsEnum)
		{
			return (TKey)Enum.Parse(type, key, ignoreCase: true);
		}

#if NET8_0_OR_GREATER
		if (type == typeof(Half))
		{
			result = Half.Parse(key, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(Int128))
		{
			result = Int128.Parse(key, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(UInt128))
		{
			result = UInt128.Parse(key, CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(DateOnly))
		{
			result = DateOnly.ParseExact(key, "O", CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(TimeOnly))
		{
			result = TimeOnly.ParseExact(key, "O", CultureInfo.InvariantCulture);
			return (TKey)result;
		}

		if (type == typeof(Rune))
		{
			if (!Rune.TryGetRuneAt(key, 0, out Rune rune) || rune.Utf16SequenceLength != key.Length)
			{
				throw new FormatException("Expected a single-rune dictionary key.");
			}

			result = rune;
			return (TKey)result;
		}
#endif

		throw CreateNotSupportedException(type);
	}

	internal static NotSupportedException CreateNotSupportedException(Type type)
		=> new($"JSON serialization does not yet support dictionary keys of type {type.FullName}.");
}
