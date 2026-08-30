// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal navigator members are intentionally undocumented in this file.

namespace Nerdbank.Json;

/// <summary>
/// Navigates a <see cref="JsonReader"/> through a <see cref="JsonPath"/>, skipping unrelated JSON and leaving the
/// reader positioned at the selected value.
/// </summary>
internal static class JsonTargetedNavigator
{
	internal static bool TryNavigate(ref JsonReader reader, JsonPath path, bool ignoreCase, StringComparer comparer, ref SerializationContext context)
	{
		ReadOnlySpan<JsonPathSegment> segments = path.Segments;
		for (int i = 0; i < segments.Length; i++)
		{
			context.DepthStep();
			if (reader.TryReadNull())
			{
				return false;
			}

			JsonPathSegment segment = segments[i];
			bool found = segment.Kind == JsonPathSegmentKind.Member
				? TryNavigateMember(ref reader, segment, ignoreCase, comparer)
				: TryNavigateIndex(ref reader, segment.Index);
			if (!found)
			{
				return false;
			}
		}

		return true;
	}

	private static bool TryNavigateMember(ref JsonReader reader, JsonPathSegment segment, bool ignoreCase, StringComparer comparer)
	{
		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			return false;
		}

		while (true)
		{
			if (MatchName(ref reader, segment, ignoreCase, comparer))
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

	private static bool MatchName(ref JsonReader reader, JsonPathSegment segment, bool ignoreCase, StringComparer comparer)
	{
		if (!ignoreCase && reader.TryReadUnescapedUtf8StringToken(out ReadOnlySpan<byte> token))
		{
			reader.ReadNameSeparator();
			return token.Length >= 2 && token[1..^1].SequenceEqual(segment.Utf8Name);
		}

		string name = reader.ReadRequiredString();
		reader.ReadNameSeparator();
		return comparer.Equals(name, segment.Name);
	}

	private static bool TryNavigateIndex(ref JsonReader reader, int index)
	{
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
