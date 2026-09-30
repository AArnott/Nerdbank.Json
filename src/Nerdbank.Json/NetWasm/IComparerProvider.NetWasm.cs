// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETWASM
#pragma warning disable SA1600, CS1591

// NetWasm (netwasm0.1) proof of concept: Nerdbank.MessagePack is not referenced on netwasm.
// This internal stand-in keeps the internal plumbing compiling. No comparer provider is ever set on netwasm,
// so the (hash-collision resistant) SecureComparerProvider default is not available there.
namespace MessagePack;

internal interface IComparerProvider
{
	IEqualityComparer<T>? GetEqualityComparer<T>(ITypeShape<T> shape);

	IComparer<T>? GetComparer<T>(ITypeShape<T> shape);
}
#endif
