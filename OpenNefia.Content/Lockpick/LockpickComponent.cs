﻿using OpenNefia.Content.Prototypes;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Serialization.Manager.Attributes;
using System;
using System.Collections.Generic;

namespace OpenNefia.Content.Lockpick
{
    [RegisterComponent]
    [ComponentUsage(ComponentTarget.Normal)]
    public sealed class LockpickComponent : Component
    {
        public override string Name => "Lockpick";
    }
}