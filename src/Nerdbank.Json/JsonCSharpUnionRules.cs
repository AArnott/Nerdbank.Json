// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1204 // Keep helper methods grouped by their dependent state.
#pragma warning disable SA1402 // This file contains closely related policy helpers.
#pragma warning disable SA1600 // Internal policy helpers are intentionally undocumented.
#pragma warning disable SA1602 // Internal enum members are intentionally undocumented.
#pragma warning disable SA1649 // The file is named after the policy.

namespace Nerdbank.Json;

internal enum JsonCSharpUnionCaseRepresentation
{
	Tagged,
	Boolean,
	Number,
	String,
	Array,
	Object,

	// A non-recursive C# union case whose inner cases are written and read directly.
	Nested,
}

internal sealed class JsonCSharpUnionFormat
{
	private readonly bool tagObjects;
	private readonly HashSet<JsonCSharpUnionCaseRepresentation>? collided;

	internal JsonCSharpUnionFormat(IReadOnlyList<JsonCSharpUnionCaseRepresentation> representations, UnionTagging mode)
	{
		// Auto and Untagged already decided which cases are tagged. Tagged still wraps colliding kinds.
		if (mode != UnionTagging.Tagged)
		{
			return;
		}

		bool hasTagged = false;
		Dictionary<JsonCSharpUnionCaseRepresentation, int> counts = [];
		foreach (JsonCSharpUnionCaseRepresentation representation in representations)
		{
			if (representation == JsonCSharpUnionCaseRepresentation.Tagged)
			{
				hasTagged = true;
				continue;
			}

			counts.TryGetValue(representation, out int count);
			counts[representation] = count + 1;
		}

		foreach (KeyValuePair<JsonCSharpUnionCaseRepresentation, int> pair in counts)
		{
			if (pair.Value <= 1)
			{
				continue;
			}

			if (mode == UnionTagging.Untagged)
			{
				throw new NotSupportedException($"C# union cases with the '{pair.Key}' JSON representation require explicit case identifiers.");
			}

			(this.collided ??= []).Add(pair.Key);
		}

		bool objectCollidesWithEnvelope = counts.ContainsKey(JsonCSharpUnionCaseRepresentation.Object) && (hasTagged || this.collided is not null);
		if (objectCollidesWithEnvelope)
		{
			if (mode == UnionTagging.Untagged)
			{
				throw new NotSupportedException("C# union object cases cannot be distinguished from a $type wrapper without explicit case identifiers.");
			}

			this.tagObjects = true;
		}
	}

	internal bool IsTagged(JsonCSharpUnionCaseRepresentation representation)
		=> representation == JsonCSharpUnionCaseRepresentation.Tagged
			|| (this.tagObjects && representation == JsonCSharpUnionCaseRepresentation.Object)
			|| (this.collided?.Contains(representation) ?? false);
}

internal static class JsonCSharpUnionRules
{
	internal static UnionTagging ResolveMode(IUnionTypeShape unionShape, ConverterCache owner)
	{
		if (owner.UnionTaggingProvider?.GetTagging(unionShape) is UnionTagging provided)
		{
			return provided;
		}

		if (unionShape.AttributeProvider is not null)
		{
			foreach (UnionTaggingAttribute attribute in unionShape.AttributeProvider.GetCustomAttributes<UnionTaggingAttribute>(inherit: false))
			{
				return attribute.Mode;
			}
		}

		return owner.DefaultUnionTagging;
	}

	internal static JsonCSharpUnionCaseRepresentation[] Plan(IUnionTypeShape unionShape, ConverterCache owner, UnionTagging mode)
	{
		HashSet<Type> stack = [];
		return Plan(unionShape, owner, mode, stack, aliases: null);
	}

	internal static JsonCSharpUnionCaseRepresentation[] Plan(IUnionTypeShape unionShape, ConverterCache owner, UnionTagging mode, UnionCaseAliasesResolution aliases)
	{
		HashSet<Type> stack = [];
		return Plan(unionShape, owner, mode, stack, aliases);
	}

	internal static JsonCSharpUnionCaseRepresentation GetRepresentation(ITypeShape shape, bool hasExplicitIdentifier, ConverterCache owner, UnionTagging mode)
		=> GetRepresentation(shape, hasExplicitIdentifier, owner, mode, ancestors: null);

	internal static JsonCSharpUnionCaseRepresentation GetRepresentation(ITypeShape shape, bool hasExplicitIdentifier, ConverterCache owner, UnionTagging mode, HashSet<Type>? ancestors)
	{
		if (hasExplicitIdentifier || owner.ShouldPreserveReferences(shape.Type) || (owner.TryGetCustomConverter(shape, out JsonConverter? customConverter) && customConverter is not null))
		{
			return JsonCSharpUnionCaseRepresentation.Tagged;
		}

		Type type = shape.Type;
		if (type == typeof(bool))
		{
			return JsonCSharpUnionCaseRepresentation.Boolean;
		}

		if (IsNumber(type))
		{
			return JsonCSharpUnionCaseRepresentation.Number;
		}

		if (IsString(type))
		{
			return JsonCSharpUnionCaseRepresentation.String;
		}

		if (type == typeof(System.Drawing.Point))
		{
			return JsonCSharpUnionCaseRepresentation.Array;
		}

		if (shape.Kind is TypeShapeKind.Enumerable)
		{
			return JsonCSharpUnionCaseRepresentation.Array;
		}

		if (shape.Kind is TypeShapeKind.Object or TypeShapeKind.Dictionary)
		{
			return mode == UnionTagging.Tagged
				? JsonCSharpUnionCaseRepresentation.Tagged
				: JsonCSharpUnionCaseRepresentation.Object;
		}

		if (shape is IUnionTypeShape union && union.UnionKind == UnionTypeShapeKind.CSharpUnion)
		{
			if (mode == UnionTagging.Tagged || (ancestors is not null && Reaches(shape, ancestors)))
			{
				return JsonCSharpUnionCaseRepresentation.Tagged;
			}

			return JsonCSharpUnionCaseRepresentation.Nested;
		}

		return JsonCSharpUnionCaseRepresentation.Tagged;
	}

	internal static JsonCSharpUnionCaseRepresentation[] Plan(IUnionTypeShape unionShape, ConverterCache owner, UnionTagging mode, HashSet<Type> stack)
		=> Plan(unionShape, owner, mode, stack, aliases: null);

	private static JsonCSharpUnionCaseRepresentation[] Plan(IUnionTypeShape unionShape, ConverterCache owner, UnionTagging mode, HashSet<Type> stack, UnionCaseAliasesResolution? aliases)
	{
		if (!stack.Add(unionShape.Type))
		{
			JsonCSharpUnionCaseRepresentation[] tagged = new JsonCSharpUnionCaseRepresentation[unionShape.UnionCases.Count];
			for (int i = 0; i < tagged.Length; i++)
			{
				tagged[i] = JsonCSharpUnionCaseRepresentation.Tagged;
			}

			return tagged;
		}

		try
		{
			UnionCaseAliasesResolution resolved = aliases ?? UnionCaseAliases.ResolveDetailed(unionShape, owner.UnionTypeAliasProvider);
			JsonCSharpUnionCaseRepresentation[] representations = new JsonCSharpUnionCaseRepresentation[unionShape.UnionCases.Count];
			for (int i = 0; i < representations.Length; i++)
			{
				IUnionCaseShape unionCase = unionShape.UnionCases[i];
				representations[i] = !unionShape.Type.IsValueType && unionCase.IsNullable
					? JsonCSharpUnionCaseRepresentation.Tagged
					: GetRepresentation(unionCase.UnionCaseType, resolved.ExplicitlySpecified[i], owner, mode, stack);
			}

			if (mode != UnionTagging.Tagged)
			{
				ApplyClassifiability(unionShape, representations, owner, mode, stack);
			}

			return representations;
		}
		finally
		{
			stack.Remove(unionShape.Type);
		}
	}

	private static void ApplyClassifiability(IUnionTypeShape unionShape, JsonCSharpUnionCaseRepresentation[] representations, ConverterCache owner, UnionTagging mode, HashSet<Type> stack)
	{
		List<ObjectFact> facts = [];
		for (int i = 0; i < representations.Length; i++)
		{
			if (representations[i] is JsonCSharpUnionCaseRepresentation.Object or JsonCSharpUnionCaseRepresentation.Nested)
			{
				CollectObjectFacts(i, unionShape.UnionCases[i].UnionCaseType, representations[i], owner, stack, facts);
			}
		}

		if (facts.Count == 0)
		{
			return;
		}

		int catchAlls = 0;
		for (int i = 0; i < facts.Count; i++)
		{
			if (facts[i].IsCatchAll)
			{
				catchAlls++;
			}
		}

		bool[] unclassifiable = new bool[representations.Length];
		for (int i = 0; i < facts.Count; i++)
		{
			if (!IsUniqueWinner(facts, i, catchAlls))
			{
				unclassifiable[facts[i].CaseIndex] = true;
			}
		}

		for (int i = 0; i < representations.Length; i++)
		{
			if (!unclassifiable[i])
			{
				continue;
			}

			if (mode == UnionTagging.Untagged)
			{
				throw new NotSupportedException($"C# union '{unionShape.Type.FullName}' has object cases that cannot be distinguished without a $type wrapper. Untagged mode requires every object case to be recognizable.");
			}

			representations[i] = JsonCSharpUnionCaseRepresentation.Tagged;
		}
	}

	private static void CollectObjectFacts(int caseIndex, ITypeShape shape, JsonCSharpUnionCaseRepresentation representation, ConverterCache owner, HashSet<Type> stack, List<ObjectFact> facts)
	{
		if (representation == JsonCSharpUnionCaseRepresentation.Nested && shape is IUnionTypeShape nested && nested.UnionKind == UnionTypeShapeKind.CSharpUnion)
		{
			UnionTagging innerMode = ResolveMode(nested, owner);
			JsonCSharpUnionCaseRepresentation[] inner = Plan(nested, owner, innerMode, stack);
			for (int i = 0; i < inner.Length; i++)
			{
				if (inner[i] is JsonCSharpUnionCaseRepresentation.Object or JsonCSharpUnionCaseRepresentation.Nested)
				{
					CollectObjectFacts(caseIndex, nested.UnionCases[i].UnionCaseType, inner[i], owner, stack, facts);
				}
			}

			return;
		}

		if (representation != JsonCSharpUnionCaseRepresentation.Object || shape.Kind != TypeShapeKind.Object)
		{
			return;
		}

		UnionDiscriminatorBinding? discriminator = UnionDiscriminatorBinding.TryGet(shape, owner);
		HashSet<string> required = RuntimeUnionHelpers.GetRequiredSerializedPropertyNames(shape, owner);
		if (discriminator is not null)
		{
			required.Remove(discriminator.Name);
		}

		facts.Add(new ObjectFact(caseIndex, required, discriminator is not null));
	}

	private static bool IsUniqueWinner(List<ObjectFact> facts, int index, int catchAlls)
	{
		ObjectFact fact = facts[index];
		if (fact.HasDiscriminator)
		{
			return true;
		}

		int matches = 0;
		int winner = -1;
		for (int i = 0; i < facts.Count; i++)
		{
			if (facts[i].IsCatchAll || !IsSubset(facts[i].Required, fact.Required))
			{
				continue;
			}

			matches++;
			winner = i;
		}

		if (matches == 0)
		{
			return fact.IsCatchAll && catchAlls == 1;
		}

		if (matches == 1)
		{
			return winner == index;
		}

		for (int candidate = 0; candidate < facts.Count; candidate++)
		{
			if (facts[candidate].IsCatchAll || !IsSubset(facts[candidate].Required, fact.Required))
			{
				continue;
			}

			bool strictOverAll = true;
			for (int other = 0; other < facts.Count; other++)
			{
				if (other == candidate || facts[other].IsCatchAll || !IsSubset(facts[other].Required, fact.Required))
				{
					continue;
				}

				if (!IsStrictSuperset(facts[candidate].Required, facts[other].Required))
				{
					strictOverAll = false;
					break;
				}
			}

			if (strictOverAll)
			{
				return candidate == index;
			}
		}

		return false;
	}

	private static bool IsSubset(HashSet<string> required, HashSet<string> present)
	{
		foreach (string name in required)
		{
			if (!present.Contains(name))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsStrictSuperset(HashSet<string> candidate, HashSet<string> other)
	{
		if (candidate.Count <= other.Count)
		{
			return false;
		}

		foreach (string name in other)
		{
			if (!candidate.Contains(name))
			{
				return false;
			}
		}

		return true;
	}

	private static bool Reaches(ITypeShape shape, HashSet<Type> targets)
	{
		HashSet<Type> seen = [];
		return Reaches(shape, targets, seen);
	}

	private static bool Reaches(ITypeShape shape, HashSet<Type> targets, HashSet<Type> seen)
	{
		if (!seen.Add(shape.Type))
		{
			return false;
		}

		if (targets.Contains(shape.Type))
		{
			return true;
		}

		if (shape is IUnionTypeShape union)
		{
			foreach (IUnionCaseShape unionCase in union.UnionCases)
			{
				if (Reaches(unionCase.UnionCaseType, targets, seen))
				{
					return true;
				}
			}
		}

		return false;
	}

	private static bool IsNumber(Type type)
		=> type == typeof(byte)
			|| type == typeof(sbyte)
			|| type == typeof(short)
			|| type == typeof(ushort)
			|| type == typeof(int)
			|| type == typeof(uint)
			|| type == typeof(long)
			|| type == typeof(ulong)
			|| type == typeof(float)
			|| type == typeof(double)
			|| type == typeof(decimal)
			|| type == typeof(System.Numerics.BigInteger)
			|| type == typeof(System.Drawing.Color)
#if NET
			|| type == typeof(Half)
			|| type == typeof(Int128)
			|| type == typeof(UInt128)
#endif
			;

	private static bool IsString(Type type)
		=> type == typeof(string)
			|| type == typeof(char)
			|| type == typeof(DateTime)
			|| type == typeof(DateTimeOffset)
			|| type == typeof(TimeSpan)
			|| type == typeof(Guid)
			|| type == typeof(Version)
			|| type == typeof(Uri)
			|| type == typeof(System.Globalization.CultureInfo)
			|| type == typeof(System.Text.Encoding)
			|| type == typeof(byte[])
			|| type == typeof(Memory<byte>)
			|| type == typeof(ReadOnlyMemory<byte>)
#if NET
			|| type == typeof(DateOnly)
			|| type == typeof(TimeOnly)
			|| type == typeof(System.Text.Rune)
#endif
			;

	private readonly struct ObjectFact
	{
		internal ObjectFact(int caseIndex, HashSet<string> required, bool hasDiscriminator)
		{
			this.CaseIndex = caseIndex;
			this.Required = required;
			this.HasDiscriminator = hasDiscriminator;
			this.IsCatchAll = !hasDiscriminator && required.Count == 0;
		}

		internal int CaseIndex { get; }

		internal HashSet<string> Required { get; }

		internal bool HasDiscriminator { get; }

		internal bool IsCatchAll { get; }
	}
}
