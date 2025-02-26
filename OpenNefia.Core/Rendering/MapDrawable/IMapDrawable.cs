﻿using OpenNefia.Core.Maps;
using OpenNefia.Core.Maths;
using OpenNefia.Core.UI.Element;

namespace OpenNefia.Core.Rendering
{
    /// <summary>
    /// An animatable graphic that is rendered in worldspace.
    /// </summary>
    public interface IMapDrawable : IDrawable
    {
        public bool IsFinished { get; }
        public IMap Map { get; }

        /// <summary>
        /// Offset of the animation in worldspace.
        /// </summary>
        public Vector2 ScreenOffset { get; set; }

        /// <summary>
        /// Position of the animation in worldspace.
        /// TODO make into Vector2
        /// </summary>
        public Vector2i ScreenLocalPos { get; set; }

        public bool CanEnqueue();
        public void OnEnqueue();
        public void OnThemeSwitched();
    }
}
