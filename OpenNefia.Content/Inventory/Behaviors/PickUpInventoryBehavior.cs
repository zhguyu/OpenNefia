﻿using OpenNefia.Content.Pickable;
﻿using OpenNefia.Content.GameObjects;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Locale;
using OpenNefia.Core.UI.Element;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HspIdsInv = OpenNefia.Core.Prototypes.HspIds<OpenNefia.Content.Inventory.InvElonaId>;

namespace OpenNefia.Content.Inventory
{
    public class PickUpInventoryBehavior : BaseInventoryBehavior
    {
        [Dependency] private readonly IVerbSystem _verbSystem = default!;
        [Dependency] private readonly IPickableSystem _pickable = default!;

        public override HspIdsInv HspIds { get; } = HspIdsInv.From122(new(id: 3));

        public override string WindowTitle => Loc.GetString("Elona.Inventory.Behavior.PickUp.WindowTitle");
        public override UiElement MakeIcon() => InventoryHelpers.MakeIcon(InventoryIcon.PickUp);
        public override bool ApplyNameModifiers => false;
        public override TurnResult? TurnResultAfterSelectionIfEmpty => TurnResult.Aborted;

        public override IEnumerable<IInventorySource> GetSources(InventoryContext context)
        {
            yield return new GroundInvSource(context.User);
        }

        public override string GetQueryText(InventoryContext context)
        {
            return Loc.GetString("Elona.Inventory.Behavior.PickUp.QueryText");
        }

        public override bool IsAccepted(InventoryContext context, EntityUid item)
        {
            return _verbSystem.CanUseVerbOn(context.User, item, PickableSystem.VerbTypePickUp);
        }

        public override InventoryResult OnSelect(InventoryContext context, EntityUid item, int amount)
        {
            if (!_pickable.CheckPickableOwnStateAndMessage(item))
                return new InventoryResult.Finished(TurnResult.Failed);

            var result = TurnResult.NoResult;
            if (_verbSystem.TryGetVerb(context.User, item, PickableSystem.VerbTypePickUp, out var verb))
                result = verb.Act();

            // TODO harvest quest
            if (result == TurnResult.Failed || result == TurnResult.Aborted)
                return new InventoryResult.Finished(result);

            return new InventoryResult.Continuing();
        }
    }
}
