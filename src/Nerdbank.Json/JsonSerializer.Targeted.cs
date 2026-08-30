// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1601 // Partial elements are documented on their primary declaration.

using System.Linq.Expressions;
using System.Text;

namespace Nerdbank.Json;

public partial record JsonSerializer
{
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
	public TValue? DeserializeAt<TValue>(string json, JsonPath path, ITypeShape<TValue> targetShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(json);
		byte[] utf8Json = Encoding.UTF8.GetBytes(json);
		JsonReader reader = new(utf8Json, this.AllowTrailingCommas, this.ReadCommentHandling);
		return this.DeserializeAtCore(ref reader, path, targetShape, missingBehavior, cancellationToken);
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
	public TValue? DeserializeAt<TValue>(ReadOnlyMemory<byte> utf8Json, JsonPath path, ITypeShape<TValue> targetShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
	{
		JsonReader reader = new(utf8Json.Span, this.AllowTrailingCommas, this.ReadCommentHandling);
		return this.DeserializeAtCore(ref reader, path, targetShape, missingBehavior, cancellationToken);
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
	public TValue? DeserializeAt<TValue>(scoped in ReadOnlySequence<byte> utf8Json, JsonPath path, ITypeShape<TValue> targetShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
	{
		JsonReader reader = new(utf8Json, this.AllowTrailingCommas, this.ReadCommentHandling);
		return this.DeserializeAtCore(ref reader, path, targetShape, missingBehavior, cancellationToken);
	}

	/// <summary>
	/// Deserializes only the value selected by a member-access expression, skipping unrelated JSON.
	/// </summary>
	/// <typeparam name="TRoot">The type of the root document.</typeparam>
	/// <typeparam name="TValue">The type of the selected value.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="path">An expression such as <c>x =&gt; x.Items[3].Name</c> that selects the value.</param>
	/// <param name="rootShape">The shape of <typeparamref name="TRoot"/>.</param>
	/// <param name="missingBehavior">Controls behavior when the path is not found.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	/// <remarks>
	/// The expression is parsed but never compiled, so this overload is NativeAOT-safe. CLR member names are translated
	/// to serialized names using the configured naming policies. Array and dictionary indices must be compile-time
	/// constants; unsupported expressions are rejected before any JSON is read.
	/// </remarks>
	public TValue? DeserializeAt<TRoot, TValue>(string json, Expression<Func<TRoot, TValue>> path, ITypeShape<TRoot> rootShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(json);
		Requires.NotNull(path);
		Requires.NotNull(rootShape);
		(JsonPath parsedPath, ITypeShape<TValue> targetShape) = JsonPathExpressionParser.Parse(path, rootShape, this.ConverterCache, this.DictionaryKeyNamingPolicy);
		byte[] utf8Json = Encoding.UTF8.GetBytes(json);
		JsonReader reader = new(utf8Json, this.AllowTrailingCommas, this.ReadCommentHandling);
		return this.DeserializeAtCore(ref reader, parsedPath, targetShape, missingBehavior, cancellationToken);
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
	public TValue? DeserializeAt<TValue>(string json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TValue : IShapeable<TValue> => this.DeserializeAt(json, path, TValue.GetTypeShape(), missingBehavior, cancellationToken);

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
	public TValue? DeserializeAt<TValue, TProvider>(string json, JsonPath path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TProvider : IShapeable<TValue> => this.DeserializeAt(json, path, TProvider.GetTypeShape(), missingBehavior, cancellationToken);

	/// <summary>
	/// Deserializes only the value selected by a member-access expression, using the root type's own shape.
	/// </summary>
	/// <typeparam name="TRoot">The type of the root document.</typeparam>
	/// <typeparam name="TValue">The type of the selected value.</typeparam>
	/// <param name="json">The JSON document.</param>
	/// <param name="path">An expression such as <c>x =&gt; x.Items[3].Name</c> that selects the value.</param>
	/// <param name="missingBehavior">Controls behavior when the path is not found.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The deserialized value.</returns>
	public TValue? DeserializeAt<TRoot, TValue>(string json, Expression<Func<TRoot, TValue>> path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializeAt(json, path, TRoot.GetTypeShape(), missingBehavior, cancellationToken);
#endif

	private TValue? DeserializeAtCore<TValue>(ref JsonReader reader, JsonPath path, ITypeShape<TValue> targetShape, MissingPathBehavior missingBehavior, CancellationToken cancellationToken)
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

		if (!JsonTargetedNavigator.TryNavigate(ref reader, path, ignoreCase, comparer, ref context))
		{
			return missingBehavior == MissingPathBehavior.Throw
				? throw new JsonPathNotFoundException(path.ToString())
				: default;
		}

		return this.ConverterCache.GetOrAddConverter(targetShape).Read(ref reader, context);
	}
}
