﻿using OpenNefia.Content.DisplayName;
using OpenNefia.Content.Resists;
using OpenNefia.Content.Skills;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Locale;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Serialization.Manager.Attributes;
using OpenNefia.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenNefia.Content.Combat
{
    public interface IDamageType
    {
        /// <summary>
        /// Displayed in-game when a character is killed.
        /// </summary>
        /// <remarks>
        /// "You were squashed by a putitoro."
        /// </remarks>
        string LocalizeDeathMessage(EntityUid target, EntityUid? attacker, IEntityManager entityManager);

        /// <summary>
        /// Displayed in the high-score (bones) menu when prompted to revive.
        /// </summary>
        /// <remarks>
        /// "was squashed by a putitoro."
        /// </remarks>
        string LocalizeDeathCauseMessage(EntityUid target, EntityUid? attacker, IEntityManager entityManager);
    }

    public sealed class DamageHPExtraArgs
    {
        public int OriginalDamage { get; set; }

        /// <summary>
        /// Number of recursive calls to DamageHP().
        /// This is for damage types that themselves call DamageHP() somewhere,
        /// so that certain effects like splitting up monsters (bubbles, etc.) are
        /// not applied twice.
        /// </summary>
        public int DamageSubLevel { get; set; } = 0;

        // The following properties are only used for printing the damage message.

        public bool ShowMessage { get; set; } = true;
        public EntityUid? Weapon { get; set; }
        public int AttackCount { get; set; }
        public PrototypeId<SkillPrototype>? AttackSkill { get; set; }
        public DamageHPTense MessageTense { get; set; }
        public bool IsThirdPerson { get; set; }
        public bool NoAttackText { get; set; }
    }

    public enum DamageHPTense
    {
        Passive,
        Active
    }

    public enum CharaDeathType
    {
        Killed,
        Minced,
        TransformedIntoMeat,
        Destroyed,
    }

    [DataDefinition]
    public sealed class CharaDamageType : IDamageType
    {
        [DataField]
        public CharaDeathType CharaDeathType { get; }

        [DataField]
        public int AttackCount { get; }

        [DataField]
        public IDamageType? Inner { get; }

        public CharaDamageType(CharaDeathType type, IDamageType inner)
        {
            CharaDeathType = type;
            Inner = inner;
        }

        public string LocalizeDeathCauseMessage(EntityUid target, EntityUid? attacker, IEntityManager entityManager)
        {
            return Loc.GetString("Elona.DamageType.Chara.DeathCause", ("attacker", attacker));
        }

        public string LocalizeDeathMessage(EntityUid target, EntityUid? attacker, IEntityManager entityManager)
        {
            return Loc.GetString($"Elona.DamageType.Chara.{CharaDeathType}.Passive", ("entity", target), ("attacker", attacker));
        }
    }

    [DataDefinition]
    public sealed class ElementalDamageType : IDamageType
    {
        [DataField]
        public PrototypeId<ElementPrototype> ElementID { get; }

        [DataField]
        public int Power { get; }

        public ElementalDamageType(PrototypeId<ElementPrototype> elementID, int power)
        {
            ElementID = elementID;
            Power = power;
        }

        public string LocalizeDeathCauseMessage(EntityUid target, EntityUid? attacker, IEntityManager entityManager)
        {
            return Loc.GetPrototypeString(ElementID, "Death.Passive", ("entity", target));
        }

        public string LocalizeDeathMessage(EntityUid target, EntityUid? attacker, IEntityManager entityManager)
        {
            return Loc.GetPrototypeString(ElementID, "Death.Active", ("entity", target), ("attacker", attacker));
        }
    }

    [DataDefinition]
    public sealed class BurdenDamageType : IDamageType
    {
        [DataField]
        public EntityUid? ItemSquashedBy { get; }

        public BurdenDamageType(EntityUid itemSquashedBy)
        {
            ItemSquashedBy = itemSquashedBy;
        }

        public string LocalizeDeathCauseMessage(EntityUid target, EntityUid? attacker, IEntityManager entityManager)
        {
            var itemName = GetItemName(entityManager);
            return Loc.GetString("Elona.DamageType.Burden.DeathCause", ("entity", target), ("itemName", itemName));
        }

        public string LocalizeDeathMessage(EntityUid target, EntityUid? attacker, IEntityManager entityManager)
        {
            var itemName = GetItemName(entityManager);
            return Loc.GetString($"Elona.DamageType.Burden.Message", ("entity", target), ("itemName", itemName));
        }

        private string GetItemName(IEntityManager entityManager)
        {
            if (entityManager.IsAlive(ItemSquashedBy))
                return EntitySystem.Get<IDisplayNameSystem>().GetDisplayName(ItemSquashedBy.Value);
            else
                return Loc.GetString("Elona.DamageType.Burden.Backpack");
        }
    }

    [DataDefinition]
    public sealed class GenericDamageType : IDamageType
    {
        /// <summary>
        /// Locale key like "Elona.DamageType.Poison"
        /// </summary>
        [DataField]
        public LocaleKey LocaleKey { get; }

        public GenericDamageType(LocaleKey localeKey)
        {
            LocaleKey = localeKey;
        }

        public string LocalizeDeathCauseMessage(EntityUid target, EntityUid? attacker, IEntityManager entityManager)
        {
            return Loc.GetString(LocaleKey.With("DeathCause"), ("entity", target));
        }

        public string LocalizeDeathMessage(EntityUid target, EntityUid? attacker, IEntityManager entityManager)
        {
            return Loc.GetString(LocaleKey.With("Message"), ("entity", target), ("attacker", attacker));
        }
    }
}