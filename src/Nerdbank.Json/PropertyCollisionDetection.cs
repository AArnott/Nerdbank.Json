// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Internal helper members are intentionally undocumented.

using System.Diagnostics.CodeAnalysis;

namespace Nerdbank.Json;

/// <summary>
/// Tracks object-property initialization to verify that no JSON property name is assigned more than once.
/// </summary>
internal struct PropertyCollisionDetection
{
	private readonly StringComparer comparer;
	private readonly bool[]? manyKnownPropertyNames;
	private ulong knownPropertyNames;
	private HashSet<string>? propertyNames;

	internal PropertyCollisionDetection(StringComparer comparer, int knownPropertyCount = 0)
	{
		this.comparer = comparer;
		this.manyKnownPropertyNames = knownPropertyCount > 64 ? new bool[knownPropertyCount] : null;
		this.knownPropertyNames = 0;
		this.propertyNames = null;
	}

	internal void MarkAsRead(string propertyName)
	{
		if (!(this.propertyNames ??= new(this.comparer)).Add(propertyName))
		{
			ThrowAlreadyAssigned(propertyName);
		}
	}

	internal void MarkAsRead(int knownPropertyIndex, string propertyName)
	{
		if (this.manyKnownPropertyNames is not null)
		{
			if (this.manyKnownPropertyNames[knownPropertyIndex])
			{
				ThrowAlreadyAssigned(propertyName);
			}

			this.manyKnownPropertyNames[knownPropertyIndex] = true;
			return;
		}

		ulong mask = 1UL << knownPropertyIndex;
		if ((this.knownPropertyNames & mask) != 0)
		{
			ThrowAlreadyAssigned(propertyName);
		}

		this.knownPropertyNames |= mask;
	}

	[DoesNotReturn]
	private static void ThrowAlreadyAssigned(string propertyName)
	{
		throw new JsonSerializationException($"The property '{propertyName}' has already been assigned a value.")
		{
			Code = JsonSerializationException.ErrorCode.DoublePropertyAssignment,
		};
	}
}
