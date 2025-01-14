﻿using OpenNefia.Content.Logic;
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
using OpenNefia.Content.DeferredEvents;
using OpenNefia.Content.Damage;
using OpenNefia.Content.Encounters;
using OpenNefia.Content.UI;
using OpenNefia.Content.EntityGen;
using OpenNefia.Content.Factions;
using OpenNefia.Content.Charas;
using OpenNefia.Content.Parties;
using OpenNefia.Core.Log;

namespace OpenNefia.Content.Quests
{
    public interface IQuestEliminateTargetSystem : IEntitySystem
    {
        IEnumerable<TargetForEliminateQuestComponent> EnumerateQuestEliminateTargets(IMap map, string tag);
        bool AllTargetsEliminated(IMap map, string tag);
    }

    public sealed class QuestEliminateTargetSystem : EntitySystem, IQuestEliminateTargetSystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IAreaManager _areaManager = default!;
        [Dependency] private readonly IRandom _rand = default!;
        [Dependency] private readonly IMessagesManager _mes = default!;
        [Dependency] private readonly IEntityLookup _lookup = default!;
        [Dependency] private readonly IDeferredEventsSystem _deferredEvents = default!;
        [Dependency] private readonly IFactionSystem _factions = default!;
        [Dependency] private readonly IPartySystem _parties = default!;

        public override void Initialize()
        {
            SubscribeComponent<CharaComponent, EntityBeingGeneratedEvent>(AutoTagTargetEntity);
            SubscribeComponent<TargetForEliminateQuestComponent, BeforeEntityDeletedEvent>(HandleEntityBeingDeleted);
            SubscribeComponent<TargetForEliminateQuestComponent, EntityKilledEvent>(HandleEntityKilled);
            SubscribeComponent<MapReportQuestEliminateTargetsComponent, MapQuestTargetKilledEvent>(ReportTargetEntities);
        }

        // TODO expand to all entities even if spawned outside IEntityGen
        private void AutoTagTargetEntity(EntityUid uid, CharaComponent component, ref EntityBeingGeneratedEvent args)
        {
            if (TryMap(uid, out var map) && TryComp<MapTagEntitiesAsQuestTargetsComponent>(map.MapEntityUid, out var mapComp))
            {
                if (_factions.GetRelationToPlayer(uid) <= Relation.Hate)
                {
                    Logger.DebugS("quest.target", $"Autotag spawned entity as target: {uid} -> {mapComp.Tag}");
                    EnsureComp<TargetForEliminateQuestComponent>(uid).Tag = mapComp.Tag;
                }
            }
        }

        private void HandleEntityBeingDeleted(EntityUid uid, TargetForEliminateQuestComponent component, ref BeforeEntityDeletedEvent args)
        {
            if (IsAlive(uid))
                CheckQuestTargetElimination(uid, component);
        }

        private void HandleEntityKilled(EntityUid uid, TargetForEliminateQuestComponent component, ref EntityKilledEvent args)
        {
            CheckQuestTargetElimination(uid, component);
        }

        private void CheckQuestTargetElimination(EntityUid uid, TargetForEliminateQuestComponent component)
        {
            if (!TryMap(uid, out var map))
                return;

            var targets = EnumerateQuestEliminateTargets(map, component.Tag).ToList();
            var raiseEliminatedEvent = targets.Count == 0
                || targets.Count == 1 && targets[0].Owner == uid;

            var count = targets.Count;
            if (raiseEliminatedEvent)
                count--;

            var ev1 = new MapQuestTargetKilledEvent(component.Tag, int.Max(count, 0));
            RaiseEvent(map.MapEntityUid, ev1);

            if (raiseEliminatedEvent)
            {
                _deferredEvents.Enqueue(() =>
                {
                    var ev2 = new MapQuestTargetsEliminatedEvent(component.Tag);
                    RaiseEvent(map.MapEntityUid, ev2);
                    return TurnResult.Aborted;
                });
            }
        }

        private void ReportTargetEntities(EntityUid uid, MapReportQuestEliminateTargetsComponent component, MapQuestTargetKilledEvent args)
        {
            // >>>>>>>> elona122/shade2/chara_func.hsp:192 			if p=0:	evAdd evQuestEliminate :else: txtMore:t ...
            if (args.TargetsRemaining > 0)
                _mes.Display(Loc.GetString("Elona.Quest.Eliminate.TargetsRemaining", ("count", args.TargetsRemaining)), color: UiColors.MesBlue);
            // <<<<<<<< elona122/shade2/chara_func.hsp:192 			if p=0:	evAdd evQuestEliminate :else: txtMore:t ...
        }

        public bool AllTargetsEliminated(IMap map, string tag)
        {
            return EnumerateQuestEliminateTargets(map, tag).Count() == 0;
        }

        public IEnumerable<TargetForEliminateQuestComponent> EnumerateQuestEliminateTargets(IMap map, string tag)
        {
            return _lookup.EntityQueryInMap<TargetForEliminateQuestComponent, FactionComponent>(map)
                 .Where(pair => pair.Item1.Tag == tag 
                         && pair.Item2.RelationToPlayer <= Relation.Hate
                         && !_parties.IsInPlayerParty(pair.Item1.Owner))
                 .Select(pair => pair.Item1);
        }
    }

    /// <summary>
    /// Raised when all targets with a specific tag are eliminated.
    /// Raised immediately after the entity is killed/deleted.
    /// </summary>
    [EventUsage(EventTarget.Map)]
    public class MapQuestTargetKilledEvent : EntityEventArgs
    {
        public MapQuestTargetKilledEvent(string tag, int targetsRemaining)
        {
            Tag = tag;
            TargetsRemaining = targetsRemaining;
        }

        public string Tag { get; }
        public int TargetsRemaining { get; }
    }

    /// <summary>
    /// Raised when all targets with a specific tag are eliminated.
    /// Deferred to the beginning of the next turn.
    /// </summary>
    [EventUsage(EventTarget.Map)]
    public class MapQuestTargetsEliminatedEvent : EntityEventArgs
    {
        public MapQuestTargetsEliminatedEvent(string tag)
        {
            Tag = tag;
        }

        public string Tag { get; }
    }
}