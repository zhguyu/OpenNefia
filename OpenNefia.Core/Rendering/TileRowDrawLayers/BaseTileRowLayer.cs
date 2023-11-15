﻿using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Maps;
using OpenNefia.Core.Maths;
using OpenNefia.Core.UI.Element;

namespace OpenNefia.Core.Rendering.TileRowDrawLayers
{
    public abstract class BaseTileRowLayer : ITileRowLayer
    {
        [Dependency] protected readonly IAssetManager Assets = default!;
        [Dependency] protected readonly IEntityManager EntityManager = default!;
        
        protected IMap? Map;

        public virtual void Initialize() { }
        public virtual void OnThemeSwitched() { }
        public virtual void SetMap(IMap map) => Map = map;
        public virtual void RedrawAll() {}
        public virtual void RedrawDirtyTiles(HashSet<Vector2i> dirtyTilesThisTurn) { }
        public virtual void Update(float dt) { }
        public virtual void DrawRow(int row, int screenX, int screenY) {}
    }
}