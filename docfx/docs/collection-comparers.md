# Collection comparers

The deserializer creates a fresh collection when it populates a settable dictionary
or set member. By default that collection uses its type's default comparer, which
loses member-specific semantics such as a case-insensitive dictionary. Apply
<xref:Nerdbank.Json.JsonCollectionComparerAttribute> to select the
<xref:System.Collections.Generic.IEqualityComparer`1> or
<xref:System.Collections.Generic.IComparer`1> used for that member's collection.

[!code-csharp[](../../samples/cs/CollectionComparers.cs#CollectionComparers)]

The comparer type must declare a public parameterless constructor and implement the
comparer interface required by the collection: an equality comparer for hash-based
collections such as <xref:System.Collections.Generic.Dictionary`2> and
<xref:System.Collections.Generic.HashSet`1>, or an order comparer for sorted
collections such as <xref:System.Collections.Generic.SortedDictionary`2>. The
comparer is activated through a source-generated type shape rather than runtime
reflection, so the feature is trimming-safe and NativeAOT-compatible.

The attribute only influences the instances the deserializer creates. Specify the same
comparer in the member's initializer so instances created by application code share the
same semantics, as shown above.

The attribute may also be applied to a constructor parameter, and it takes precedence
over the global <xref:Nerdbank.Json.JsonSerializer.ComparerProvider> for the decorated
member. When a collection member is getter-only and already initialized with the desired
comparer, the deserializer populates that existing instance and this attribute is
unnecessary; that zero-configuration pattern remains the preferred alternative.

## Diagnostics

The `NBJson003` analyzer reports invalid uses of
<xref:Nerdbank.Json.JsonCollectionComparerAttribute> at compile time when any of the
following is true:

* The decorated member is not a dictionary or set.
* The comparer type does not declare a public parameterless constructor.
* The comparer type does not implement `IEqualityComparer<T>` or `IComparer<T>` for the
  collection's key or element type.
