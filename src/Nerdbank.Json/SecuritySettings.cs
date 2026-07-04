// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Security settings that may be applied to serialization.
/// </summary>
/// <remarks>
/// Applications <em>may</em> derive from this class to add additional settings
/// that its custom converters may honor.
/// Added settings should have secure defaults.
/// </remarks>
public record SecuritySettings
{
	/// <summary>
	/// Default settings to use when (de)serializing untrusted data.
	/// </summary>
	/// <remarks>
	/// This value is optimized for security when processing untrusted data.
	/// </remarks>
	public static readonly SecuritySettings UntrustedData = new();

	/// <summary>
	/// Default settings to use with trusted data.
	/// </summary>
	/// <remarks>
	/// This value is optimized for high performance assuming the data is trustworthy, and should not be used with untrusted data.
	/// </remarks>
	public static readonly SecuritySettings TrustedData = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="SecuritySettings"/> class
	/// with secure defaults (those matching the values found in <see cref="UntrustedData"/>).
	/// </summary>
	public SecuritySettings()
	{
	}
}
