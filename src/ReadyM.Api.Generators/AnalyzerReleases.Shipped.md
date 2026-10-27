; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
; Release headers take a plain version, so 1.0.0 is where 1.0.0-rc.1 went out.

## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
READYM001 | ReadyM | Warning | The server SDK is referenced but its chunk types were not found, so queries fall back to walking identities.
READYM002 | ReadyM | Error | Growing the world inside a query loop, which the SDK refuses at run time.
READYM004 | ReadyM | Error | A type named by [ExplicitComponent] cannot back a shape.
READYM005 | ReadyM | Error | An explicit component has no member matching a declared accessor.
READYM006 | ReadyM | Error | A setter was declared over a member that cannot be assigned.
READYM007 | ReadyM | Error | An explicit component is not visible from the declaring assembly.
READYM008 | ReadyM | Error | An [ExplicitCollection] name matches no member of the component.
READYM009 | ReadyM | Error | A replicated shape holds no values.
READYM010 | ReadyM | Error | Replication is stated on a shape backed by a borrowed component.
READYM011 | ReadyM | Error | A collection on a replicated shape is not declared private.
READYM012 | ReadyM | Error | An explicit member holds a type that cannot be read as the declared value.
READYM013 | ReadyM | Error | A generic component instantiation is used without being registered.
READYM014 | ReadyM | Error | A replicated shape does not say how its values travel.
READYM015 | ReadyM | Error | Propagation is stated on a shape backed by a borrowed component.
READYM016 | ReadyM | Error | Propagation is stated on a shape that is not replicated.
READYM017 | ReadyM | Error | A client writes a replicated value straight through its setter.
READYM018 | ReadyM | Error | A collection sits on a shape that is not replicated.
READYM019 | ReadyM | Error | A client changes a replicated collection through its own members.
READYM020 | ReadyM | Error | A create handler a shape declared for itself cannot be called.
READYM021 | ReadyM | Error | A shape declares more than one create handler.
READYM022 | ReadyM | Error | A service is not declared partial.
READYM023 | ReadyM | Error | A service is not declared sealed.
READYM024 | ReadyM | Error | A method named Update, Start or Stop cannot be called as one.
READYM025 | ReadyM | Error | A handler declared in a service cannot be called.
READYM026 | ReadyM | Error | A handler declared in a service does not name the shape it watches.
READYM027 | ReadyM | Error | A mod entry point is not declared partial.
READYM028 | ReadyM | Error | A delete handler a shape declared for itself cannot be called.
READYM029 | ReadyM | Error | A shape declares more than one delete handler.
READYM030 | ReadyM | Error | A mod declares more than one entry point.
READYM031 | ReadyM | Error | A mod config class cannot be created.
READYM032 | ReadyM | Error | A mod config names no file.
READYM033 | ReadyM | Error | An update order names something that does not update.
READYM034 | ReadyM | Error | An update order is stated somewhere other than a service's Update.
READYM035 | ReadyM | Error | The update order has a cycle.
READYM036 | ReadyM | Error | An archetype states replication with [Replicated] rather than on [Archetype].
READYM037 | ReadyM | Error | Interning is stated on a shape backed by a borrowed component.
READYM038 | ReadyM | Error | Interning is stated on a shape that is not replicated.
READYM039 | ReadyM | Error | Quantization is stated on a shape backed by a borrowed component.
READYM040 | ReadyM | Error | Quantization is stated on a shape that is not replicated.
READYM041 | ReadyM | Error | A shape would expose one name twice.
READYM042 | ReadyM | Error | [Include] names an archetype rather than a mixin.
READYM043 | ReadyM | Error | [IncludeArchetype] names a mixin rather than an archetype.
READYM044 | ReadyM | Error | A mod entry point is not declared sealed.
READYM045 | ReadyM | Error | A mod entry point's Init cannot be called by the loader.
READYM046 | ReadyM | Error | A replicated shape holds a value of a type the SDK cannot send.
