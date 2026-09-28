using ReadyM.Api.Idents;

namespace ReadyM.Relay.Server.Sdk.Interop;

// These mirror the server state's methods of the same names one to one. An exception cannot cross the
// interop boundary, so where the server state throws, these return 0.

/// <summary>1 and the area's scope entity id written to <paramref name="entityId"/> if it has one, else 0.</summary>
internal unsafe delegate byte TryGetAreaScopeEntityDelegate(AreaId areaId, int* entityId);

/// <summary>The id of the area's scope entity, or 0 if it has none.</summary>
internal delegate int GetAreaScopeEntityDelegate(AreaId areaId);

/// <summary>The id of the area's new scope entity, or 0 if the area already has one.</summary>
internal delegate int CreateAreaScopeEntityDelegate(AreaId areaId);

/// <summary>1 and the cell's scope entity id written to <paramref name="entityId"/> if it has one, else 0.</summary>
internal unsafe delegate byte TryGetCellScopeEntityDelegate(FullCellId cellId, int* entityId);

/// <summary>The id of the cell's scope entity, or 0 if it has none.</summary>
internal delegate int GetCellScopeEntityDelegate(FullCellId cellId);

/// <summary>The id of the cell's new scope entity, or 0 if the cell already has one or its area has none.</summary>
internal delegate int CreateCellScopeEntityDelegate(FullCellId cellId);
