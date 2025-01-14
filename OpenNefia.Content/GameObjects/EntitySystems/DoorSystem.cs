﻿using OpenNefia.Content.DisplayName;
using OpenNefia.Content.Logic;
using OpenNefia.Core.Audio;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Locale;
using OpenNefia.Core.Maps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenNefia.Content.Visibility;
using OpenNefia.Content.EntityGen;

namespace OpenNefia.Content.GameObjects
{
    public class DoorSystem : EntitySystem
    {
        public const string VerbTypeClose = "Elona.Close";

        [Dependency] private readonly IAudioManager _sounds = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IMessagesManager _mes = default!;

        public override void Initialize()
        {
            SubscribeComponent<DoorComponent, EntityMapInitEvent>(HandleInitialize);
            SubscribeComponent<DoorComponent, GetVerbsEventArgs>(HandleGetVerbs);
            SubscribeComponent<DoorComponent, WasCollidedWithEventArgs>(HandleCollidedWith);
            SubscribeComponent<DoorComponent, EntityBashedEventArgs>(HandleBashed);
        }

        private void HandleInitialize(EntityUid uid, DoorComponent door, ref EntityMapInitEvent args)
        {
            SetOpen(uid, door.IsOpen, door);
        }

        private void HandleGetVerbs(EntityUid uid, DoorComponent component, GetVerbsEventArgs args)
        {
            if (component.IsOpen)
            {
                args.OutVerbs.Add(new Verb(VerbTypeClose, "Close Door", () => DoClose(args.Source, args.Target)));
            }
        }

        private TurnResult DoClose(EntityUid closer, EntityUid doorEntity, DoorComponent? doorComp = null)
        {
            if (!Resolve(doorEntity, ref doorComp))
                return TurnResult.Failed;
            
            if (!doorComp.IsOpen)
                return TurnResult.Failed;

            if (!EntityManager.TryGetComponent(doorEntity, out SpatialComponent spatial))
                return TurnResult.Failed;

            if (!_mapManager.TryGetMap(spatial.MapID, out var map))
                return TurnResult.Failed;

            if (!map.CanAccess(spatial.MapPosition))
            {
                _mes.Display(Loc.GetString("Elona.Door.Close.Blocked"), entity: closer);
                return TurnResult.Aborted;
            }

            _mes.Display(Loc.GetString("Elona.Door.Close.Succeeds", ("entity", closer)), entity: closer);
            SetOpen(doorEntity, false, doorComp);

            return TurnResult.Succeeded;
        }

        public void SetOpen(EntityUid uid, bool isOpen, DoorComponent? door = null)
        {
            if (!Resolve(uid, ref door))
                return;

            door.IsOpen = isOpen;

            if (EntityManager.TryGetComponent(uid, out SpatialComponent spatial))
            {
                spatial.IsSolid = !isOpen;
                spatial.IsOpaque = !isOpen;
            }

            if (EntityManager.TryGetComponent(uid, out ChipComponent chip))
            {
                chip.ChipID = isOpen ? door.ChipOpen : door.ChipClosed;
            }
        }

        private void HandleCollidedWith(EntityUid uid, DoorComponent door, WasCollidedWithEventArgs args)
        {
            _mes.Display(Loc.GetString("Elona.Door.Open.Succeeds", ("entity", args.Source)), entity: args.Source);

            if (door.SoundOpen != null)
            {
                var sound = door.SoundOpen.GetSound();
                if (sound != null)
                    _sounds.Play(sound.Value, uid);
            }

            SetOpen(uid, true, door);
            args.Handle(TurnResult.Succeeded);
        }

        private void HandleBashed(EntityUid uid, DoorComponent component, EntityBashedEventArgs args)
        {
            if (args.Handled)
                return;

            _mes.Display("TODO");
            args.Handle(TurnResult.Succeeded);
        }
    }
}
