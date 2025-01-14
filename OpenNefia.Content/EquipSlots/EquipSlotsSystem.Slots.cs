﻿using OpenNefia.Content.Inventory;
using OpenNefia.Core.Areas;
using OpenNefia.Core.Containers;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Log;
using OpenNefia.Core.Prototypes;
using System.Diagnostics.CodeAnalysis;
using static OpenNefia.Content.Prototypes.Protos;
using EquipSlotPrototypeId = OpenNefia.Core.Prototypes.PrototypeId<OpenNefia.Content.EquipSlots.EquipSlotPrototype>;

namespace OpenNefia.Content.EquipSlots
{
    public sealed partial class EquipSlotsSystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IContainerSystem _containerSys = default!;

        // That's one beefy mutant.
        private const int MaxContainerSlotsPerEquipSlot = 1024;

        /// <inheritdoc/>
        public void InitializeEquipSlots(EntityUid uid, IEnumerable<EquipSlotPrototypeId> equipSlotProtoIds,
            EquipSlotsComponent? equipSlotsComp = null,
            ContainerManagerComponent? containerComp = null)
        {
            if (!Resolve(uid, ref equipSlotsComp, logMissing: false))
                equipSlotsComp = EntityManager.AddComponent<EquipSlotsComponent>(uid);
            if (!Resolve(uid, ref containerComp, logMissing: false))
                containerComp = EntityManager.AddComponent<ContainerManagerComponent>(uid);

            ClearEquipSlots(uid, equipSlotsComp, containerComp);

            foreach (var slotId in equipSlotProtoIds)
            {
                TryAddEquipSlot(uid, slotId, out _, out _, equipSlotsComp, containerComp);
            }
        }

        /// <inheritdoc/>
        public bool TryAddEquipSlot(EntityUid uid, EquipSlotPrototypeId slotId,
            [NotNullWhen(true)] out EquipSlotInstance? equipSlotInstance, [NotNullWhen(true)] out ContainerSlot? containerSlot,
            EquipSlotsComponent? equipSlotsComp = null,
            ContainerManagerComponent? containerComp = null)
        {
            containerSlot = null;
            equipSlotInstance = null;
            if (!Resolve(uid, ref equipSlotsComp, ref containerComp))
                return false;

            if (!_prototypeManager.HasIndex(slotId))
                return false;

            var containerId = FindFreeContainerIdForSlot(uid, slotId, equipSlotsComp, containerComp);
            if (containerId == null)
                return false;

            equipSlotInstance = new EquipSlotInstance(slotId, containerId.Value);
            equipSlotsComp.EquipSlots.Add(equipSlotInstance);

            containerSlot = _containerSys.MakeContainer<ContainerSlot>(uid, equipSlotInstance.ContainerID, containerManager: containerComp);
            containerSlot.OccludesLight = false;

            return true;
        }

        /// <inheritdoc/>
        public bool TryRemoveEquipSlot(EntityUid uid, EquipSlotInstance instance,
            EquipSlotsComponent? equipSlotsComp = null,
            ContainerManagerComponent? containerComp = null)
        {
            if (!Resolve(uid, ref equipSlotsComp, ref containerComp))
                return false;

            if (!equipSlotsComp.EquipSlots.Contains(instance))
            {
                Logger.WarningS("equip", $"Tried to remove equip slot {instance}, but it wasn't owned by entity {uid}!");
                return false;
            }

            if (_containerSys.TryGetContainer(uid, instance.ContainerID, out var container))
            {
                // Wipe the equipment item in this slot.
                _containerSys.CleanContainer(container);
                container.Shutdown();
            }
            else
            {
                Logger.WarningS("equip", $"Tried to remove equip slot {instance} from entity {uid}, but its container was not found.");
            }

            equipSlotsComp.EquipSlots.Remove(instance);
            return true;
        }

        /// <inheritdoc/>
        public void ClearEquipSlots(EntityUid uid,
            EquipSlotsComponent? equipSlotsComp = null,
            ContainerManagerComponent? containerComp = null)
        {
            if (!Resolve(uid, ref equipSlotsComp, ref containerComp))
                return;

            foreach (var slot in GetEquipSlots(uid).ToList())
            {
                TryRemoveEquipSlot(uid, slot, equipSlotsComp, containerComp);
            }

            // In case any errors occurred.
            equipSlotsComp.EquipSlots.Clear();
        }

        private static ContainerId MakeContainerId(EquipSlotPrototypeId slotId, int index)
        {
            return new($"Elona.EquipSlot:{slotId}:{index}");
        }

        private ContainerId? FindFreeContainerIdForSlot(EntityUid uid, EquipSlotPrototypeId slotId,
            EquipSlotsComponent? equipSlotsComp = null,
            ContainerManagerComponent? containerComp = null)
        {
            if (!Resolve(uid, ref equipSlotsComp, ref containerComp))
                return null;

            var index = 0;

            while (index < MaxContainerSlotsPerEquipSlot)
            {
                var containerId = MakeContainerId(slotId, index);

                if (!_containerSys.HasContainer(uid, containerId, containerComp))
                {
                    return containerId;
                }

                index++;
            }

            Logger.WarningS("equip", $"Could not find free container ID for equipment slot {slotId} (entity {uid}) after {index} tries!");

            return null;
        }

        /// <inheritdoc/>
        public bool TryGetContainerForEquipSlot(EntityUid uid, EquipSlotInstance equipSlotInstance,
            [NotNullWhen(true)] out ContainerSlot? containerSlot,
            EquipSlotsComponent? equipSlotsComp = null,
            ContainerManagerComponent? containerComp = null)
        {
            containerSlot = null;
            if (!Resolve(uid, ref equipSlotsComp, ref containerComp, false))
                return false;

            if (!_containerSys.TryGetContainer(uid, equipSlotInstance.ContainerID, out var container))
            {
                Logger.WarningS("equip", $"EquipSlot declared container ID {equipSlotInstance.ContainerID}, but it wasn't allocated yet.");
                containerSlot = _containerSys.MakeContainer<ContainerSlot>(uid, equipSlotInstance.ContainerID, containerManager: containerComp);
                containerSlot.OccludesLight = false;
                return true;
            }

            if (container is not ContainerSlot containerSlotChecked) return false;

            containerSlot = containerSlotChecked;
            return true;
        }

        /// <inheritdoc/>
        public bool TryGetEquipSlotForContainer(EntityUid uid, ContainerId containerId,
            [NotNullWhen(true)] out EquipSlotInstance? equipSlotInstance,
            EquipSlotsComponent? equipSlotsComp = null,
            ContainerManagerComponent? containerComp = null)
        {
            equipSlotInstance = null;
            if (!Resolve(uid, ref equipSlotsComp, ref containerComp, false))
                return false;

            equipSlotInstance = equipSlotsComp.EquipSlots.FirstOrDefault(x => x.ContainerID == containerId);
            return equipSlotInstance != default;
        }

        /// <inheritdoc/>
        public bool HasEquipSlot(EntityUid uid, EquipSlotPrototypeId slotId, EquipSlotsComponent? equipSlotsComp = null) =>
            TryGetEquipSlot(uid, slotId, out _, equipSlotsComp);


        /// <inheritdoc/>
        public bool TryGetEquipSlot(EntityUid uid, EquipSlotPrototypeId slotId, [NotNullWhen(true)] out EquipSlotInstance? equipSlotInstance,
            EquipSlotsComponent? equipSlotsComp = null)
        {
            equipSlotInstance = null;
            if (!Resolve(uid, ref equipSlotsComp, false))
                return false;

            if (!_prototypeManager.HasIndex(slotId))
                return false;

            equipSlotInstance = equipSlotsComp.EquipSlots.FirstOrDefault(x => x.ID == slotId);
            return equipSlotInstance != default;
        }

        /// <inheritdoc/>
        public bool HasEmptyEquipSlot(EntityUid uid, EquipSlotPrototypeId slotId, EquipSlotsComponent? equipSlotsComp = null) =>
            TryGetEmptyEquipSlot(uid, slotId, out _, equipSlotsComp);


        /// <inheritdoc/>
        public bool TryGetEmptyEquipSlot(EntityUid uid, EquipSlotPrototypeId slotId, [NotNullWhen(true)] out EquipSlotInstance? equipSlotInstance,
            EquipSlotsComponent? equipSlotsComp = null)
        {
            equipSlotInstance = null;
            if (!Resolve(uid, ref equipSlotsComp, false))
                return false;

            if (!_prototypeManager.HasIndex(slotId))
                return false;

            bool IsEmptyEquipSlotWithType(EquipSlotInstance equipSlot, EquipSlotPrototypeId slotId)
            {
                if (equipSlot.ID != slotId)
                    return false;

                if (!TryGetContainerForEquipSlot(uid, equipSlot, out var containerSlot))
                    return false;

                return containerSlot.ContainedEntity == null;
            }

            equipSlotInstance = equipSlotsComp.EquipSlots.FirstOrDefault(x => IsEmptyEquipSlotWithType(x, slotId));
            return equipSlotInstance != default;
        }

        /// <inheritdoc/>
        public bool TryGetContainerSlotEnumerator(EntityUid uid, out ContainerSlotEnumerator containerSlotEnumerator,
            EquipSlotsComponent? equipSlotsComp = null)
        {
            containerSlotEnumerator = default;
            if (!Resolve(uid, ref equipSlotsComp, false))
                return false;

            containerSlotEnumerator = new ContainerSlotEnumerator(uid, equipSlotsComp.EquipSlots, this);
            return true;
        }

        /// <inheritdoc/>
        public bool TryGetEquipSlots(EntityUid uid, [NotNullWhen(true)] out IList<EquipSlotInstance>? slotDefinitions,
            EquipSlotsComponent? equipSlotsComp = null)
        {
            slotDefinitions = null;
            if (!Resolve(uid, ref equipSlotsComp, false))
                return false;

            slotDefinitions = equipSlotsComp.EquipSlots;
            return true;
        }

        /// <inheritdoc/>
        public IList<EquipSlotInstance> GetEquipSlots(EntityUid uid, EquipSlotsComponent? equipSlotsComp = null)
        {
            if (!Resolve(uid, ref equipSlotsComp))
                return new List<EquipSlotInstance>();

            return equipSlotsComp.EquipSlots;
        }

        /// <inheritdoc/>
        public IEnumerable<EntityUid> EnumerateEquippedEntities(EntityUid uid, EquipSlotsComponent? equipSlotsComp = null)
        {
            foreach (var equipSlot in GetEquipSlots(uid, equipSlotsComp))
            {
                if (!TryGetContainerForEquipSlot(uid, equipSlot, out var containerSlot))
                    continue;

                if (!IsAlive(containerSlot.ContainedEntity))
                    continue;

                yield return containerSlot.ContainedEntity.Value;
            }
        }

        /// <inheritdoc/>
        public bool IsEquippedOnSlotOfType(EntityUid uid, EquipSlotPrototypeId slotId)
        {
            if (!TryComp<SpatialComponent>(uid, out var spatialComp))
                return false;

            foreach (var equipSlot in GetEquipSlots(spatialComp.ParentUid))
            {
                if (equipSlot.ID != slotId)
                    continue;

                if (!TryGetContainerForEquipSlot(spatialComp.ParentUid, equipSlot, out var containerSlot))
                    continue;

                if (containerSlot.ContainedEntity == uid)
                    return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public bool IsEquippedOnAnySlot(EntityUid item) => TryGetSlotEquippedOn(item, out _, out _);

        public bool CanEquipOn(EntityUid uid, PrototypeId<EquipSlotPrototype> slotId)
        {
            return GetEquipSlots(uid).Any(x => x.ID == slotId);
        }

        public bool TryGetSlotEquippedOn(EntityUid item, [NotNullWhen(true)] out EntityUid? owner, [NotNullWhen(true)] out EquipSlotInstance? equipSlotInstance)
        {
            if (!TryComp<SpatialComponent>(item, out var spatialComp))
            {
                owner = null;
                equipSlotInstance = null;
                return false;
            }

            foreach (var equipSlot in GetEquipSlots(spatialComp.ParentUid))
            {
                if (!TryGetContainerForEquipSlot(spatialComp.ParentUid, equipSlot, out var containerSlot))
                    continue;

                if (containerSlot.ContainedEntity == item)
                {
                    owner = spatialComp.ParentUid;
                    equipSlotInstance = equipSlot;
                    return true;
                }
            }

            owner = null;
            equipSlotInstance = null;
            return false;
        }

        public struct ContainerSlotEnumerator
        {
            private readonly EquipSlotsSystem _equipSlotsSystem;
            private readonly EntityUid _uid;
            private readonly IList<EquipSlotInstance> _slots;
            private int _nextIdx = 0;

            public ContainerSlotEnumerator(EntityUid uid, IList<EquipSlotInstance> slots, EquipSlotsSystem inventorySystem)
            {
                _uid = uid;
                _equipSlotsSystem = inventorySystem;
                _slots = slots;
            }

            public bool MoveNext([NotNullWhen(true)] out ContainerSlot? container)
            {
                container = null;
                if (_nextIdx >= _slots.Count) return false;

                while (_nextIdx < _slots.Count && !_equipSlotsSystem.TryGetContainerForEquipSlot(_uid, _slots[_nextIdx++], out container)) { }

                return container != null;
            }
        }
    }
}
