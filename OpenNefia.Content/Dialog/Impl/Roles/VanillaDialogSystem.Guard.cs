﻿using OpenNefia.Content.Currency;
using OpenNefia.Content.Food;
using OpenNefia.Content.Hunger;
using OpenNefia.Content.Prototypes;
using OpenNefia.Content.Roles;
using OpenNefia.Content.UI;
using OpenNefia.Content.Weather;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Locale;
using OpenNefia.Content.Quests;
using OpenNefia.Core;
using OpenNefia.Core.Directions;
using OpenNefia.Core.Serialization.Manager.Attributes;
using OpenNefia.Content.Chests;
using OpenNefia.Core.SaveGames;
using OpenNefia.Content.EntityGen;
using OpenNefia.Content.World;
using OpenNefia.Content.Fame;
using System.Reflection;
using OpenNefia.Core.Reflection;
using OpenNefia.Content.Items;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Game;

namespace OpenNefia.Content.Dialog
{
    public sealed partial class VanillaDialogSystem : EntitySystem
    {
        [Dependency] private readonly IQuestSystem _quests = default!;

        [RegisterSaveData("Elona.VanillaDialogSystem.LostItemsTurnedIn")]
        public int LostItemsTurnedIn { get; set; }

        private void Guard_Initialize()
        {
            SubscribeComponent<RoleGuardComponent, GetDefaultDialogChoicesEvent>(RoleGuard_AddDialogChoices);
            SubscribeBroadcast<MapOnTimePassedEvent>(ResetItemsTurnedIn);
        }

        private void RoleGuard_AddDialogChoices(EntityUid uid, RoleGuardComponent component, GetDefaultDialogChoicesEvent args)
        {
            // Quest client locations
            var spatial = Spatial(uid);
            var clients = _quests.EnumerateAcceptedQuests()
                .SelectMany(q => _quests.EnumerateTargetCharasInMap(spatial.MapID, q.Owner, q).Select(e => (q, e)));

            var seenClients = new HashSet<EntityUid>();
            foreach (var (quest, client) in clients)
            {
                if (seenClients.Contains(client))
                    continue;

                seenClients.Add(client);
                var guardLocateData = new DialogRoleGuardLocateCharaData(client);
                args.OutChoices.Add(new()
                {
                    Text = DialogTextEntry.FromString(Loc.GetString("Elona.Dialog.Guard.Choices.WhereIs", ("entity", client))),
                    NextNode = new(Protos.Dialog.Guard, "WhereIs"),
                    ExtraData = new List<IDialogExtraData>() { guardLocateData }
                });
            }

            // Lost property (wallets/suitcases)
            var seen = new HashSet<PrototypeId<EntityPrototype>>();
            foreach (var lostProperty in _inv.EntityQueryInInventory<LostPropertyComponent>(args.Player))
            {
                if (TryProtoID(lostProperty.Owner, out var protoID) && !seen.Contains(protoID.Value))
                {
                    seen.Add(protoID.Value);

                    var guardLostPropertyData = new DialogRoleGuardLostPropertyData(lostProperty.Owner);
                    args.OutChoices.Add(new()
                    {
                        Text = DialogTextEntry.FromString(Loc.GetString("Elona.Dialog.Guard.Choices.LostProperty", ("item", lostProperty.Owner))),
                        NextNode = new(Protos.Dialog.Guard, "LostProperty"),
                        ExtraData = new List<IDialogExtraData>() { guardLostPropertyData }
                    });
                }
            }
        }

        private void ResetItemsTurnedIn(ref MapOnTimePassedEvent ev)
        {
            if (ev.YearsPassed > 0)
                LostItemsTurnedIn = Math.Max(LostItemsTurnedIn - ev.YearsPassed, 0);
        }

        private string GetLocateCharaText(EntityUid chara, EntityUid player, EntityUid speaker)
        {
            // >>>>>>>> shade2/chat.hsp:2967 	if chatVal>=headChatListClientGuide{ ...
            var root = new LocaleKey("Elona.Dialog.Guard.WhereIs");

            if (!IsAlive(chara))
                return Loc.GetString(root.With("Dead"), ("speaker", speaker));

            var playerSpatial = Spatial(player);
            var charaSpatial = Spatial(chara);

            if (!playerSpatial.MapPosition.TryDirectionTowards(charaSpatial.MapPosition, out var direction)
                || !playerSpatial.MapPosition.TryDistanceTiled(charaSpatial.MapPosition, out var distance))
                return Loc.GetString(root.With("Dead"), ("speaker", speaker));

            var directionName = Loc.GetString($"Elona.Spatial.Directions.{direction}");

            var locArgs = new LocaleArg[] { ("speaker", speaker), ("chara", chara), ("directionName", directionName) };

            if (chara == speaker)
                return Loc.GetString("Elona.Dialog.Common.YouKidding", ("speaker", speaker));
            else if (distance < 6)
                return Loc.GetString(root.With("VeryClose"), locArgs);
            else if (distance < 12)
                return Loc.GetString(root.With("Close"), locArgs);
            else if (distance < 20)
                return Loc.GetString(root.With("Moderate"), locArgs);
            else if (distance < 35)
                return Loc.GetString(root.With("Far"), locArgs);
            else
                return Loc.GetString(root.With("VeryFar"), locArgs);
            // <<<<<<<< shade2/chat.hsp:2983 		} ..
        }

        public QualifiedDialogNode? Guard_WhereIs(IDialogEngine engine, IDialogNode node)
        {
            var locateData = engine.Data.Get<DialogRoleGuardLocateCharaData>();

            var text = GetLocateCharaText(locateData.Chara, engine.Player, engine.Speaker!.Value);

            var texts = new List<DialogTextEntry>()
            {
                DialogTextEntry.FromString(text)
            };
            var nextNodeID = new QualifiedDialogNodeID(Protos.Dialog.Default, "Talk");

            var newNode = new DialogJumpNode(texts, nextNodeID);
            return new QualifiedDialogNode(Protos.Dialog.Guard, newNode);
        }

        public QualifiedDialogNode? Guard_LostProperty(IDialogEngine engine, IDialogNode node)
        {
            var lostPropertyData = engine.Data.Get<DialogRoleGuardLostPropertyData>();

            var empty = !TryComp<ChestComponent>(lostPropertyData.Item, out var chest) || !chest.HasItems;
            _stacks.Use(lostPropertyData.Item, 1);

            if (empty)
            {
                return engine.GetNodeByID(Protos.Dialog.Guard, "LostProperty_Empty");
            }
            else
            {
                return engine.GetNodeByID(Protos.Dialog.Guard, "LostProperty_TurnIn");
            }
        }
    }

    public sealed class ModifyKarmaAction : IDialogAction
    {
        [Dependency] private readonly IKarmaSystem _karma = default!;

        [DataField(required: true)]
        public int Amount { get; set; }

        public void Invoke(IDialogEngine engine, IDialogNode node)
        {
            _karma.ModifyKarma(engine.Player, Amount);
        }
    }

    public enum ModifyFlagOperation
    {
        Set,
        Add
    }

    public sealed class ModifyFlagAction : IDialogAction
    {
        [DataField(required: true)]
        public EntitySystemPropertyRef Property { get; }

        [DataField(required: true)]
        public ModifyFlagOperation Operation { get; }

        [DataField(required: true)]
        public int Value { get; }

        public void Invoke(IDialogEngine engine, IDialogNode node)
        {
            var props = IoCManager.Resolve<IEntitySystemPropertiesManager>();
            var property = props.GetProperty(Property);
            var value = property.GetValue<int>();

            switch (Operation)
            {
                case ModifyFlagOperation.Set:
                default:
                    value = Value;
                    break;
                case ModifyFlagOperation.Add:
                    value += Value;
                    break;
            }
            
            property.SetValue(value);
        }
    }

    public sealed class CheckFlagCondition : IDialogCondition
    {
        [DataField(required: true)]
        public EntitySystemPropertyRef Property { get; }

        public int GetValue(IDialogEngine engine)
        {
            var props = IoCManager.Resolve<IEntitySystemPropertiesManager>();

            return props.GetProperty(Property).GetValue<int>();
        }
    }

    public sealed class CheckGoldCondition : IDialogCondition
    {
        public int GetValue(IDialogEngine engine)
        {
            var entMan = IoCManager.Resolve<IEntityManager>();
            var session = IoCManager.Resolve<IGameSessionManager>();
            if (!entMan.TryGetComponent<MoneyComponent>(session.Player, out var money))
                return 0;
            return money.Gold;
        }
    }

    public sealed class CheckPlatinumCondition : IDialogCondition
    {
        public int GetValue(IDialogEngine engine)
        {
            var entMan = IoCManager.Resolve<IEntityManager>();
            var session = IoCManager.Resolve<IGameSessionManager>();
            if (!entMan.TryGetComponent<MoneyComponent>(session.Player, out var money))
                return 0;
            return money.Platinum;
        }
    }

    public sealed class DialogRoleGuardLocateCharaData : IDialogExtraData
    {
        /// <summary>
        /// Character the player is looking for, like a quest target.
        /// </summary>
        [DataField]
        public EntityUid Chara { get; set; }

        public DialogRoleGuardLocateCharaData() { }
        public DialogRoleGuardLocateCharaData(EntityUid chara)
        {
            Chara = chara;
        }
    }

    public sealed class DialogRoleGuardLostPropertyData : IDialogExtraData
    {
        /// <summary>
        /// Lost item, typically a suitcase or wallet.
        /// </summary>
        [DataField]
        public EntityUid Item { get; set; }

        public DialogRoleGuardLostPropertyData() { }
        public DialogRoleGuardLostPropertyData(EntityUid item)
        {
            Item = item;
        }
    }
}