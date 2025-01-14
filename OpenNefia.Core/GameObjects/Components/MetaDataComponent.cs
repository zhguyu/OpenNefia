using System;
using NLua;
using OpenNefia.Analyzers;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Locale;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Serialization.Manager.Attributes;
using OpenNefia.Core.Utility;

namespace OpenNefia.Core.GameObjects
{
    /// <summary>
    ///     Contains meta data about this entity that isn't component specific.
    /// </summary>
    public class MetaDataComponent : Component, IComponentLocalizable
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;

        /// <summary>
        ///     The in-game name of this entity.
        /// </summary>
        [Localize]
        public string? DisplayName { get; set; }

        /// <summary>
        /// If true, don't add an article before this entity's name.
        /// <remarks>
        /// <para>
        /// Contrast "the putit" with "Lomias".
        /// </para>
        /// <para>
        /// In HSP, any character whose name started with a '<' had a proper noun as a name.
        /// </para>
        /// </remarks>
        [Localize]
        public bool NameIsProperNoun { get; private set; } = false;

        private EntityPrototype? _entityPrototype;

        /// <summary>
        ///     The prototype this entity was created from, if any.
        /// </summary>
        public EntityPrototype? EntityPrototype
        {
            get => _entityPrototype;
            set
            {
                _entityPrototype = value;
            }
        }

        /// <summary>
        ///     The current lifetime stage of this entity. You can use this to check
        ///     if the entity is initialized or being deleted.
        /// </summary>
        public EntityLifeStage EntityLifeStage { get; internal set; }

        public bool EntityInitialized => EntityLifeStage >= EntityLifeStage.Initialized;
        public bool EntityInitializing => EntityLifeStage == EntityLifeStage.Initializing;
        public bool EntityDeleted => EntityLifeStage >= EntityLifeStage.Deleted;
        public bool EntityTerminating => EntityLifeStage >= EntityLifeStage.Terminating;

        [DataField]
        private EntityGameLiveness _liveness = EntityGameLiveness.Alive;

        /// <summary>
        /// Liveness of the entity for validity purposes.
        /// 
        /// As an example, if a character is killed it will still remain in
        /// the map as a DeadAndBuried entity, for example if something wants to
        /// cast Resurrect on it. If the character is an ally, it will become
        /// Hidden when killed instead, so it doesn't get permanently removed
        /// when the map is changed.
        /// 
        /// A stackable entity with amount 0 counts as DeadAndBuried.
        /// </summary>
        public EntityGameLiveness Liveness
        {
            get => _liveness;
            set
            {
                var oldLiveness = _liveness;
                _liveness = value;

                var livenessEvent = new EntityLivenessChangedEvent(Owner, oldLiveness, Liveness);
                _entityManager.EventBus.RaiseEvent(Owner, ref livenessEvent);
            }
        }

        public bool IsAlive => (Liveness == EntityGameLiveness.Alive) && !EntityTerminating;
        public bool IsDeadAndBuried => Liveness == EntityGameLiveness.DeadAndBuried || EntityTerminating;

        /// <summary>
        /// If false, this entity will not be serialized when its map is unloaded.
        /// Useful for generating lists of temporary entities that might not all
        /// need saving.
        /// </summary>
        [DataField("isMapSavable")]
        private bool _isMapSavable { get; set; } = true;

        public bool IsMapSavable {
            get => _isMapSavable && (EntityPrototype?.MapSavable ?? true);
            set => _isMapSavable = value; 
        }

        void IComponentLocalizable.LocalizeFromLua(LuaTable table)
        {
            DisplayName = table.GetStringOrNull("Name");
            NameIsProperNoun = table.GetBoolean(nameof(NameIsProperNoun), false);
        }
    }

    public enum EntityGameLiveness
    {
        /// <summary>
        /// This entity is active in the map.
        /// </summary>
        Alive,

        /// <summary>
        /// This entity is not active, but should not be removed from the map.
        /// </summary>
        Hidden,

        /// <summary>
        /// This entity should be removed from the map when certain actions are performed
        /// (enumerating a container, changing maps).
        /// </summary>
        DeadAndBuried
    }

    [ByRefEvent]
    public struct EntityLivenessChangedEvent
    {
        public readonly EntityUid EntityUid;
        public readonly EntityGameLiveness OldLiveness;
        public readonly EntityGameLiveness NewLiveness;

        public EntityLivenessChangedEvent(EntityUid entityUid, EntityGameLiveness oldLiveness, EntityGameLiveness liveness)
        {
            EntityUid = entityUid;
            OldLiveness = oldLiveness;
            NewLiveness = liveness;
        }
    }
}
