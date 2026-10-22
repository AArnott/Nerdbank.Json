// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Json;

/// <summary>
/// Indicates that the decorated type or member has a preferred modern .NET alternative.
/// </summary>
/// <param name="advice">The advice to include in the diagnostic.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property)]
#pragma warning disable CS9113 // The analyzer reads the constructor argument from metadata.
internal sealed class PreferDotNetAlternativeApiAttribute(string advice) : Attribute;
#pragma warning restore CS9113
