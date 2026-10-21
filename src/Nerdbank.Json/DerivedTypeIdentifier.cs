// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Nerdbank.Json;

/// <summary>
/// Identifies a derived type using a string or integer alias.
/// </summary>
public struct DerivedTypeIdentifier : IEquatable<DerivedTypeIdentifier>
{
	private readonly string? stringAlias;
	private readonly int? intAlias;
	private readonly ReadOnlyMemory<byte> utf8Alias;

	/// <summary>Initializes a new instance of the <see cref="DerivedTypeIdentifier"/> struct.</summary>
	/// <param name="alias">The integer alias.</param>
	public DerivedTypeIdentifier(int alias)
	{
		this.intAlias = alias;
	}

	/// <summary>Initializes a new instance of the <see cref="DerivedTypeIdentifier"/> struct.</summary>
	/// <param name="alias">The string alias.</param>
	public DerivedTypeIdentifier(string alias)
	{
		Requires.NotNull(alias);
		this.stringAlias = alias;
		this.utf8Alias = Encoding.UTF8.GetBytes(alias);
	}

	/// <summary>The supported alias kinds.</summary>
	public enum AliasType
	{
		/// <summary>An uninitialized identifier.</summary>
		None,

		/// <summary>An integer alias.</summary>
		Integer,

		/// <summary>A string alias.</summary>
		String,
	}

	/// <summary>Gets the alias kind.</summary>
	public AliasType Type => this.stringAlias is not null ? AliasType.String : this.intAlias is not null ? AliasType.Integer : AliasType.None;

	/// <summary>Gets the string alias.</summary>
	/// <exception cref="InvalidOperationException">The identifier is not a string.</exception>
	public string StringAlias => this.stringAlias ?? throw new InvalidOperationException();

	/// <summary>Gets the integer alias.</summary>
	/// <exception cref="InvalidOperationException">The identifier is not an integer.</exception>
	public int IntAlias => this.intAlias ?? throw new InvalidOperationException();

	/// <summary>Gets the UTF-8 encoding of the string alias.</summary>
	/// <exception cref="InvalidOperationException">The identifier is not a string.</exception>
	public ReadOnlyMemory<byte> Utf8Alias => this.stringAlias is not null ? this.utf8Alias : throw new InvalidOperationException();

	/// <summary>Converts a string to an identifier.</summary>
	/// <param name="alias">The alias.</param>
	public static implicit operator DerivedTypeIdentifier(string alias) => new(alias);

	/// <summary>Converts an integer to an identifier.</summary>
	/// <param name="alias">The alias.</param>
	public static implicit operator DerivedTypeIdentifier(int alias) => new(alias);

	/// <inheritdoc/>
	public bool Equals(DerivedTypeIdentifier other) => this.stringAlias == other.stringAlias && this.intAlias == other.intAlias;

	/// <inheritdoc/>
	public override bool Equals([NotNullWhen(true)] object? obj) => obj is DerivedTypeIdentifier other && this.Equals(other);

	/// <inheritdoc/>
	public override int GetHashCode() => this.stringAlias?.GetHashCode() ?? this.intAlias?.GetHashCode() ?? 0;
}
