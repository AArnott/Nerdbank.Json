// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using PolyType;

namespace Nerdbank.Json.AspNetCore;

/// <summary>
/// The exception thrown when the configured <see cref="ITypeShapeProvider"/> does not contain a shape for a type that
/// the MVC pipeline asked the Nerdbank.Json formatters to read or write.
/// </summary>
/// <remarks>
/// This almost always indicates a configuration mistake: the model type was not included in the source-generated
/// <see cref="ITypeShapeProvider"/>. Annotate the type with <c>[GenerateShape]</c> or add it to a witness class via
/// <c>[GenerateShapeFor&lt;T&gt;]</c>, and register that provider with the formatters. The formatters throw loudly
/// rather than silently binding <see langword="null"/> or falling through to another formatter, so misconfiguration is
/// obvious.
/// </remarks>
public class MissingTypeShapeException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MissingTypeShapeException"/> class.
	/// </summary>
	/// <param name="type">The type that lacked a shape.</param>
	/// <param name="provider">The provider that was consulted.</param>
	public MissingTypeShapeException(Type type, ITypeShapeProvider provider)
		: base($"The configured ITypeShapeProvider '{provider?.GetType().FullName}' does not contain a shape for type '{type?.FullName}'. Add [GenerateShape] to the type or include it in a witness via [GenerateShapeFor<{type?.Name}>], and register that provider with the Nerdbank.Json MVC formatters.")
	{
		this.UnshapedType = type;
		this.Provider = provider;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MissingTypeShapeException"/> class.
	/// </summary>
	/// <param name="message">The message.</param>
	public MissingTypeShapeException(string message)
		: base(message)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MissingTypeShapeException"/> class.
	/// </summary>
	/// <param name="message">The message.</param>
	/// <param name="innerException">The inner exception.</param>
	public MissingTypeShapeException(string message, Exception innerException)
		: base(message, innerException)
	{
	}

	/// <summary>
	/// Gets the type that lacked a shape, when known.
	/// </summary>
	public Type? UnshapedType { get; }

	/// <summary>
	/// Gets the provider that was consulted, when known.
	/// </summary>
	public ITypeShapeProvider? Provider { get; }
}
