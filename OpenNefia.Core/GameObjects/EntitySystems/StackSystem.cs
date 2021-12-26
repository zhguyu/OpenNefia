﻿using OpenNefia.Analyzers;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Maps;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Serialization.Manager;
using OpenNefia.Core.Utility;

namespace OpenNefia.Core.GameObjects
{
    /// <summary>
    /// Entity stacking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This implementation differs from Robust's StackSystem in several notable ways:
    /// </para>
    /// <para>
    /// 1. Robust's stacking system spawns a new entity when the stack is split.
    ///    In contrast, Elona's item_separate copies every property of the original
    ///    item to the split item.
    /// </para>
    /// <para>
    /// 2. Robust uses a "stackType" property to check if two entities can be
    ///    stacked together. If the stackType matches, then all is good. On the other
    ///    hand, Elona's item_stack does the equivalent of a deepcompare on every item
    ///    property to check for stackability, with special exceptions for some properties 
    ///    like quantity and quality. Naturally, this makes things way harder, especially
    ///    considering that some components were never meant to be stacked as part of an item.
    /// </para>
    /// <para>
    /// 3. Robust has an enforced maximum entity count per stack, while Elona lacks one.
    /// </para>
    /// </remarks>
    public interface IStackSystem : IEntitySystem
    {
        /// <summary>
        /// Sets the stack count of this entity, if it has a <see cref="StackComponent"/>.
        /// </summary>
        /// <param name="uid">Entity to modify.</param>
        /// <param name="amount">New stack amount. Must be greater than zero.</param>
        void SetCount(EntityUid uid, int amount,
            StackComponent? stack = null);

        /// <summary>
        /// Try to use an amount of items on this stack. Returns whether this succeeded.
        /// </summary>
        bool Use(EntityUid uid, int amount,
            StackComponent? stack = null);

        /// <summary>
        /// Returns true if the provided entities can be stacked together.
        /// </summary>
        bool CanStack(EntityUid ent1, EntityUid ent2, 
            StackComponent? stackEnt1 = null, 
            StackComponent? stackEnt2 = null);

        /// <summary>
        /// Tries to stack the provided entities.
        /// </summary>
        /// <param name="target">Entity to stack into.</param>
        /// <param name="with">Entity to stack with. This entity will be deleted if the stack succeeds.</param>
        /// <returns>True if the stack succeeded.</returns>
        bool TryStack(EntityUid target, EntityUid with,
            StackComponent? stackTarget = null,
            StackComponent? stackWith = null);

        /// <summary>
        /// If this entity is in the map, tries to stack this entity with all entities on the same tile. 
        /// If this entity is not in the map, tries to stack it with all other entities in the entity's parent.
        /// </summary>
        /// <param name="target">Entity to stack.</param>
        /// <param name="stackTarget">Stackable component of the entity.</param>
        /// <returns>True if any stacking occurred.</returns>
        /// <hsp>item_stack</hsp>
        bool TryStackAtSamePos(EntityUid target,
            SpatialComponent? spatialTarget = null,
            StackComponent? stackTarget = null);

        /// <summary>
        /// Tries to clone this entity.
        /// </summary>
        /// <param name="target">Entity to clone.</param>
        /// <param name="spawnPosition">Position to spawn the newly cloned entity.</param>
        /// <returns>A non-null <see cref="EntityUid"/> if successful.</returns>
        EntityUid? Clone(EntityUid target, EntityCoordinates spawnPosition);

        /// <summary>
        /// Tries to split this stack into two.
        /// </summary>
        /// <returns>A non-null <see cref="EntityUid"/> if successful.</returns>
        EntityUid? Split(EntityUid uid, int amount, EntityCoordinates spawnPosition, 
            StackComponent? stack = null);
    }

    public class StackSystem : EntitySystem, IStackSystem
    {
        [Dependency] private readonly IEntityLookup _lookup = default!;
        [Dependency] private readonly ISerializationManager _serializationManager = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<StackComponent, MapInitEvent>(HandleEntityInitialized, nameof(HandleEntityInitialized));
            SubscribeLocalEvent<StackComponent, StackCountChangedEvent>(HandleStackCountChanged, nameof(HandleStackCountChanged));

            SubscribeLocalEvent<SpatialComponent, EntityClonedEventArgs>(HandleCloneSpatial, nameof(HandleCloneSpatial));
            SubscribeLocalEvent<MetaDataComponent, EntityClonedEventArgs>(HandleCloneMetaData, nameof(HandleCloneMetaData));
            SubscribeLocalEvent<StackComponent, EntityClonedEventArgs>(HandleCloneStack, nameof(HandleCloneStack));
        }

        private void HandleEntityInitialized(EntityUid uid, StackComponent stackable, ref MapInitEvent args)
        {
            MetaDataComponent? metaData = null;

            if (!Resolve(uid, ref metaData))
                return;

            if (stackable.Count <= 0)
            {
                stackable.Count = 0;
                metaData.Liveness = EntityGameLiveness.DeadAndBuried;
            }
        }

        /// <summary>
        /// Check the new stack count and kill the entity if it's below zero.
        /// </summary>
        private void HandleStackCountChanged(EntityUid uid, StackComponent stackable, StackCountChangedEvent args)
        {
            MetaDataComponent? metaData = null;

            if (!Resolve(uid, ref metaData))
                return;

            // Kill entity if stack count becomes less than zero.
            if (args.NewCount <= 0)
            {
                metaData.Liveness = EntityGameLiveness.DeadAndBuried;
            }
            else if (args.OldCount <= 0 && args.NewCount > 0)
            {
                metaData.Liveness = EntityGameLiveness.Alive;
            }
        }

        #region Count Modification

        public void SetCount(EntityUid uid, int amount, StackComponent? stack = null)
        {
            if (!Resolve(uid, ref stack))
                return;

            // Do nothing if amount is already the same.
            if (amount == stack.Count)
                return;

            // Store old value for event-raising purposes...
            var old = stack.Count;

            // Clamp the value.
            amount = Math.Max(amount, 0);

            stack.Count = amount;

            RaiseLocalEvent(uid, new StackCountChangedEvent(old, stack.Count), false);
        }

        /// <inheritdoc/>
        public bool Use(EntityUid uid, int amount, StackComponent? stack = null)
        {
            if (!Resolve(uid, ref stack))
                return false;

            // Check if we have enough things in the stack for this...
            if (stack.Count < amount)
            {
                // Not enough things in the stack, return false.
                return false;
            }

            // We do have enough things in the stack, so remove them and change.
            if (!stack.Unlimited)
            {
                SetCount(uid, stack.Count - amount, stack);
            }

            return true;
        }

        #endregion

        #region Stacking

        /// <inheritdoc/>
        public bool CanStack(EntityUid ent1, EntityUid ent2,
            StackComponent? stackEnt1 = null,
            StackComponent? stackEnt2 = null)
        {
            if (!EntityManager.IsAlive(ent1) || !EntityManager.IsAlive(ent2))
                return false;

            // Both entities must at least have StackComponents.
            if (!Resolve(ent1, ref stackEnt1) || !Resolve(ent2, ref stackEnt2))
                return false;

            foreach (var comp1 in EntityManager.GetComponents(ent1))
            {
                var compType = comp1.GetType();

                if (!EntityManager.TryGetComponent(ent2, compType, out var comp2))
                {
                    return false;
                }

                if (!_serializationManager.Compare(comp1, comp2))
                {
                    return false;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public bool TryStack(EntityUid target, EntityUid with,
            StackComponent? stackTarget = null,
            StackComponent? stackWith = null)
        {
            if (!Resolve(target, ref stackTarget) || !Resolve(with, ref stackWith))
                return false;

            if (!CanStack(target, with, stackTarget, stackWith))
                return false;

            DebugTools.Assert(stackWith.Count > 0);

            var ev = new EntityStackedEvent(with);
            RaiseLocalEvent(target, ref ev);

            stackTarget.Count += stackWith.Count;
            stackWith.Count = 0;
            EntityManager.DeleteEntity(with);

            return true;
        }

        /// <inheritdoc/>
        public bool TryStackAtSamePos(EntityUid target,
            SpatialComponent? spatialTarget = null,
            StackComponent? stackTarget = null)
        {
            if (!Resolve(target, ref spatialTarget, ref stackTarget))
                return false;

            if (spatialTarget.Parent == null)
                return false;

            IEnumerable<Entity> ents;

            if (EntityManager.HasComponent<MapComponent>(spatialTarget.Parent.OwnerUid))
            {
                DebugTools.Assert(spatialTarget.Parent.Parent == null, "Map entity should be the root entity.");
                var coords = spatialTarget.Coordinates;
                ents = _lookup.GetLiveEntitiesAtCoords(coords);
            }
            else
            {
                ents = _lookup.GetEntitiesDirectlyIn(spatialTarget.Parent.Owner);
            }

            var stackedSomething = false;

            foreach (var ent in ents)
            {
                stackedSomething &= TryStack(target, ent.Uid, stackTarget);
            }

            return stackedSomething;
        }

        #endregion

        #region Cloning

        private void CopyComponents(EntityUid target, EntityUid source, EntityClonedEventArgs args)
        {
            foreach (var sourceComp in EntityManager.GetComponents(source))
            {
                var compType = sourceComp.GetType();
                if (!args.HandledTypes.Contains(compType))
                {
                    var targetComp = EntityManager.EnsureComponent(target, compType);

                    DebugTools.Assert(sourceComp.GetType() == targetComp.GetType());

                    _serializationManager.Copy(sourceComp, targetComp);
                }
            }
        }
        
        /// <inheritdoc/>
        public EntityUid? Clone(EntityUid target, EntityCoordinates spawnPosition)
        {
            var newEntity = EntityManager.SpawnEntity(null, spawnPosition).Uid;

            var args = new EntityClonedEventArgs(newEntity);
            RaiseLocalEvent(target, args);

            CopyComponents(newEntity, target, args);

            return newEntity;
        }

        private void HandleCloneMetaData(EntityUid source, MetaDataComponent spatial, EntityClonedEventArgs args)
        {
            var newSpatial = EntityManager.EnsureComponent<MetaDataComponent>(args.NewEntity);

            newSpatial.EntityPrototype = spatial.EntityPrototype;
            newSpatial.Liveness = spatial.Liveness;

            args.Handle<MetaDataComponent>();
        }

        private void HandleCloneSpatial(EntityUid source, SpatialComponent spatial, EntityClonedEventArgs args)
        {
            var newSpatial = EntityManager.EnsureComponent<SpatialComponent>(args.NewEntity);

            newSpatial.IsSolid = spatial.IsSolid;
            newSpatial.IsOpaque = spatial.IsOpaque;

            args.Handle<SpatialComponent>();
        }

        private void HandleCloneStack(EntityUid source, StackComponent spatial, EntityClonedEventArgs args)
        {
            // Don't copy anything.
            args.Handle<StackComponent>();
        }

        #endregion

        #region Splitting

        /// <inheritdoc/>
        public EntityUid? Split(EntityUid uid, int amount, EntityCoordinates spawnPosition, StackComponent? stack = null)
        {
            if (!Resolve(uid, ref stack))
                return default;

            // Get a prototype ID to spawn the new entity. Null is also valid, although it should rarely be picked...
            PrototypeId<EntityPrototype>? prototype = null;
            if (EntityManager.TryGetComponent(uid, out MetaDataComponent metaData))
                prototype = metaData.EntityPrototype?.GetStrongID();

            // Try to remove the amount of things we want to split from the original stack...
            if (!Use(uid, amount, stack))
                return null;

            // Try to clone the entity.
            var newEntity = Clone(uid, spawnPosition);
            if (newEntity == null)
                return null;

            if (EntityManager.TryGetComponent(newEntity.Value, out StackComponent? stackComp))
            {
                // Set the split stack's count.
                SetCount(newEntity.Value, amount, stackComp);
                // Don't let people dupe unlimited stacks
                stackComp.Unlimited = false;
            }

            return newEntity;
        }

        #endregion
    }

    [EventArgsUsage(EventArgsTargets.ByValue)]
    public class EntityClonedEventArgs
    {
        public EntityUid NewEntity { get; }

        public HashSet<Type> HandledTypes { get; } = new();

        public EntityClonedEventArgs(EntityUid newEntity)
        {
            NewEntity = newEntity;
        }

        public void Handle<T>() where T: IComponent
        {
            HandledTypes.Add(ComponentTypeCache<T>.Type);
        }
    }

    [EventArgsUsage(EventArgsTargets.ByRef)]
    public struct EntityStackedEvent
    {
        public EntityUid StackedWith { get; }

        public EntityStackedEvent(EntityUid stackingWith)
        {
            StackedWith = stackingWith;
        }
    }

    /// <summary>
    ///     Event raised when a stack's count has changed.
    /// </summary>
    [EventArgsUsage(EventArgsTargets.ByValue)]
    public class StackCountChangedEvent : EntityEventArgs
    {
        /// <summary>
        ///     The old stack count.
        /// </summary>
        public int OldCount { get; }

        /// <summary>
        ///     The new stack count.
        /// </summary>
        public int NewCount { get; }

        public StackCountChangedEvent(int oldCount, int newCount)
        {
            OldCount = oldCount;
            NewCount = newCount;
        }
    }
}
