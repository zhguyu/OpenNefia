﻿using OpenNefia.Core.GameObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenNefia.Content.Prototypes;
using OpenNefia.Core.Prototypes;
using OpenNefia.Content.TurnOrder;
using OpenNefia.Content.Maps;
using OpenNefia.Content.VanillaAI;
using OpenNefia.Core.IoC;
using OpenNefia.Content.Visibility;
using OpenNefia.Core.Random;
using OpenNefia.Content.Logic;
using OpenNefia.Content.EmotionIcon;
using OpenNefia.Content.UI;
using OpenNefia.Core.Locale;
using OpenNefia.Core.Game;
using OpenNefia.Content.Factions;
using OpenNefia.Content.Hunger;
using OpenNefia.Core.Maths;
using OpenNefia.Core.Directions;
using OpenNefia.Content.Damage;
using OpenNefia.Content.Hud;
using OpenNefia.Content.Sleep;

namespace OpenNefia.Content.StatusEffects
{
    public sealed partial class VanillaStatusEffectsSystem : EntitySystem
    {
        [Dependency] private readonly IEntityLookup _lookup = default!;
        [Dependency] private readonly IVisibilitySystem _vis = default!;
        [Dependency] private readonly IRandom _rand = default!;
        [Dependency] private readonly IMessagesManager _mes = default!;
        [Dependency] private readonly IGameSessionManager _gameSession = default!;
        [Dependency] private readonly IFactionSystem _factions = default!;
        [Dependency] private readonly IVanillaAISystem _vanillaAI = default!;
        [Dependency] private readonly IEmotionIconSystem _emoIcons = default!;
        [Dependency] private readonly IStatusEffectSystem _statusEffects = default!;
        [Dependency] private readonly IHungerSystem _hunger = default!;

        public override void Initialize()
        {
            SubscribeComponent<StatusEffectsComponent, OnCharaSleepEvent>(Sick_OnSleep);
            SubscribeComponent<StatusEffectsComponent, CalcFinalDamageEvent>(HandleCalcDamageFury, priority: EventPriorities.VeryHigh);
            SubscribeComponent<StatusEffectsComponent, EntityPassTurnEventArgs>(HandlePassTurnDrunk);
            SubscribeComponent<StatusEffectsComponent, BeforeMoveEventArgs>(ProcRandomMovement);
        }

        private void HandleCalcDamageFury(EntityUid uid, StatusEffectsComponent component, ref CalcFinalDamageEvent args)
        {
            // >>>>>>>> elona122/shade2/chara_func.hsp:1441 	dmg = dmgOrg * (1+(cAngry(tc)>0)) ..
            if (_statusEffects.HasEffect(uid, Protos.StatusEffect.Fury, component))
                args.OutFinalDamage *= 2;

            if (EntityManager.IsAlive(args.Attacker) && _statusEffects.HasEffect(args.Attacker.Value, Protos.StatusEffect.Fury))
                args.OutFinalDamage *= 2;
            // <<<<<<<< elona122/shade2/chara_func.hsp:1442 	if dmgSource>=0 : if cAngry(dmgSource)>0:dmg*=2 ..
        }

        public const int DrunkLevelHeavy = 45;

        private void Sick_OnSleep(EntityUid uid, StatusEffectsComponent comp, OnCharaSleepEvent ev)
        {
            if (_statusEffects.HasEffect(uid, Protos.StatusEffect.Sick))
            {
                _statusEffects.Heal(uid, Protos.StatusEffect.Sick, 7 + _rand.Next(7));
            }
        }

        private void HandlePassTurnDrunk(EntityUid drunkard, StatusEffectsComponent component, EntityPassTurnEventArgs args)
        {
            if (args.Handled || !_statusEffects.HasEffect(drunkard, Protos.StatusEffect.Drunk, component))
                return;

            if (_rand.OneIn(200) && !_gameSession.IsPlayer(drunkard))
                TryToPickDrunkardFight(drunkard);

            if (_statusEffects.GetTurns(drunkard, Protos.StatusEffect.Drunk) >= DrunkLevelHeavy
                || (TryComp<HungerComponent>(drunkard, out var hunger) && hunger.Nutrition >= HungerLevels.Vomit))
            {
                if (_rand.OneIn(60))
                {
                    _hunger.Vomit(drunkard);
                    args.Handle(TurnResult.Failed);
                    return;
                }
            }
        }

        private bool CanPickFightWith(SpatialComponent drunkard, SpatialComponent opponent)
        {
            return IsAlive(opponent.Owner)
                && drunkard.Owner != opponent.Owner
                && drunkard.MapPosition.TryDistanceTiled(opponent.MapPosition, out var dist)
                && dist <= 5
                && _vis.HasLineOfSight(drunkard.Owner, opponent.Owner)
                && _rand.OneIn(3);
        }

        private void TryToPickDrunkardFight(EntityUid drunkard)
        {
            if (!TryMap(drunkard, out var map) || HasComp<MapTypeWorldMapComponent>(map.MapEntityUid))
                return;

            var drunkardSpatial = Spatial(drunkard);

            var opponentPair = _lookup.EntityQueryInMap<SpatialComponent, VanillaAIComponent>(map)
                .Where(pair => CanPickFightWith(drunkardSpatial, pair.Item1))
                .FirstOrDefault();

            if (opponentPair == default)
                return;

            var (opponentSpatial, opponentAI) = opponentPair;
            var opponent = opponentSpatial.Owner;

            var isInFov = _vis.IsInWindowFov(drunkard) || _vis.IsInWindowFov(opponent);

            if (isInFov)
            {
                _mes.Display(Loc.GetString("Elona.StatusEffect.Drunk.GetsTheWorse", ("chara", drunkard), ("target", opponent)), UiColors.MesSkyBlue);
                _mes.Display(Loc.GetString("Elona.StatusEffect.Drunk.Dialog"));
            }

            if (_rand.OneIn(4) && !_gameSession.IsPlayer(opponent) && isInFov)
            {
                _mes.Display(Loc.GetString("Elona.StatusEffect.Drunk.Annoyed.Text"), UiColors.MesSkyBlue);
                _mes.Display(Loc.GetString("Elona.StatusEffect.Drunk.Annoyed.Dialog"));

                // XXX: This may not be correct.
                if (TryComp<FactionComponent>(opponent, out var faction))
                {
                    _factions.SetPersonalRelationTowards(opponent, drunkard, Relation.Enemy);

                    _vanillaAI.SetTarget(opponent, drunkard, 20);
                    _emoIcons.SetEmotionIcon(opponent, EmotionIcons.Angry, 2);
                }
            }
        }

        private void ProcRandomMovement(EntityUid uid, StatusEffectsComponent statusEffects, BeforeMoveEventArgs args)
        {
            var stumble = false;

            if (_statusEffects.HasEffect(uid, Protos.StatusEffect.Dimming, statusEffects)
                && _statusEffects.GetTurns(uid, Protos.StatusEffect.Dimming, statusEffects) + 10 > _rand.Next(60))
            {
                stumble = true;
            }

            if (_statusEffects.HasEffect(uid, Protos.StatusEffect.Drunk, statusEffects) && _rand.OneIn(5))
            {
                _mes.Display(Loc.GetString("Elona.StatusEffect.Drunk.Stagger"), UiColors.MesSkyBlue);
                stumble = true;
            }

            if (_statusEffects.HasEffect(uid, Protos.StatusEffect.Confusion))
            {
                stumble = true;
            }

            if (stumble)
            {
                args.OutNewPosition = new(args.DesiredPosition.MapId,
                    Spatial(uid).WorldPosition + DirectionUtility.RandomDirections().First().ToIntVec());
            }
        }
    }
}
