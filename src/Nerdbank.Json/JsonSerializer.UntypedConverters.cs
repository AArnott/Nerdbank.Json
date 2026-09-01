// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Dynamic;

namespace Nerdbank.Json;

public partial record JsonSerializer
{
	private static readonly IReadOnlyList<JsonConverter> UntypedConverterList =
	[
		ObjectConverter.Instance,
		ExpandoObjectConverter.Instance,
		JsonValueConverter.Instance,
	];

	private static readonly IReadOnlyList<JsonConverter> SystemTextJsonConverterList =
	[
		SystemTextJsonElementConverter.Instance,
		SystemTextJsonNodeConverter.Instance,
		SystemTextJsonDocumentConverter.Instance,
	];

	/// <summary>
	/// Creates a copy of this serializer with opt-in converters registered for untyped JSON: the CLR
	/// <see cref="object"/> and <see cref="ExpandoObject"/> types, along with the native <see cref="JsonValue"/>
	/// document object model when it appears as a member of another shaped type.
	/// </summary>
	/// <returns>A new <see cref="JsonSerializer"/> instance with the untyped converters registered.</returns>
	/// <remarks>
	/// <para>
	/// These converters use no reflection and root no additional dependencies. Because they are registered only when you
	/// call this method, the default serializer remains trimming- and NativeAOT-safe and never discovers arbitrary CLR
	/// types.
	/// </para>
	/// <para>
	/// A member typed as <see cref="object"/> deserializes to a boxed <see cref="JsonValue"/>. Serializing an
	/// <see cref="object"/> requires a <see cref="JsonValue"/> or a boxed JSON primitive; any other CLR type throws a
	/// <see cref="NotSupportedException"/> that guides you to use a generated type shape instead.
	/// </para>
	/// </remarks>
	public JsonSerializer WithUntypedConverters()
		=> this with { Converters = MergeConverters(this.Converters, UntypedConverterList) };

	/// <summary>
	/// Creates a copy of this serializer with opt-in converters registered for the
	/// <see cref="System.Text.Json.JsonElement"/>, <see cref="System.Text.Json.Nodes.JsonNode"/>, and
	/// <see cref="System.Text.Json.JsonDocument"/> types.
	/// </summary>
	/// <returns>A new <see cref="JsonSerializer"/> instance with the System.Text.Json interop converters registered.</returns>
	/// <remarks>
	/// <para>
	/// These converters bridge to <c>System.Text.Json</c> by round-tripping raw JSON text, so they add no reflection.
	/// However, calling this method roots the <c>System.Text.Json</c> assembly. Because the converters are registered
	/// only when you call this method, the default serializer keeps <c>System.Text.Json</c> trimmable and out of the
	/// NativeAOT closure.
	/// </para>
	/// </remarks>
	public JsonSerializer WithSystemTextJsonConverters()
		=> this with { Converters = MergeConverters(this.Converters, SystemTextJsonConverterList) };

	private static ConverterCollection MergeConverters(ConverterCollection existing, IReadOnlyList<JsonConverter> additions)
	{
		HashSet<Type> added = [.. additions.Select(static c => c.DataType)];
		List<JsonConverter> merged = [.. additions];
		foreach (JsonConverter converter in existing)
		{
			if (!added.Contains(converter.DataType))
			{
				merged.Add(converter);
			}
		}

		return new ConverterCollection(merged);
	}
}
