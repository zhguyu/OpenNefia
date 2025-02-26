﻿using OpenNefia.Core.GameObjects;
using OpenNefia.Core.Rendering;
using OpenNefia.Core.UI.Layer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenNefia.Core.Game
{
    public interface IGameSessionManager
    {
        public EntityUid Player { get; set; }

        // TODO move this to IFactionSystem
        bool IsPlayer(EntityUid objEntity);
    }
}
