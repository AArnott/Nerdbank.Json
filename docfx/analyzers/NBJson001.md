# NBJson001: Delegate types are not supported

| Property | Value |
|----------|-------|
| **Rule ID** | NBJson001 |
| **Category** | Usage |
| **Severity** | Warning |

## Status

The current analyzer declares this descriptor but does not implement shape analysis for it, so it does not report this diagnostic.
Delegate serialization is nevertheless unsupported at runtime.

Exclude delegate members from the serialized contract or represent the required data as a supported value.
There is no automatic code fix.
