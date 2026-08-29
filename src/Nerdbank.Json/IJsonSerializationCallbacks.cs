// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Receives notifications immediately before an object is serialized and after it is deserialized.
/// </summary>
/// <remarks>
/// <para>
/// These callbacks are invoked by shape-generated object converters. A custom converter controls its own
/// representation and is responsible for invoking callbacks when that is appropriate.
/// </para>
/// <para>
/// Callback exceptions are propagated to the serializer caller. Implementations should avoid initiating
/// another top-level serialization operation for the same object.
/// </para>
/// </remarks>
public interface IJsonSerializationCallbacks
{
	/// <summary>
	/// Performs any final validation or state preparation before the object's properties are read for serialization.
	/// </summary>
	void OnBeforeSerialize();

	/// <summary>
	/// Restores invariants or computed state after the object's constructor, properties, and extension data have been applied.
	/// </summary>
	void OnAfterDeserialize();
}
