// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Json;

/// <summary>
/// Selects the classifier that reads one C# union when the JSON kind and discriminator do not settle it.
/// </summary>
/// <remarks>
/// <para>
/// The classifier runs for <see cref="UnionTagging.Auto"/> and <see cref="UnionTagging.Untagged"/> after a
/// <see cref="UnionDiscriminatorAttribute"/> and before required properties or the first-declared rule.
/// <see cref="UnionTagging.Tagged"/> does not call it.
/// Return <see langword="null"/> to continue. A returned type must be one of the union's cases.
/// </para>
/// <para>
/// This example reads a GUID-shaped string as <see cref="Guid"/>. Any other string continues, and the first-declared rule selects <c>string</c>.
/// </para>
/// <example>
/// <code>
/// public sealed class GuidStringClassifier : JsonUnionClassifier
/// {
///     public override Type? Classify(ref JsonReader reader, UnionClassifierContext context)
///     {
///         if (reader.NextJsonType == JsonValueKind.String &amp;&amp; Guid.TryParse(reader.ReadString(), out _))
///         {
///             return typeof(Guid);
///         }
///
///         return null;
///     }
/// }
///
/// [GenerateShape]
/// [UnionClassifier(typeof(GuidStringClassifier))]
/// public partial union Id(string, Guid);
/// // "3f2504e0-4f89-11d3-9a0c-0305e82c3301" is a Guid. "abc" is a string.
/// </code>
/// </example>
/// </remarks>
/// <param name="classifierType">The <see cref="JsonUnionClassifier"/> to construct. It must have a public parameterless constructor.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class UnionClassifierAttribute([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type classifierType) : Attribute
{
	/// <summary>
	/// Gets the classifier type to construct.
	/// </summary>
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
	public Type ClassifierType { get; } = classifierType ?? throw new ArgumentNullException(nameof(classifierType));
}
