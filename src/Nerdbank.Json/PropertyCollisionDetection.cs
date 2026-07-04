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
	private readonly HashSet<string> propertyNames;

	internal PropertyCollisionDetection(StringComparer comparer)
	{
		this.propertyNames = new(comparer);
	}

	internal void MarkAsRead(string propertyName)
	{
		if (!this.propertyNames.Add(propertyName))
		{
			ThrowAlreadyAssigned(propertyName);
		}
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
