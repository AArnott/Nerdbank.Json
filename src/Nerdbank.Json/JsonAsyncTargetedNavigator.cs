// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal navigator members are intentionally undocumented in this file.

namespace Nerdbank.Json;

/// <summary>
/// Navigates a <see cref="JsonAsyncReader"/> through a <see cref="JsonPath"/> incrementally, without buffering the
/// enclosing document, leaving the reader positioned at the selected value. Per-segment converters supply their
/// <see cref="JsonConverter.TryNavigateAsync(JsonAsyncReader, JsonNavigationSegment, JsonNavigationOptions, SerializationContext)"/>
/// hook so navigation can traverse through unions and custom representations.
/// </summary>
/// <remarks>
/// Methods return the number of JSON containers left open on the path to the selected value (so the caller can drain
/// them), or a negative value when the value is absent.
/// </remarks>
internal static class JsonAsyncTargetedNavigator
{
	internal static async ValueTask<int> TryNavigateAsync(JsonAsyncReader reader, JsonPath path, JsonConverter?[]? converters, JsonNavigationOptions options, SerializationContext context)
	{
		JsonPathSegment[] segments = path.SegmentArray;
		int openContainers = 0;
		for (int i = 0; i < segments.Length; i++)
		{
			context.DepthStep();
			if (await reader.TryReadNullAsync(context).ConfigureAwait(false))
			{
				return -1;
			}

			JsonNavigationSegment segment = new(in segments[i]);
			JsonConverter? converter = converters is not null ? converters[i] : null;
			int opened = converter is not null
				? await converter.TryNavigateAsync(reader, segment, options, context).ConfigureAwait(false)
				: await NavigateRawTokenAsync(reader, segment, options, context).ConfigureAwait(false);
			if (opened < 0)
			{
				return -1;
			}

			openContainers += opened;
		}

		return openContainers;
	}

	internal static ValueTask<int> NavigateRawTokenAsync(JsonAsyncReader reader, JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
		=> segment.IsIndex
			? NavigateIndexAsync(reader, segment.Index, context)
			: NavigateMemberAsync(reader, segment, options, context);

	private static async ValueTask<int> NavigateMemberAsync(JsonAsyncReader reader, JsonNavigationSegment segment, JsonNavigationOptions options, SerializationContext context)
	{
		int token = await reader.PeekNextByteAsync().ConfigureAwait(false);
		if (token != '{')
		{
			throw new NotSupportedException($"Cannot navigate to member '{segment.Name}' because the value is not a plain JSON object (its first token is '{(token < 0 ? "end of stream" : (char)token)}'). If the value uses a union envelope or a custom-converter representation, use the expression-based overload, which supplies the converter metadata required to traverse it.");
		}

		await reader.ReadStartObjectAsync(context).ConfigureAwait(false);
		if (await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
		{
			return -1;
		}

		while (true)
		{
			string name = await reader.ReadPropertyNameAsync(context).ConfigureAwait(false);
			if (options.NameComparer.Equals(name, segment.Name))
			{
				return 1;
			}

			await reader.SkipValueAsync(context).ConfigureAwait(false);
			if (await reader.TryReadEndObjectAsync(context).ConfigureAwait(false))
			{
				return -1;
			}

			await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
		}
	}

	private static async ValueTask<int> NavigateIndexAsync(JsonAsyncReader reader, int index, SerializationContext context)
	{
		int token = await reader.PeekNextByteAsync().ConfigureAwait(false);
		if (token != '[')
		{
			throw new NotSupportedException($"Cannot navigate to index {index} because the value is not a plain JSON array (its first token is '{(token < 0 ? "end of stream" : (char)token)}'). If the value uses a union envelope or a custom-converter representation, use the expression-based overload, which supplies the converter metadata required to traverse it.");
		}

		await reader.ReadStartArrayAsync(context).ConfigureAwait(false);
		if (await reader.TryReadEndArrayAsync(context).ConfigureAwait(false))
		{
			return -1;
		}

		int current = 0;
		while (true)
		{
			if (current == index)
			{
				return 1;
			}

			await reader.SkipValueAsync(context).ConfigureAwait(false);
			current++;
			if (await reader.TryReadEndArrayAsync(context).ConfigureAwait(false))
			{
				return -1;
			}

			await reader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
		}
	}
}
