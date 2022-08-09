#nullable enable
#r "System.Runtime"
#r "System.Collections"
#r "NLua, Version=1.6.0.0, Culture=neutral, PublicKeyToken=6a194c04b9c89217"
#r "C:/Users/yuno/build/OpenNefia.NET/OpenNefia.EntryPoint/bin/Debug/net6.0/OpenNefia.Core.dll"
#r "C:/Users/yuno/build/OpenNefia.NET/OpenNefia.EntryPoint/bin/Debug/net6.0/Resources/Assemblies/OpenNefia.Content.dll"

using NLua;
using OpenNefia.Core.Log;
using OpenNefia.Core.Maths;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Maps;
using OpenNefia.Core.Areas;
using OpenNefia.Core.Locale;
using OpenNefia.Core.Game;
using OpenNefia.Core.Utility;
using OpenNefia.Core.GameObjects;
using OpenNefia.Content.Prototypes;
using OpenNefia.Content.RandomText;
using OpenNefia.Content.Debug;
using OpenNefia.Content.Nefia;
using OpenNefia.Content.Maps;
using OpenNefia.Content.GameObjects.EntitySystems.Tag;
using OpenNefia.Content.GameObjects;
using OpenNefia.Content.RandomGen;
using OpenNefia.Content.TitleScreen;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Rendering;
using OpenNefia.Core.Directions;
using OpenNefia.Content.Rendering;
using OpenNefia.Core.Random;
using OpenNefia.Content.RandomEvent;
using OpenNefia.Content.World;
using OpenNefia.Core.DebugServer;
using OpenNefia.Content.Activity;
using OpenNefia.Content.Logic;
using OpenNefia.Core.UserInterface;
using OpenNefia.Content.UI.Layer;
using OpenNefia.Content.Skills;
using OpenNefia.Content.Inventory;
using OpenNefia.Content.Spells;
using OpenNefia.Content.StatusEffects;
using OpenNefia.Content.Factions;
using OpenNefia.Content.Damage;
using OpenNefia.Content.Charas;

var _entityMan = IoCManager.Resolve<IEntityManager>();
var _mapMan = IoCManager.Resolve<IMapManager>();
var _areaMan = IoCManager.Resolve<IAreaManager>();
var _itemGen = EntitySystem.Get<IItemGen>();
var _charaGen = EntitySystem.Get<ICharaGen>();
var _gameSession = IoCManager.Resolve<IGameSessionManager>();
var _rand = IoCManager.Resolve<IRandom>();
var _randEvents = EntitySystem.Get<IRandomEventSystem>();
var _protos = IoCManager.Resolve<IPrototypeManager>();
var _world = EntitySystem.Get<IWorldSystem>();
var _lookup = EntitySystem.Get<IEntityLookup>();
var _activities = EntitySystem.Get<IActivitySystem>();
var _playerQuery = IoCManager.Resolve<IPlayerQuery>();
var _uiMan = IoCManager.Resolve<IUserInterfaceManager>();
var _loc = IoCManager.Resolve<ILocalizationManager>();
var _inv = EntitySystem.Get<IInventorySystem>();
var _mapEntrance = EntitySystem.Get<IMapEntranceSystem>();
var _effects = EntitySystem.Get<IStatusEffectSystem>();
var _tags = EntitySystem.Get<ITagSystem>();
var _factions = EntitySystem.Get<IFactionSystem>();
var _damage = EntitySystem.Get<IDamageSystem>();
var _skills = EntitySystem.Get<ISkillsSystem>();

public EntityUid player() => _gameSession.Player;
public SpatialComponent playerS() => _entityMan.GetComponent<SpatialComponent>(_gameSession.Player);
public IMap curMap() => _mapMan.ActiveMap!;

public MapCoordinates promptPos()
{
    var args = new PositionPrompt.Args(playerS().MapPosition);
    var result = _uiMan.Query<PositionPrompt, PositionPrompt.Args, PositionPrompt.Result>(args);
    return result.Value.Coords;
}

public T res<T>() => IoCManager.Resolve<T>();
public T sys<T>() where T : IEntitySystem => EntitySystem.Get<T>();

public T comp<T>(EntityUid? uid = null) where T : class, IComponent
{
    return _entityMan.GetComponent<T>(uid ?? player());
}

public SpatialComponent entityAt()
{
    var coords = promptPos();
    return _lookup.GetLiveEntitiesAtCoords(coords).First();
}

public SpatialComponent spatial(EntityUid uid)
{
    return _entityMan.GetComponent<SpatialComponent>(uid);
}

public void gotoArea(string id)
{
    var entrance = new MapEntrance()
    {
        MapIdSpecifier = new GlobalAreaMapIdSpecifier(new GlobalAreaId(id)),
        StartLocation = new MapEdgesLocation()
    };
    _mapEntrance.UseMapEntrance(player(), entrance);
}

public LevelAndPotential skill(PrototypeId<SkillPrototype> id)
{
    return _entityMan.GetComponent<SkillsComponent>(player()).Ensure(id);
}

public LevelPotentialAndStock spell(PrototypeId<SpellPrototype> id)
{
    return _entityMan.GetComponent<SpellsComponent>(player()).Ensure(id);
}

public void skillLv(PrototypeId<SkillPrototype> id, int level)
{
    var sk = skill(id);
    sk.Level.Base = level;
}

public void spellLv(PrototypeId<SpellPrototype> id, int level)
{
    var sp = spell(id);
    sp.Level.Base = level;
}

public EntityUid? give(PrototypeId<EntityPrototype> id, int? amount = null)
{
    if (!_inv.TryGetInventoryContainer(player(), out var inv))
        return null;

    return _itemGen.GenerateItem(inv, id, amount: amount);
}

public void clearEffects()
{
    _effects.RemoveAll(player());
}

public void gotoDownStairs()
{
    var delving = _tags.EntityWithTagInMap(curMap().Id, Protos.Tag.DungeonStairsDelving);
    if (delving != null)
    {
        playerS().Coordinates = spatial(delving.Owner).Coordinates;
    }
}

public void gotoUpStairs()
{
    var surfacing = _tags.EntityWithTagInMap(curMap().Id, Protos.Tag.DungeonStairsSurfacing);
    if (surfacing != null)
    {
        playerS().Coordinates = spatial(surfacing.Owner).Coordinates;
    }
}

public void gotoWithComp<T>() where T: IComponent
{
    var comp = _lookup.EntityQueryInMap<T>(curMap().Id).FirstOrDefault();
    if (comp != null)
    {
        playerS().Coordinates = spatial(comp.Owner).Coordinates;
    }
}

public void killAllFoes()
{
    foreach (var foe in _lookup.EntityQueryInMap<CharaComponent>(curMap()).Where(c => _factions.GetRelationToPlayer(c.Owner) <= Relation.Enemy))
    {
        _damage.Kill(foe.Owner, player());
    }
}