# NBJson002: Dictionary key type is not supported

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson002 |
| **Category** | Usage |
| **Severity** | Warning |

## Status

The current analyzer declares this descriptor but does not implement shape analysis for it, so it does not report this diagnostic.
Dictionary keys must still be supported scalar or enum types that can be encoded as JSON property names.

Use a supported key type or a custom representation for complex keys.
There is no automatic code fix.
