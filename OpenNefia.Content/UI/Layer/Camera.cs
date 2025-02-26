﻿using OpenNefia.Content.UI.Hud;
using OpenNefia.Core.Configuration;
using OpenNefia.Core.Game;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Graphics;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Maps;
using OpenNefia.Core.Maths;
using OpenNefia.Core.Rendering;
using OpenNefia.Core.UI.Element;

namespace OpenNefia.Content.UI.Layer
{
    public class Camera
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly ICoords _coords = default!;
        [Dependency] private readonly IHudLayer _hud = default!;
        [Dependency] private readonly IGraphics _graphics = default!;
        [Dependency] private readonly IConfigurationManager _config = default!;

        private Vector2i _mapSize;
        private IDrawable _parent;
        private Vector2 _screenPos;
        public Vector2 ScreenPos { get => _screenPos; set => _screenPos = value; }

        public Camera(IDrawable parent)
        {
            _parent = parent;
        }

        public void Initialize()
        {
            IoCManager.InjectDependencies(this);
        }

        public void SetMapSize(Vector2i mapSize)
        {
            this._mapSize = mapSize;
        }

        public void CenterOnScreenPos(Vector2i screenPos)
        {
            var size = _coords.TileToScreen(_mapSize);
            _screenPos = _coords.BoundDrawPosition(screenPos, size, _parent.PixelSize - (0, (int)(_graphics.WindowPixelSize.Y) - (int)(_hud.GameBounds.Bottom * _parent.UIScale)));
        }

        public void CenterOnTilePos(EntityCoordinates coords)
        {
            CenterOnTilePos(coords.ToMap(_entityManager));
        }

        public void CenterOnTilePos(EntityUid uid)
        {
            if (!_entityManager.TryGetComponent(uid, out SpatialComponent? spatial))
                return;

            CenterOnTilePos(spatial.MapPosition);
        }

        public void CenterOnTilePos(MapCoordinates coords)
        {
            var screenPos = _coords.TileToScreenScaled(coords.Position);
            CenterOnScreenPos(screenPos);
        }

        public void Pan(Vector2 screenDPos)
        {
            _screenPos += (Vector2i)screenDPos;
        }

        public Vector2 TileToVisibleScreen(Vector2i tilePos)
        {
            return _coords.TileToScreenScaled(tilePos) + _screenPos;
        }

        public Vector2 TileToVisibleScreen(MapCoordinates coords)
        {
            return TileToVisibleScreen(coords.Position);
        }

        public Vector2 TileToVisibleScreen(EntityCoordinates coords)
        {
            return TileToVisibleScreen(coords.ToMap(_entityManager));
        }

        public Vector2i VisibleScreenToTile(Vector2 screenPos)
        {
            return _coords.ScreenToTileScaled((Vector2i)(screenPos - _screenPos));
        }
    }
}
