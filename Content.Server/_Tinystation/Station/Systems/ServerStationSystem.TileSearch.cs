using System.Diagnostics.CodeAnalysis;
using Content.Shared.Station.Components;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Station.Systems;

public sealed partial class ServerStationSystem
{
    private bool TryFindOpenStationTile(Entity<StationDataComponent> station,
        out Vector2i tile,
        [NotNullWhen(true)] out Entity<MapGridComponent>? targetGrid,
        out EntityCoordinates targetCoords)
    {
        tile = default;
        targetGrid = null;
        targetCoords = EntityCoordinates.Invalid;

        var candidates = new ValueList<(Entity<MapGridComponent> Grid, Vector2i Tile)>();
        foreach (var gridUid in station.Comp.Grids)
        {
            if (!GridQuery.TryComp(gridUid, out var grid))
                continue;

            var mapUid = Transform(gridUid).MapUid;
            foreach (var tileRef in Map.GetAllTiles(gridUid, grid))
            {
                var indices = tileRef.GridIndices;
                if (_atmos.IsTileSpace(gridUid, mapUid, indices)
                    || _atmos.IsTileAirBlockedCached(gridUid, indices))
                {
                    continue;
                }

                candidates.Add(((gridUid, grid), indices));
            }
        }

        if (candidates.Count == 0)
            return false;

        var candidate = candidates[Random.Next(candidates.Count)];
        targetGrid = candidate.Grid;
        tile = candidate.Tile;
        targetCoords = Map.GridTileToLocal(candidate.Grid, candidate.Grid.Comp, tile);
        return true;
    }
}
