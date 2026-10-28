// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NET11_0_OR_GREATER
#pragma warning disable SA1402 // Related compiler polyfills.
#pragma warning disable SA1649 // Related compiler polyfills.

namespace System.Runtime.CompilerServices;

internal interface IUnion
{
	object? Value { get; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
internal sealed class UnionAttribute : Attribute;
#endif
