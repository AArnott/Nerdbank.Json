// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

internal sealed class LowLevelSegment : ReadOnlySequenceSegment<byte>
{
	internal LowLevelSegment(ReadOnlyMemory<byte> memory) => this.Memory = memory;

	internal LowLevelSegment Append(ReadOnlyMemory<byte> memory)
	{
		LowLevelSegment next = new(memory) { RunningIndex = this.RunningIndex + this.Memory.Length };
		this.Next = next;
		return next;
	}
}
