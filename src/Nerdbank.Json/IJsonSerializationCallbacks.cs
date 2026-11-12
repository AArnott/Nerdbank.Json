// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Receives notifications around the serialization and deserialization of a reference type.
/// </summary>
/// <remarks>
/// <para>
/// This interface is supported only on reference types. Calling the methods on a struct boxes a copy,
/// and mutations made by the callback are lost.
/// </para>
/// <para>
/// These callbacks are invoked by shape-generated object converters. A custom converter controls its own
/// representation and is responsible for invoking callbacks when that is appropriate.
/// </para>
/// <para>
/// Callback exceptions are propagated to the serializer caller. Implementations should avoid initiating
/// another top-level serialization operation for the same object.
/// </para>
/// <para>
/// On modern .NET, each method has an empty default implementation so an existing implementer keeps compiling
/// when it only supplies the callbacks it needs. On .NET Framework and .NET Standard 2.0 the methods are abstract,
/// matching <c>Nerdbank.MessagePack.IMessagePackSerializationCallbacks</c>.
/// </para>
/// </remarks>
public interface IJsonSerializationCallbacks
{
	/// <summary>
	/// Performs any final validation or state preparation before the object's properties are read for serialization.
	/// </summary>
	void OnBeforeSerialize()
#if NET
	{
	}
#else
	;
#endif

	/// <summary>
	/// Performs any additional operations on an object after its properties and extension data have been written.
	/// </summary>
	/// <remarks>
	/// Changes made by this callback are not written. It is not called when serialization of the object fails.
	/// </remarks>
	void OnAfterSerialize()
#if NET
	{
	}
#else
	;
#endif

	/// <summary>
	/// Performs any additional operations on an object before any properties are set.
	/// </summary>
	/// <remarks>
	/// A converter may not call this method if it is not supported.
	/// In particular, types with <see langword="required" /> or <see langword="init" /> properties
	/// or a deserializing constructor should expect this method to not be called
	/// if the default converter is used.
	/// </remarks>
	void OnBeforeDeserialize()
#if NET
	{
	}
#else
	;
#endif

	/// <summary>
	/// Restores invariants or computed state after the object's constructor, properties, and extension data have been applied.
	/// </summary>
	void OnAfterDeserialize()
#if NET
	{
	}
#else
	;
#endif
}
