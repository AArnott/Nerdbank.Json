# NBJson003: UseComparer attribute is invalid

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson003 |
| **Category** | Usage |
| **Severity** | Warning |

## Cause and resolution

<xref:Nerdbank.Json.UseComparerAttribute> targets an unsupported member, selects an incompatible comparer, or specifies a comparer or provider that cannot be activated.
See [collection comparers](../docs/collection-comparers.md#diagnostics) for the supported comparer and provider-property patterns and the complete trigger list.
The analyzer's help link points directly to that guide.

There is no automatic code fix.
