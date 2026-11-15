// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Describes the C# union a <see cref="JsonUnionClassifier"/> is reading.
/// </summary>
public sealed class UnionClassifierContext
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UnionClassifierContext"/> class.
	/// </summary>
	/// <param name="unionType">The union type being read.</param>
	/// <param name="cases">The union's case types, in declaration order.</param>
	/// <param name="candidates">The case types that can still accept the JSON value.</param>
	/// <param name="catchAllCase">The catch-all case, or <see langword="null"/> when the union has none.</param>
	/// <param name="serializationContext">The serialization operation that is reading the union.</param>
	internal UnionClassifierContext(Type unionType, IReadOnlyList<Type> cases, IReadOnlyList<Type> candidates, Type? catchAllCase, SerializationContext serializationContext)
	{
		this.UnionType = unionType;
		this.Cases = cases;
		this.Candidates = candidates;
		this.CatchAllCase = catchAllCase;
		this.SerializationContext = serializationContext;
	}

	/// <summary>
	/// Gets the union type being read.
	/// </summary>
	public Type UnionType { get; }

	/// <summary>
	/// Gets the union's case types, in declaration order.
	/// </summary>
	public IReadOnlyList<Type> Cases { get; }

	/// <summary>
	/// Gets the case types that can still accept this JSON value after its kind is known.
	/// </summary>
	public IReadOnlyList<Type> Candidates { get; }

	/// <summary>
	/// Gets the object case with no required properties and no discriminator, when the union has exactly one.
	/// </summary>
	public Type? CatchAllCase { get; }

	/// <summary>
	/// Gets the serialization operation that is reading the union.
	/// </summary>
	public SerializationContext SerializationContext { get; }
}
