using ReadyM.Api.Saves;
using ReadyM.SDK.Attributes;
using Yooni.Native.Container;

namespace ReadyM.SDK.Tests.Client.Fixtures;

/// A local mixin whose values are saved with the entity.
[ArchetypeMixin]
[Persisted("Test.Hoard")]
public readonly partial struct Hoard
{
    public partial int Coins { get; set; }

    public partial float Weight { get; set; }

    public partial NativeString256 Label { get; set; }
}

/// Saved and replicated at once, with its records stamped past the first version.
[ArchetypeMixin]
[Replicated]
[Propagates(Propagation.OwnershipBased)]
[Persisted("Test.Stash")]
[SaveVersion(3)]
public readonly partial struct Stash
{
    public partial int Count { get; set; }
}
