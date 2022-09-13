﻿using OpenNefia.Content.Areas;
using OpenNefia.Content.Logic;
using OpenNefia.Content.Prototypes;
using OpenNefia.Core.Areas;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Locale;
using OpenNefia.Core.Maps;
using OpenNefia.Core.Random;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenNefia.Content.Home;
using OpenNefia.Content.Maps;
using OpenNefia.Core.SaveGames;
using OpenNefia.Content.RandomAreas;
using OpenNefia.Content.DeferredEvents;
using OpenNefia.Content.Dialog;
using OpenNefia.Content.Charas;
using OpenNefia.Core.Game;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Utility;

namespace OpenNefia.Content.Scenarios
{
    public sealed partial class VanillaScenariosSystem : EntitySystem
    {
        [Dependency] private readonly IAreaManager _areaManager = default!;
        [Dependency] private readonly IHomeSystem _homes = default!;
        [Dependency] private readonly IMapEntranceSystem _mapEntrances = default!;
        [Dependency] private readonly IAreaEntranceSystem _areaEntrances = default!;
        [Dependency] private readonly IMapLoader _mapLoader = default!;
        [Dependency] private readonly ISaveGameManager _saveGame = default!;
        [Dependency] private readonly IDeferredEventsSystem _deferredEvents = default!;
        [Dependency] private readonly IDialogSystem _dialog = default!;
        [Dependency] private readonly ICharaSystem _charas = default!;
        [Dependency] private readonly IGameSessionManager _gameSession = default!;

        public void Default_OnGameStart(ScenarioPrototype proto, P_ScenarioOnGameStartEvent ev)
        {
            // Major TODO for now. Just set up important things like the player's house.

            var northTyrisArea = _areaManager.GetGlobalArea(GlobalAreas.NorthTyris);
            var northTyrisMap = _areaManager.GetOrGenerateMapForFloor(northTyrisArea.Id, GlobalAreas.NorthTyris_FloorNorthTyris)!;

            var yourHomeArea = _areaManager.CreateArea(Protos.Area.HomeCave, parent: northTyrisArea.Id);
            IMap yourHomeMap = _areaManager.GetOrGenerateMapForFloor(yourHomeArea.Id, HomeSystem.AreaFloorHome.WithFloorNumber(0))!;

            _homes.ActiveHomeID = yourHomeMap.Id;

            // Make sure when the player steps outside of their home for the first time that they
            // will end up at the right location.
            var homeEntrancePos = northTyrisMap.AtPos(22, 21);
            _areaEntrances.CreateAreaEntrance(yourHomeArea, homeEntrancePos);
            _mapEntrances.SetPreviousMap(yourHomeMap, homeEntrancePos);

            _mapLoader.SaveMap(yourHomeMap.Id, _saveGame.CurrentSave!);
            _mapLoader.SaveMap(northTyrisMap.Id, _saveGame.CurrentSave!);

            Spatial(ev.Player).Coordinates = yourHomeMap.AtPosEntity(yourHomeMap.Width / 2, yourHomeMap.Height / 2);

            if (TryComp<MapRandomAreaManagerComponent>(northTyrisMap.MapEntityUid, out var randomAreas))
                randomAreas.RegenerateRandomAreas = true;

            _deferredEvents.Enqueue(() =>
            {
                var lomias = _charas.EnumerateNonAllies(yourHomeMap)
                    .FirstOrDefault(c => ProtoIDOrNull(c.Owner) == Protos.Chara.Lomias);
                if (lomias != null)
                {
                    _dialog.StartDialog(_gameSession.Player, lomias.Owner, Protos.Dialog.LomiasNewGame);
                }

                return TurnResult.Aborted;
            });
            
            ev.OutActiveMap = yourHomeMap;
        }

        public void Quickstart_OnGameStart(ScenarioPrototype proto, P_ScenarioOnGameStartEvent ev)
        {
            var map = _mapLoader.LoadBlueprint(new ResourcePath("/Maps/LecchoTorte/Test.yml"));
            map.MemorizeAllTiles();

            _homes.ActiveHomeID = map.Id;

            var playerSpatial = Spatial(ev.Player);
            playerSpatial.Coordinates = map.AtPosEntity(2, 2);

            ev.OutActiveMap = map;
        }
    }

    [PrototypeEvent(typeof(ScenarioPrototype))]
    public sealed class P_ScenarioOnGameStartEvent : PrototypeEventArgs
    {
        public EntityUid Player { get; }

        public IMap? OutActiveMap { get; set; }

        public P_ScenarioOnGameStartEvent(EntityUid player)
        {
            Player = player;
        }
    }
}