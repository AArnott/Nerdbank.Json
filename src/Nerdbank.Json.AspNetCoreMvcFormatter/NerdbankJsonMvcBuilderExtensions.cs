// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Nerdbank.Json;
using Nerdbank.Json.AspNetCore;
using PolyType;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods that register the Nerdbank.Json MVC input and output formatters.
/// </summary>
public static class NerdbankJsonMvcBuilderExtensions
{
	/// <summary>
	/// Adds the Nerdbank.Json input and output formatters to an MVC pipeline.
	/// </summary>
	/// <param name="builder">The MVC builder (for example from <c>AddControllers</c>).</param>
	/// <param name="serializer">The serializer the formatters use.</param>
	/// <param name="shapeProvider">The source-generated shape provider that describes every model type read or written.</param>
	/// <param name="configure">An optional callback to configure registration behavior.</param>
	/// <returns>The <paramref name="builder"/> for chaining.</returns>
	/// <remarks>
	/// By default the formatters are inserted at the front of the formatter collections so they win content negotiation
	/// for JSON media types, but the built-in <c>System.Text.Json</c> formatters are left in place. Set
	/// <see cref="NerdbankJsonFormatterOptions.ReplaceSystemTextJsonFormatters"/> to remove them entirely.
	/// </remarks>
	public static IMvcBuilder AddNerdbankJsonFormatters(this IMvcBuilder builder, JsonSerializer serializer, ITypeShapeProvider shapeProvider, Action<NerdbankJsonFormatterOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ConfigureFormatters(builder.Services, serializer, shapeProvider, configure);
		return builder;
	}

	/// <summary>
	/// Adds the Nerdbank.Json input and output formatters to an MVC-Core pipeline.
	/// </summary>
	/// <param name="builder">The MVC-Core builder (for example from <c>AddControllersAsServices</c> or <c>AddMvcCore</c>).</param>
	/// <param name="serializer">The serializer the formatters use.</param>
	/// <param name="shapeProvider">The source-generated shape provider that describes every model type read or written.</param>
	/// <param name="configure">An optional callback to configure registration behavior.</param>
	/// <returns>The <paramref name="builder"/> for chaining.</returns>
	/// <remarks>
	/// By default the formatters are inserted at the front of the formatter collections so they win content negotiation
	/// for JSON media types, but the built-in <c>System.Text.Json</c> formatters are left in place. Set
	/// <see cref="NerdbankJsonFormatterOptions.ReplaceSystemTextJsonFormatters"/> to remove them entirely.
	/// </remarks>
	public static IMvcCoreBuilder AddNerdbankJsonFormatters(this IMvcCoreBuilder builder, JsonSerializer serializer, ITypeShapeProvider shapeProvider, Action<NerdbankJsonFormatterOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ConfigureFormatters(builder.Services, serializer, shapeProvider, configure);
		return builder;
	}

	private static void ConfigureFormatters(IServiceCollection services, JsonSerializer serializer, ITypeShapeProvider shapeProvider, Action<NerdbankJsonFormatterOptions>? configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(serializer);
		ArgumentNullException.ThrowIfNull(shapeProvider);

		NerdbankJsonFormatterOptions options = new();
		configure?.Invoke(options);

		services.Configure<MvcOptions>(mvc =>
		{
			if (options.ReplaceSystemTextJsonFormatters)
			{
				RemoveByType(mvc.InputFormatters, typeof(SystemTextJsonInputFormatter));
				RemoveByType(mvc.OutputFormatters, typeof(SystemTextJsonOutputFormatter));
			}

			NerdbankJsonInputFormatter input = new(serializer, shapeProvider);
			NerdbankJsonOutputFormatter output = new(serializer, shapeProvider);

			if (options.InsertFirst)
			{
				mvc.InputFormatters.Insert(0, input);
				mvc.OutputFormatters.Insert(0, output);
			}
			else
			{
				mvc.InputFormatters.Add(input);
				mvc.OutputFormatters.Add(output);
			}
		});
	}

	private static void RemoveByType<T>(IList<T> formatters, Type formatterType)
	{
		for (int i = formatters.Count - 1; i >= 0; i--)
		{
			if (formatterType.IsInstanceOfType(formatters[i]))
			{
				formatters.RemoveAt(i);
			}
		}
	}
}
