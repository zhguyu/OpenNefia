﻿using OpenNefia.Content.Inventory;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Utility;

namespace OpenNefia.Content.GameObjects.EntitySystems
{
    public interface IItemDescriptionSystem
    {
        void GetItemDescription(EntityUid entity, IList<ItemDescriptionEntry> entries);
    }

    public class ItemDescriptionSystem : EntitySystem, IItemDescriptionSystem
    {
        public override void Initialize()
        {
            SubscribeLocalEvent<ItemComponent, GetItemDescriptionEventArgs>(GetDescItem, nameof(GetDescItem));
            SubscribeLocalEvent<ItemDescriptionComponent, GetItemDescriptionEventArgs>(GetDescItemDesc, nameof(GetDescItemDesc));
        }

        private void GetDescItem(EntityUid uid, ItemComponent item, GetItemDescriptionEventArgs args)
        {
            if (item.Material != null)
            {
                var entry = new ItemDescriptionEntry()
                {
                    Text = $"It is made of {item.Material}"
                };
                args.Entries.Add(entry);
            }
        }

        private void GetDescItemDesc(EntityUid uid, ItemDescriptionComponent itemDesc, GetItemDescriptionEventArgs args)
        {
            args.Entries.AddRange(itemDesc.Extra);
        }

        public void GetItemDescription(EntityUid entity, IList<ItemDescriptionEntry> entries)
        {
            var ev = new GetItemDescriptionEventArgs(entries);
            RaiseLocalEvent(entity, ev);
         
            if (entries.Count == 0)
            {
                entries.Add(new ItemDescriptionEntry() { Text = "There is no information about this object." });
            }
        }
    }

    public class GetItemDescriptionEventArgs : EntityEventArgs
    {
        public IList<ItemDescriptionEntry> Entries { get; }

        public GetItemDescriptionEventArgs(IList<ItemDescriptionEntry> entries)
        {
            Entries = entries;
        }
    }
}