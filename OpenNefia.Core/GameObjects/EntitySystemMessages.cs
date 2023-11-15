﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenNefia.Core.Maps;

namespace OpenNefia.Core.GameObjects
{
    public sealed class EntityInitializedEvent : EntityEventArgs
    {
        public EntityUid EntityUid { get; }

        public EntityInitializedEvent(EntityUid entityUid)
        {
            EntityUid = entityUid;
        }
    }

    public class EntMapIdChangedEvent : EntityEventArgs
    {
        public EntMapIdChangedEvent(EntityUid entityUid, MapId oldMapId)
        {
            EntityUid = entityUid;
            OldMapId = oldMapId;
        }

        public EntityUid EntityUid { get; }
        public MapId OldMapId { get; }
    }

    /// <summary>
    /// The children of this entity are about to be deleted permanently, and will not be saved.
    /// </summary>
    [ByRefEvent]
    public sealed class EntityTerminatingEvent : EntityEventArgs 
    {
    }

    /// <summary>
    /// The children of this entity are about to be unloaded, and will be kept in the save file.
    /// </summary>
    [ByRefEvent]
    public sealed class EntityUnloadingEvent : EntityEventArgs
    {
    }

    public sealed class EntityDeletedEvent : EntityEventArgs
    {
        public EntityUid EntityUid { get; }

        public EntityDeletedEvent(EntityUid entityUid)
        {
            EntityUid = entityUid;
        }
    }

    public sealed class EntityUnloadedEvent : EntityEventArgs
    {
        public EntityUid EntityUid { get; }

        public EntityUnloadedEvent(EntityUid entityUid)
        {
            EntityUid = entityUid;
        }
    }
}
