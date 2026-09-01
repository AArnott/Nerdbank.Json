// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using PolyType;

namespace Nerdbank.Json.AspNetCore.Tests;

/// <summary>
/// Shared helpers for building the MVC formatter context objects that the framework would normally hand to a formatter.
/// </summary>
public abstract class FormatterTestBase
{
	/// <summary>The assembly-wide, source-generated shape provider for the test models.</summary>
	protected static readonly ITypeShapeProvider Provider =
		PolyType.SourceGenerator.TypeShapeProvider_Nerdbank_Json_AspNetCoreMvcFormatter_Tests.Default;

	private static readonly IModelMetadataProvider MetadataProvider = new EmptyModelMetadataProvider();

	private protected static InputFormatterContext CreateInputContext(
		Stream body,
		string? contentType,
		Type modelType,
		bool treatEmptyInputAsDefaultValue = false,
		CancellationToken requestAborted = default)
	{
		DefaultHttpContext httpContext = new();
		httpContext.Request.Body = body;
		if (contentType is not null)
		{
			httpContext.Request.ContentType = contentType;
			httpContext.Request.ContentLength = body.CanSeek ? body.Length : null;
		}

		httpContext.RequestAborted = requestAborted;

		ModelMetadata metadata = MetadataProvider.GetMetadataForType(modelType);
		return new InputFormatterContext(
			httpContext,
			modelName: "model",
			modelState: new ModelStateDictionary(),
			metadata: metadata,
			readerFactory: (stream, encoding) => new StreamReader(stream, encoding),
			treatEmptyInputAsDefaultValue: treatEmptyInputAsDefaultValue);
	}

	private protected static (OutputFormatterWriteContext Context, MemoryStream Body) CreateOutputContext(
		Type objectType,
		object? value,
		string contentType = "application/json",
		CancellationToken requestAborted = default)
	{
		DefaultHttpContext httpContext = new();
		MemoryStream body = new();
		httpContext.Response.Body = body;
		httpContext.RequestAborted = requestAborted;

		OutputFormatterWriteContext context = new(
			httpContext,
			writerFactory: (stream, encoding) => new StreamWriter(stream, encoding),
			objectType: objectType,
			@object: value)
		{
			ContentType = new Microsoft.Extensions.Primitives.StringSegment(contentType),
		};
		return (context, body);
	}
}
