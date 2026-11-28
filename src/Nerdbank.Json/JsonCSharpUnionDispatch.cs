// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1202 // Read helpers read more clearly beside the scan they use.
#pragma warning disable SA1204 // Local classification helpers stay with the dispatcher.
#pragma warning disable SA1402 // The fact is an implementation detail of the dispatcher.
#pragma warning disable SA1600 // Internal classification helpers are intentionally undocumented.
#pragma warning disable SA1618 // The type parameter is an implementation detail.
#pragma warning disable SA1649 // The file is named after the dispatcher.

using System.Globalization;
using System.Numerics;
using System.Text;

namespace Nerdbank.Json;

internal sealed class JsonCSharpUnionDispatch<TUnion>
{
	private readonly ConverterCache owner;
	private readonly JsonUnionCaseMetadata<TUnion>[] cases;
	private readonly CaseFact[] facts;
	private readonly Type[] caseTypes;
	private readonly Type unionType;
	private readonly StringComparer nameComparer;
	private readonly JsonUnionClassifier? classifier;
	private readonly JsonConverter? nullConverter;
	private readonly bool hasEnvelope;
	private readonly int catchAllFact = -1;
	private readonly Type? catchAllCase;
	private readonly string? discriminatorName;
	private readonly string[] evidenceNames;
	private readonly int wordCount;
	private readonly ulong[] masks;

	internal JsonCSharpUnionDispatch(IUnionTypeShape unionShape, JsonUnionCaseMetadata<TUnion>[] cases, ConverterCache owner)
	{
		this.owner = owner;
		this.cases = cases;
		this.unionType = unionShape.Type;
		this.nameComparer = owner.PropertyNameComparer;
		this.caseTypes = new Type[unionShape.UnionCases.Count];
		List<CaseFact> built = [];
		HashSet<Type> stack = [unionShape.Type];
		for (int i = 0; i < cases.Length; i++)
		{
			IUnionCaseShape unionCase = unionShape.UnionCases[i];
			this.caseTypes[i] = unionCase.UnionCaseType.Type;
			if (cases[i].IsNullable && cases[i].Representation != JsonCSharpUnionCaseRepresentation.Tagged && this.nullConverter is null)
			{
				this.nullConverter = cases[i].Converter;
			}

			if (cases[i].Representation == JsonCSharpUnionCaseRepresentation.Tagged)
			{
				this.hasEnvelope = true;
				continue;
			}

			if (cases[i].Representation == JsonCSharpUnionCaseRepresentation.Nested)
			{
				this.Expand(i, unionCase.UnionCaseType, owner, stack, built);
			}
			else
			{
				built.Add(CaseFact.Create(i, unionCase.UnionCaseType, cases[i].Representation, owner));
			}
		}

		this.facts = built.ToArray();
		this.classifier = CreateClassifier(unionShape);
		List<string> names = [];
		Dictionary<string, int> evidenceIndex = new(this.nameComparer);
		for (int i = 0; i < this.facts.Length; i++)
		{
			if (this.facts[i].IsCatchAll)
			{
				this.catchAllFact = this.catchAllFact < 0 ? i : -2;
			}

			UnionDiscriminatorBinding? discriminator = this.facts[i].Discriminator;
			if (this.discriminatorName is null && discriminator is not null)
			{
				this.discriminatorName = discriminator.Name;
			}

			foreach (string name in this.facts[i].Required)
			{
				if (!evidenceIndex.ContainsKey(name))
				{
					evidenceIndex.Add(name, names.Count);
					names.Add(name);
				}
			}
		}

		this.evidenceNames = names.ToArray();
		this.wordCount = names.Count == 0 ? 0 : ((names.Count - 1) / 64) + 1;
		this.masks = new ulong[Math.Max(this.facts.Length, 1) * Math.Max(this.wordCount, 1)];
		if (this.wordCount > 0)
		{
			for (int i = 0; i < this.facts.Length; i++)
			{
				foreach (string name in this.facts[i].Required)
				{
					int bit = evidenceIndex[name];
					this.masks[(i * this.wordCount) + (bit / 64)] |= 1UL << (bit % 64);
				}
			}
		}

		if (this.catchAllFact >= 0)
		{
			this.catchAllCase = this.caseTypes[this.facts[this.catchAllFact].CaseIndex];
		}
	}

	internal bool TryGetUnique(char token, out JsonConverter converter)
	{
		converter = null!;
		if (token == 'n')
		{
			if (this.nullConverter is null)
			{
				return false;
			}

			converter = this.nullConverter;
			return true;
		}

		if (!TryMapToken(token, out JsonCSharpUnionCaseRepresentation representation))
		{
			return false;
		}

		int caseIndex = -1;
		bool any = false;
		foreach (CaseFact fact in this.facts)
		{
			if (fact.Representation != representation)
			{
				continue;
			}

			any = true;
			if (caseIndex < 0)
			{
				caseIndex = fact.CaseIndex;
			}
			else if (caseIndex != fact.CaseIndex)
			{
				return false;
			}
		}

		if (!any || (representation == JsonCSharpUnionCaseRepresentation.Object && this.hasEnvelope))
		{
			return false;
		}

		converter = this.cases[caseIndex].Converter;
		return true;
	}

	internal JsonConverter Select(ref JsonReader reader, SerializationContext context, JsonUnionObjectEnvelope? envelope, out bool useEnvelope)
	{
		useEnvelope = false;
		char token = reader.PeekValueToken();
		if (token == 'n')
		{
			return this.nullConverter ?? throw NoMatch(this.unionType);
		}

		if (!TryMapToken(token, out JsonCSharpUnionCaseRepresentation representation))
		{
			throw NoMatch(this.unionType);
		}

		int matchCount = 0;
		Span<int> matches = this.facts.Length <= 16 ? stackalloc int[16] : new int[this.facts.Length];
		for (int i = 0; i < this.facts.Length; i++)
		{
			if (this.facts[i].Representation == representation)
			{
				matches[matchCount++] = i;
			}
		}

		if (matchCount == 0)
		{
			if (token == '{' && envelope is not null)
			{
				useEnvelope = true;
				return null!;
			}

			throw NoMatch(this.unionType);
		}

		if (this.SharesCase(matches[..matchCount]) && !(representation == JsonCSharpUnionCaseRepresentation.Object && this.hasEnvelope))
		{
			return this.cases[this.facts[matches[0]].CaseIndex].Converter;
		}

		if (representation == JsonCSharpUnionCaseRepresentation.Object)
		{
			return this.SelectObject(ref reader, matches[..matchCount], context, envelope, out useEnvelope);
		}

		JsonConverter? classified = this.TryClassify(ref reader, matches[..matchCount], context);
		if (classified is not null)
		{
			return classified;
		}

		if (representation == JsonCSharpUnionCaseRepresentation.Array)
		{
			return this.SelectArray(ref reader, matches[..matchCount], context);
		}

		return this.SelectScalar(ref reader, matches[..matchCount], representation);
	}

	private static bool TryMapToken(char token, out JsonCSharpUnionCaseRepresentation representation)
	{
		representation = token switch
		{
			't' or 'f' => JsonCSharpUnionCaseRepresentation.Boolean,
			'"' => JsonCSharpUnionCaseRepresentation.String,
			'[' => JsonCSharpUnionCaseRepresentation.Array,
			'{' => JsonCSharpUnionCaseRepresentation.Object,
			'-' or (>= '0' and <= '9') => JsonCSharpUnionCaseRepresentation.Number,
			_ => JsonCSharpUnionCaseRepresentation.Tagged,
		};
		return representation != JsonCSharpUnionCaseRepresentation.Tagged;
	}

	private static JsonUnionClassifier? CreateClassifier(IUnionTypeShape unionShape)
	{
		if (unionShape.AttributeProvider is null)
		{
			return null;
		}

		UnionClassifierAttribute? attribute = null;
		foreach (UnionClassifierAttribute candidate in unionShape.AttributeProvider.GetCustomAttributes<UnionClassifierAttribute>(inherit: false))
		{
			attribute = candidate;
			break;
		}

		if (attribute is null)
		{
			return null;
		}

		if (!typeof(JsonUnionClassifier).IsAssignableFrom(attribute.ClassifierType))
		{
			throw new NotSupportedException($"Union classifier '{attribute.ClassifierType.FullName}' must derive from {nameof(JsonUnionClassifier)}.");
		}

		try
		{
			return Activator.CreateInstance(attribute.ClassifierType) as JsonUnionClassifier
				?? throw new NotSupportedException($"Union classifier '{attribute.ClassifierType.FullName}' must derive from {nameof(JsonUnionClassifier)}.");
		}
		catch (Exception ex) when (ex is not NotSupportedException)
		{
			throw new NotSupportedException($"Union classifier '{attribute.ClassifierType.FullName}' must have a public parameterless constructor.", ex);
		}
	}

	private static JsonSerializationException NoMatch(Type unionType)
		=> new($"The JSON value matches no case of union '{unionType.FullName}'.")
		{
			Code = JsonSerializationException.ErrorCode.NoMatchingUnionCase,
		};

	private static JsonSerializationException Ambiguous(Type unionType, IReadOnlyList<Type> types)
	{
		StringBuilder builder = new();
		builder.Append("The JSON value is ambiguous for union '");
		builder.Append(unionType.FullName);
		builder.Append("': it matches");
		for (int i = 0; i < types.Count; i++)
		{
			builder.Append(i == 0 ? " " : i == types.Count - 1 ? " and " : ", ");
			builder.Append('\'');
			builder.Append(types[i].FullName);
			builder.Append('\'');
		}

		builder.Append('.');
		return new JsonSerializationException(builder.ToString())
		{
			Code = JsonSerializationException.ErrorCode.AmbiguousUnionCase,
		};
	}

	private static ITypeShape UnwrapOptional(ITypeShape shape)
	{
		while (shape is IOptionalTypeShape optional)
		{
			shape = optional.ElementType;
		}

		return shape;
	}

	private static bool AllowsNull(ITypeShape shape)
	{
		if (shape is IOptionalTypeShape)
		{
			return true;
		}

		Type type = shape.Type;
		return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
	}

	private static bool NumberFits(Type type, string token)
	{
		bool fractional = false;
		for (int i = 0; i < token.Length; i++)
		{
			char ch = token[i];
			if (ch is '.' or 'e' or 'E')
			{
				fractional = true;
				break;
			}
		}

		if (IsIntegral(type))
		{
			if (fractional)
			{
				return false;
			}

			if (type == typeof(byte))
			{
				return byte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
			}

			if (type == typeof(sbyte))
			{
				return sbyte.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
			}

			if (type == typeof(short))
			{
				return short.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
			}

			if (type == typeof(ushort))
			{
				return ushort.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
			}

			if (type == typeof(int))
			{
				return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
			}

			if (type == typeof(uint))
			{
				return uint.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
			}

			if (type == typeof(long))
			{
				return long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
			}

			if (type == typeof(ulong))
			{
				return ulong.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
			}

			if (type == typeof(BigInteger))
			{
				return BigInteger.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
			}

#if NET
			if (type == typeof(Int128))
			{
				return Int128.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
			}

			if (type == typeof(UInt128))
			{
				return UInt128.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
			}
#endif
			return !fractional;
		}

		if (type == typeof(float))
		{
			return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
		}

		if (type == typeof(double))
		{
			return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
		}

		if (type == typeof(decimal))
		{
			return decimal.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
		}

#if NET
		if (type == typeof(Half))
		{
			return Half.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
		}
#endif
		return true;
	}

	private static bool IsIntegral(Type type)
		=> type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
			|| type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
			|| type == typeof(BigInteger)
#if NET
			|| type == typeof(Int128) || type == typeof(UInt128)
#endif
			;

	private static bool StringFits(Type type, string value)
	{
		if (type == typeof(string))
		{
			return true;
		}

		if (type == typeof(char))
		{
			return value.Length == 1;
		}

		if (type == typeof(Guid))
		{
			return Guid.TryParse(value, out _);
		}

		if (type == typeof(DateTime))
		{
			return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
		}

		if (type == typeof(DateTimeOffset))
		{
			return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
		}

		if (type == typeof(TimeSpan))
		{
			return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out _);
		}

		if (type == typeof(Version))
		{
			return Version.TryParse(value, out _);
		}

		if (type == typeof(Uri))
		{
			return Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out _);
		}

#if NET
		if (type == typeof(DateOnly))
		{
			return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
		}

		if (type == typeof(TimeOnly))
		{
			return TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
		}

		if (type == typeof(Rune))
		{
			return Rune.TryGetRuneAt(value, 0, out Rune rune) && rune.Utf16SequenceLength == value.Length;
		}
#endif
		return true;
	}

	private void Expand(int caseIndex, ITypeShape shape, ConverterCache owner, HashSet<Type> stack, List<CaseFact> built)
	{
		if (shape is not IUnionTypeShape union || union.UnionKind != UnionTypeShapeKind.CSharpUnion)
		{
			return;
		}

		UnionTagging mode = JsonCSharpUnionRules.ResolveMode(union, owner);
		JsonCSharpUnionCaseRepresentation[] inner = JsonCSharpUnionRules.Plan(union, owner, mode, stack);
		for (int i = 0; i < inner.Length; i++)
		{
			ITypeShape child = union.UnionCases[i].UnionCaseType;
			if (inner[i] == JsonCSharpUnionCaseRepresentation.Nested)
			{
				this.Expand(caseIndex, child, owner, stack, built);
			}
			else if (inner[i] != JsonCSharpUnionCaseRepresentation.Tagged)
			{
				built.Add(CaseFact.Create(caseIndex, child, inner[i], owner));
			}
		}
	}

	private bool SharesCase(scoped ReadOnlySpan<int> matches)
	{
		int caseIndex = this.facts[matches[0]].CaseIndex;
		for (int i = 1; i < matches.Length; i++)
		{
			if (this.facts[matches[i]].CaseIndex != caseIndex)
			{
				return false;
			}
		}

		return true;
	}

	private JsonConverter SelectObject(ref JsonReader reader, scoped ReadOnlySpan<int> matches, SerializationContext context, JsonUnionObjectEnvelope? envelope, out bool useEnvelope)
	{
		useEnvelope = false;
		JsonReader peek = reader.CreatePeekReader();
		ObjectScan scan = this.ScanObject(ref peek, context);
		bool allowLate = context.Cache.AllowOutOfOrderDiscriminator;
		if (scan.TypeAlias is not null && (scan.TypeFirst || allowLate))
		{
			if (envelope is not null && envelope.OwnsAlias(scan.TypeAlias))
			{
				useEnvelope = true;
				return null!;
			}

			int nested = this.SingleNestedCase();
			if (nested >= 0)
			{
				return this.cases[nested].Converter;
			}

			if (envelope is not null)
			{
				useEnvelope = true;
				return null!;
			}
		}

		bool discriminatorUnused = scan.DiscriminatorRaw is not null && !scan.DiscriminatorFirst && !allowLate;
		if (scan.DiscriminatorRaw is not null && (scan.DiscriminatorFirst || allowLate))
		{
			int matched = this.MatchDiscriminator(scan.DiscriminatorRaw);
			if (matched >= 0)
			{
				return this.cases[matched].Converter;
			}

			int narrowed = 0;
			Span<int> rest = matches.Length <= 16 ? stackalloc int[16] : new int[matches.Length];
			foreach (int factIndex in matches)
			{
				CaseFact fact = this.facts[factIndex];
				if (fact.Discriminator is null && !fact.IsCatchAll)
				{
					rest[narrowed++] = factIndex;
				}
			}

			matches = rest[..narrowed];
			if (matches.Length == 0)
			{
				throw NoMatch(this.unionType);
			}
		}

		JsonConverter? classified = this.TryClassify(ref reader, matches, context);
		if (classified is not null)
		{
			return classified;
		}

		int chosen = this.SelectByProperties(matches, scan.Present, excludeCatchAll: scan.DiscriminatorRaw is not null && (scan.DiscriminatorFirst || allowLate));
		if (chosen >= 0)
		{
			return this.cases[this.facts[chosen].CaseIndex].Converter;
		}

		if (discriminatorUnused || (scan.TypeAlias is not null && !scan.TypeFirst && !allowLate))
		{
			throw new JsonSerializationException($"The JSON object matches no case of union '{this.unionType.FullName}'. A discriminator was not first; set {nameof(JsonSerializer.AllowOutOfOrderDiscriminator)} to accept it later.")
			{
				Code = JsonSerializationException.ErrorCode.NoMatchingUnionCase,
			};
		}

		throw NoMatch(this.unionType);
	}

	private int SingleNestedCase()
	{
		int found = -1;
		for (int i = 0; i < this.cases.Length; i++)
		{
			if (this.cases[i].Representation != JsonCSharpUnionCaseRepresentation.Nested)
			{
				continue;
			}

			if (found >= 0)
			{
				return -1;
			}

			found = i;
		}

		return found;
	}

	private int MatchDiscriminator(string raw)
	{
		int found = -1;
		foreach (CaseFact fact in this.facts)
		{
			if (fact.Discriminator is null || !string.Equals(fact.Discriminator.SerializedToken, raw, StringComparison.Ordinal))
			{
				continue;
			}

			found = fact.CaseIndex;
		}

		return found;
	}

	private JsonConverter SelectArray(ref JsonReader reader, scoped ReadOnlySpan<int> matches, SerializationContext context)
	{
		Span<int> remaining = matches.Length <= 16 ? stackalloc int[16] : new int[matches.Length];
		matches.CopyTo(remaining);
		int count = matches.Length;
		JsonReader peek = reader.CreatePeekReader();
		peek.ReadStartArray();
		if (!peek.TryReadEndArray())
		{
			while (count > 1)
			{
				JsonReader elementStart = peek.CreatePeekReader();
				int kept = 0;
				for (int i = 0; i < count; i++)
				{
					JsonReader element = elementStart.CreatePeekReader();
					if (this.ElementAccepts(this.facts[remaining[i]].ElementShape, ref element, context))
					{
						remaining[kept++] = remaining[i];
					}
				}

				count = kept;
				if (count <= 1)
				{
					break;
				}

				peek.Skip(context);
				if (peek.TryReadEndArray())
				{
					break;
				}

				peek.ReadValueSeparator();
			}
		}

		if (count == 0)
		{
			throw NoMatch(this.unionType);
		}

		return this.cases[this.facts[remaining[0]].CaseIndex].Converter;
	}

	private JsonConverter SelectScalar(ref JsonReader reader, scoped ReadOnlySpan<int> matches, JsonCSharpUnionCaseRepresentation representation)
	{
		foreach (int factIndex in matches)
		{
			CaseFact fact = this.facts[factIndex];
			JsonReader peek = reader.CreatePeekReader();
			bool fits = representation switch
			{
				JsonCSharpUnionCaseRepresentation.Number => NumberFits(fact.PayloadType, peek.ReadNumberToken()),
				JsonCSharpUnionCaseRepresentation.String => StringFits(fact.PayloadType, peek.ReadRequiredString()),
				_ => true,
			};
			if (fits)
			{
				return this.cases[fact.CaseIndex].Converter;
			}
		}

		throw NoMatch(this.unionType);
	}

	private bool ElementAccepts(ITypeShape? elementShape, ref JsonReader reader, SerializationContext context)
	{
		if (elementShape is null)
		{
			return true;
		}

		char token = reader.PeekValueToken();
		if (token == 'n')
		{
			return AllowsNull(elementShape);
		}

		ITypeShape bare = UnwrapOptional(elementShape);
		if (bare is IUnionTypeShape || (this.owner.TryGetCustomConverter(bare, out JsonConverter? custom) && custom is not null))
		{
			return true;
		}

		JsonCSharpUnionCaseRepresentation representation = JsonCSharpUnionRules.GetRepresentation(bare, hasExplicitIdentifier: false, this.owner, UnionTagging.Auto);
		if (representation is JsonCSharpUnionCaseRepresentation.Tagged or JsonCSharpUnionCaseRepresentation.Nested)
		{
			return true;
		}

		if (token == '[')
		{
			if (representation != JsonCSharpUnionCaseRepresentation.Array)
			{
				return false;
			}

			return this.NestedArrayAccepts(bare, ref reader, context);
		}

		if (!TryMapToken(token, out JsonCSharpUnionCaseRepresentation tokenRepresentation) || tokenRepresentation != representation)
		{
			return false;
		}

		if (representation == JsonCSharpUnionCaseRepresentation.Number)
		{
			return NumberFits(bare.Type, reader.ReadNumberToken());
		}

		if (representation == JsonCSharpUnionCaseRepresentation.String)
		{
			return StringFits(bare.Type, reader.ReadRequiredString());
		}

		return true;
	}

	private bool NestedArrayAccepts(ITypeShape arrayShape, ref JsonReader reader, SerializationContext context)
	{
		if (arrayShape is not IEnumerableTypeShape enumerable)
		{
			return true;
		}

		reader.ReadStartArray();
		if (reader.TryReadEndArray())
		{
			return true;
		}

		while (true)
		{
			JsonReader element = reader.CreatePeekReader();
			if (!this.ElementAccepts(enumerable.ElementType, ref element, context))
			{
				return false;
			}

			reader.Skip(context);
			if (reader.TryReadEndArray())
			{
				return true;
			}

			reader.ReadValueSeparator();
		}
	}

	private JsonConverter? TryClassify(ref JsonReader reader, scoped ReadOnlySpan<int> matches, SerializationContext context)
	{
		if (this.classifier is null || matches.Length == 0)
		{
			return null;
		}

		int candidateCount = 0;
		Span<int> indexes = matches.Length <= 16 ? stackalloc int[16] : new int[matches.Length];
		foreach (int factIndex in matches)
		{
			int caseIndex = this.facts[factIndex].CaseIndex;
			bool seen = false;
			for (int i = 0; i < candidateCount; i++)
			{
				if (indexes[i] == caseIndex)
				{
					seen = true;
					break;
				}
			}

			if (!seen)
			{
				indexes[candidateCount++] = caseIndex;
			}
		}

		if (candidateCount <= 1)
		{
			return null;
		}

		Type[] candidates = new Type[candidateCount];
		for (int i = 0; i < candidateCount; i++)
		{
			candidates[i] = this.caseTypes[indexes[i]];
		}

		JsonReader peek = reader.CreatePeekReader();
		UnionClassifierContext classifierContext = new(this.unionType, this.caseTypes, candidates, this.catchAllCase, context);
		Type? chosen;
		try
		{
			chosen = this.classifier.Classify(ref peek, classifierContext);
		}
		catch (Exception ex) when (ex is not JsonSerializationException)
		{
			throw new JsonSerializationException($"Union classifier '{this.classifier.GetType().FullName}' failed while reading '{this.unionType.FullName}'.", ex);
		}

		if (chosen is null)
		{
			return null;
		}

		for (int i = 0; i < this.caseTypes.Length; i++)
		{
			if (this.caseTypes[i] == chosen)
			{
				return this.cases[i].Converter;
			}
		}

		throw new JsonSerializationException($"Union classifier '{this.classifier.GetType().FullName}' returned '{chosen.FullName}', which is not a case of '{this.unionType.FullName}'.")
		{
			Code = JsonSerializationException.ErrorCode.NoMatchingUnionCase,
		};
	}

	private int SelectByProperties(scoped ReadOnlySpan<int> matches, scoped ReadOnlySpan<ulong> present, bool excludeCatchAll)
	{
		int matchCount = 0;
		Span<int> matched = matches.Length <= 16 ? stackalloc int[16] : new int[matches.Length];
		foreach (int factIndex in matches)
		{
			if (this.facts[factIndex].IsCatchAll)
			{
				continue;
			}

			if (this.Covers(present, factIndex))
			{
				matched[matchCount++] = factIndex;
			}
		}

		if (matchCount == 0)
		{
			if (!excludeCatchAll && this.catchAllFact >= 0 && Contains(matches, this.catchAllFact))
			{
				return this.catchAllFact;
			}

			return -1;
		}

		if (matchCount == 1)
		{
			return matched[0];
		}

		for (int i = 0; i < matchCount; i++)
		{
			bool strict = true;
			for (int j = 0; j < matchCount; j++)
			{
				if (i != j && !this.IsStrictSuperset(matched[i], matched[j]))
				{
					strict = false;
					break;
				}
			}

			if (strict)
			{
				return matched[i];
			}
		}

		Type[] types = new Type[matchCount];
		for (int i = 0; i < matchCount; i++)
		{
			types[i] = this.facts[matched[i]].PayloadType;
		}

		throw Ambiguous(this.unionType, types);
	}

	private static bool Contains(scoped ReadOnlySpan<int> matches, int factIndex)
	{
		foreach (int match in matches)
		{
			if (match == factIndex)
			{
				return true;
			}
		}

		return false;
	}

	private bool Covers(scoped ReadOnlySpan<ulong> present, int factIndex)
	{
		if (this.wordCount == 0)
		{
			return this.facts[factIndex].Required.Count == 0;
		}

		int offset = factIndex * this.wordCount;
		for (int word = 0; word < this.wordCount; word++)
		{
			ulong required = this.masks[offset + word];
			if ((present[word] & required) != required)
			{
				return false;
			}
		}

		return true;
	}

	private bool IsStrictSuperset(int candidate, int other)
	{
		if (this.wordCount == 0)
		{
			return false;
		}

		bool extra = false;
		int candidateOffset = candidate * this.wordCount;
		int otherOffset = other * this.wordCount;
		for (int word = 0; word < this.wordCount; word++)
		{
			ulong left = this.masks[candidateOffset + word];
			ulong right = this.masks[otherOffset + word];
			if ((left & right) != right)
			{
				return false;
			}

			if (left != right)
			{
				extra = true;
			}
		}

		return extra;
	}

	private ObjectScan ScanObject(ref JsonReader reader, SerializationContext context)
	{
		ulong[] present = this.wordCount == 0 ? [] : new ulong[this.wordCount];
		int maxMembers = context.Security.MaxObjectMemberCount;
		reader.ReadStartObject();
		if (reader.TryReadEndObject())
		{
			return new ObjectScan(present, discriminatorRaw: null, discriminatorFirst: false, typeAlias: null, typeFirst: false);
		}

		int count = 0;
		int index = 0;
		string? discriminatorRaw = null;
		bool discriminatorFirst = false;
		string? typeAlias = null;
		bool typeFirst = false;
		while (true)
		{
			if (count++ >= maxMembers)
			{
				throw new FormatException($"The JSON object exceeds the configured maximum of {maxMembers} members.");
			}

			string name = this.ReadPropertyName(ref reader);
			int evidence = this.FindEvidence(name);
			if (evidence >= 0)
			{
				present[evidence / 64] |= 1UL << (evidence % 64);
			}

			reader.ReadNameSeparator();
			bool first = index == 0;
			if (this.discriminatorName is not null && this.nameComparer.Equals(name, this.discriminatorName))
			{
				discriminatorRaw = reader.ReadRawValue(context);
				discriminatorFirst = first;
			}
			else if (string.Equals(name, JsonUnionObjectEnvelope.DiscriminatorPropertyName, StringComparison.Ordinal))
			{
				typeAlias = reader.ReadRequiredString();
				typeFirst = first;
			}
			else
			{
				reader.Skip(context);
			}

			index++;
			if (reader.TryReadEndObject())
			{
				break;
			}

			reader.ReadValueSeparator();
		}

		return new ObjectScan(present, discriminatorRaw, discriminatorFirst, typeAlias, typeFirst);
	}

	private string ReadPropertyName(ref JsonReader reader)
	{
		if (reader.TryReadUnescapedUtf8StringToken(out ReadOnlySpan<byte> token, out _, out _) && !token.IsEmpty)
		{
			return Encoding.UTF8.GetString(token[1..^1]);
		}

		return reader.ReadRequiredString();
	}

	private int FindEvidence(string name)
	{
		for (int i = 0; i < this.evidenceNames.Length; i++)
		{
			if (this.nameComparer.Equals(name, this.evidenceNames[i]))
			{
				return i;
			}
		}

		return -1;
	}

	private readonly struct ObjectScan(ulong[] present, string? discriminatorRaw, bool discriminatorFirst, string? typeAlias, bool typeFirst)
	{
		internal ReadOnlySpan<ulong> Present => present;

		internal string? DiscriminatorRaw { get; } = discriminatorRaw;

		internal bool DiscriminatorFirst { get; } = discriminatorFirst;

		internal string? TypeAlias { get; } = typeAlias;

		internal bool TypeFirst { get; } = typeFirst;
	}

	private readonly struct CaseFact
	{
		private CaseFact(int caseIndex, Type payloadType, JsonCSharpUnionCaseRepresentation representation, ITypeShape? elementShape, UnionDiscriminatorBinding? discriminator, HashSet<string> required)
		{
			this.CaseIndex = caseIndex;
			this.PayloadType = payloadType;
			this.Representation = representation;
			this.ElementShape = elementShape;
			this.Discriminator = discriminator;
			this.Required = required;
		}

		internal int CaseIndex { get; }

		internal Type PayloadType { get; }

		internal JsonCSharpUnionCaseRepresentation Representation { get; }

		internal ITypeShape? ElementShape { get; }

		internal UnionDiscriminatorBinding? Discriminator { get; }

		internal HashSet<string> Required { get; }

		internal bool IsCatchAll => this.Representation == JsonCSharpUnionCaseRepresentation.Object && this.Discriminator is null && this.Required.Count == 0;

		internal static CaseFact Create(int caseIndex, ITypeShape shape, JsonCSharpUnionCaseRepresentation representation, ConverterCache owner)
		{
			ITypeShape? element = representation == JsonCSharpUnionCaseRepresentation.Array && shape is IEnumerableTypeShape enumerable
				? enumerable.ElementType
				: null;
			UnionDiscriminatorBinding? discriminator = representation == JsonCSharpUnionCaseRepresentation.Object
				? UnionDiscriminatorBinding.TryGet(shape, owner)
				: null;
			HashSet<string> required = representation == JsonCSharpUnionCaseRepresentation.Object
				? RuntimeUnionHelpers.GetRequiredSerializedPropertyNames(shape, owner)
				: [];
			if (discriminator is not null)
			{
				required.Remove(discriminator.Name);
			}

			return new CaseFact(caseIndex, shape.Type, representation, element, discriminator, required);
		}
	}
}
