// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Json;

/// <summary>
/// Defines an experimental discriminator-free union identified by required serialized property names.
/// </summary>
/// <remarks>
/// Deserialization buffers the value and rejects ambiguous or insufficient evidence rather than falling back to the base contract.
/// A concrete base type is included as a case and must satisfy the same requirements as derived types.
/// Required members are validated during construction; their serialized names depend on the serializer's naming policy and are resolved when its converter is created.
/// </remarks>
[Experimental("DuckTyping")]
public class DerivedTypeDuckTyping : DerivedTypeUnion
{
	/// <summary>Initializes a new instance of the <see cref="DerivedTypeDuckTyping"/> class.</summary>
	/// <param name="baseShape">The base type shape.</param>
	/// <param name="derivedTypeShapes">The concrete derived type shapes.</param>
	/// <exception cref="ArgumentException">The cases are duplicated, not concrete or assignable, number fewer than two, or lack required object properties.</exception>
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
		if (types.Contains(baseShape.Type))
		{
			ValidateRequiredProperties(baseShape, nameof(baseShape));
		}

		foreach (ITypeShape shape in derivedTypeShapes)
		{
			ValidateRequiredProperties(shape, nameof(derivedTypeShapes));
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
	private static void ValidateRequiredProperties(ITypeShape shape, string parameterName)
		=> Requires.Argument(RuntimeUnionHelpers.GetRequiredProperties(shape).Any(), parameterName, $"Duck-typing union case '{shape.Type.FullName}' must have at least one required object property.");
}
