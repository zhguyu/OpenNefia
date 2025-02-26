﻿using OpenNefia.Content.GameObjects;
using OpenNefia.Content.GameObjects.Components;
using OpenNefia.Content.Prototypes;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Prototypes;
using OpenNefia.Core.Serialization.Manager.Attributes;
using OpenNefia.Core.Stats;
using System;
using System.Collections.Generic;

namespace OpenNefia.Content.Combat
{
    [RegisterComponent]
    [ComponentUsage(ComponentTarget.Normal)]
    public sealed class ExplosiveComponent : Component, IComponentRefreshable
    {
        [DataField]
        public Stat<bool> IsExplosive { get; set; } = new(true);

        [DataField]
        public bool ExplodesRandomlyWhenAttacked { get; set; } = true;

        [DataField]
        public Stat<float> ExplodeChance { get; set; } = new(0.03f);

        [DataField]
        public bool IsAboutToExplode { get; set; } = false;

        public void Refresh()
        {
            IsExplosive.Reset();
            ExplodeChance.Reset();
        }
    }
}