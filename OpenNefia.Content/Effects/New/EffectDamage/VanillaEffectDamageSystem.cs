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
using OpenNefia.Content.Skills;
using OpenNefia.Content.Parties;
using OpenNefia.Content.Damage;
using NetVips;
using OpenNefia.Content.Effects.New.EffectAreas;
using OpenNefia.Core.Prototypes;

namespace OpenNefia.Content.Effects.New.EffectDamage
{
    public sealed class VanillaEffectDamageSystem : EntitySystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IAreaManager _areaManager = default!;
        [Dependency] private readonly IRandom _rand = default!;
        [Dependency] private readonly IMessagesManager _mes = default!;
        [Dependency] private readonly IEntityLookup _lookup = default!;
        [Dependency] private readonly ISkillsSystem _skills = default!;
        [Dependency] private readonly IPartySystem _parties = default!;
        [Dependency] private readonly IDamageSystem _damages = default!;
        [Dependency] private readonly IPrototypeManager _protos = default!;

        public override void Initialize()
        {
            SubscribeComponent<EffectBaseDamageDiceComponent, ApplyEffectDamageEvent>(ApplyDamage_Dice, priority: EventPriorities.VeryHigh - 10000);
            SubscribeComponent<EffectDamageControlMagicComponent, ApplyEffectDamageEvent>(ApplyDamage_ControlMagic, priority: EventPriorities.VeryHigh - 100);

            SubscribeComponent<EffectDamageElementalComponent, ApplyEffectDamageEvent>(ApplyDamage_Elemental);
            SubscribeComponent<EffectDamageElementalComponent, GetEffectAnimationParamsEvent>(GetAnimParams_Elemental);
        }

        private void ApplyDamage_Dice(EntityUid uid, EffectBaseDamageDiceComponent component, ApplyEffectDamageEvent args)
        {
            if (args.Cancelled)
                return;

            // TODO formula
            // TODO distance modifier
            args.OutDamage = 500;
        }

        private enum ControlMagicStatus
        {
            Success,
            Partial,
            Failure
        }
        private record class ControlMagicResult(ControlMagicStatus Status, int NewDamage);

        private ControlMagicResult ProcControlMagic(EntityUid source, EntityUid target, int damage)
        {
            if (!_skills.TryGetKnown(source, Protos.Skill.ControlMagic, out var controlMagicLv)
                || !_parties.IsInSameParty(source, target))
            {
                return new(ControlMagicStatus.Failure, damage);
            }

            if (controlMagicLv.Level.Buffed * 5 > _rand.Next(damage + 1))
            {
                damage = 0;
            }
            else
            {
                damage = _rand.Next(damage * 100 / (100 + controlMagicLv.Level.Buffed * 10) + 1);
            }

            return new(damage <= 0 ? ControlMagicStatus.Success : ControlMagicStatus.Partial, damage);
        }

        private void ApplyDamage_ControlMagic(EntityUid uid, EffectDamageControlMagicComponent component, ApplyEffectDamageEvent args)
        {
            if (args.Cancelled)
                return;

            var result = ProcControlMagic(args.Source, args.InnerTarget, args.OutDamage);
            args.OutDamage = result.NewDamage;

            switch (result.Status)
            {
                case ControlMagicStatus.Success:
                    _mes.Display(Loc.GetString("Elona.Magic.ControlMagic.PassesThrough", ("target", args.InnerTarget)), entity: args.InnerTarget);
                    args.Cancel();
                    break;
                case ControlMagicStatus.Partial:
                    _skills.GainSkillExp(args.Source, Protos.Skill.ControlMagic, 30, 2);
                    break;
                case ControlMagicStatus.Failure:
                default:
                    break;
            }
        }

        private void GetAnimParams_Elemental(EntityUid uid, EffectDamageElementalComponent component, GetEffectAnimationParamsEvent args)
        {
            if (_protos.TryIndex(component.Element, out var eleProto))
            {
                args.OutColor = eleProto.Color;
                args.OutSound = eleProto.Sound?.GetSound();
            }
        }

        /// <summary>
        /// Shows a message like "The bolt hits the putit and" or "The bolt hits the putit.".
        /// The rest of the message is displayed in <see cref="IDamageSystem.DamageHP"/>.
        /// </summary>
        private void DisplayEffectDamageMessage(EntityUid uid, EntityUid innerTarget, DamageHPMessageTense tense)
        {
            if (TryComp<EffectDamageMessageComponent>(uid, out var damageMessage))
            {
                var endKey = tense == DamageHPMessageTense.Active ? "Other" : "Ally";
                _mes.Display(Loc.GetString(damageMessage.RootKey.With(endKey), ("entity", innerTarget)), entity: innerTarget);
            }
        }

        private void ApplyDamage_Elemental(EntityUid uid, EffectDamageElementalComponent component, ApplyEffectDamageEvent args)
        {
            if (args.Cancelled)
                return;

            var tense = _damages.GetDamageMessageTense(args.InnerTarget);
            DisplayEffectDamageMessage(uid, args.InnerTarget, tense);

            var elementalPower = 0; // TODO is kept on dice

            var extraArgs = new DamageHPExtraArgs()
            {
                MessageTense = tense,
                NoAttackText = true,
                IsThirdPerson = true
            };
            var damageType = new ElementalDamageType(component.Element, elementalPower);
            _damages.DamageHP(args.InnerTarget, args.OutDamage, args.Source, damageType, extraArgs);
        }
    }
}