using System.ComponentModel;
using ReadyM.Api.Idents;

namespace ReadyM.SDK.Events;

/// <summary>
/// Where generated dispatch reads the audience of a scoped event from its subject, so a mod never names a cell
/// or an area itself.
/// </summary>
/// <exclude />
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NetworkEventScopes
{
    /// <summary>The cell the subject is in.</summary>
    public static FullCellId CellOf<TShape>(object host, TShape subject) => throw new NotImplementedException();

    /// <summary>The area the subject is in.</summary>
    public static AreaId AreaOf<TShape>(object host, TShape subject) => throw new NotImplementedException();
}
