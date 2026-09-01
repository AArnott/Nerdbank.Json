// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// An immutable description of a polymorphic union for the base type <typeparamref name="TBase"/>, built from
/// explicitly supplied case type shapes.
/// </summary>
/// <typeparam name="TBase">The base type of the union.</typeparam>
/// <remarks>
/// Use <see cref="JsonUnionConfiguration.WithUnion{TBase}(JsonUnion{TBase})"/> to apply the definition to a serializer.
/// </remarks>
public sealed class JsonUnion<TBase>
{
	private readonly IReadOnlyList<RuntimeUnionCase<TBase>> cases;

	private JsonUnion(IReadOnlyList<RuntimeUnionCase<TBase>> cases, bool includeGeneratedCases, bool duckTyping)
	{
		this.cases = cases;
		this.IncludeGeneratedCases = includeGeneratedCases;
		this.DuckTyping = duckTyping;
	}

	/// <summary>Gets the runtime-added cases.</summary>
	internal IReadOnlyList<RuntimeUnionCase<TBase>> Cases => this.cases;

	/// <summary>Gets a value indicating whether shape-generated cases are also included.</summary>
	internal bool IncludeGeneratedCases { get; }

	/// <summary>Gets a value indicating whether the discriminator-free duck-typing strategy is used.</summary>
	internal bool DuckTyping { get; }

	/// <summary>
	/// Creates an empty union definition that replaces the shape-generated cases with only the cases added to it.
	/// </summary>
	/// <returns>A new definition.</returns>
	public static JsonUnion<TBase> Create() => new([], includeGeneratedCases: false, duckTyping: false);

	/// <summary>
	/// Returns a definition that also includes the shape-generated cases (if any) in addition to the added cases.
	/// </summary>
	/// <returns>A new definition.</returns>
	public JsonUnion<TBase> IncludeGenerated() => new(this.cases, includeGeneratedCases: true, this.DuckTyping);

	/// <summary>
	/// Returns a definition that uses the experimental discriminator-free "duck typing" strategy, which selects a case
	/// during deserialization from the unique set of required serialized property names present in a JSON object.
	/// </summary>
	/// <returns>A new definition.</returns>
	/// <remarks>
	/// Duck typing does not use the <c>[discriminator, payload]</c> envelope: values are written as the bare case
	/// object, and read by matching required property names. It is slower than the default strategy because it buffers
	/// and re-scans each object, it rejects ambiguous or insufficient evidence, and it cannot use optional properties.
	/// </remarks>
	public JsonUnion<TBase> UseDuckTyping() => new(this.cases, this.IncludeGeneratedCases, duckTyping: true);

	/// <summary>
	/// Returns a definition with an additional case identified by an integer tag.
	/// </summary>
	/// <typeparam name="TCase">The case type.</typeparam>
	/// <param name="tag">The integer discriminator for this case.</param>
	/// <param name="caseShape">The shape for the case type.</param>
	/// <returns>A new definition.</returns>
	public JsonUnion<TBase> AddCase<TCase>(int tag, ITypeShape<TCase> caseShape)
		where TCase : TBase
	{
		Requires.NotNull(caseShape);
		return new(Append(this.cases, new RuntimeUnionCase<TBase, TCase>(caseShape, tag, null)), this.IncludeGeneratedCases, this.DuckTyping);
	}

	/// <summary>
	/// Returns a definition with an additional case identified by a string name.
	/// </summary>
	/// <typeparam name="TCase">The case type.</typeparam>
	/// <param name="name">The string discriminator for this case.</param>
	/// <param name="caseShape">The shape for the case type.</param>
	/// <returns>A new definition.</returns>
	public JsonUnion<TBase> AddCase<TCase>(string name, ITypeShape<TCase> caseShape)
		where TCase : TBase
	{
		Requires.NotNull(name);
		Requires.NotNull(caseShape);
		return new(Append(this.cases, new RuntimeUnionCase<TBase, TCase>(caseShape, null, name)), this.IncludeGeneratedCases, this.DuckTyping);
	}

	private static IReadOnlyList<RuntimeUnionCase<TBase>> Append(IReadOnlyList<RuntimeUnionCase<TBase>> existing, RuntimeUnionCase<TBase> added)
	{
		RuntimeUnionCase<TBase>[] result = new RuntimeUnionCase<TBase>[existing.Count + 1];
		for (int i = 0; i < existing.Count; i++)
		{
			result[i] = existing[i];
		}

		result[existing.Count] = added;
		return result;
	}
}
