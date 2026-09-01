// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json.AspNetCore;

/// <summary>
/// Options that control how the Nerdbank.Json MVC formatters are registered.
/// </summary>
public sealed class NerdbankJsonFormatterOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether the built-in <c>System.Text.Json</c> input and output formatters should
	/// be removed when the Nerdbank.Json formatters are added.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The default is <see langword="false"/>: the Nerdbank.Json formatters are added <em>alongside</em> the framework
	/// defaults and never silently replace them. Because <see cref="InsertFirst"/> defaults to <see langword="true"/>,
	/// the Nerdbank.Json formatters still win content negotiation for JSON media types, but the built-in formatters
	/// remain available as a fallback (for example for types that other formatters can handle).
	/// </para>
	/// <para>
	/// Set this to <see langword="true"/> to fully take over JSON handling and guarantee that only Nerdbank.Json
	/// processes <c>application/json</c> and <c>+json</c> payloads.
	/// </para>
	/// </remarks>
	public bool ReplaceSystemTextJsonFormatters { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the Nerdbank.Json formatters are inserted at the front of the formatter
	/// collections (so they take precedence during content negotiation) rather than appended.
	/// </summary>
	/// <remarks>The default is <see langword="true"/>.</remarks>
	public bool InsertFirst { get; set; } = true;
}
