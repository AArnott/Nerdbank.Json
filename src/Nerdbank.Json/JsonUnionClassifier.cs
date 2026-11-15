// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Chooses a C# union case from the JSON value about to be read.
/// </summary>
/// <remarks>
/// Register it with <see cref="UnionClassifierAttribute"/>.
/// The method is synchronous. An asynchronous read buffers the union value first, then calls it.
/// Return a type listed in <see cref="UnionClassifierContext.Cases"/>, or <see langword="null"/> to continue with required properties and the first-declared rule.
/// A type outside that list throws <see cref="JsonSerializationException"/>.
/// An exception thrown here is wrapped in <see cref="JsonSerializationException"/>.
/// <see cref="UnionTagging.Tagged"/> never calls a classifier.
/// </remarks>
public abstract class JsonUnionClassifier
{
	/// <summary>
	/// Chooses a case for the union value at the reader's current position.
	/// </summary>
	/// <param name="reader">A reader positioned at the start of the union value. Consuming it does not consume the value the selected case reads.</param>
	/// <param name="context">The union, the cases that accept this JSON kind, and the active serialization operation.</param>
	/// <returns>The case type to deserialize, or <see langword="null"/> to keep classifying.</returns>
	public abstract Type? Classify(ref JsonReader reader, UnionClassifierContext context);
}
