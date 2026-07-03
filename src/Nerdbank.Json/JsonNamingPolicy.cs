// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Nerdbank.Json;

/// <summary>
/// Defines a transformation for property names from .NET to JSON.
/// </summary>
public abstract class JsonNamingPolicy
{
	/// <summary>
	/// Gets a naming policy that converts identifiers to camelCase.
	/// </summary>
	public static JsonNamingPolicy CamelCase { get; } = new CamelCaseNamingPolicy();

	/// <summary>
	/// Gets a naming policy that converts identifiers to PascalCase.
	/// </summary>
	public static JsonNamingPolicy PascalCase { get; } = new PascalCaseNamingPolicy();

	/// <summary>
	/// Gets a naming policy that converts identifiers to lower-case kebab-case.
	/// </summary>
	public static JsonNamingPolicy KebabLowerCase { get; } = new KebabLowerCaseNamingPolicy();

	/// <summary>
	/// Gets a naming policy that converts identifiers to upper-case kebab-case.
	/// </summary>
	public static JsonNamingPolicy KebabUpperCase { get; } = new KebabUpperCaseNamingPolicy();

	/// <summary>
	/// Gets a naming policy that converts identifiers to snake_case.
	/// </summary>
	public static JsonNamingPolicy SnakeLowerCase { get; } = new SnakeLowerCaseNamingPolicy();

	/// <summary>
	/// Gets a naming policy that converts identifiers to SNAKE_CASE.
	/// </summary>
	public static JsonNamingPolicy SnakeUpperCase { get; } = new SnakeUpperCaseNamingPolicy();

	/// <summary>
	/// Transforms a .NET property name to the serialized JSON property name.
	/// </summary>
	/// <param name="name">The .NET property name.</param>
	/// <returns>The JSON property name.</returns>
	public abstract string ConvertName(string name);

	private abstract class BuiltInPolicy : JsonNamingPolicy
	{
		private const int StackallocByteThreshold = 256;
		private const int StackallocCharThreshold = StackallocByteThreshold / 2;

		private readonly bool lowercase;
		private readonly bool capitalizeFirstLetterOfSubsequentWords;
		private readonly bool capitalizeFirstLetterOfFirstWord;
		private readonly char? separator;

		internal BuiltInPolicy(bool lowercase, bool capitalizeFirstLetterOfSubsequentWords = false, bool capitalizeFirstLetterOfFirstWord = false, char? separator = null)
		{
			Debug.Assert(separator is null || char.IsPunctuation(separator.Value), "Separator is expected to be punctuation.");

			this.lowercase = lowercase;
			this.separator = separator;
			this.capitalizeFirstLetterOfFirstWord = capitalizeFirstLetterOfFirstWord;
			this.capitalizeFirstLetterOfSubsequentWords = capitalizeFirstLetterOfSubsequentWords;
		}

		private enum SeparatorState
		{
			NotStarted,
			UppercaseLetter,
			LowercaseLetterOrDigit,
			SpaceSeparator,
		}

		/// <inheritdoc/>
		public sealed override string ConvertName(string name)
		{
			Requires.NotNull(name);

			return ConvertNameCore(this.separator, this.lowercase, this.capitalizeFirstLetterOfSubsequentWords, this.capitalizeFirstLetterOfFirstWord, name.AsSpan());
		}

		private static string ConvertNameCore(char? separator, bool lowercase, bool capitalizeFirstLetterOfSubsequentWords, bool capitalizeFirstLetterOfFirstWord, ReadOnlySpan<char> chars)
		{
			char[]? rentedBuffer = null;
			int initialBufferLength = (int)(1.2 * chars.Length);
			Span<char> destination = initialBufferLength <= StackallocCharThreshold
				? stackalloc char[StackallocCharThreshold]
				: (rentedBuffer = ArrayPool<char>.Shared.Rent(initialBufferLength));

			SeparatorState state = SeparatorState.NotStarted;
			int charsWritten = 0;
			bool nextCharacterStartsFirstWord = true;
			bool nextCharacterStartsSubsequentWord = false;

			for (int i = 0; i < chars.Length; i++)
			{
				char current = chars[i];
				UnicodeCategory category = char.GetUnicodeCategory(current);

				switch (category)
				{
					case UnicodeCategory.UppercaseLetter:
						switch (state)
						{
							case SeparatorState.NotStarted:
								break;
							case SeparatorState.LowercaseLetterOrDigit:
							case SeparatorState.SpaceSeparator:
								if (separator.HasValue)
								{
									WriteChar(separator.Value, ref destination, ref charsWritten, ref rentedBuffer);
								}

								nextCharacterStartsSubsequentWord = true;
								break;
							case SeparatorState.UppercaseLetter:
								if (i + 1 < chars.Length && char.IsLower(chars[i + 1]))
								{
									if (separator.HasValue)
									{
										WriteChar(separator.Value, ref destination, ref charsWritten, ref rentedBuffer);
									}

									nextCharacterStartsSubsequentWord = true;
								}

								break;
						}

						if (lowercase && !((nextCharacterStartsSubsequentWord && capitalizeFirstLetterOfSubsequentWords) || (nextCharacterStartsFirstWord && capitalizeFirstLetterOfFirstWord)))
						{
							current = char.ToLowerInvariant(current);
						}

						WriteChar(current, ref destination, ref charsWritten, ref rentedBuffer);
						nextCharacterStartsFirstWord = false;
						nextCharacterStartsSubsequentWord = false;
						state = SeparatorState.UppercaseLetter;
						break;

					case UnicodeCategory.LowercaseLetter:
					case UnicodeCategory.DecimalDigitNumber:
						if (state is SeparatorState.SpaceSeparator)
						{
							if (separator.HasValue)
							{
								WriteChar(separator.Value, ref destination, ref charsWritten, ref rentedBuffer);
							}

							nextCharacterStartsSubsequentWord = true;
						}

						bool shouldBeUpperCase = !lowercase ||
							((nextCharacterStartsSubsequentWord && capitalizeFirstLetterOfSubsequentWords) ||
							(nextCharacterStartsFirstWord && capitalizeFirstLetterOfFirstWord));
						if (shouldBeUpperCase && category is UnicodeCategory.LowercaseLetter)
						{
							current = char.ToUpperInvariant(current);
						}

						WriteChar(current, ref destination, ref charsWritten, ref rentedBuffer);
						nextCharacterStartsFirstWord = false;
						nextCharacterStartsSubsequentWord = false;
						state = SeparatorState.LowercaseLetterOrDigit;
						break;

					case UnicodeCategory.SpaceSeparator:
						if (state != SeparatorState.NotStarted)
						{
							state = SeparatorState.SpaceSeparator;
						}

						break;

					default:
						WriteChar(current, ref destination, ref charsWritten, ref rentedBuffer);
						state = SeparatorState.NotStarted;
						break;
				}
			}

			string result = destination[..charsWritten].ToString();
			if (rentedBuffer is not null)
			{
				destination[..charsWritten].Clear();
				ArrayPool<char>.Shared.Return(rentedBuffer);
			}

			return result;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void WriteChar(char value, ref Span<char> destination, ref int charsWritten, ref char[]? rentedBuffer)
		{
			if (charsWritten == destination.Length)
			{
				ExpandBuffer(ref destination, ref rentedBuffer, charsWritten);
			}

			destination[charsWritten++] = value;
		}

		private static void ExpandBuffer(ref Span<char> destination, ref char[]? rentedBuffer, int charsWritten)
		{
			int newSize = checked(destination.Length * 2);
			char[] newBuffer = ArrayPool<char>.Shared.Rent(newSize);
			destination.CopyTo(newBuffer);

			if (rentedBuffer is not null)
			{
				destination[..charsWritten].Clear();
				ArrayPool<char>.Shared.Return(rentedBuffer);
			}

			rentedBuffer = newBuffer;
			destination = rentedBuffer;
		}
	}

	private sealed class CamelCaseNamingPolicy : BuiltInPolicy
	{
		internal CamelCaseNamingPolicy()
			: base(lowercase: true, capitalizeFirstLetterOfSubsequentWords: true)
		{
		}
	}

	private sealed class PascalCaseNamingPolicy : BuiltInPolicy
	{
		internal PascalCaseNamingPolicy()
			: base(lowercase: true, capitalizeFirstLetterOfFirstWord: true, capitalizeFirstLetterOfSubsequentWords: true)
		{
		}
	}

	private sealed class KebabLowerCaseNamingPolicy : BuiltInPolicy
	{
		internal KebabLowerCaseNamingPolicy()
			: base(lowercase: true, separator: '-')
		{
		}
	}

	private sealed class KebabUpperCaseNamingPolicy : BuiltInPolicy
	{
		internal KebabUpperCaseNamingPolicy()
			: base(lowercase: false, separator: '-')
		{
		}
	}

	private sealed class SnakeLowerCaseNamingPolicy : BuiltInPolicy
	{
		internal SnakeLowerCaseNamingPolicy()
			: base(lowercase: true, separator: '_')
		{
		}
	}

	private sealed class SnakeUpperCaseNamingPolicy : BuiltInPolicy
	{
		internal SnakeUpperCaseNamingPolicy()
			: base(lowercase: false, separator: '_')
		{
		}
	}
}
