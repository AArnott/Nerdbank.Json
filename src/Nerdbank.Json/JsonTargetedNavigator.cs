// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal navigator members are intentionally undocumented in this file.

namespace Nerdbank.Json;

/// <summary>
/// Navigates a <see cref="JsonReader"/> through a <see cref="JsonPath"/>, skipping unrelated JSON and leaving the
/// reader positioned at the selected value. When a per-segment converter is available, its
/// <see cref="JsonConverter.TryNavigate(ref JsonReader, in JsonNavigationSegment, JsonNavigationOptions)"/> hook is
/// used so navigation can traverse through unions and custom representations.
/// </summary>
internal static class JsonTargetedNavigator
{
	internal static bool TryNavigate(ref JsonReader reader, JsonPath path, JsonConverter?[]? converters, JsonNavigationOptions options, ref SerializationContext context)
	{
		ReadOnlySpan<JsonPathSegment> segments = path.Segments;
		for (int i = 0; i < segments.Length; i++)
		{
			context.DepthStep();
			if (reader.TryReadNull())
			{
				return false;
			}

			JsonNavigationSegment segment = new(in segments[i]);
			JsonConverter? converter = converters is not null ? converters[i] : null;
			bool found = converter is not null
				? converter.TryNavigate(ref reader, in segment, options)
				: NavigateRawToken(ref reader, in segment, options);
			if (!found)
			{
				return false;
			}
		}

		return true;
	}

	internal static bool NavigateRawToken(ref JsonReader reader, in JsonNavigationSegment segment, JsonNavigationOptions options)
		=> segment.IsIndex
			? NavigateIndex(ref reader, segment.Index)
			: NavigateMember(ref reader, in segment, options);

	private static bool NavigateMember(ref JsonReader reader, in JsonNavigationSegment segment, JsonNavigationOptions options)
	{
		char token = reader.PeekValueToken();
		if (token != '{')
		{
			throw new NotSupportedException($"Cannot navigate to member '{segment.Name}' because the value is not a plain JSON object (its first token is '{token}'). If the value uses a union envelope or a custom-converter representation, use the expression-based DeserializeAt overload, which supplies the converter metadata required to traverse it.");
		}

		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			return false;
		}

		while (true)
		{
			if (MatchName(ref reader, in segment, options))
			{
				return true;
			}

			reader.SkipValue();
			if (reader.TryReadEndObject())
			{
				return false;
			}

			reader.ReadValueSeparator();
		}
	}

	private static bool MatchName(ref JsonReader reader, in JsonNavigationSegment segment, JsonNavigationOptions options)
	{
		if (!options.IgnoreCase && reader.TryReadUnescapedUtf8StringToken(out ReadOnlySpan<byte> token))
		{
			reader.ReadNameSeparator();
			return token.Length >= 2 && token[1..^1].SequenceEqual(segment.Utf8Name);
		}

		string name = reader.ReadRequiredString();
		reader.ReadNameSeparator();
		return options.NameComparer.Equals(name, segment.Name);
	}

	private static bool NavigateIndex(ref JsonReader reader, int index)
	{
		char token = reader.PeekValueToken();
		if (token != '[')
		{
			throw new NotSupportedException($"Cannot navigate to index {index} because the value is not a plain JSON array (its first token is '{token}'). If the value uses a union envelope or a custom-converter representation, use the expression-based DeserializeAt overload, which supplies the converter metadata required to traverse it.");
		}

		reader.ReadStartArray();
		if (reader.TryReadEndArray())
		{
			return false;
		}

		int current = 0;
		while (true)
		{
			if (current == index)
			{
				return true;
			}

			reader.SkipValue();
			current++;
			if (reader.TryReadEndArray())
			{
				return false;
			}

			reader.ReadValueSeparator();
		}
	}
}
