﻿using OpenNefia.Content.Resists;
using OpenNefia.Content.Skills;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Prototypes;
using OpenNefia.Content.Prototypes;
using OpenNefia.Content.Logic;
using OpenNefia.Core.Random;
using OpenNefia.Core.Locale;
using OpenNefia.Content.GameObjects;
using OpenNefia.Content.UI.Layer;
using OpenNefia.Content.UI;
using OpenNefia.Core.Audio;
using OpenNefia.Core.GameController;
using OpenNefia.Core.Maths;
using OpenNefia.Core.Game;
using OpenNefia.Core.Rendering;
using OpenNefia.Content.Rendering;
using OpenNefia.Content.Parties;
using OpenNefia.Content.RandomGen;
using OpenNefia.Content.EntityGen;
using OpenNefia.Content.Memory;
using OpenNefia.Content.Activity;
using OpenNefia.Core;
using OpenNefia.Content.Dialog;
using OpenNefia.Content.Effects;
using OpenNefia.Content.World;
using OpenNefia.Content.Maps;
using OpenNefia.Content.Sleep;
using OpenNefia.Content.GameController;
using OpenNefia.Content.Spells;
using OpenNefia.Content.Items;
using OpenNefia.Core.Maps;
using OpenNefia.Content.Effects.New;

namespace OpenNefia.Content.Religion
{
    public interface IReligionSystem : IEntitySystem
    {
        string GetGodName(EntityUid uid, ReligionComponent? religion = null);
        string GetGodName(PrototypeId<GodPrototype>? godID);

        PrototypeId<GodPrototype>? PickRandomGodID(bool includeEyth = true);
        void GodSays(EntityUid believer, string text, ReligionComponent? religion = null);
        void GodSays(PrototypeId<GodPrototype>? godId, string text);
        bool ModifyPiety(EntityUid target, int amount, ReligionComponent? religion = null);
        bool CanOfferItemToGod(PrototypeId<GodPrototype>? godId, EntityUid item);

        void ApplySkillBlessing(EntityUid target, PrototypeId<SkillPrototype> skillId, int coefficient, int add, ReligionComponent? religion = null);
        void ApplyResistBlessing(EntityUid target, PrototypeId<ElementPrototype> elementId, int coefficient, int add, ReligionComponent? religion = null);

        void SwitchReligionWithPenalty(EntityUid target, PrototypeId<GodPrototype>? newGod, ReligionComponent? religion = null);
        void SwitchReligion(EntityUid target, PrototypeId<GodPrototype>? newGod, ReligionComponent? religion = null);
        bool Pray(EntityUid target, EntityUid? altar = null, ReligionComponent? religion = null);

        (EntityUid? servantUid, bool wasDeclined) CreateGiftServant(PrototypeId<EntityPrototype> servantId, EntityUid target, PrototypeId<GodPrototype> godId);
        EntityUid? CreateGiftItem(GodItem godItem, EntityUid target, PrototypeId<GodPrototype> godId);
        EntityUid? CreateGiftArtifact(PrototypeId<EntityPrototype> itemId, EntityUid target, PrototypeId<GodPrototype> godId);

        int CalculateItemPietyValue(EntityUid item);

        bool OfferToGod(EntityUid chara, EntityUid offeringItem, EntityUid? altar = null, ReligionComponent? religion = null);

        void Punish(EntityUid chara);
    }

    public sealed class ReligionSystem : EntitySystem, IReligionSystem
    {
        [Dependency] private readonly ISkillsSystem _skills = default!;
        [Dependency] private readonly IResistsSystem _resists = default!;
        [Dependency] private readonly IPrototypeManager _protos = default!;
        [Dependency] private readonly IMessagesManager _mes = default!;
        [Dependency] private readonly IRandom _rand = default!;
        [Dependency] private readonly IFieldLayer _field = default!;
        [Dependency] private readonly ISpellSystem _spells = default!;
        [Dependency] private readonly IAudioManager _audio = default!;
        [Dependency] private readonly IRefreshSystem _refresh = default!;
        [Dependency] private readonly IGameController _gameController = default!;
        [Dependency] private readonly IGameSessionManager _gameSession = default!;
        [Dependency] private readonly IPlayerQuery _playerQuery = default!;
        [Dependency] private readonly IMapDrawablesManager _drawables = default!;
        [Dependency] private readonly IPartySystem _parties = default!;
        [Dependency] private readonly ICharaGen _charaGen = default!;
        [Dependency] private readonly IItemGen _itemGen = default!;
        [Dependency] private readonly IEntityGenMemorySystem _memory = default!;
        [Dependency] private readonly IStackSystem _stacks = default!;
        [Dependency] private readonly IActivitySystem _activities = default!;
        [Dependency] private readonly IEntityLookup _lookup = default!;
        [Dependency] private readonly ISleepSystem _sleep = default!;
        [Dependency] private readonly IEffectSystem _effects = default!;
        [Dependency] private readonly INewEffectSystem _newEffects = default!;
        
        public override void Initialize()
        {
            SubscribeComponent<ReligionComponent, OnJoinFaithEvent>(OnJoinFaith, priority: EventPriorities.High);
            SubscribeComponent<ReligionComponent, OnLeaveFaithEvent>(OnLeaveFaith, priority: EventPriorities.High);
            SubscribeComponent<ReligionComponent, EntityBeingGeneratedEvent>(SetRandomGod, priority: EventPriorities.Highest);
            SubscribeComponent<ReligionComponent, EntityRefreshEvent>(ApplyBlessings, priority: EventPriorities.Low);
            SubscribeEntity<MapOnTimePassedEvent>(UpdatePrayerChargeAndPiety, priority: EventPriorities.Low);
        }

        private void SetRandomGod(EntityUid uid, ReligionComponent component, ref EntityBeingGeneratedEvent args)
        {
            var canTalk = CompOrNull<DialogComponent>(uid)?.CanTalk ?? false;

            if (!_gameSession.IsPlayer(uid) && canTalk && component.HasRandomGod && component.GodID == null)
                component.GodID = PickRandomGodID();
        }

        private void ApplyBlessings(EntityUid uid, ReligionComponent component, ref EntityRefreshEvent args)
        {
            if (component.GodID == null)
                return;

            var godProto = _protos.Index(component.GodID.Value);
            var spatial = EntityManager.GetComponent<SpatialComponent>(uid);

            if (godProto.Blessing != null)
                _effects.Apply(godProto.Blessing, source: uid, coords: spatial.Coordinates, target: uid, verb: null, args: new EffectArgSet());
        }

        private void OnJoinFaith(EntityUid uid, ReligionComponent component, OnJoinFaithEvent args)
        {
            var godProto = _protos.Index(args.NewGodID);

            if (godProto.Callbacks != null)
            {
                EntitySystem.InjectDependencies(godProto.Callbacks);
                godProto.Callbacks?.OnJoinFaith(uid);
            }
        }

        private void OnLeaveFaith(EntityUid uid, ReligionComponent component, OnLeaveFaithEvent args)
        {
            var godProto = _protos.Index(args.OldGodID);

            if (godProto.Callbacks != null)
            {
                EntitySystem.InjectDependencies(godProto.Callbacks);
                godProto.Callbacks?.OnLeaveFaith(uid);
            }
        }

        private void UpdatePrayerChargeAndPiety(EntityUid uid, ref MapOnTimePassedEvent args)
        {
            if (args.HoursPassed <= 0 || _sleep.IsPlayerSleeping)
                return;

            if (HasComp<MapTypeWorldMapComponent>(args.Map.MapEntityUid))
            {
                foreach (var religion in _lookup.EntityQueryInMap<ReligionComponent>(args.Map))
                {
                    if (_rand.OneIn(40))
                    {
                        religion.Piety = Math.Max(religion.Piety - 1, 0);
                        religion.PrayerCharge += 4;
                    }
                }
            }
            else
            {
                foreach (var religion in _lookup.EntityQueryInMap<ReligionComponent>(args.Map))
                {
                    if (_rand.OneIn(5))
                    {
                        religion.Piety = Math.Max(religion.Piety - 1, 0);
                        religion.PrayerCharge += 32;
                    }
                }
            }
        }

        public void ApplySkillBlessing(EntityUid target, PrototypeId<SkillPrototype> skillId, int coefficient, int add, ReligionComponent? religion = null)
        {
            if (!Resolve(target, ref religion))
                return;

            if (!_skills.HasSkill(target, skillId))
                return;

            if (!_skills.TryGetKnown(target, skillId, out var skill))
                return;

            var amount = Math.Clamp((religion.Piety) / coefficient, 1, add + _skills.Level(target, Protos.Skill.Faith) / 10);
            skill.Level.Buffed += amount;
        }

        public void ApplyResistBlessing(EntityUid target, PrototypeId<ElementPrototype> elementId, int coefficient, int add, ReligionComponent? religion = null)
        {
            if (!Resolve(target, ref religion))
                return;

            if (!_resists.HasResist(target, elementId))
                return;

            if (!_resists.TryGetKnown(target, elementId, out var resist))
                return;

            var amount = Math.Clamp((religion.Piety) / coefficient, 1, add + _skills.Level(target, Protos.Skill.Faith) / 10);
            resist.Level.Buffed += amount;
        }

        public string GetGodName(EntityUid uid, ReligionComponent? religion = null)
        {
            if (!Resolve(uid, ref religion))
                return GetGodName(null);

            return GetGodName(religion.GodID);
        }

        public string GetGodName(PrototypeId<GodPrototype>? godID)
        {
            if (godID == null)
                return Loc.GetString("Elona.God.Eyth.Name");

            return Loc.GetPrototypeString(godID.Value, "Name");
        }

        public PrototypeId<GodPrototype>? PickRandomGodID(bool includeEyth = true)
        {
            var ids = _protos.EnumeratePrototypes<GodPrototype>()
                .Where(proto => proto.CanAppearRandomly)
                .Select(proto => proto.GetStrongID())
                .ToList();

            if (includeEyth)
                ids.Add(Protos.God.Eyth);

            var result = _rand.Pick(ids);
            if (result == Protos.God.Eyth)
                return null;

            return result;
        }

        public void GodSays(EntityUid believer, string text, ReligionComponent? religion = null)
        {
            if (!Resolve(believer, ref religion))
                return;

            GodSays(religion.GodID, text);
        }

        public void GodSays(PrototypeId<GodPrototype>? godId, string text)
        {
            if (godId == null)
                return;

            _mes.Display($"TODO god talk {text}");
        }

        public bool ModifyPiety(EntityUid target, int amount, ReligionComponent? religion = null)
        {
            if (!Resolve(target, ref religion))
                return false;

            if (_skills.Level(target, Protos.Skill.Faith) * 100 < religion.Piety)
            {
                _mes.Display(Loc.GetString("Elona.Religion.Pray.Indifferent"));
                return false;
            }

            if (religion.GodRank == 4 && religion.Piety >= 4000)
            {
                religion.GodRank++;
                GodSays(religion.GodID, "Elona.GodGift2");
            }
            if (religion.GodRank == 2 && religion.Piety >= 2500)
            {
                religion.GodRank++;
                GodSays(religion.GodID, "Elona.GodGift2");
            }
            if (religion.GodRank == 0 && religion.Piety >= 1500)
            {
                religion.GodRank++;
                GodSays(religion.GodID, "Elona.GodGift2");
            }

            // TODO show house

            religion.Piety += amount;

            return true;
        }

        public bool CanOfferItemToGod(PrototypeId<GodPrototype>? godId, EntityUid item)
        {
            if (godId == null)
            {
                // Eyth.
                return false;
            }

            if (EntityManager.TryGetComponent<ItemComponent>(item, out var itemComp))
            {
                if (itemComp.IsPrecious)
                    return false;
            }

            var godProto = _protos.Index(godId.Value);

            var metaComp = EntityManager.GetComponent<MetaDataComponent>(item);
            EntityManager.TryGetComponent<TagComponent>(item, out var tagComp);

            foreach (var offering in godProto.Offerings)
            {
                // TODO one or the other
                if (offering.Category != null)
                {
                    if (tagComp?.Tags.Contains(offering.Category.Value) ?? false)
                        return true;
                }
                else if (offering.ItemId != null)
                {
                    if (metaComp.EntityPrototype?.GetStrongID() == offering.ItemId.Value)
                        return true;
                }
            }

            return false;
        }

        public void SwitchReligionWithPenalty(EntityUid target, PrototypeId<GodPrototype>? newGod, ReligionComponent? religion = null)
        {
            if (!Resolve(target, ref religion))
                return;

            _field.RefreshScreen();

            if (religion.GodID != null)
            {
                var godName = Loc.GetPrototypeString(religion.GodID.Value, "Name");
                _mes.Display(Loc.GetString("Elona.Religion.Enranged", ("godName", godName)), UiColors.MesPurple);
                GodSays(religion.GodID.Value, "Elona.GodStopBelievingIn");

                _newEffects.Apply(target, target, null, Protos.Effect.ActionBuffPunishment, power: 10000);
                _audio.Play(Protos.Sound.Punish1, target);
                _gameController.WaitSecs(0.5f);
            }

            SwitchReligion(target, newGod, religion);
        }

        public void SwitchReligion(EntityUid target, PrototypeId<GodPrototype>? newGodID, ReligionComponent? religion = null)
        {
            if (!Resolve(target, ref religion))
                return;

            if (religion.GodID != null)
            {
                var ev = new OnLeaveFaithEvent(religion.GodID.Value);
                RaiseEvent(target, ev);
            }

            religion.GodID = newGodID;

            if (newGodID == null)
            {
                _mes.Display(Loc.GetString("Elona.Religion.Switch.Unbeliever"), UiColors.MesYellow);
            }
            else
            {
                var godName = Loc.GetPrototypeString(newGodID.Value, "Name");
                _audio.Play(Protos.Sound.Complete1);
                _mes.Display(Loc.GetString("Elona.Religion.Switch.Follower", ("godName", godName)), UiColors.MesYellow);
                GodSays(newGodID.Value, "Elona.GodStartBelievingIn");

                var ev = new OnJoinFaithEvent(newGodID.Value);
                RaiseEvent(target, ev);
            }

            _refresh.Refresh(target);
        }

        public bool Pray(EntityUid target, EntityUid? altar = null, ReligionComponent? religion = null)
        {
            if (!Resolve(target, ref religion) || religion.GodID == null)
            {
                _mes.Display(Loc.GetString("Elona.Religion.Pray.DoNotBelieve"));
                return false;
            }

            var godId = religion.GodID.Value;
            var godProto = _protos.Index(godId);

            if (_gameSession.IsPlayer(target))
            {
                if (!_playerQuery.YesOrNo(Loc.GetString("Elona.Religion.Pray.Prompt")))
                {
                    _field.RefreshScreen();
                    return false;
                }
            }

            var godName = Loc.GetPrototypeString(religion.GodID.Value, "Name");

            _mes.Display(Loc.GetString("Elona.Religion.Pray.YouPrayTo", ("godName", godName)));

            if (religion.Piety < 200 || religion.PrayerCharge < 1000)
            {
                _mes.Display(Loc.GetString("Elona.Religion.Pray.Indifferent", ("godName", godName)));
                return false;
            }

            var spatial = EntityManager.GetComponent<SpatialComponent>(target);
            var positions = new MapCoordinates[] { spatial.MapPosition };
            var anim = new MiracleMapDrawable(positions, Protos.Sound.Heal1, Protos.Sound.Pray2);
            _drawables.Enqueue(anim, spatial.MapPosition);

            _newEffects.Apply(target, target, null, Protos.Effect.Elixir, power: 100);
            _newEffects.Apply(target, target, null, Protos.Effect.SpellBuffHolyVeil, power: 200);

            religion.PrayerCharge = 0;
            religion.Piety = Math.Max(religion.Piety * 85 / 100, 0);

            if (religion.GodRank % 2 == 1)
            {
                GodSays(godId, "Elona.GodGift");

                if (religion.GodRank == 1)
                {
                    if (godProto.Servant != null)
                    {
                        var (servantEntity, wasDeclined) = CreateGiftServant(godProto.Servant.Value, target, godId);
                        if (!EntityManager.IsAlive(servantEntity))
                        {
                            if (wasDeclined)
                            {
                                // Player opted to skip this reward.
                                religion.GodRank++;
                            }
                            return true;
                        }
                    }
                }
                else if (religion.GodRank == 3)
                {
                    if (godProto.Items.Count > 0)
                    {
                        foreach (var item in godProto.Items)
                        {
                            CreateGiftItem(item, target, godId);
                        }
                        _mes.Display(Loc.GetString("Elona.Common.SomethingIsPut"));
                    }
                }
                else if (religion.GodRank == 5)
                {
                    if (godProto.Artifact != null)
                    {
                        CreateGiftArtifact(godProto.Artifact.Value, target, godId);
                        _mes.Display(Loc.GetString("Elona.Common.SomethingIsPut"));
                    }
                }

                religion.GodRank += 1;
            }

            return true;
        }

        public (EntityUid? servantUid, bool wasDeclined) CreateGiftServant(PrototypeId<EntityPrototype> servantId, EntityUid target, PrototypeId<GodPrototype> godId)
        {
            // TODO
            var servantsInParty = 0;

            var success = true;

            if (servantsInParty >= 2)
            {
                _mes.Display(Loc.GetString("Elona.Religion.Pray.Servant.NoMore"));
                success = false;
            }
            else if (!_parties.CanRecruitMoreMembers(target))
            {
                _mes.Display(Loc.GetString("Elona.Religion.Pray.Servant.PartyIsFull"));
                success = false;
            }

            if (!success)
            {
                if (!_playerQuery.YesOrNo(Loc.GetString("Elona.Religion.Pray.Servant.PromptDecline")))
                {
                    return (null, true);
                }

                return (null, false);
            }

            var ally = _charaGen.GenerateChara(target, id: servantId,
                args: EntityGenArgSet.Make(new EntityGenCommonArgs() { NoRandomModify = true }));

            if (ally != null)
                _parties.TryRecruitAsAlly(target, ally.Value);

            return (ally, false);
        }

        public EntityUid? CreateGiftItem(GodItem godItem, EntityUid target, PrototypeId<GodPrototype> godId)
        {
            var id = godItem.ItemId;
            var noStack = godItem.NoStack;
            if (godItem.OnlyOnce && _memory.Generated(godItem.ItemId) > 0)
            {
                id = Protos.Item.PotionOfCureCorruption;
                noStack = false;
            }

            var item = _itemGen.GenerateItem(target, id: id,
                args: EntityGenArgSet.Make(new EntityGenCommonArgs() { NoStack = true }));

            // TODO properties

            return item;
        }

        public EntityUid? CreateGiftArtifact(PrototypeId<EntityPrototype> itemId, EntityUid target, PrototypeId<GodPrototype> godId)
        {
            if (_memory.Generated(itemId) > 0)
                itemId = Protos.Item.TreasureMap;

            return _itemGen.GenerateItem(target, id: itemId);
        }

        public int CalculateItemPietyValue(EntityUid item)
        {
            var ev = new CalculateItemPietyValueEvent(item);
            RaiseEvent(item, ev);
            return ev.ResultPietyValue;
        }

        public bool OfferToGod(EntityUid chara, EntityUid offeringItem, EntityUid? altar = null, ReligionComponent? religion = null)
        {
            if (!Resolve(chara, ref religion) || religion.GodID == null)
            {
                _mes.Display(Loc.GetString("Elona.Religion.Pray.DoNotBelieve"));
                return false;
            }

            var godId = religion.GodID.Value;
            var godProto = _protos.Index(godId);
            var godName = Loc.GetPrototypeString(godId, "Name");

            _activities.InterruptUsing(offeringItem);
            if (!_stacks.TrySplit(offeringItem, 1, out var item))
                return false;

            _mes.Display(Loc.GetString("Elona.Religion.Offer.Execute", ("item", item), ("godName", godName)));

            _audio.Play(Protos.Sound.Offer2, chara);
            _drawables.Enqueue(new ParticleMapDrawable(Protos.Asset.OfferEffect), chara);

            if (!EntityManager.IsAlive(altar) || !EntityManager.TryGetComponent<AltarComponent>(altar.Value, out var altarComp))
                return true;

            var pietyValue = CalculateItemPietyValue(item);

            if (altarComp.GodID != godId)
            {
                var otherGodName = "";
                if (altarComp.GodID != null)
                    otherGodName = Loc.GetPrototypeString(altarComp.GodID.Value, "Name");

                var wasClaimed = false;

                if (altarComp.GodID == null)
                {
                    wasClaimed = true;
                    _mes.Display(Loc.GetString("Elona.Religion.Offer.Claims", ("godName", godName)));
                }
                else
                {
                    _mes.Display(Loc.GetString("Elona.Religion.Offer.TakeOver.Attempt", ("godName", godName), ("otherGodName", otherGodName)));
                    wasClaimed = _rand.Next(17) <= pietyValue;
                }

                if (wasClaimed)
                {
                    ModifyPiety(chara, pietyValue * 5);
                    religion.PrayerCharge += pietyValue * 30;
                    var spatial = EntityManager.GetComponent<SpatialComponent>(chara);
                    var positions = new MapCoordinates[] { spatial.MapPosition };
                    _drawables.Enqueue(new MiracleMapDrawable(positions, Protos.Sound.Heal1, Protos.Sound.Pray2), spatial.MapPosition);
                    if (altarComp.GodID != null)
                        _mes.Display(Loc.GetString("Elona.Religion.Offer.TakeOver.Shadow"));
                    _mes.Display(Loc.GetString("Elona.Religion.Offer.TakeOver.Succeed", ("godName", godName), ("altar", altar.Value)), UiColors.MesYellow);
                    GodSays(godId, "Elona.GodTakeOverSucceed");
                    altarComp.GodID = godId;
                }
                else
                {
                    _mes.Display(Loc.GetString("Elona.Religion.TakeOver.Fail", ("godName", godName)));
                    GodSays(godId, "Elona.GodTakeOverFail");
                    Punish(chara);
                }
            }
            else
            {
                LocaleKey? key = null;
                if (pietyValue >= 15)
                {
                    key = "Elona.Religion.Offer.Result.Best";
                    GodSays(godId, "Elona.GodOfferGreat");
                }
                else if (pietyValue >= 10)
                    key = "Elona.Religion.Offer.Result.Good";
                else if (pietyValue >= 5)
                    key = "Elona.Religion.Offer.Result.Okay";
                else if (pietyValue >= 1)
                    key = "Elona.Religion.Offer.Result.Poor";

                if (key != null)
                {
                    _mes.Display(Loc.GetString(key.Value, ("item", item)), UiColors.MesGreen);
                }
                ModifyPiety(chara, pietyValue);
                religion.PrayerCharge += pietyValue * 7;
            }

            _stacks.SetCount(item, 0);
            return true;
        }

        public void Punish(EntityUid chara)
        {
            _newEffects.Apply(chara, chara, null, Protos.Effect.ActionCurse, power: 500);
            if (_rand.OneIn(2))
            {
                _newEffects.Apply(chara, chara, null, Protos.Effect.ActionBuffPunishment, power: 250);
                _audio.Play(Protos.Sound.Punish1, chara);
            }
            if (_rand.OneIn(2))
            {
                _newEffects.Apply(chara, chara, null, Protos.Effect.PunishDecrementStats, power: 100);
            }
        }
    }

    public sealed class CalculateItemPietyValueEvent : EntityEventArgs
    {
        public EntityUid Item { get; }
        public int ResultPietyValue { get; set; } = 25;

        public CalculateItemPietyValueEvent(EntityUid item)
        {
            Item = item;
        }
    }

    public sealed class OnJoinFaithEvent : EntityEventArgs
    {
        public PrototypeId<GodPrototype> NewGodID { get; }

        public OnJoinFaithEvent(PrototypeId<GodPrototype> newGodID)
        {
            NewGodID = newGodID;
        }
    }

    public sealed class OnLeaveFaithEvent : EntityEventArgs
    {
        public PrototypeId<GodPrototype> OldGodID { get; }

        public OnLeaveFaithEvent(PrototypeId<GodPrototype> newGodID)
        {
            OldGodID = newGodID;
        }
    }
}
