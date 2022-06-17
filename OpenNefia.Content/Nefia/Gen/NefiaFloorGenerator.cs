﻿using OpenNefia.Content.Maps;
using OpenNefia.Core.Areas;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Log;
using OpenNefia.Core.Maps;
using OpenNefia.Core.Maths;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Random;
using OpenNefia.Core.Utility;
using OpenNefia.Content.Prototypes;
using System.Diagnostics.CodeAnalysis;

namespace OpenNefia.Content.Nefia
{
    public class NefiaFloorGenerator
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IRandom _rand = default!;

        public NefiaFloorGenerator()
        {
            EntitySystem.InjectDependencies(this);
        }

        private bool TryToGenerateRaw(IArea area, MapId mapId, int floorNumber, [NotNullWhen(true)] out IMap? map, [NotNullWhen(true)] out Blackboard<NefiaGenParams>? data)
        {
            var attempts = 2000;

            for (var i = 0; i < attempts; i++)
            {
                _mapManager.UnloadMap(mapId);
                _rand.RandomizeSeed();

                var width = 34 + _rand.Next(15);
                var height = 22 + _rand.Next(15);

                data = new Blackboard<NefiaGenParams>();
                data.Add(new BaseNefiaGenParams(width, height));

                var paramsEv = new GenerateNefiaFloorParamsEvent(area, mapId, data, floorNumber, i);
                _entityManager.EventBus.RaiseLocalEvent(area.AreaEntityUid, paramsEv);

                var genEv = new GenerateNefiaFloorAttemptEvent(area, mapId, data, floorNumber, i);
                _entityManager.EventBus.RaiseLocalEvent(area.AreaEntityUid, genEv);

                if (genEv.Handled)
                {
                    if (_mapManager.MapIsLoaded(mapId))
                    {
                        map = _mapManager.GetMap(mapId);
                        return true;
                    }
                    else
                    {
                        Logger.ErrorS("nefia.gen.floor", $"Map for nefia floor {mapId} not generated!");
                    }
                }
            }

            data = null;
            map = null;
            return false;
        }

        public bool TryToGenerate(IArea area, MapId mapId, int floorNumber, [NotNullWhen(true)] out IMap? map)
        {
            if (!TryToGenerateRaw(area, mapId, floorNumber, out map, out var data))
            {
                return false;
            }

            var ev = new AfterGenerateNefiaFloorEvent(area, map, data, floorNumber);
            _entityManager.EventBus.RaiseLocalEvent(area.AreaEntityUid, ev);

            return true;
        }
    }

    public class NefiaGenParams {}

    public sealed class BaseNefiaGenParams : NefiaGenParams
    {
        public BaseNefiaGenParams(int width, int height)
        {
            MapSize = Vector2i.ComponentMax(new(width, height), Vector2i.One);
        }

        public Vector2i MapSize { get; set; } = Vector2i.One;
        public int DangerLevel { get; set; } = 1;
        public int RoomCount { get; set; } = 1;
        public int MinRoomSize { get; set; } = 3;
        public int MaxRoomSize { get; set; } = 4;
        public int ExtraRoomCount { get; set; } = 10;
        public int RoomEntranceCount { get; set; } = 1;
        public int TunnelLength { get; set; } = 1;
        public float HiddenPathChance { get; set; } = 0.05f;
        public int CreaturePacks { get; set; } = 1;
        public bool CanHaveMultipleMonsterHouses { get; set; } = false;
        public int MaxCharaCount { get; set; } = 1;
        public PrototypeId<MapTilesetPrototype> Tileset = Protos.MapTileset.Dungeon;
    }

    internal class GenerateNefiaFloorParamsEvent
    {
        public IArea Area { get; }
        public MapId MapId { get; }
        public int GenerationAttempt { get; }
        public int FloorNumber { get; }
        public Blackboard<NefiaGenParams> Data { get; set; }
        public BaseNefiaGenParams BaseParams => Data.Get<BaseNefiaGenParams>();

        public GenerateNefiaFloorParamsEvent(IArea area, MapId mapId, Blackboard<NefiaGenParams> data, int generationAttempt, int floorNumber)
        {
            Area = area;
            MapId = mapId;
            Data = data;
            GenerationAttempt = generationAttempt;
            FloorNumber = floorNumber;
        }
    }

    /// <summary>
    /// Event for attempting to generate a nefia floor. If the attempt succeeds,
    /// the handler should set <c>Handled</c> to true.
    /// </summary>
    public sealed class GenerateNefiaFloorAttemptEvent : HandledEntityEventArgs
    {
        public IArea Area { get; }
        public MapId MapId { get; }
        public int GenerationAttempt { get; }
        public int FloorNumber { get; }
        public Blackboard<NefiaGenParams> Data { get; set; }
        public BaseNefiaGenParams BaseParams => Data.Get<BaseNefiaGenParams>();

        public GenerateNefiaFloorAttemptEvent(IArea area, MapId mapId, Blackboard<NefiaGenParams> data, int generationAttempt, int floorNumber)
        {
            Area = area;
            MapId = mapId;
            Data = data;
            GenerationAttempt = generationAttempt;
            FloorNumber = floorNumber;
        }

        public void Handle()
        {
            Handled = true;
        }
    }

    public sealed class AfterGenerateNefiaFloorEvent : EntityEventArgs
    {
        public IArea Area { get; }
        public IMap Map { get; }
        public int FloorNumber { get; }
        public Blackboard<NefiaGenParams> Data { get; set; }
        public BaseNefiaGenParams BaseParams => Data.Get<BaseNefiaGenParams>();

        public AfterGenerateNefiaFloorEvent(IArea area, IMap map, Blackboard<NefiaGenParams> data, int floorNumber)
        {
            Area = area;
            Map = map;
            Data = data;
            FloorNumber = floorNumber;
        }
    }
}