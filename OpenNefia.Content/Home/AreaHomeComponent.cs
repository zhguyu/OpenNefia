﻿using OpenNefia.Content.Prototypes;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Serialization.Manager.Attributes;
using System;
using System.Collections.Generic;

namespace OpenNefia.Content.Home
{
    [RegisterComponent]
    [ComponentUsage(ComponentTarget.Area)]
    public sealed class AreaHomeComponent : Component
    {
        public override string Name => "AreaHome";

        [DataField(required: true)]
        public PrototypeId<HomePrototype> HomeID { get; set; }
    }
}