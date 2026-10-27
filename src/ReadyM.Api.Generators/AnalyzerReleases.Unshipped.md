; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
READYM047 | ReadyM | Error | A shape marks more than one value with [Index].
READYM048 | ReadyM | Error | [Tag] is put on a mixin rather than an archetype.
READYM049 | ReadyM | Error | [Extends] is put on an archetype rather than a mixin.
READYM050 | ReadyM | Error | Delivery is stated on a shape that sends nothing of its own.

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
READYM010 | ReadyM | Error | [Replicated] is gone: a shape over an explicit component states the component's Replication instead (READYM015).
READYM036 | ReadyM | Error | [Replicated] is gone: an archetype states Networked and Replication on [Archetype].
