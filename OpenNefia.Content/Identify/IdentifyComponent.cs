﻿using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Locale;
using OpenNefia.Core.Serialization.Manager.Attributes;

namespace OpenNefia.Content.Identify
{
    [RegisterComponent]
    public class IdentifyComponent : Component, IComponentLocalizable
    {
        public override string Name => "Identify";

        [DataField]
        public IdentifyState IdentifyState { get; set; } = IdentifyState.None;

        [Localize]
        public string? UnidentifiedName { get; private set; }

        [DataField]
        public int IdentifyDifficulty { get; set; } = 0;

        void IComponentLocalizable.LocalizeFromLua(NLua.LuaTable table)
        {
            UnidentifiedName = table.GetStringOrNull(nameof(UnidentifiedName));
        }
    }

    public enum IdentifyState : byte
    {
        None = 0,
        Name = 1,
        Quality = 2,
        Full = 3
    }
}
