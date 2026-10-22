// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Json;

/// <summary>
/// Selects the <see cref="IEqualityComparer{T}"/> or <see cref="IComparer{T}"/> that the deserializer
/// uses when it constructs the collection for the decorated dictionary or set member.
/// </summary>
/// <remarks>
/// <para>
/// The serializer already exposes a global <see cref="JsonSerializer.ComparerProvider"/>, but that provider
/// cannot preserve member-specific semantics such as a case-insensitive dictionary. Apply this attribute to a
/// property, field, or constructor parameter whose type is a dictionary or set to control the comparer
/// used for the freshly constructed collection during deserialization.
/// </para>
/// <para>
/// The comparer type must declare a public parameterless constructor
/// and implement <see cref="IEqualityComparer{T}"/> or <see cref="IComparer{T}"/> for the collection's key or
/// element type. It is activated through a source-generated type shape rather than runtime reflection, so the
/// feature remains trimming-safe and NativeAOT-compatible. The comparer only influences the instances the
/// deserializer creates; specify the same comparer in the member's initializer to influence instances that
/// application code creates.
/// </para>
/// <para>
/// Getter-only collection initializers remain the preferred zero-configuration alternative: when a member is
/// getter-only and already initialized with the desired comparer, the deserializer populates that instance and
/// this attribute is unnecessary.
/// </para>
/// </remarks>
/// <example>
/// <code source="../../samples/cs/CollectionComparers.cs" region="CollectionComparers" lang="C#" />
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
[AssociatedTypeAttribute("comparerType", TypeShapeRequirements.Constructor)]
[AssociatedTypeAttribute("comparerSource", TypeShapeRequirements.Constructor)]
public class UseComparerAttribute : Attribute
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UseComparerAttribute"/> class.
	/// </summary>
	/// <param name="comparerType">
	/// The comparer type to activate. It must declare a public parameterless constructor and implement
	/// <see cref="IEqualityComparer{T}"/> or <see cref="IComparer{T}"/> for the decorated collection's key or element type.
	/// </param>
	public UseComparerAttribute(
#if NET
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
		Type comparerType)
	{
		this.ComparerType = comparerType ?? throw new ArgumentNullException(nameof(comparerType));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="UseComparerAttribute"/> class with a comparer provider.
	/// </summary>
	/// <param name="comparerSource">The type that provides the comparer.</param>
	/// <param name="memberName">
	/// The name of a public property that returns the comparer. Static properties require no provider
	/// instance; instance properties require a public parameterless constructor on the provider.
	/// </param>
	public UseComparerAttribute(
#if NET
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
		Type comparerSource,
		string memberName)
	{
		this.ComparerType = comparerSource ?? throw new ArgumentNullException(nameof(comparerSource));
		this.MemberName = memberName ?? throw new ArgumentNullException(nameof(memberName));
	}

	/// <summary>
	/// Gets the comparer type activated for the decorated collection member.
	/// </summary>
#if NET
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
	public Type ComparerType { get; }

	/// <summary>
	/// Gets the name of the property on <see cref="ComparerType"/> that provides the comparer,
	/// or <see langword="null"/> when the type itself is the comparer.
	/// </summary>
	public string? MemberName { get; }
}
