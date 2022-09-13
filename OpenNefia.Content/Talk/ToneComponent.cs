﻿using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Serialization.Manager.Attributes;

namespace OpenNefia.Content.Talk
{
    [RegisterComponent]
    public class ToneComponent : Component
    {
        public override string Name => "Tone";

        [IdDataField]
        public PrototypeId<TonePrototype> ID;

        [DataField]
        public bool IsTalkSilenced { get; set; }
    }
}
