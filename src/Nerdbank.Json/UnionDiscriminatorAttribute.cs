// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Marks the constant value that identifies an object as a union case.
/// </summary>
/// <remarks>
/// <para>
/// The member is always written with the object, whether or not the value is serialized through a union.
/// It is written ahead of the type's other properties. A <c>Key</c> index, including <c>Key(0)</c>, does not change that position:
/// JSON objects are name-keyed. <c>Key(0)</c> is the recommended index when the same member is also keyed
/// for another serializer, and it is not required.
/// </para>
/// <para>
/// On read the member is optional, including when the object is read as its own type.
/// When the member is present, the value must equal the declared constant or deserialization throws
/// <see cref="JsonSerializationException"/> with <see cref="JsonSerializationException.ErrorCode.DiscriminatorMismatch"/>.
/// The value is checked and is not assigned.
/// </para>
/// <para>
/// The member is a get-only property whose getter returns a constant.
/// A constant field is not part of the object shape; use a property or the type-level attribute instead.
/// It is never omitted as a default value. Supported constants are <see cref="string"/>, <see cref="bool"/>,
/// integer primitives, and enums. Apply <c>[UnionDiscriminator("kind", "create")]</c> to the type when it has
/// no such member; that name is the JSON property name and is not passed through the naming policy.
/// </para>
/// </remarks>
/// <example>
/// <code source="../../samples/cs/CSharpUnions.cs" region="DocumentChange" lang="C#" />
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class UnionDiscriminatorAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UnionDiscriminatorAttribute"/> class
	/// that uses the decorated member's constant value.
	/// </summary>
	public UnionDiscriminatorAttribute()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UnionDiscriminatorAttribute"/> class
	/// with the constant value of the decorated member.
	/// </summary>
	/// <param name="value">The constant <see cref="string"/>, <see cref="bool"/>, integer, or enum value.</param>
	public UnionDiscriminatorAttribute(object? value)
	{
		this.Value = value;
		this.HasValue = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UnionDiscriminatorAttribute"/> class
	/// for a type that has no discriminator member.
	/// </summary>
	/// <param name="propertyName">The JSON property name. It is written as given.</param>
	/// <param name="value">The constant <see cref="string"/>, <see cref="bool"/>, integer, or enum value.</param>
	public UnionDiscriminatorAttribute(string propertyName, object? value)
	{
		this.PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
		this.Value = value;
		this.HasValue = true;
	}

	/// <summary>
	/// Gets the JSON property name used by the type-level form of this attribute.
	/// </summary>
	/// <value>The JSON name, or <see langword="null"/> when the attribute is applied to a member.</value>
	public string? PropertyName { get; }

	/// <summary>
	/// Gets the declared constant, when this attribute supplies one.
	/// </summary>
	/// <value>The constant, or <see langword="null"/> when the decorated member's value is used.</value>
	public object? Value { get; }

	/// <summary>
	/// Gets a value indicating whether <see cref="Value"/> was supplied.
	/// </summary>
	/// <value><see langword="true"/> when a constructor argument supplied the constant.</value>
	public bool HasValue { get; }
}
