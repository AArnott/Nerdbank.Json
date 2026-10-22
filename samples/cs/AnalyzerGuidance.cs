// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600, SA1601 // Documentation samples intentionally omit API comments.
#pragma warning disable SA1123, SA1124 // Regions select compilable documentation snippets.

using System;
using System.Collections.Generic;
using Nerdbank.Json;
using PolyType;

internal static partial class AnalyzerGuidanceSamples
{
	public static DerivedShapeMapping<object> CreateMapping()
	{
		#region ModernShapeMapping
		DerivedShapeMapping<object> mapping = new();
		mapping.Add<Model>(1);
		mapping.Add<List<Model>, Witness>(2);
		#endregion

		return mapping;
	}

	#region SupportedContract
	[GenerateShape]
	public partial class Model
	{
		[PropertyShape(Ignore = true)]
		public Action? Callback { get; set; }

		public Dictionary<Guid, int> Values { get; set; } = new();
	}

	#endregion

	[GenerateShapeFor<List<Model>>]
	public partial class Witness;
}
