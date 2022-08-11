﻿using OpenNefia.Content.Feats;
using OpenNefia.Content.Prototypes;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Serialization.Manager.Attributes;
using System;
using System.Collections.Generic;

namespace OpenNefia.Content.Items
{
    [RegisterComponent]
    [ComponentUsage(ComponentTarget.Normal)]
    public sealed class SecretTreasureComponent : Component
    {
        public override string Name => "SecretTreasure";

        [DataField(required: true)]
        public PrototypeId<FeatPrototype> FeatID { get; set; }
    }
}