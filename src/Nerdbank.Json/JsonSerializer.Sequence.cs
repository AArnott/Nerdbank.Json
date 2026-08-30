// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Nerdbank.Json;

public partial record JsonSerializer
{
	/// <summary>
	/// Asynchronously streams the elements of a top-level JSON array, reading only enough to produce each element on demand.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="reader">The pipe to read from. It is advanced but never completed by this method.</param>
	/// <param name="shape">The shape of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>An asynchronous sequence of elements.</returns>
	public async IAsyncEnumerable<T?> DeserializeArrayAsync<T>(PipeReader reader, ITypeShape<T> shape, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		Requires.NotNull(reader);
		Requires.NotNull(shape);
		this.ThrowIfSequenceStreamingUnsupported();

		SerializationContext context = this.CreateSerializationContext(cancellationToken);
		using JsonAsyncReader asyncReader = new(reader, this.AllowTrailingCommas, this.ReadCommentHandling)
		{
			CancellationToken = context.CancellationToken,
		};
		JsonConverter<T> converter = this.ConverterCache.GetOrAddConverter(shape);

		if (!await asyncReader.TryReadNullAsync(context).ConfigureAwait(false))
		{
			await asyncReader.ReadStartArrayAsync(context).ConfigureAwait(false);
			if (!await asyncReader.TryReadEndArrayAsync(context).ConfigureAwait(false))
			{
				while (true)
				{
					yield return await asyncReader.ReadValueAsync(converter, context).ConfigureAwait(false);
					if (await asyncReader.TryReadEndArrayAsync(context).ConfigureAwait(false))
					{
						break;
					}

					await asyncReader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
				}
			}
		}

		await asyncReader.EnsureFullyConsumedAsync(context).ConfigureAwait(false);
	}

	/// <summary>
	/// Asynchronously streams the elements of a top-level JSON array from a stream.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="stream">The stream to read from. It is read but not disposed or closed by this method.</param>
	/// <param name="shape">The shape of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>An asynchronous sequence of elements.</returns>
	public async IAsyncEnumerable<T?> DeserializeArrayAsync<T>(Stream stream, ITypeShape<T> shape, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		PipeReader reader = PipeReader.Create(stream, StreamPipeReaderOptions);
		try
		{
			await foreach (T? element in this.DeserializeArrayAsync(reader, shape, cancellationToken).ConfigureAwait(false))
			{
				yield return element;
			}
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously streams the values of a newline-delimited JSON (NDJSON / JSON Lines) document, where each value is
	/// framed on its own line.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="reader">The pipe to read from. It is advanced but never completed by this method.</param>
	/// <param name="shape">The shape of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>An asynchronous sequence of values.</returns>
	/// <exception cref="FormatException">Thrown if two values are not separated by a line terminator.</exception>
	public async IAsyncEnumerable<T?> DeserializeNewlineDelimitedAsync<T>(PipeReader reader, ITypeShape<T> shape, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		Requires.NotNull(reader);
		Requires.NotNull(shape);
		this.ThrowIfSequenceStreamingUnsupported();

		SerializationContext context = this.CreateSerializationContext(cancellationToken);
		using JsonAsyncReader asyncReader = new(reader, this.AllowTrailingCommas, this.ReadCommentHandling)
		{
			CancellationToken = context.CancellationToken,
		};
		JsonConverter<T> converter = this.ConverterCache.GetOrAddConverter(shape);

		bool first = true;
		while (true)
		{
			(int next, bool sawLineTerminator) = await asyncReader.PeekNextByteAcrossLinesAsync().ConfigureAwait(false);
			if (next == -1)
			{
				break;
			}

			if (!first && !sawLineTerminator)
			{
				throw new FormatException("Newline-delimited JSON values must be separated by a line terminator.");
			}

			first = false;
			yield return await asyncReader.ReadValueAsync(converter, context).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously streams the values of a newline-delimited JSON document from a stream.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="stream">The stream to read from. It is read but not disposed or closed by this method.</param>
	/// <param name="shape">The shape of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>An asynchronous sequence of values.</returns>
	public async IAsyncEnumerable<T?> DeserializeNewlineDelimitedAsync<T>(Stream stream, ITypeShape<T> shape, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		PipeReader reader = PipeReader.Create(stream, StreamPipeReaderOptions);
		try
		{
			await foreach (T? element in this.DeserializeNewlineDelimitedAsync(reader, shape, cancellationToken).ConfigureAwait(false))
			{
				yield return element;
			}
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously streams the elements of a JSON array selected by a pre-parsed <see cref="JsonPath"/> inside an
	/// object or array envelope, parsing the preamble incrementally and draining the remainder of the envelope on
	/// completion.
	/// </summary>
	/// <typeparam name="TElement">The element type.</typeparam>
	/// <param name="reader">The pipe to read from. It is advanced but never completed by this method.</param>
	/// <param name="path">The pre-parsed path that selects the array. Names are the serialized (wire) names.</param>
	/// <param name="elementShape">The shape of <typeparamref name="TElement"/>.</param>
	/// <param name="missingBehavior">Controls behavior when the path is not found.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>An asynchronous sequence of elements.</returns>
	public IAsyncEnumerable<TElement?> DeserializeArrayAtAsync<TElement>(PipeReader reader, JsonPath path, ITypeShape<TElement> elementShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(reader);
		Requires.NotNull(path);
		Requires.NotNull(elementShape);
		return this.DeserializeArrayAtCoreAsync(reader, path, null, elementShape, missingBehavior, cancellationToken);
	}

	/// <summary>
	/// Asynchronously streams the elements of a JSON array selected by a pre-parsed <see cref="JsonPath"/> from a stream.
	/// </summary>
	/// <typeparam name="TElement">The element type.</typeparam>
	/// <param name="stream">The stream to read from. It is read but not disposed or closed by this method.</param>
	/// <param name="path">The pre-parsed path that selects the array.</param>
	/// <param name="elementShape">The shape of <typeparamref name="TElement"/>.</param>
	/// <param name="missingBehavior">Controls behavior when the path is not found.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>An asynchronous sequence of elements.</returns>
	public async IAsyncEnumerable<TElement?> DeserializeArrayAtAsync<TElement>(Stream stream, JsonPath path, ITypeShape<TElement> elementShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		Requires.NotNull(path);
		Requires.NotNull(elementShape);
		PipeReader reader = PipeReader.Create(stream, StreamPipeReaderOptions);
		try
		{
			await foreach (TElement? element in this.DeserializeArrayAtCoreAsync(reader, path, null, elementShape, missingBehavior, cancellationToken).ConfigureAwait(false))
			{
				yield return element;
			}
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously streams the elements of a JSON array selected by a member-access expression inside an object or
	/// array envelope. The expression is parsed but never compiled, so this overload is NativeAOT-safe and can traverse
	/// intermediate union or custom-converter representations.
	/// </summary>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The element type.</typeparam>
	/// <param name="reader">The pipe to read from. It is advanced but never completed by this method.</param>
	/// <param name="path">An expression such as <c>x =&gt; x.Items</c> selecting an <see cref="IEnumerable{T}"/>.</param>
	/// <param name="rootShape">The shape of <typeparamref name="TRoot"/>.</param>
	/// <param name="missingBehavior">Controls behavior when the path is not found.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>An asynchronous sequence of elements.</returns>
	public IAsyncEnumerable<TElement?> DeserializeArrayAtAsync<TRoot, TElement>(PipeReader reader, Expression<Func<TRoot, IEnumerable<TElement>>> path, ITypeShape<TRoot> rootShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(reader);
		Requires.NotNull(path);
		Requires.NotNull(rootShape);
		(JsonPath parsedPath, JsonConverter?[] converters, ITypeShape<TElement> elementShape) = JsonPathExpressionParser.ParseSequence(path, rootShape, this.ConverterCache, this.DictionaryKeyNamingPolicy);
		return this.DeserializeArrayAtCoreAsync(reader, parsedPath, converters, elementShape, missingBehavior, cancellationToken);
	}

	/// <summary>
	/// Asynchronously streams the elements of a JSON array selected by a member-access expression from a stream.
	/// </summary>
	/// <typeparam name="TRoot">The root document type.</typeparam>
	/// <typeparam name="TElement">The element type.</typeparam>
	/// <param name="stream">The stream to read from. It is read but not disposed or closed by this method.</param>
	/// <param name="path">An expression selecting an <see cref="IEnumerable{T}"/>.</param>
	/// <param name="rootShape">The shape of <typeparamref name="TRoot"/>.</param>
	/// <param name="missingBehavior">Controls behavior when the path is not found.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>An asynchronous sequence of elements.</returns>
	public async IAsyncEnumerable<TElement?> DeserializeArrayAtAsync<TRoot, TElement>(Stream stream, Expression<Func<TRoot, IEnumerable<TElement>>> path, ITypeShape<TRoot> rootShape, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		Requires.NotNull(path);
		Requires.NotNull(rootShape);
		(JsonPath parsedPath, JsonConverter?[] converters, ITypeShape<TElement> elementShape) = JsonPathExpressionParser.ParseSequence(path, rootShape, this.ConverterCache, this.DictionaryKeyNamingPolicy);
		PipeReader reader = PipeReader.Create(stream, StreamPipeReaderOptions);
		try
		{
			await foreach (TElement? element in this.DeserializeArrayAtCoreAsync(reader, parsedPath, converters, elementShape, missingBehavior, cancellationToken).ConfigureAwait(false))
			{
				yield return element;
			}
		}
		finally
		{
			await reader.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Asynchronously serializes an <see cref="IAsyncEnumerable{T}"/> as a top-level JSON array, writing elements as they
	/// are produced with bounded memory and backpressure.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="writer">The pipe to write to. It is flushed but never completed by this method.</param>
	/// <param name="values">The asynchronous source of elements.</param>
	/// <param name="shape">The shape of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task tracking the serialization.</returns>
	public async ValueTask SerializeArrayAsync<T>(PipeWriter writer, IAsyncEnumerable<T> values, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(writer);
		Requires.NotNull(values);
		Requires.NotNull(shape);

		SerializationContext context = this.CreateSerializationContext(cancellationToken);
		JsonAsyncWriter asyncWriter = new(writer, this.WriteIndented);
		JsonConverter<T> converter = this.ConverterCache.GetOrAddConverter(shape);

		JsonWriter syncWriter = asyncWriter.CreateWriter();
		syncWriter.WriteStartArray();
		asyncWriter.ReturnWriter(ref syncWriter);

		bool first = true;
		await foreach (T value in values.WithCancellation(context.CancellationToken).ConfigureAwait(false))
		{
			if (!first)
			{
				syncWriter = asyncWriter.CreateWriter();
				syncWriter.WriteValueSeparator();
				asyncWriter.ReturnWriter(ref syncWriter);
			}

			first = false;
			await asyncWriter.WriteValueAsync(converter, value, context).ConfigureAwait(false);
		}

		syncWriter = asyncWriter.CreateWriter();
		syncWriter.WriteEndArray();
		asyncWriter.ReturnWriter(ref syncWriter);
		await asyncWriter.FlushAsync(context.CancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Asynchronously serializes an <see cref="IAsyncEnumerable{T}"/> as a top-level JSON array to a stream.
	/// </summary>
	/// <typeparam name="T">The element type.</typeparam>
	/// <param name="stream">The stream to write to. It is flushed but not disposed or closed by this method.</param>
	/// <param name="values">The asynchronous source of elements.</param>
	/// <param name="shape">The shape of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task tracking the serialization.</returns>
	public async ValueTask SerializeArrayAsync<T>(Stream stream, IAsyncEnumerable<T> values, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		PipeWriter writer = PipeWriter.Create(stream, StreamPipeWriterLeaveOpen);
		await this.SerializeArrayAsync(writer, values, shape, cancellationToken).ConfigureAwait(false);
		await writer.CompleteAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Asynchronously serializes an <see cref="IAsyncEnumerable{T}"/> as newline-delimited JSON (one value per line).
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="writer">The pipe to write to. It is flushed but never completed by this method.</param>
	/// <param name="values">The asynchronous source of values.</param>
	/// <param name="shape">The shape of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task tracking the serialization.</returns>
	public async ValueTask SerializeNewlineDelimitedAsync<T>(PipeWriter writer, IAsyncEnumerable<T> values, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(writer);
		Requires.NotNull(values);
		Requires.NotNull(shape);

		SerializationContext context = this.CreateSerializationContext(cancellationToken);
		JsonAsyncWriter asyncWriter = new(writer, writeIndented: false);
		JsonConverter<T> converter = this.ConverterCache.GetOrAddConverter(shape);

		await foreach (T value in values.WithCancellation(context.CancellationToken).ConfigureAwait(false))
		{
			await asyncWriter.WriteValueAsync(converter, value, context).ConfigureAwait(false);
			JsonWriter syncWriter = asyncWriter.CreateWriter();
			syncWriter.WriteRawValue("\n");
			asyncWriter.ReturnWriter(ref syncWriter);
			await asyncWriter.FlushIfAppropriateAsync(context).ConfigureAwait(false);
		}

		await asyncWriter.FlushAsync(context.CancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Asynchronously serializes an <see cref="IAsyncEnumerable{T}"/> as newline-delimited JSON to a stream.
	/// </summary>
	/// <typeparam name="T">The value type.</typeparam>
	/// <param name="stream">The stream to write to. It is flushed but not disposed or closed by this method.</param>
	/// <param name="values">The asynchronous source of values.</param>
	/// <param name="shape">The shape of <typeparamref name="T"/>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task tracking the serialization.</returns>
	public async ValueTask SerializeNewlineDelimitedAsync<T>(Stream stream, IAsyncEnumerable<T> values, ITypeShape<T> shape, CancellationToken cancellationToken = default)
	{
		Requires.NotNull(stream);
		PipeWriter writer = PipeWriter.Create(stream, StreamPipeWriterLeaveOpen);
		await this.SerializeNewlineDelimitedAsync(writer, values, shape, cancellationToken).ConfigureAwait(false);
		await writer.CompleteAsync().ConfigureAwait(false);
	}

#pragma warning disable SA1202 // Keep the private core enumerator adjacent to its public entry points.
	private async IAsyncEnumerable<TElement?> DeserializeArrayAtCoreAsync<TElement>(PipeReader reader, JsonPath path, JsonConverter?[]? converters, ITypeShape<TElement> elementShape, MissingPathBehavior missingBehavior, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		this.ThrowIfSequenceStreamingUnsupported();

		SerializationContext context = this.CreateSerializationContext(cancellationToken);
		using JsonAsyncReader asyncReader = new(reader, this.AllowTrailingCommas, this.ReadCommentHandling)
		{
			CancellationToken = context.CancellationToken,
		};
		JsonConverter<TElement> converter = this.ConverterCache.GetOrAddConverter(elementShape);
		bool ignoreCase = this.PropertyNameCaseInsensitive;
		StringComparer comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		JsonNavigationOptions options = new(comparer, ignoreCase);

		int openContainers = await JsonAsyncTargetedNavigator.TryNavigateAsync(asyncReader, path, converters, options, context).ConfigureAwait(false);
		if (openContainers < 0)
		{
			if (missingBehavior == MissingPathBehavior.Throw)
			{
				throw new JsonPathNotFoundException(path.ToString());
			}

			yield break;
		}

		if (!await asyncReader.TryReadNullAsync(context).ConfigureAwait(false))
		{
			await asyncReader.ReadStartArrayAsync(context).ConfigureAwait(false);
			if (!await asyncReader.TryReadEndArrayAsync(context).ConfigureAwait(false))
			{
				while (true)
				{
					yield return await asyncReader.ReadValueAsync(converter, context).ConfigureAwait(false);
					if (await asyncReader.TryReadEndArrayAsync(context).ConfigureAwait(false))
					{
						break;
					}

					await asyncReader.ReadValueSeparatorAsync(context).ConfigureAwait(false);
				}
			}
		}

		await asyncReader.CloseContainersAsync(openContainers, context).ConfigureAwait(false);
		await asyncReader.EnsureFullyConsumedAsync(context).ConfigureAwait(false);
	}

	private void ThrowIfSequenceStreamingUnsupported()
	{
		if (this.PreserveReferences != ReferencePreservationMode.Off)
		{
			throw new NotSupportedException("Asynchronous sequence streaming is not supported when reference preservation is enabled, because reference metadata wraps every value.");
		}
	}

#if NET
	/// <inheritdoc cref="DeserializeArrayAsync{T}(PipeReader, ITypeShape{T}, CancellationToken)"/>
	public IAsyncEnumerable<T?> DeserializeArrayAsync<T>(PipeReader reader, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeArrayAsync(reader, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="DeserializeArrayAsync{T}(Stream, ITypeShape{T}, CancellationToken)"/>
	public IAsyncEnumerable<T?> DeserializeArrayAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeArrayAsync(stream, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="DeserializeNewlineDelimitedAsync{T}(PipeReader, ITypeShape{T}, CancellationToken)"/>
	public IAsyncEnumerable<T?> DeserializeNewlineDelimitedAsync<T>(PipeReader reader, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeNewlineDelimitedAsync(reader, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="DeserializeNewlineDelimitedAsync{T}(Stream, ITypeShape{T}, CancellationToken)"/>
	public IAsyncEnumerable<T?> DeserializeNewlineDelimitedAsync<T>(Stream stream, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.DeserializeNewlineDelimitedAsync(stream, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="DeserializeArrayAtAsync{TRoot, TElement}(PipeReader, Expression{Func{TRoot, IEnumerable{TElement}}}, ITypeShape{TRoot}, MissingPathBehavior, CancellationToken)"/>
	public IAsyncEnumerable<TElement?> DeserializeArrayAtAsync<TRoot, TElement>(PipeReader reader, Expression<Func<TRoot, IEnumerable<TElement>>> path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializeArrayAtAsync(reader, path, TRoot.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="DeserializeArrayAtAsync{TRoot, TElement}(Stream, Expression{Func{TRoot, IEnumerable{TElement}}}, ITypeShape{TRoot}, MissingPathBehavior, CancellationToken)"/>
	public IAsyncEnumerable<TElement?> DeserializeArrayAtAsync<TRoot, TElement>(Stream stream, Expression<Func<TRoot, IEnumerable<TElement>>> path, MissingPathBehavior missingBehavior = MissingPathBehavior.Throw, CancellationToken cancellationToken = default)
		where TRoot : IShapeable<TRoot> => this.DeserializeArrayAtAsync(stream, path, TRoot.GetTypeShape(), missingBehavior, cancellationToken);

	/// <inheritdoc cref="SerializeArrayAsync{T}(PipeWriter, IAsyncEnumerable{T}, ITypeShape{T}, CancellationToken)"/>
	public ValueTask SerializeArrayAsync<T>(PipeWriter writer, IAsyncEnumerable<T> values, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeArrayAsync(writer, values, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="SerializeArrayAsync{T}(Stream, IAsyncEnumerable{T}, ITypeShape{T}, CancellationToken)"/>
	public ValueTask SerializeArrayAsync<T>(Stream stream, IAsyncEnumerable<T> values, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeArrayAsync(stream, values, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="SerializeNewlineDelimitedAsync{T}(PipeWriter, IAsyncEnumerable{T}, ITypeShape{T}, CancellationToken)"/>
	public ValueTask SerializeNewlineDelimitedAsync<T>(PipeWriter writer, IAsyncEnumerable<T> values, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeNewlineDelimitedAsync(writer, values, T.GetTypeShape(), cancellationToken);

	/// <inheritdoc cref="SerializeNewlineDelimitedAsync{T}(Stream, IAsyncEnumerable{T}, ITypeShape{T}, CancellationToken)"/>
	public ValueTask SerializeNewlineDelimitedAsync<T>(Stream stream, IAsyncEnumerable<T> values, CancellationToken cancellationToken = default)
		where T : IShapeable<T> => this.SerializeNewlineDelimitedAsync(stream, values, T.GetTypeShape(), cancellationToken);
#endif
#pragma warning restore SA1202
}
