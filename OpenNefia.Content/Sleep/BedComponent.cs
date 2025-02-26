﻿using OpenNefia.Content.Prototypes;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Serialization.Manager.Attributes;
using System;
using System.Collections.Generic;

namespace OpenNefia.Content.Sleep
{
    [RegisterComponent]
    [ComponentUsage(ComponentTarget.Normal)]
    public sealed class BedComponent : Component
    {
        [DataField]
        public float BedQuality { get; set; }
    }
}