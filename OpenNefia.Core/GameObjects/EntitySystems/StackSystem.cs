﻿using OpenNefia.Core.Containers;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Log;
using OpenNefia.Core.Maps;
using OpenNefia.Core.Serialization.Manager;
using OpenNefia.Core.Utility;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using OpenNefia.Core.GameObjects;

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
    ///    item to the split item. In OpenNefia, an empty entity is spawned, then all
    ///    component data from the source entity is copied to the new entity.
    /// </para>
    /// <para>
    /// 2. Robust uses a "stackType" property to check if two entities can be
    ///    stacked together. If the stackType matches, then all is good. On the other
    ///    hand, Elona's item_stack does the equivalent of a deepcompare on every item
    ///    property to check for stackability, with special exceptions for some properties 
    ///    like quantity and quality. Naturally, this makes things way harder, especially
    ///    considering that some components were never meant to be stacked as part of an item.
    ///    OpenNefia adds a new comparison feature to the serialization manager to check for
    ///    equality of component fields without the need to use interfaces.
    /// </para>
    /// <para>
    /// 3. Robust has an enforced maximum entity count per stack, while Elona lacks one.
    /// </para>
    /// </remarks>
    public interface IStackSystem : IEntitySystem
    {
        int GetCount(EntityUid? uid, StackComponent? stack = null);

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
            ref StackComponent? stackEnt1,
            ref StackComponent? stackEnt2);

        /// <summary>
        /// Returns true if the provided entities can be stacked together.
        /// </summary>
        bool CanStack(EntityUid ent1, EntityUid ent2);

        /// <summary>
        /// Tries to stack the provided entities.
        /// </summary>
        /// <param name="target">Entity to stack into.</param>
        /// <param name="with">Entity to stack with. This entity will be deleted if the stack succeeds.</param>
        /// <param name="showMessage">Whether to show a message to the player if the stack succeeds.</param>
        /// <returns>True if the stack succeeded.</returns>
        bool TryStack(EntityUid target, EntityUid with,
            bool showMessage = false,
            StackComponent? stackTarget = null,
            StackComponent? stackWith = null);

        /// <summary>
        /// If this entity is in a map, tries to stack this entity with all entities on the same tile. 
        /// If this entity is not in a map, tries to stack it with all other entities in the entity's parent,
        /// if it has one.
        /// </summary>
        /// <param name="target">Entity to stack.</param>
        /// <param name="showMessage">Whether to show a message to the player if the stack succeeds.</param>
        /// <returns>True if any stacking occurred.</returns>
        /// <hsp>item_stack</hsp>
        bool TryStackAtSamePos(EntityUid target,
            bool showMessage = false,
            SpatialComponent? spatialTarget = null,
            StackComponent? stackTarget = null);

        /// <summary>
        /// Tries to stack all entities at the given coordinates.
        /// </summary>
        /// <param name="coords">Coordinates to stack at.</param>
        /// <param name="showMessage">Whether to show a message to the player if the stack succeeds.</param>
        /// <returns>True if any stacking occurred.</returns>
        bool TryStackAtPos(MapCoordinates coords, bool showMessage = false);

        /// <summary>
        /// Clones this entity.
        /// </summary>
        /// <param name="target">Entity to clone.</param>
        /// <param name="spawnPosition">Position to spawn the newly cloned entity.</param>
        /// <returns>A non-null <see cref="EntityUid"/> if successful.</returns>
        EntityUid Clone(EntityUid target, EntityCoordinates spawnPosition);

        /// <summary>
        /// Clones this entity.
        /// </summary>
        /// <param name="target">Entity to clone.</param>
        /// <param name="spawnPosition">Position to spawn the newly cloned entity.</param>
        /// <returns>A non-null <see cref="EntityUid"/> if successful.</returns>
        EntityUid? Clone(EntityUid target, MapCoordinates spawnPosition);

        /// <summary>
        /// Tries to split this stack into two.
        /// </summary>
        /// <returns>A non-null <see cref="EntityUid"/> if successful.</returns>
        /// <remarks>
        /// <para>
        /// If the input amount is exactly the same as the entity's stack amount, then
        /// the input entity will be returned unchanged as the split entity.
        /// </para>
        /// <para>
        /// You can also call this method on entities without a <see cref="StackComponent"/>,
        /// as long as <c>amount</c> is 1. In that case, the entity returned will
        /// be the same as the input entity.
        /// </para>
        /// </remarks>
        bool TrySplit(EntityUid uid, int amount, EntityCoordinates spawnPosition,
            out EntityUid split,
            IContainer? container = null,
            StackComponent? stack = null);

        /// <summary>
        /// Tries to split this stack into two.
        /// </summary>
        /// <returns>A non-null <see cref="EntityUid"/> if successful.</returns>
        bool TrySplit(EntityUid uid, int amount, MapCoordinates spawnPosition,
            out EntityUid split,
            StackComponent? stack = null);

        /// <summary>
        /// Tries to split this stack into two.
        /// </summary>
        /// <returns>A non-null <see cref="EntityUid"/> if successful.</returns>
        bool TrySplit(EntityUid uid, int amount, out EntityUid split,
            StackComponent? stack = null);
    }

    public class StackSystem : EntitySystem, IStackSystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IEntityLookup _lookup = default!;
        [Dependency] private readonly ISerializationManager _serializationManager = default!;
        [Dependency] private readonly IContainerSystem _containers = default!;
        [Dependency] private readonly IComponentLocalizer _componentLocalizer = default!;

        public override void Initialize()
        {
            SubscribeComponent<StackComponent, EntityMapInitEvent>(HandleEntityInitialized);
            SubscribeComponent<StackComponent, StackCountChangedEvent>(HandleStackCountChanged);

            SubscribeComponent<SpatialComponent, BeforeEntityClonedEvent>(HandleCloneSpatial);
            SubscribeComponent<MetaDataComponent, BeforeEntityClonedEvent>(HandleCloneMetaData);
            SubscribeComponent<StackComponent, BeforeEntityClonedEvent>(HandleCloneStack);
            SubscribeComponent<StackComponent, AfterEntityClonedEvent>(HandleAfterCloned, priority: EventPriorities.Highest);
        }

        private void HandleEntityInitialized(EntityUid uid, StackComponent stackable, ref EntityMapInitEvent args)
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

        public int GetCount(EntityUid? uid, StackComponent? stack = null)
        {
            if (uid == null)
                return 0;

            if (!Resolve(uid.Value, ref stack))
                return 1;

            return stack.Count;
        }

        public void SetCount(EntityUid uid, int amount, StackComponent? stack = null)
        {
            if (!Resolve(uid, ref stack))
                return;

            // Clamp the value.
            amount = Math.Max(amount, 0);

            // Do nothing if amount is already the same.
            if (amount == stack.Count)
                return;

            // Store old value for event-raising purposes...
            var old = stack.Count;

            stack.Count = amount;

            RaiseEvent(uid, new StackCountChangedEvent(old, stack.Count), false);
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
            [NotNullWhen(true)] ref StackComponent? stackEnt1,
            [NotNullWhen(true)] ref StackComponent? stackEnt2)
        {
            if (!EntityManager.IsAlive(ent1) || !EntityManager.IsAlive(ent2))
                return false;

            if (ent1 == ent2)
                return false;

            // Both entities must at least have StackComponents.
            if (!Resolve(ent1, ref stackEnt1, logMissing: false) || !Resolve(ent2, ref stackEnt2, logMissing: false))
                return false;

            // Check parents/containment.
            var spatial1 = Spatial(ent1);
            var spatial2 = Spatial(ent2);

            if (spatial1.Parent != spatial2.Parent)
                return false;

            var inCont1 = _containers.TryGetContainingContainer(ent1, out var container1);
            var inCont2 = _containers.TryGetContainingContainer(ent2, out var container2);

            if (inCont1 != inCont2)
                return false;

            if (inCont1 && inCont2 && container1!.ID != container2!.ID)
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

            return true;
        }

        public bool CanStack(EntityUid target, EntityUid with)
        {
            StackComponent? stack1 = null;
            StackComponent? stack2 = null;
            return CanStack(target, with, ref stack1, ref stack2);
        }

        /// <inheritdoc/>
        public bool TryStack(EntityUid target, EntityUid with,
            bool showMessage = false,
            StackComponent? stackTarget = null,
            StackComponent? stackWith = null)
        {
            if (!CanStack(target, with, ref stackTarget, ref stackWith))
                return false;

            DebugTools.Assert(stackWith.Count > 0);

            var newCount = stackTarget.Count + stackWith.Count;

            var ev = new EntityStackedEvent(with, stackTarget.Count, newCount, showMessage);
            RaiseEvent(target, ref ev);

            SetCount(target, newCount, stackTarget);
            SetCount(with, 0, stackWith);
            EntityManager.DeleteEntity(with);

            return true;
        }

        /// <inheritdoc/>
        public bool TryStackAtSamePos(EntityUid target,
            bool showMessage = false,
            SpatialComponent? spatialTarget = null,
            StackComponent? stackTarget = null)
        {
            if (!Resolve(target, ref spatialTarget))
                return false;

            if (!Resolve(target, ref stackTarget, logMissing: false))
                return false;

            if (spatialTarget.Parent == null)
                return false;

            IEnumerable<SpatialComponent> spatials;

            if (EntityManager.HasComponent<MapComponent>(spatialTarget.Parent.Owner))
            {
                DebugTools.Assert(spatialTarget.Parent.Parent == null, "Map entity should be the root entity.");
                var coords = spatialTarget.Coordinates;
                spatials = _lookup.GetLiveEntitiesAtCoords(coords);
            }
            else
            {
                spatials = _lookup.GetEntitiesDirectlyIn(spatialTarget.Parent.Owner);
            }

            var stackedSomething = false;

            foreach (var spatial in spatials.ToList())
            {
                stackedSomething &= TryStack(target, spatial.Owner, showMessage, stackTarget);
            }

            return stackedSomething;
        }

        /// <inheritdoc/>
        public bool TryStackAtPos(MapCoordinates coords, bool showMessage = false)
        {
            var stackedSomething = false;

            foreach (var spatial in _lookup.GetLiveEntitiesAtCoords(coords).ToList())
            {
                if (!EntityManager.IsAlive(spatial.Owner))
                    continue;

                stackedSomething &= TryStackAtSamePos(spatial.Owner, showMessage);
            }

            return stackedSomething;
        }

        #endregion

        #region Cloning

        private void CopyComponents(EntityUid target, EntityUid source, BeforeEntityClonedEvent args)
        {
            var sourceComponents = EntityManager.GetComponents(source);
            foreach (var sourceComp in sourceComponents)
            {
                var compType = sourceComp.GetType();
                if (!args.HandledTypes.Contains(compType))
                {
                    var targetComp = EntityManager.EnsureComponent(target, compType);

                    DebugTools.Assert(sourceComp.GetType() == targetComp.GetType());

                    _serializationManager.Copy(sourceComp, ref targetComp);
                }
            }
        }

        /// <inheritdoc/>
        public EntityUid Clone(EntityUid target, EntityCoordinates spawnPosition)
        {
            var newEntity = EntityManager.SpawnEntity(null, spawnPosition);

            var args = new BeforeEntityClonedEvent(newEntity);
            RaiseEvent(target, args);

            CopyComponents(newEntity, target, args);

            var ev = new AfterEntityClonedEvent(target);
            RaiseEvent(newEntity, ev);

            return newEntity;
        }

        /// <inheritdoc/>
        public EntityUid? Clone(EntityUid target, MapCoordinates spawnPosition)
        {
            if (!spawnPosition.TryToEntity(_mapManager, out var entityPosition))
                return null;
            return Clone(target, entityPosition);
        }

        private void HandleCloneMetaData(EntityUid source, MetaDataComponent spatial, BeforeEntityClonedEvent args)
        {
            var newSpatial = EntityManager.EnsureComponent<MetaDataComponent>(args.NewEntity);

            newSpatial.EntityPrototype = spatial.EntityPrototype;

            args.MarkAsCloned<MetaDataComponent>();
        }

        private void HandleCloneSpatial(EntityUid source, SpatialComponent spatial, BeforeEntityClonedEvent args)
        {
            var newSpatial = EntityManager.EnsureComponent<SpatialComponent>(args.NewEntity);

            newSpatial.IsSolid = spatial.IsSolid;
            newSpatial.IsOpaque = spatial.IsOpaque;

            args.MarkAsCloned<SpatialComponent>();
        }

        private void HandleCloneStack(EntityUid source, StackComponent stack, BeforeEntityClonedEvent args)
        {
            var newStack = EntityManager.EnsureComponent<StackComponent>(args.NewEntity);

            newStack.Count = stack.Count;
            newStack.Unlimited = stack.Unlimited;

            args.MarkAsCloned<StackComponent>();
        }

        private void HandleAfterCloned(EntityUid uid, StackComponent component, AfterEntityClonedEvent args)
        {
            _componentLocalizer.LocalizeComponents(uid);
        }

        #endregion

        #region Splitting

        /// <inheritdoc/>
        public bool TrySplit(EntityUid uid, int amount, EntityCoordinates spawnPosition, out EntityUid split, IContainer? container = null, StackComponent? stack = null)
        {
            split = EntityUid.Invalid;

            if (amount <= 0)
                return false;

            if (!Resolve(uid, ref stack, logMissing: false))
            {
                // Special case: If this entity doesn't support stacking, but we're only
                // requesting a stack size of 1, just return the entity itself.
                if (amount == 1)
                {
                    split = uid;
                    return true;
                }

                Logger.ErrorS("resolve", $"Can't resolve \"{typeof(StackComponent)}\" on entity {uid}!\n{new StackTrace(1, true)}");
                return false;
            }

            // Special case: If the requested amount is the same as the input count, 
            // just return the entity.
            if (stack.Count == amount)
            {
                split = uid;
                return true;
            }

            // Try to remove the amount of things we want to split from the original stack...
            if (!Use(uid, amount, stack))
                return false;

            // Try to clone the entity.
            split = Clone(uid, spawnPosition);

            if (EntityManager.TryGetComponent(split, out StackComponent? stackComp))
            {
                // Set the split stack's count.
                SetCount(split, amount, stackComp);
                // Don't let people dupe unlimited stacks
                stackComp.Unlimited = false;
            }

            var ev = new EntitySplitEvent(split);
            RaiseEvent(uid, ref ev);

            if (container != null && !container.Insert(split))
            {
                Logger.ErrorS("stack", $"Could not insert split entity {split} into container {container.ID}");
                EntityManager.DeleteEntity(split);
                split = EntityUid.Invalid;
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public bool TrySplit(EntityUid uid, int amount, MapCoordinates spawnPosition, out EntityUid split, StackComponent? stack = null)
        {
            split = EntityUid.Invalid;

            if (!spawnPosition.TryToEntity(_mapManager, out var entityCoords))
                return false;

            return TrySplit(uid, amount, entityCoords, out split, null, stack);
        }

        /// <inheritdoc/>
        public bool TrySplit(EntityUid uid, int amount, out EntityUid split, StackComponent? stack = null)
        {
            split = EntityUid.Invalid;

            if (!EntityManager.TryGetComponent<SpatialComponent>(uid, out var spatial))
                return false;

            _containers.TryGetContainingContainer(uid, out IContainer? container);

            return TrySplit(uid, amount, spatial.Coordinates, out split, container, stack);
        }

        #endregion
    }

    /// <summary>
    /// Raised when this entity is cloned, for example when an entity with
    /// a <see cref="StackComponent"/> is split off. Use this event to
    /// run custom deep copying logic per component.
    /// </summary>
    public sealed class BeforeEntityClonedEvent : EntityEventArgs
    {
        /// <summary>
        /// The UID of the newly created entity.
        /// </summary>
        public EntityUid NewEntity { get; }

        /// <summary>
        /// Component types that this event has modified. By adding
        /// a type to this set, the cloning system is instructed not to
        /// attempt to deepcopy the component after the other event
        /// handlers have finished running.
        /// </summary>
        public HashSet<Type> HandledTypes { get; } = new();

        public BeforeEntityClonedEvent(EntityUid newEntity)
        {
            NewEntity = newEntity;
        }

        /// <summary>
        /// Marks this component type as being fully ready for use 
        /// on this entity.
        /// </summary>
        /// <typeparam name="T">Type of component that the calling event handler added/set up.</typeparam>
        public void MarkAsCloned<T>() where T : IComponent
        {
            HandledTypes.Add(typeof(T));
        }
    }

    /// <summary>
    /// Raised on a cloned entity after all its components have been finalized.
    /// </summary>
    public sealed class AfterEntityClonedEvent : EntityEventArgs
    {
        /// <summary>
        /// The UID of the entity this entity was cloned from.
        /// </summary>
        public EntityUid ClonedFrom { get; }

        public AfterEntityClonedEvent(EntityUid clonedFrom)
        {
            ClonedFrom = clonedFrom;
        }
    }

    /// <summary>
    /// Raised when this entity has been successfully stacked with another
    /// entity, but before their stack counts have been updated.
    /// </summary>
    [ByRefEvent]
    public struct EntityStackedEvent
    {
        public EntityUid StackedWith { get; }
        public int OldCount { get; }
        public int NewCount { get; }
        public bool ShowMessage { get; }

        public EntityStackedEvent(EntityUid stackedWith, int oldCount, int newCount, bool showMessage)
        {
            StackedWith = stackedWith;
            OldCount = oldCount;
            NewCount = newCount;
            ShowMessage = showMessage;
        }
    }

    /// <summary>
    /// Raised after this entity has been successfully split into two stacks.
    /// </summary>
    [ByRefEvent]
    public struct EntitySplitEvent
    {
        /// <summary>
        /// The newly created entity that has been split off from 
        /// this entity.
        /// </summary>
        public EntityUid SplitInto { get; }

        public EntitySplitEvent(EntityUid splitInto)
        {
            SplitInto = splitInto;
        }
    }

    /// <summary>
    ///     Event raised when a stack's count has changed.
    /// </summary>
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
