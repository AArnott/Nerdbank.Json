// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Json;

/// <summary>
/// Defines an experimental discriminator-free union identified by required serialized property names.
/// </summary>
/// <remarks>
/// <para>
/// A case matches when every required property name is present.
/// If several cases match, the unique case whose required names are a strict superset of each of the others is selected.
/// An object case with no required properties is the catch-all and is used only when no other case matches.
/// At most one catch-all is allowed.
/// Ambiguous input, and input that matches nothing when there is no catch-all, is rejected.
/// There is no fallback to a base contract.
/// </para>
/// <para>
/// A concrete base type is included as a case. An abstract or interface base is not.
/// Required members follow object deserialization: constructor parameters, C# <c>required</c> members, and members flagged by <see cref="PropertyShapeAttribute"/>.
/// Optional properties and computed getters are not evidence.
/// A case whose required members cannot be read is rejected, every case must be an object, and at least two concrete cases are required.
/// </para>
/// <para>
/// Serialized names honor <see cref="PropertyShapeAttribute.Name"/>, the serializer's naming policy, and its case-insensitive setting.
/// Those names are resolved when the converter is created, because one union can be reused with different serializers.
/// Cases that require the same names under that policy are rejected then.
/// The value is buffered so every case can be considered.
/// Custom case converters are used, but they do not change which names count as evidence.
/// </para>
/// </remarks>
[Experimental("DuckTyping")]
public class DerivedTypeDuckTyping : DerivedTypeUnion
{
	/// <summary>Initializes a new instance of the <see cref="DerivedTypeDuckTyping"/> class.</summary>
	/// <param name="baseShape">The base type shape.</param>
	/// <param name="derivedTypeShapes">The concrete derived type shapes.</param>
	/// <exception cref="ArgumentNullException"><paramref name="baseShape"/> or a derived shape is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">The cases are duplicated, not concrete or assignable, number fewer than two, are not objects, include more than one catch-all, or declare required members that cannot be read.</exception>
	public DerivedTypeDuckTyping(ITypeShape baseShape, params ReadOnlySpan<ITypeShape> derivedTypeShapes)
	{
		Requires.NotNull(baseShape);
		this.BaseShape = baseShape;
		HashSet<Type> types = [];
		if (!baseShape.Type.IsAbstract && !baseShape.Type.IsInterface)
		{
			types.Add(baseShape.Type);
		}

		foreach (ITypeShape shape in derivedTypeShapes)
		{
			Requires.NotNull(shape);
			Requires.Argument(baseShape.Type.IsAssignableFrom(shape.Type) && !shape.Type.IsAbstract && !shape.Type.IsInterface, nameof(derivedTypeShapes), "Derived types must be concrete and assignable to the base type.");
			Requires.Argument(types.Add(shape.Type), nameof(derivedTypeShapes), "Duplicate type shape.");
		}

		Requires.Argument(types.Count > 1, nameof(derivedTypeShapes), "At least two concrete type shapes must be provided.");
		int catchAlls = 0;
		if (types.Contains(baseShape.Type))
		{
			ValidateCase(baseShape, nameof(baseShape), ref catchAlls);
		}

		foreach (ITypeShape shape in derivedTypeShapes)
		{
			ValidateCase(shape, nameof(derivedTypeShapes), ref catchAlls);
		}

		this.DerivedShapes = derivedTypeShapes.ToArray();
	}

	/// <inheritdoc/>
	public override Type BaseType => this.BaseShape.Type;

	/// <summary>Gets the explicit base shape.</summary>
	internal ITypeShape BaseShape { get; }

	/// <summary>Gets the explicit derived shapes.</summary>
	internal ReadOnlyMemory<ITypeShape> DerivedShapes { get; }

	/// <inheritdoc/>
	internal override void InternalDerivationsOnly() => throw new NotImplementedException();

	/// <summary>Validates requirements that do not depend on serializer configuration.</summary>
	/// <param name="shape">The case shape.</param>
	/// <param name="parameterName">The name of the argument supplying the shape.</param>
	/// <param name="catchAlls">The number of catch-all cases accepted so far.</param>
	private static void ValidateCase(ITypeShape shape, string parameterName, ref int catchAlls)
	{
		Requires.Argument(RuntimeUnionHelpers.IsObjectShape(shape), parameterName, $"Duck-typing union case '{shape.Type.FullName}' must be an object.");
		Requires.Argument(
			!RuntimeUnionHelpers.HasRequiredPropertyThatCannotBeRead(shape),
			parameterName,
			$"Duck-typing union case '{shape.Type.FullName}' has required members that cannot be read, so it cannot be identified or used as a catch-all.");
		if (RuntimeUnionHelpers.GetRequiredProperties(shape).Any())
		{
			return;
		}

		catchAlls++;
		Requires.Argument(catchAlls == 1, parameterName, "A duck-typing union can include only one case with no required properties.");
	}
}
