﻿using Love;
using OpenNefia.Content.EntityGen;
using OpenNefia.Content.Hud;
using OpenNefia.Content.Logic;
using OpenNefia.Content.TurnOrder;
using OpenNefia.Content.Weight;
using OpenNefia.Core.Containers;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Locale;
using OpenNefia.Core.Maps;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpenNefia.Content.Prototypes.Protos;
using OpenNefia.Content.Mount;
using OpenNefia.Core.Game;

namespace OpenNefia.Content.Inventory
{
    public interface IInventorySystem : IEntitySystem
    {
        IEnumerable<EntityUid> EnumerateLiveItems(EntityUid entity, InventoryComponent? inv = null);
        
        IEnumerable<TComp> EntityQueryInInventory<TComp>(EntityUid entity, bool includeDead = false, InventoryComponent? inv = null) 
            where TComp : IComponent;

        int GetItemWeight(EntityUid item, WeightComponent? weight = null);

        int GetTotalInventoryWeight(EntityUid ent, InventoryComponent? inv = null);
        int? GetMaxInventoryWeight(EntityUid ent, InventoryComponent? inv = null);
        bool TryGetInventoryContainer(EntityUid ent, [NotNullWhen(true)] out IContainer? inv, InventoryComponent? invComp = null);
        bool IsInventoryFull(EntityUid ent, InventoryComponent? inv = null);
    }

    /// <summary>
    /// Handles character items.
    /// </summary>
    public sealed class InventorySystem : EntitySystem, IInventorySystem
    {
        [Dependency] private readonly IMessagesManager _mes = default!;
        [Dependency] private readonly IMountSystem _mounts = default!;
        [Dependency] private readonly IGameSessionManager _gameSession = default!;

        public override void Initialize()
        {
            SubscribeComponent<InventoryComponent, GetStatusIndicatorsEvent>(AddStatusIndicator);
            SubscribeComponent<InventoryComponent, BeforeMoveEventArgs>(ProcMovementPreventionOnBurden);
            SubscribeComponent<InventoryComponent, EntityRefreshSpeedEvent>(HandleRefreshSpeed, priority: EventPriorities.VeryHigh);
        }

        private void AddStatusIndicator(EntityUid uid, InventoryComponent inv, GetStatusIndicatorsEvent args)
        {
            if (inv.BurdenType > BurdenType.None)
            {
                var color = new Color(0, Math.Min((int)inv.BurdenType * 40, 255), Math.Min((int)inv.BurdenType * 40, 255), 255);
                args.OutIndicators.Add(new()
                {
                    Text = Loc.GetString($"Elona.Inventory.Burden.Indicator.{inv.BurdenType}"),
                    Color = color
                });
            }
        }

        private void HandleRefreshSpeed(EntityUid uid, InventoryComponent component, ref EntityRefreshSpeedEvent args)
        {
            if (!_gameSession.IsPlayer(uid))
                return;

            if (component.BurdenType >= BurdenType.Heavy)
                args.OutSpeedModifier -= 0.5f;
            if (component.BurdenType >= BurdenType.Moderate)
                args.OutSpeedModifier -= 0.3f;
            if (component.BurdenType >= BurdenType.Light)
                args.OutSpeedModifier -= 0.1f;
        }

        private void ProcMovementPreventionOnBurden(EntityUid uid, InventoryComponent inv, BeforeMoveEventArgs args)
        {
            if (args.Handled)
                return;

            if (inv.BurdenType >= BurdenType.Max)
            {
                _mes.Display(Loc.GetString("Elona.Inventory.Burden.CarryTooMuch"), combineDuplicates: true);
                args.Handle(TurnResult.Failed);
                return;
            }
        }

        public IEnumerable<EntityUid> EnumerateLiveItems(EntityUid entity, InventoryComponent? inv = null)
        {
            if (!Resolve(entity, ref inv))
                return Enumerable.Empty<EntityUid>();

            return inv.Container.ContainedEntities
                .Where(x => EntityManager.IsAlive(x));
        }

        public int GetItemWeight(EntityUid item, WeightComponent? weight = null)
        {
            if (!Resolve(item, ref weight))
                return 0;

            // TODO sum container item weights here too.

            return weight.Weight;
        }

        public int GetTotalInventoryWeight(EntityUid ent, InventoryComponent? inv = null)
        {
            if (!Resolve(ent, ref inv))
                return 0;

            return EnumerateLiveItems(ent, inv)
                .Select(item => GetItemWeight(item))
                .Sum();
        }

        public int? GetMaxInventoryWeight(EntityUid ent, InventoryComponent? inv = null)
        {
            if (!Resolve(ent, ref inv))
                return null;

            return inv.MaxWeight;
        }

        public bool TryGetInventoryContainer(EntityUid ent, [NotNullWhen(true)] out IContainer? inv, InventoryComponent? invComp = null)
        {
            if (!Resolve(ent, ref invComp))
            {
                inv = null;
                return false;
            }

            inv = invComp.Container;
            return true;
        }

        public bool IsInventoryFull(EntityUid ent, InventoryComponent? invComp = null)
        {
            if (!Resolve(ent, ref invComp))
                return true;

            if (invComp.MaxItemCount == null)
                return false;

            return invComp.Container.ContainedEntities.Count >= invComp.MaxItemCount;
        }

        public IEnumerable<TComp> EntityQueryInInventory<TComp>(EntityUid entity, bool includeDead = false, InventoryComponent? inv = null)
            where TComp : IComponent
        {
            if (!Resolve(entity, ref inv))
                yield break;

            foreach (var ent in EntityManager.EntityQuery<TComp>())
            {
                if (inv.Container.Contains(ent.Owner)
                    && (includeDead || IsAlive(ent.Owner)))
                {
                    yield return ent;
                }
            }
        }
    }
}