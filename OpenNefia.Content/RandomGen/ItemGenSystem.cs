﻿using OpenNefia.Content.GameObjects;
using OpenNefia.Content.Levels;
using OpenNefia.Content.Qualities;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Prototypes;
using OpenNefia.Content.Prototypes;
using OpenNefia.Core.Random;
using OpenNefia.Core.Log;
using OpenNefia.Core.Maps;
using OpenNefia.Core.Game;
using OpenNefia.Content.Skills;
using OpenNefia.Content.EntityGen;
using OpenNefia.Content.Maps;
using OpenNefia.Core.Containers;
using OpenNefia.Core.Serialization.Manager.Attributes;

namespace OpenNefia.Content.RandomGen
{
    public interface IItemGen : IEntitySystem
    {
        PrototypeId<EntityPrototype>? PickRandomItemIdRaw(IMap? map, int minLevel = 1, PrototypeId<TagPrototype>[]? tags = null, string? fltselect = null);
        PrototypeId<EntityPrototype> PickRandomItemId(IMap? map, EntityGenArgSet argSet, int minLevel = 1, PrototypeId<TagPrototype>[]? tags = null, string? fltselect = null);

        EntityUid? GenerateItem(MapCoordinates coords, PrototypeId<EntityPrototype>? id = null, int minLevel = 1, PrototypeId<TagPrototype>[]? tags = null, string? fltselect = null,
            int? amount = null, Quality? quality = null, EntityGenArgSet? args = null);
        EntityUid? GenerateItem(EntityUid ent, PrototypeId<EntityPrototype>? id = null, int minLevel = 1, PrototypeId<TagPrototype>[]? tags = null, string? fltselect = null,
            int? amount = null, Quality? quality = null, EntityGenArgSet? args = null);
        EntityUid? GenerateItem(IMap map, PrototypeId<EntityPrototype>? id = null, int minLevel = 1, PrototypeId<TagPrototype>[]? tags = null, string? fltselect = null,
            int? amount = null, Quality? quality = null, EntityGenArgSet? args = null);
        EntityUid? GenerateItem(IContainer container, PrototypeId<EntityPrototype>? id = null, int minLevel = 1, PrototypeId<TagPrototype>[]? tags = null, string? fltselect = null,
            int? amount = null, Quality? quality = null, EntityGenArgSet? args = null);

        EntityUid? GenerateItem(MapCoordinates coords, ItemFilter filter);
        EntityUid? GenerateItem(EntityUid ent, ItemFilter filter);
        EntityUid? GenerateItem(IMap map, ItemFilter filter);
        EntityUid? GenerateItem(IContainer container, ItemFilter filter);
    }

    public sealed class ItemGenSystem : EntitySystem, IItemGen
    {
        [Dependency] private readonly IRandomGenSystem _randomGen = default!;
        [Dependency] private readonly IRandom _rand = default!;
        [Dependency] private readonly IGameSessionManager _session = default!;
        [Dependency] private readonly ISkillsSystem _skills = default!;
        [Dependency] private readonly IEntityGen _entityGen = default!;
        [Dependency] private readonly IMapPlacement _placement = default!;

        public PrototypeId<EntityPrototype>? PickRandomItemIdRaw(IMap? map, int minLevel = 1, PrototypeId<TagPrototype>[]? tags = null, string? fltselect = null)
        {
            int GetWeight(EntityPrototype proto, int minLevel)
            {
                var baseRarity = _randomGen.GetBaseRarity(proto, RandomGenTables.Item, map);
                var level = proto.Components.GetComponent<LevelComponent>().Level;

                return baseRarity.Rarity / (1000 + Math.Abs(minLevel - level) * baseRarity.Coefficient) + 1;
            }

            var id = _randomGen.PickRandomEntityId(RandomGenTables.Item, GetWeight, null, minLevel, tags, fltselect);

            if (Logger.GetSawmill("randomgen.item").Level <= LogLevel.Debug)
            {
                var tagString = string.Join(", ", tags ?? new PrototypeId<TagPrototype>[] { });
                Logger.DebugS("randomgen.item", $"ID: minLevel={minLevel} tags={tagString} fltselect={fltselect} -> {id}");
            }

            return id;
        }

        public PrototypeId<EntityPrototype> PickRandomItemId(IMap? map, EntityGenArgSet argSet, int minLevel = 1, PrototypeId<TagPrototype>[]? tags = null, string? fltselect = null)
        {
            var isShop = false;
            var commonArgs = argSet.Ensure<EntityGenCommonArgs>();
            if (argSet.TryGet<ItemGenArgs>(out var itemGenArgs))
                isShop = itemGenArgs.IsShop;

            // >>>>>>>> shade2/item.hsp:595 	if dbId=-1{ ..
            if (fltselect == null && !isShop)
            {
                if (commonArgs.Quality == Quality.Good && _rand.OneIn(1000))
                    fltselect = FltSelects.SpUnique;
                if (commonArgs.Quality == Quality.Great && _rand.OneIn(100))
                    fltselect = FltSelects.SpUnique;
            }

            var raw = PickRandomItemIdRaw(map, minLevel, tags, fltselect);

            if (raw == null)
            {
                if (fltselect == FltSelects.SpUnique)
                    commonArgs.Quality = Quality.Great;
                minLevel += 10;
                fltselect = null;
                raw = PickRandomItemIdRaw(map, minLevel, tags, fltselect);
            }

            if (raw == null && (tags?.Contains(Protos.Tag.ItemCatFurnitureAltar) ?? false))
            {
                raw = Protos.Item.ScrollOfChangeMaterial;
            }

            if (raw == null)
            {
                var tagString = tags != null ? string.Join(", ", tags.ToArray()) : "<no tags>";
                raw = Protos.Item.Bug;
                Logger.ErrorS("randomgen.item", $"No item generation candidates found: {minLevel} {tagString} {fltselect}");
            }

            return raw!.Value;
            // <<<<<<<< shade2/item.hsp:609 		} ..
        }

        public EntityUid? GenerateItem(MapCoordinates coords, PrototypeId<EntityPrototype>? id = null,
            int minLevel = 1, PrototypeId<TagPrototype>[]? tags = null, string? fltselect = null,
            int? amount = null, Quality? quality = null, EntityGenArgSet? args = null)
        {
            args ??= EntityGenArgSet.Make();
            var commonArgs = args.Get<EntityGenCommonArgs>();

            if (EntityManager.IsAlive(_session.Player))
            {
                if (commonArgs.Quality < Quality.Unique && _skills.Level(_session.Player, Protos.Skill.AttrLuck) > _rand.Next(5000))
                    commonArgs.Quality++;
            }

            if (id == null)
            {
                var map = GetMap(coords);
                id = PickRandomItemId(map, args, minLevel, tags, fltselect);
            }

            if (amount != null)
                commonArgs.Amount = amount.Value;
            if (quality != null)
                commonArgs.Quality = quality.Value;

            return _entityGen.SpawnEntity(id, coords, args: args);
        }

        public EntityUid? GenerateItem(IMap map, PrototypeId<EntityPrototype>? id = null,
            int minLevel = 1, PrototypeId<TagPrototype>[]? tags = null, string? fltselect = null,
            int? amount = null, Quality? quality = null, EntityGenArgSet? args = null)
        {
            var pos = _placement.FindFreePosition(map);
            if (pos == null)
                return null;

            return GenerateItem(pos.Value, id, minLevel, tags, fltselect, amount, quality, args);
        }

        public EntityUid? GenerateItem(EntityUid ent, PrototypeId<EntityPrototype>? id = null,
            int minLevel = 1, PrototypeId<TagPrototype>[]? tags = null, string? fltselect = null,
            int? amount = null, Quality? quality = null, EntityGenArgSet? args = null)
        {
            if (!EntityManager.TryGetComponent<SpatialComponent>(ent, out var spatial))
                return null;

            return GenerateItem(spatial.MapPosition, id, minLevel, tags, fltselect, amount, quality, args);
        }

        public EntityUid? GenerateItem(IContainer container, PrototypeId<EntityPrototype>? id = null, int minLevel = 1, PrototypeId<TagPrototype>[]? tags = null, string? fltselect = null, int? amount = null, Quality? quality = null, EntityGenArgSet? args = null)
        {
            args ??= EntityGenArgSet.Make();
            var commonArgs = args.Get<EntityGenCommonArgs>();

            if (EntityManager.IsAlive(_session.Player))
            {
                if (commonArgs.Quality < Quality.Unique && _skills.Level(_session.Player, Protos.Skill.AttrLuck) > _rand.Next(5000))
                    commonArgs.Quality++;
            }

            if (id == null)
            {
                var map = GetMap(container.Owner); // TODO should this be configurable?
                id = PickRandomItemId(map, args, minLevel, tags, fltselect);
            }

            commonArgs.MinLevel = minLevel;
            if (amount != null)
                commonArgs.Amount = amount.Value;
            if (quality != null)
                commonArgs.Quality = quality.Value;

            return _entityGen.SpawnEntity(id, container, args: args);
        }

        public EntityUid? GenerateItem(MapCoordinates coords, ItemFilter filter)
        {
            return GenerateItem(coords, filter.Id, filter.MinLevel, filter.Tags, filter.Fltselect, filter.Amount, filter.Quality, filter.Args);
        }

        public EntityUid? GenerateItem(EntityUid ent, ItemFilter filter)
        {
            return GenerateItem(ent, filter.Id, filter.MinLevel, filter.Tags, filter.Fltselect, filter.Amount, filter.Quality, filter.Args);
        }

        public EntityUid? GenerateItem(IMap map, ItemFilter filter)
        {
            return GenerateItem(map, filter.Id, filter.MinLevel, filter.Tags, filter.Fltselect, filter.Amount, filter.Quality, filter.Args);
        }

        public EntityUid? GenerateItem(IContainer container, ItemFilter filter)
        {
            return GenerateItem(container, filter.Id, filter.MinLevel, filter.Tags, filter.Fltselect, filter.Amount, filter.Quality, filter.Args);
        }
    }

    [DataDefinition]
    public sealed class ItemFilter
    {
        [DataField]
        public PrototypeId<EntityPrototype>? Id { get; set; } = null;

        [DataField]
        public int MinLevel { get; set; } = 1;

        [DataField]
        public PrototypeId<TagPrototype>[]? Tags { get; set; } = null;

        [DataField]
        public string? Fltselect { get; set; } = null;

        // TODO: args are not serializable yet
        public EntityGenArgSet Args { get; set; } = EntityGenArgSet.Make();

        public EntityGenCommonArgs CommonArgs => Args.Get<EntityGenCommonArgs>();

        [DataField]
        public int? Amount { get => CommonArgs.Amount; set => CommonArgs.Amount = value; }

        [DataField]
        public Quality? Quality { get => CommonArgs.Quality; set => CommonArgs.Quality = value; }
    }
}
