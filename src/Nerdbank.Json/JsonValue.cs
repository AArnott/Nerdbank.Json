// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Nerdbank.Json;

/// <summary>
/// Identifies the kind of a <see cref="JsonValue"/>.
/// </summary>
public enum JsonValueKind : byte
{
	/// <summary>The JSON <see langword="null"/> literal.</summary>
	Null,

	/// <summary>A JSON boolean.</summary>
	Boolean,

	/// <summary>A JSON number.</summary>
	Number,

	/// <summary>A JSON string.</summary>
	String,

	/// <summary>A JSON array.</summary>
	Array,

	/// <summary>A JSON object.</summary>
	Object,
}

/// <summary>
/// A compact, immutable, native representation of an arbitrary JSON value, usable without a static model.
/// </summary>
/// <remarks>
/// <para>
/// This type is entirely self-contained: it does not depend on <c>System.Text.Json</c>, dynamic binding, or reflection,
/// so using it keeps the default serializer trimming-safe and NativeAOT-safe.
/// </para>
/// <para>
/// Numbers preserve their exact JSON token text (see <see cref="JsonNumber"/>), so a value round-trips without loss of
/// precision or normalization.
/// </para>
/// </remarks>
public abstract class JsonValue : IEquatable<JsonValue>
{
	/// <summary>Initializes a new instance of the <see cref="JsonValue"/> class.</summary>
	private protected JsonValue()
	{
	}

	/// <summary>Gets the JSON <see langword="null"/> literal.</summary>
	public static JsonValue Null => JsonNull.Instance;

	/// <summary>Gets the kind of this value.</summary>
	public abstract JsonValueKind Kind { get; }

	/// <summary>Creates a JSON boolean value.</summary>
	/// <param name="value">The boolean.</param>
	/// <returns>The value.</returns>
	public static JsonValue Create(bool value) => value ? JsonBoolean.True : JsonBoolean.False;

	/// <summary>Creates a JSON string value, or the <see langword="null"/> literal when <paramref name="value"/> is <see langword="null"/>.</summary>
	/// <param name="value">The string.</param>
	/// <returns>The value.</returns>
	public static JsonValue Create(string? value) => value is null ? JsonNull.Instance : new JsonString(value);

	/// <summary>Creates a JSON number value.</summary>
	/// <param name="value">The number.</param>
	/// <returns>The value.</returns>
	public static JsonValue Create(long value) => new JsonNumber(value.ToString(CultureInfo.InvariantCulture));

	/// <summary>Creates a JSON number value.</summary>
	/// <param name="value">The number.</param>
	/// <returns>The value.</returns>
	public static JsonValue Create(double value) => new JsonNumber(JsonNumber.FormatDouble(value));

	/// <summary>Creates a JSON number value.</summary>
	/// <param name="value">The number.</param>
	/// <returns>The value.</returns>
	public static JsonValue Create(decimal value) => new JsonNumber(value.ToString(CultureInfo.InvariantCulture));

	/// <inheritdoc/>
	public abstract bool Equals(JsonValue? other);

	/// <inheritdoc/>
	public override bool Equals(object? obj) => this.Equals(obj as JsonValue);

	/// <inheritdoc/>
	public abstract override int GetHashCode();
}

/// <summary>The JSON <see langword="null"/> literal.</summary>
public sealed class JsonNull : JsonValue
{
	private JsonNull()
	{
	}

	/// <summary>Gets the singleton instance.</summary>
	public static JsonNull Instance { get; } = new();

	/// <inheritdoc/>
	public override JsonValueKind Kind => JsonValueKind.Null;

	/// <inheritdoc/>
	public override bool Equals(JsonValue? other) => other is JsonNull;

	/// <inheritdoc/>
	public override int GetHashCode() => 0;
}

/// <summary>A JSON boolean value.</summary>
public sealed class JsonBoolean : JsonValue
{
	private JsonBoolean(bool value) => this.Value = value;

	/// <summary>Gets the <see langword="true"/> instance.</summary>
	public static JsonBoolean True { get; } = new(true);

	/// <summary>Gets the <see langword="false"/> instance.</summary>
	public static JsonBoolean False { get; } = new(false);

	/// <summary>Gets a value indicating whether this boolean is JSON <see langword="true"/>.</summary>
	public bool Value { get; }

	/// <inheritdoc/>
	public override JsonValueKind Kind => JsonValueKind.Boolean;

	/// <inheritdoc/>
	public override bool Equals(JsonValue? other) => other is JsonBoolean b && b.Value == this.Value;

	/// <inheritdoc/>
	public override int GetHashCode() => this.Value ? 1 : 2;
}

/// <summary>A JSON string value.</summary>
public sealed class JsonString : JsonValue
{
	/// <summary>Initializes a new instance of the <see cref="JsonString"/> class.</summary>
	/// <param name="value">The string value.</param>
	public JsonString(string value) => this.Value = Requires.NotNull(value);

	/// <summary>Gets the string value.</summary>
	public string Value { get; }

	/// <inheritdoc/>
	public override JsonValueKind Kind => JsonValueKind.String;

	/// <inheritdoc/>
	public override bool Equals(JsonValue? other) => other is JsonString s && string.Equals(s.Value, this.Value, StringComparison.Ordinal);

	/// <inheritdoc/>
	public override int GetHashCode() => this.Value.GetHashCode();
}

/// <summary>
/// A JSON number value that preserves the exact JSON token text so that no precision or formatting is lost.
/// </summary>
public sealed class JsonNumber : JsonValue
{
	/// <summary>Initializes a new instance of the <see cref="JsonNumber"/> class from a raw JSON number token.</summary>
	/// <param name="rawToken">The exact JSON number token, for example <c>1.0</c> or <c>1e10</c>.</param>
	public JsonNumber(string rawToken) => this.RawToken = Requires.NotNull(rawToken);

	/// <summary>Gets the exact JSON number token text.</summary>
	public string RawToken { get; }

	/// <inheritdoc/>
	public override JsonValueKind Kind => JsonValueKind.Number;

	/// <summary>Gets the value as a <see cref="long"/>.</summary>
	/// <returns>The value.</returns>
	/// <exception cref="FormatException">Thrown if the token is not an integer in range.</exception>
	public long GetInt64() => long.Parse(this.RawToken, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);

	/// <summary>Attempts to get the value as a <see cref="long"/>.</summary>
	/// <param name="value">Receives the value.</param>
	/// <returns><see langword="true"/> if the token is an integer in range.</returns>
	public bool TryGetInt64(out long value) => long.TryParse(this.RawToken, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

	/// <summary>Gets the value as a <see cref="double"/>.</summary>
	/// <returns>The value.</returns>
	public double GetDouble() => double.Parse(this.RawToken, NumberStyles.Float, CultureInfo.InvariantCulture);

	/// <summary>Gets the value as a <see cref="decimal"/>.</summary>
	/// <returns>The value.</returns>
	/// <exception cref="FormatException">Thrown if the token is not representable as a decimal.</exception>
	public decimal GetDecimal() => decimal.Parse(this.RawToken, NumberStyles.Float, CultureInfo.InvariantCulture);

	/// <inheritdoc/>
	public override bool Equals(JsonValue? other) => other is JsonNumber n && string.Equals(n.RawToken, this.RawToken, StringComparison.Ordinal);

	/// <inheritdoc/>
	public override int GetHashCode() => this.RawToken.GetHashCode();

	/// <summary>Formats a <see cref="double"/> as a round-trippable JSON number token.</summary>
	/// <param name="value">The value to format.</param>
	/// <returns>The token text.</returns>
	internal static string FormatDouble(double value)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			throw new ArgumentException("JSON numbers cannot be NaN or infinity.", nameof(value));
		}

		return value.ToString("R", CultureInfo.InvariantCulture);
	}
}

/// <summary>An immutable JSON array.</summary>
public sealed class JsonArray : JsonValue, IReadOnlyList<JsonValue>
{
	private readonly JsonValue[] items;

	/// <summary>Initializes a new instance of the <see cref="JsonArray"/> class.</summary>
	/// <param name="items">The elements. The sequence is copied.</param>
	public JsonArray(IEnumerable<JsonValue> items)
	{
		Requires.NotNull(items);
		this.items = [.. items];
	}

	/// <summary>Initializes a new instance of the <see cref="JsonArray"/> class that takes ownership of the array.</summary>
	/// <param name="items">The elements array to wrap without copying.</param>
	/// <param name="takeOwnership">Unused discriminator to distinguish this internal constructor.</param>
	internal JsonArray(JsonValue[] items, bool takeOwnership) => this.items = items;

	/// <inheritdoc/>
	public override JsonValueKind Kind => JsonValueKind.Array;

	/// <inheritdoc/>
	public int Count => this.items.Length;

	/// <inheritdoc/>
	public JsonValue this[int index] => this.items[index];

	/// <inheritdoc/>
	public IEnumerator<JsonValue> GetEnumerator() => ((IEnumerable<JsonValue>)this.items).GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => this.items.GetEnumerator();

	/// <inheritdoc/>
	public override bool Equals(JsonValue? other)
	{
		if (other is not JsonArray array || array.items.Length != this.items.Length)
		{
			return false;
		}

		for (int i = 0; i < this.items.Length; i++)
		{
			if (!this.items[i].Equals(array.items[i]))
			{
				return false;
			}
		}

		return true;
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		foreach (JsonValue item in this.items)
		{
			hash = unchecked((hash * 31) + item.GetHashCode());
		}

		return hash;
	}
}

/// <summary>An immutable JSON object that preserves member order.</summary>
public sealed class JsonObject : JsonValue, IReadOnlyList<KeyValuePair<string, JsonValue>>
{
	private readonly KeyValuePair<string, JsonValue>[] members;
	private readonly Dictionary<string, int> lookup;

	/// <summary>Initializes a new instance of the <see cref="JsonObject"/> class.</summary>
	/// <param name="members">The ordered members. The sequence is copied. Later members win on duplicate names.</param>
	public JsonObject(IEnumerable<KeyValuePair<string, JsonValue>> members)
	{
		Requires.NotNull(members);
		this.members = [.. members];
		this.lookup = new(this.members.Length, StringComparer.Ordinal);
		for (int i = 0; i < this.members.Length; i++)
		{
			this.lookup[this.members[i].Key] = i;
		}
	}

	/// <summary>Initializes a new instance of the <see cref="JsonObject"/> class from prepared internal state.</summary>
	/// <param name="members">The ordered members array to wrap without copying.</param>
	/// <param name="lookup">The name-to-index lookup.</param>
	internal JsonObject(KeyValuePair<string, JsonValue>[] members, Dictionary<string, int> lookup)
	{
		this.members = members;
		this.lookup = lookup;
	}

	/// <inheritdoc/>
	public override JsonValueKind Kind => JsonValueKind.Object;

	/// <inheritdoc/>
	public int Count => this.members.Length;

	/// <inheritdoc/>
	public KeyValuePair<string, JsonValue> this[int index] => this.members[index];

	/// <summary>Gets the value for a member by name.</summary>
	/// <param name="name">The member name.</param>
	/// <returns>The value.</returns>
	/// <exception cref="KeyNotFoundException">Thrown if no such member exists.</exception>
	public JsonValue this[string name] => this.TryGetValue(name, out JsonValue? value) ? value : throw new KeyNotFoundException($"No member named '{name}'.");

	/// <summary>Attempts to get a member value by name.</summary>
	/// <param name="name">The member name.</param>
	/// <param name="value">Receives the value.</param>
	/// <returns><see langword="true"/> if the member exists.</returns>
	public bool TryGetValue(string name, [NotNullWhen(true)] out JsonValue? value)
	{
		Requires.NotNull(name);
		if (this.lookup.TryGetValue(name, out int index))
		{
			value = this.members[index].Value;
			return true;
		}

		value = null;
		return false;
	}

	/// <inheritdoc/>
	public IEnumerator<KeyValuePair<string, JsonValue>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, JsonValue>>)this.members).GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => this.members.GetEnumerator();

	/// <inheritdoc/>
	public override bool Equals(JsonValue? other)
	{
		if (other is not JsonObject obj || obj.members.Length != this.members.Length)
		{
			return false;
		}

		foreach (KeyValuePair<string, JsonValue> member in this.members)
		{
			if (!obj.TryGetValue(member.Key, out JsonValue? otherValue) || !member.Value.Equals(otherValue))
			{
				return false;
			}
		}

		return true;
	}

	/// <inheritdoc/>
	public override int GetHashCode()
	{
		int hash = 17;
		foreach (KeyValuePair<string, JsonValue> member in this.members)
		{
			hash = unchecked(hash + (member.Key.GetHashCode() ^ member.Value.GetHashCode()));
		}

		return hash;
	}
}
