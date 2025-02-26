﻿using OpenNefia.Content.Food;
using OpenNefia.Content.Items;
using OpenNefia.Content.Logic;
using OpenNefia.Content.Pickable;
using OpenNefia.Content.VanillaAI;
using OpenNefia.Core.Areas;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Maps;
using OpenNefia.Core.Random;

namespace OpenNefia.Content.Maps
{
    public interface IMapRenewGeometrySystem : IEntitySystem
    {
    }

    public sealed class MapRenewGeometrySystem : EntitySystem, IMapRenewGeometrySystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IEntityLookup _lookup = default!;
        [Dependency] private readonly IMapLoader _mapLoader = default!;

        public override void Initialize()
        {
            SubscribeComponent<MapRenewGeometryComponent, MapRenewGeometryEvent>(HandleRenewGeometry, priority: EventPriorities.VeryLow);
        }

        private void HandleRenewGeometry(EntityUid uid, MapRenewGeometryComponent component, MapRenewGeometryEvent args)
        {
            // >>>>>>>> elona122/shade2/map_func.hsp:270 #deffunc map_reload str s ...
            if (args.Handled || !TryMap(uid, out var currentMap))
                return;

            var newMap = _mapLoader.LoadBlueprint(component.MapBlueprintPath);

            foreach (var tileRef in newMap.AllTiles)
            {
                currentMap.SetTile(tileRef.Position, tileRef.Tile.GetStrongID());
            }

            foreach (var (item, food) in _lookup.EntityQueryInMap<ItemComponent, FoodComponent>(currentMap).ToList())
            {
                EntityManager.DeleteEntity(item.Owner);
            }

            foreach (var (spatial, anchor) in _lookup.EntityQueryInMap<SpatialComponent, AIAnchorComponent>(currentMap, includeDead: true))
            {
                spatial.Coordinates = new EntityCoordinates(spatial.ParentUid, anchor.Anchor);
            }

            // TOOD: kind of hackish.
            foreach (var (spatial, _) in _lookup.EntityQueryInMap<SpatialComponent, ItemComponent>(newMap))
            {
                // Check if any NPC-owned items are missing on this tile, and if
                // so regenerate them by moving them from the newly generated
                // map.
                // NOTE: Only allows one NPC-owned item on a tile.
                var destCoords = new EntityCoordinates(currentMap.MapEntityUid, spatial.WorldPosition);
                var npcOwnedAtCoords = _lookup.EntityQueryLiveEntitiesAtCoords<PickableComponent>(destCoords).Where(p => p.OwnState == OwnState.NPC);

                if (npcOwnedAtCoords.Count() == 0)
                    spatial.Coordinates = destCoords;
            }

            _mapManager.UnloadMap(newMap.Id, MapUnloadType.Delete);

            args.Handled = true;
            // <<<<<<<< elona122/shade2/map_func.hsp:303 	return ...
        }
    }
}