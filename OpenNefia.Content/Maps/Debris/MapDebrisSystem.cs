﻿using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Maps;
using OpenNefia.Core.Maths;
using OpenNefia.Core.Random;
using OpenNefia.Content.Combat;
using OpenNefia.Content.Skills;
using OpenNefia.Content.Damage;

namespace OpenNefia.Content.Maps
{
    public interface IMapDebrisSystem : IEntitySystem
    {
        void SpillBlood(MapCoordinates coords, int amount);
        void SpillFragments(MapCoordinates coords, int amount);
        void Clear(IMap map, MapDebrisComponent? mapDebris = null);
        void Clear(EntityUid mapEntity, MapDebrisComponent? mapDebris = null);
    }

    public sealed class MapDebrisSystem : EntitySystem, IMapDebrisSystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IRandom _rand = default!;

        public override void Initialize()
        {
            SubscribeBroadcast<MapCreatedEvent>(HandleMapCreated, priority: EventPriorities.Highest);
            SubscribeEntity<EntityWoundedEvent>(HandleEntityWounded, priority: EventPriorities.VeryHigh);
        }

        public const int MaxBlood = 6;
        public const int MaxFragments = 4;

        private void HandleMapCreated(MapCreatedEvent ev)
        {
            var map = ev.Map;
            var mapDebris = EntityManager.EnsureComponent<MapDebrisComponent>(map.MapEntityUid);
            mapDebris.Initialize(map);
        }

        private void HandleEntityWounded(EntityUid uid, ref EntityWoundedEvent args)
        {
            var damageLevel = (args.FinalDamage * 6) / CompOrNull<SkillsComponent>(uid)?.MaxHP ?? 1;
            if (damageLevel > 1)
{
                if (CompOrNull<StoneBloodComponent>(uid)?.HasStoneBlood ?? false)
                    SpillFragments(Spatial(uid).MapPosition, 1 + _rand.Next(2));
                else
                    SpillBlood(Spatial(uid).MapPosition, 1 + _rand.Next(2));
            }
        }

        public void SpillBlood(MapCoordinates coords, int amount)
        {
            if (!_mapManager.TryGetMap(coords.MapId, out var map)
                || !TryComp<MapDebrisComponent>(map.MapEntityUid, out var mapDebris))
                return;

            Vector2i pos;

            for (int i = 0; i < amount; i++)
            {
                if (i == 0)
                    pos = coords.Position;
                else
                    pos = coords.Position + _rand.NextVec2iInRadius(2);

                if (map.IsFloor(pos))
                {
                    var blood = mapDebris.DebrisState[pos.X, pos.Y].Blood;
                    mapDebris.DebrisState[pos.X, pos.Y].Blood = Math.Clamp(blood + 1, 1, MaxBlood);
                }
            }
        }

        public void SpillFragments(MapCoordinates coords, int amount)
        {
            if (!_mapManager.TryGetMap(coords.MapId, out var map)
                || !TryComp<MapDebrisComponent>(map.MapEntityUid, out var mapDebris))
                return;

            Vector2i pos;

            for (int i = 0; i < amount; i++)
            {
                if (i == 0)
                    pos = coords.Position;
                else
                    pos = coords.Position + _rand.NextVec2iInRadius(2);

                if (map.IsFloor(pos))
                {
                    var fragments = mapDebris.DebrisState[pos.X, pos.Y].Fragments;
                    mapDebris.DebrisState[pos.X, pos.Y].Fragments = Math.Clamp(fragments + 1, 1, MaxFragments);
                }
            }
        }

        public void Clear(IMap map, MapDebrisComponent? mapDebris = null)
        {
            Clear(map.MapEntityUid, mapDebris);
        }

        public void Clear(EntityUid mapEntity, MapDebrisComponent? mapDebris = null)
        {
            if (!Resolve(mapEntity, ref mapDebris))
                return;

            for (var i = 0; i < mapDebris.DebrisState.GetLength(0); i++)
            {
                for (var j = 0; j < mapDebris.DebrisState.GetLength(1); j++)
                {
                    mapDebris.DebrisState[i, j].Blood = 0;
                    mapDebris.DebrisState[i, j].Fragments = 0;
                }
            }
        }
    }
}