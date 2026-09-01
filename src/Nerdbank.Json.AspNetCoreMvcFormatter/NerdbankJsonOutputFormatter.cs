// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using PolyType;

namespace Nerdbank.Json.AspNetCore;

/// <summary>
/// An MVC output formatter that serializes responses with <see cref="JsonSerializer"/> using an explicitly supplied,
/// source-generated <see cref="ITypeShapeProvider"/>.
/// </summary>
/// <remarks>
/// The formatter writes incrementally to the response's <see cref="PipeWriter"/> with backpressure so large object
/// graphs are streamed without buffering the whole payload, honors <see cref="HttpContext.RequestAborted"/>, and never
/// uses reflection or roots <c>System.Text.Json</c>, keeping it trimming- and NativeAOT-safe.
/// </remarks>
public class NerdbankJsonOutputFormatter : TextOutputFormatter
{
	private static readonly StreamPipeWriterOptions LeaveOpenPipeWriterOptions = new(leaveOpen: true);

	private readonly JsonSerializer serializer;
	private readonly ITypeShapeProvider shapeProvider;

	/// <summary>
	/// Initializes a new instance of the <see cref="NerdbankJsonOutputFormatter"/> class.
	/// </summary>
	/// <param name="serializer">The serializer used to write responses.</param>
	/// <param name="shapeProvider">The shape provider that describes every serializable model type.</param>
	public NerdbankJsonOutputFormatter(JsonSerializer serializer, ITypeShapeProvider shapeProvider)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(shapeProvider);
		this.serializer = serializer;
		this.shapeProvider = shapeProvider;

		this.SupportedEncodings.Add(Encoding.UTF8);
		this.SupportedEncodings.Add(Encoding.Unicode);

		this.SupportedMediaTypes.Add(MediaTypeNames.ApplicationJson);
		this.SupportedMediaTypes.Add(MediaTypeNames.TextJson);
		this.SupportedMediaTypes.Add(MediaTypeNames.ApplicationAnyJsonSuffix);
	}

	/// <inheritdoc/>
	public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(selectedEncoding);

		HttpContext httpContext = context.HttpContext;
		CancellationToken cancellationToken = httpContext.RequestAborted;
		Type objectType = ResolveType(context);

		bool isUtf8 = selectedEncoding.CodePage == Encoding.UTF8.CodePage;
		if (isUtf8)
		{
			await RuntimeShapeSerialization.SerializeAsync(this.serializer, this.shapeProvider, objectType, context.Object, httpContext.Response.BodyWriter, cancellationToken).ConfigureAwait(false);
			return;
		}

		Stream transcodingStream = Encoding.CreateTranscodingStream(httpContext.Response.Body, selectedEncoding, Encoding.UTF8, leaveOpen: true);
		await using (transcodingStream.ConfigureAwait(false))
		{
			PipeWriter writer = PipeWriter.Create(transcodingStream, LeaveOpenPipeWriterOptions);
			await RuntimeShapeSerialization.SerializeAsync(this.serializer, this.shapeProvider, objectType, context.Object, writer, cancellationToken).ConfigureAwait(false);
			await writer.CompleteAsync().ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Returns <see langword="true"/> only when the configured shape provider can describe <paramref name="type"/>. This
	/// lets the formatter coexist with other output formatters (for example the built-in <c>System.Text.Json</c>
	/// formatter for framework types such as <c>ProblemDetails</c>) instead of claiming responses it cannot serialize.
	/// </remarks>
	protected override bool CanWriteType(Type? type)
		=> type is not null && this.shapeProvider.GetTypeShape(type) is not null;

	private static Type ResolveType(OutputFormatterWriteContext context)
	{
		Type? declared = context.ObjectType;
		if ((declared is null || declared == typeof(object)) && context.Object is not null)
		{
			return context.Object.GetType();
		}

		return declared ?? typeof(object);
	}
}
