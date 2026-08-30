// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using PolyType;

namespace Nerdbank.Json.AspNetCore;

/// <summary>
/// An MVC input formatter that deserializes request bodies with <see cref="JsonSerializer"/> using an explicitly
/// supplied, source-generated <see cref="ITypeShapeProvider"/>.
/// </summary>
/// <remarks>
/// The formatter reads incrementally from the request's <see cref="PipeReader"/> so large bodies are not buffered in
/// full, honors <see cref="HttpContext.RequestAborted"/>, and never uses reflection or roots <c>System.Text.Json</c>,
/// keeping it trimming- and NativeAOT-safe.
/// </remarks>
public class NerdbankJsonInputFormatter : TextInputFormatter
{
	private readonly JsonSerializer serializer;
	private readonly ITypeShapeProvider shapeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="NerdbankJsonInputFormatter"/> class.
	/// </summary>
	/// <param name="serializer">The serializer used to read request bodies.</param>
	/// <param name="shapeProvider">The shape provider that describes every deserializable model type.</param>
	public NerdbankJsonInputFormatter(JsonSerializer serializer, ITypeShapeProvider shapeProvider)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(shapeProvider);
		this.serializer = serializer;
		this.shapeProvider = shapeProvider;

		this.SupportedEncodings.Add(UTF8EncodingWithoutBOM);
		this.SupportedEncodings.Add(UTF16EncodingLittleEndian);

		this.SupportedMediaTypes.Add(MediaTypeNames.ApplicationJson);
		this.SupportedMediaTypes.Add(MediaTypeNames.TextJson);
		this.SupportedMediaTypes.Add(MediaTypeNames.ApplicationAnyJsonSuffix);
	}

	/// <inheritdoc/>
	public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(encoding);

		HttpContext httpContext = context.HttpContext;
		CancellationToken cancellationToken = httpContext.RequestAborted;
		Type modelType = context.ModelType;

		bool isUtf8 = encoding.CodePage == Encoding.UTF8.CodePage;
		Stream? transcodingStream = null;
		PipeReader reader;
		if (isUtf8)
		{
			reader = httpContext.Request.BodyReader;
		}
		else
		{
			transcodingStream = Encoding.CreateTranscodingStream(httpContext.Request.Body, encoding, Encoding.UTF8, leaveOpen: true);
			reader = PipeReader.Create(transcodingStream);
		}

		try
		{
			if (await IsBodyEmptyAsync(httpContext, reader, cancellationToken).ConfigureAwait(false))
			{
				if (context.TreatEmptyInputAsDefaultValue)
				{
					return InputFormatterResult.Success(RuntimeShapeSerialization.GetDefaultValue(this.shapeProvider, modelType));
				}

				return InputFormatterResult.NoValue();
			}

			object? model = await RuntimeShapeSerialization.DeserializeAsync(this.serializer, this.shapeProvider, modelType, reader, cancellationToken).ConfigureAwait(false);
			return InputFormatterResult.Success(model);
		}
		catch (Exception ex) when (ex is not MissingTypeShapeException && ex is not OperationCanceledException && IsInputError(ex))
		{
			context.ModelState.TryAddModelError(context.ModelName, ex.Message);
			return InputFormatterResult.Failure();
		}
		finally
		{
			if (transcodingStream is not null)
			{
				await transcodingStream.DisposeAsync().ConfigureAwait(false);
			}
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Returns <see langword="true"/> only when the configured shape provider can describe <paramref name="type"/>. This
	/// lets the formatter coexist with other input formatters (for example the built-in <c>System.Text.Json</c>
	/// formatter for framework types) instead of claiming JSON payloads it cannot deserialize.
	/// </remarks>
	protected override bool CanReadType(Type type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return this.shapeProvider.GetTypeShape(type) is not null;
	}

	private static bool IsInputError(Exception ex)
		=> ex is JsonSerializationException or FormatException or InvalidOperationException or OverflowException or NotSupportedException or DecoderFallbackException;

	private static async ValueTask<bool> IsBodyEmptyAsync(HttpContext httpContext, PipeReader reader, CancellationToken cancellationToken)
	{
		if (httpContext.Request.ContentLength == 0)
		{
			return true;
		}

		ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
		bool empty = result.IsCompleted && result.Buffer.IsEmpty;

		// Do not consume anything: mark the start as both consumed and examined so the subsequent
		// deserialization re-reads from the beginning of the body.
		reader.AdvanceTo(result.Buffer.Start, result.Buffer.Start);
		return empty;
	}
}
