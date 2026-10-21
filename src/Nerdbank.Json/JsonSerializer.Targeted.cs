// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1601 // Partial elements are documented on their primary declaration.

using System.Linq.Expressions;
using System.Text;

namespace Nerdbank.Json;

public partial record JsonSerializer
{
	/// <summary>Deserializes a value selected by an expression, leaving the reader immediately after that value.</summary>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <param name="reader">The reader positioned at the root document.</param>
	/// <param name="shape">The root type shape.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The selected value.</returns>
	public TElement? DeserializePath<TRoot, TElement>(ref JsonReader reader, ITypeShape<TRoot> shape, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(options.Path);
		Requires.NotNull(shape);
		(JsonPath path, JsonConverter?[] converters, ITypeShape<TElement> targetShape) = JsonPathExpressionParser.Parse(options.Path, shape, this.ConverterCache, this.DictionaryKeyNamingPolicy);
		return this.DeserializePathCore(ref reader, path, converters, targetShape, options.DefaultForUndiscoverablePath ? MissingPathBehavior.ReturnDefault : MissingPathBehavior.Throw, cancellationToken);
	}

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(ref JsonReader, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
	/// <param name="utf8Json">The UTF-8 document.</param>
	/// <param name="shape">The root type shape.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement>(ReadOnlyMemory<byte> utf8Json, ITypeShape<TRoot> shape, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
	{
		JsonReader reader = new(utf8Json.Span, this.AllowTrailingCommas, this.ReadCommentHandling);
		return this.DeserializePath(ref reader, shape, options, cancellationToken);
	}

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(ref JsonReader, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
	/// <param name="utf8Json">The possibly segmented UTF-8 document.</param>
	/// <param name="shape">The root type shape.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement>(scoped in ReadOnlySequence<byte> utf8Json, ITypeShape<TRoot> shape, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
	{
		JsonReader reader = new(utf8Json, this.AllowTrailingCommas, this.ReadCommentHandling);
		return this.DeserializePath(ref reader, shape, options, cancellationToken);
	}

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(ref JsonReader, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
	/// <param name="json">The JSON document.</param>
	/// <param name="shape">The root type shape.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement>(string json, ITypeShape<TRoot> shape, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(json);
		return this.DeserializePath(Encoding.UTF8.GetBytes(json).AsMemory(), shape, options, cancellationToken);
	}

	/// <inheritdoc cref="DeserializePath{TRoot, TElement}(ref JsonReader, ITypeShape{TRoot}, in DeserializePathOptions{TRoot, TElement}, CancellationToken)"/>
	/// <param name="stream">The document stream, which remains open. The stream is read to its end.</param>
	/// <param name="shape">The root type shape.</param>
	/// <param name="options">The selection options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public TElement? DeserializePath<TRoot, TElement>(Stream stream, ITypeShape<TRoot> shape, in DeserializePathOptions<TRoot, TElement> options, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		cancellationToken.ThrowIfCancellationRequested();
		using MemoryStream buffer = new();
		stream.CopyTo(buffer);
		cancellationToken.ThrowIfCancellationRequested();
		return this.DeserializePath(buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length)), shape, options, cancellationToken);
	}

	/// <summary>Deserializes a raw JSON path from a reader.</summary>
	/// <typeparam name="TValue">The selected value type.</typeparam>
	/// <param name="reader">The reader positioned at the document.</param>
	/// <param name="path">The raw path to select.</param>
	/// <param name="targetShape">The selected value shape.</param>
	/// <param name="missingBehavior">The behavior for a missing path.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The selected value.</returns>
	public TValue? DeserializePath<TValue>(ref JsonReader reader, JsonPath path, ITypeShape<TValue> targetShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		=> this.DeserializePathCore(ref reader, path, null, targetShape, missingBehavior, cancellationToken);

	/// <summary>
	/// Deserializes only the value selected by a pre-parsed <see cref="JsonPath"/>, skipping unrelated JSON.
	/// </summary>
	/// <typeparam name="TValue">The type of the selected value.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="path">The pre-parsed path that selects the value.</param>
	/// <param name="targetShape">The shape of <typeparamref name="TValue"/>.</param>
	/// <param name="missingBehavior">Controls behavior when the path is not found.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value, or the default value if the path is missing and <paramref name="missingBehavior"/> is <see cref="MissingPathBehavior.ReturnDefault"/>.</returns>
	/// <remarks>
	/// The selected value is deserialized through the full converter graph, so naming policies, custom converters, and
	/// union envelopes remain correct for the target. Path segments navigate plain JSON objects and arrays; targeted
	/// deserialization is not supported when reference preservation is enabled.
	/// </remarks>
	public TValue? DeserializePath<TValue>(string json, JsonPath path, ITypeShape<TValue> targetShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(json);
		byte[] utf8Json = Encoding.UTF8.GetBytes(json);
		JsonReader reader = new(utf8Json, this.AllowTrailingCommas, this.ReadCommentHandling);
		return this.DeserializePathCore(ref reader, path, null, targetShape, missingBehavior, cancellationToken);
	}

	/// <summary>
	/// Deserializes only the value selected by a pre-parsed <see cref="JsonPath"/> from a UTF-8 buffer.
	/// </summary>
	/// <typeparam name="TValue">The type of the selected value.</typeparam>
	/// <param name="utf8Json">The UTF-8 JSON document.</param>
	/// <param name="path">The pre-parsed path that selects the value.</param>
	/// <param name="targetShape">The shape of <typeparamref name="TValue"/>.</param>
	/// <param name="missingBehavior">Controls behavior when the path is not found.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value, or the default value when the path is missing and default behavior is selected.</returns>
	public TValue? DeserializePath<TValue>(ReadOnlyMemory<byte> utf8Json, JsonPath path, ITypeShape<TValue> targetShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
	{
		JsonReader reader = new(utf8Json.Span, this.AllowTrailingCommas, this.ReadCommentHandling);
		return this.DeserializePathCore(ref reader, path, null, targetShape, missingBehavior, cancellationToken);
	}

	/// <summary>
	/// Deserializes only the value selected by a pre-parsed <see cref="JsonPath"/> from a possibly multi-segment UTF-8 buffer.
	/// </summary>
	/// <typeparam name="TValue">The type of the selected value.</typeparam>
	/// <param name="utf8Json">The UTF-8 JSON document, possibly spanning multiple buffer segments.</param>
	/// <param name="path">The pre-parsed path that selects the value.</param>
	/// <param name="targetShape">The shape of <typeparamref name="TValue"/>.</param>
	/// <param name="missingBehavior">Controls behavior when the path is not found.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value, or the default value when the path is missing and default behavior is selected.</returns>
	public TValue? DeserializePath<TValue>(scoped in ReadOnlySequence<byte> utf8Json, JsonPath path, ITypeShape<TValue> targetShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
	{
		JsonReader reader = new(utf8Json, this.AllowTrailingCommas, this.ReadCommentHandling);
		return this.DeserializePathCore(ref reader, path, null, targetShape, missingBehavior, cancellationToken);
	}

	/// <summary>Deserializes a raw JSON path from a stream.</summary>
	/// <typeparam name="TValue">The selected value type.</typeparam>
	/// <param name="stream">The document stream, which is read to its end and remains open.</param>
	/// <param name="path">The raw JSON path.</param>
	/// <param name="targetShape">The selected value shape.</param>
	/// <param name="missingBehavior">The behavior for a missing path.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The selected value.</returns>
	public TValue? DeserializePath<TValue>(Stream stream, JsonPath path, ITypeShape<TValue> targetShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		cancellationToken.ThrowIfCancellationRequested();
		using MemoryStream buffer = new();
		stream.CopyTo(buffer);
		cancellationToken.ThrowIfCancellationRequested();
		return this.DeserializePath(buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length)), path, targetShape, missingBehavior, cancellationToken);
	}

#if NET
	/// <summary>
	/// Deserializes only the value selected by a pre-parsed <see cref="JsonPath"/>, using the type's own shape.
	/// </summary>
	/// <typeparam name="TValue">The type of the selected value.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="path">The pre-parsed path that selects the value.</param>
	/// <param name="missingBehavior">Controls behavior when the path is not found.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public TValue? DeserializePath<TValue>(string json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TValue : IShapeable<TValue> => this.DeserializePath(json, path, TValue.GetTypeShape(), missingBehavior, cancellationToken);

	/// <summary>
	/// Deserializes only the value selected by a pre-parsed <see cref="JsonPath"/>, using a witness type's shape.
	/// </summary>
	/// <typeparam name="TValue">The type of the selected value.</typeparam>
	/// <typeparam name="TProvider">A witness type that provides the shape for <typeparamref name="TValue"/>.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="path">The pre-parsed path that selects the value.</param>
	/// <param name="missingBehavior">Controls behavior when the path is not found.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public TValue? DeserializePath<TValue, TProvider>(string json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TValue> => this.DeserializePath(json, path, TProvider.GetTypeShape(), missingBehavior, cancellationToken);

#endif

	private TValue? DeserializePathCore<TValue>(ref JsonReader reader, JsonPath path, JsonConverter?[]? converters, ITypeShape<TValue> targetShape, MissingPathBehavior missingBehavior, CancellationToken cancellationToken)
	{
		Requires.NotNull(path);
		Requires.NotNull(targetShape);
		if (this.PreserveReferences != ReferencePreservationMode.Off)
		{
			throw new NotSupportedException("Targeted deserialization is not supported when reference preservation is enabled, because reference metadata wraps every value.");
		}

		SerializationContext context = this.CreateSerializationContext(cancellationToken);
		bool ignoreCase = this.PropertyNameCaseInsensitive;
		StringComparer comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		JsonNavigationOptions options = new(comparer, ignoreCase);

		try
		{
			if (!JsonTargetedNavigator.TryNavigate(ref reader, path, converters, options, ref context))
			{
				return missingBehavior == MissingPathBehavior.Throw
					? throw new JsonPathNotFoundException(path.ToString())
					: default;
			}

			return this.ConverterCache.GetOrAddConverter(targetShape).Read(ref reader, context);
		}
		catch (Exception ex) when (ShouldWrapSerializationException(ex, context.CancellationToken))
		{
			throw new JsonSerializationException("An error occurred during deserialization.", ex);
		}
	}

	/// <summary>Options for selecting a value within a JSON document.</summary>
	/// <remarks>The expression is parsed without compilation. Member names honor the naming policy; indices must be constants. Use <see cref="JsonPath"/> for dynamic indices.</remarks>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The selected value type.</typeparam>
	/// <param name="Path">The expression selecting the value.</param>
	public record struct DeserializePathOptions<TRoot, TElement>(Expression<Func<TRoot, TElement>> Path)
	{
		/// <summary>Gets a value indicating whether a missing path produces the default value instead of an exception.</summary>
		public bool DefaultForUndiscoverablePath { get; init; }
	}
}
