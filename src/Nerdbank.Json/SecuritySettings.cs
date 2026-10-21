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
	public static readonly SecuritySettings TrustedData = new()
	{
		ExpandoObjectMaxPropertyCount = int.MaxValue,
		MaxObjectMemberCount = int.MaxValue,
	};

	/// <summary>
	/// Initializes a new instance of the <see cref="SecuritySettings"/> class
	/// with secure defaults (those matching the values found in <see cref="UntrustedData"/>).
	/// </summary>
	public SecuritySettings()
	{
		this.MaxObjectMemberCount = 1_000_000;
		this.ExpandoObjectMaxPropertyCount = 128;
	}

	/// <summary>
	/// Gets the maximum number of members permitted in a single untyped JSON object (or dynamic object such as
	/// <see cref="System.Dynamic.ExpandoObject"/>) when deserializing.
	/// </summary>
	/// <value>The default value is 1,000,000.</value>
	/// <remarks>
	/// This bounds memory and, for structures whose insertion cost grows with size, CPU when processing untrusted data.
	/// It applies only to the optional untyped and dynamic converters; strongly typed objects have a fixed member set.
	/// </remarks>
	public int MaxObjectMemberCount
	{
		get => field;
		init
		{
			Requires.Range(value > 0, nameof(value), "Value must be positive.");
			field = value;
		}
	}

	/// <summary>
	/// Gets the maximum number of properties permitted on a <see cref="System.Dynamic.ExpandoObject"/>
	/// during serialization and deserialization.
	/// </summary>
	/// <value>The default value is 128.</value>
	/// <remarks>
	/// ExpandoObject insertion has quadratic complexity. The limit also applies when writing so an
	/// oversized object fails early rather than producing output that cannot be read with the same settings.
	/// Deserialization also honors <see cref="MaxObjectMemberCount"/>.
	/// </remarks>
	public int ExpandoObjectMaxPropertyCount
	{
		get => field;
		init
		{
			Requires.Range(value > 0, nameof(value), "Value must be positive.");
			field = value;
		}
	}
}
