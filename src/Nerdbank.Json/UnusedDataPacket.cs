// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// When used as the type of a property or field on a data type,
/// captures deserialized JSON members that could not be assigned to a known property.
/// </summary>
/// <remarks>
/// Only one member of this type is allowed per data type.
/// A setter is required to capture data, and a getter is required to write it back.
/// Use <see cref="PropertyShapeAttribute"/> to expose a private field or property.
/// The serializer initializes the packet only when unrecognized data is encountered.
/// Values are retained as raw JSON, including their original numeric formatting.
/// This packet is specific to JSON and is not interchangeable with other formats' packets.
/// </remarks>
[TypeShape(Kind = TypeShapeKind.None)]
public abstract class UnusedDataPacket
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UnusedDataPacket"/> class.
	/// </summary>
	private protected UnusedDataPacket()
	{
	}

	/// <summary>
	/// Stores unrecognized JSON object members.
	/// </summary>
	internal sealed class Map : UnusedDataPacket
	{
		private readonly Dictionary<string, string> values = new(StringComparer.Ordinal);

		/// <summary>
		/// Adds an unrecognized member and its raw JSON value.
		/// </summary>
		/// <param name="name">The decoded member name.</param>
		/// <param name="value">The raw JSON value.</param>
		internal void Add(string name, string value) => this.values.Add(name, value);

		/// <summary>
		/// Writes the retained object members.
		/// </summary>
		/// <param name="writer">The writer to use.</param>
		/// <param name="first">Whether no object members have been written yet.</param>
		internal void WriteTo(ref JsonWriter writer, ref bool first)
		{
			foreach (KeyValuePair<string, string> entry in this.values)
			{
				if (!first)
				{
					writer.WriteValueSeparator();
				}

				writer.WritePropertyName(entry.Key);
				writer.WriteRawValue(entry.Value);
				first = false;
			}
		}
	}
}
