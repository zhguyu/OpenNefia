﻿using OpenNefia.Content.Sleep;
using OpenNefia.Content.Sleep;
using OpenNefia.Core.GameObjects;
using OpenNefia.Core.IoC;
using OpenNefia.Core.Locale;
using OpenNefia.Core.Random;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenNefia.Content.Activity
{
    public sealed partial class VanillaActivitiesSystem
    {
        private void Initialize_Resting()
        {
            SubscribeComponent<ActivityRestingComponent, OnActivityStartEvent>(Resting_OnStart);
            SubscribeComponent<ActivityRestingComponent, OnActivityPassTurnEvent>(Resting_OnPassTurn);
            SubscribeComponent<ActivityRestingComponent, OnActivityFinishEvent>(Resting_OnFinish);
        }

        private void Resting_OnStart(EntityUid activity, ActivityRestingComponent component, OnActivityStartEvent args)
        {
            _mes.Display(Loc.GetString("Elona.Activity.Resting.Start"));
        }

        private void Resting_OnPassTurn(EntityUid activity
            , ActivityRestingComponent component, OnActivityPassTurnEvent args)
        {
            var activityComp = args.Activity;
            var actor = activityComp.Actor;

            if (activityComp.TurnsRemaining % 2 == 0)
            {
                _damage.HealStamina(actor, 1, showMessage: false);
            }
            if (activityComp.TurnsRemaining % 3 == 0)
            {
                _damage.HealHP(actor, 1, showMessage: false);
                _damage.HealMP(actor, 1, showMessage: false);
            }

            if (_world.State.AwakeTime >= SleepSystem.SleepThresholdModerate)
            {
                var doSleep = false;
                if (_world.State.AwakeTime >= SleepSystem.SleepThresholdHeavy || _rand.OneIn(2))
                {
                    doSleep = true;
                }

                if (doSleep)
                {
                    _mes.Display(Loc.GetString("Elona.Activity.Resting.DropOffToSleep"));
                    _inUse.ClearItemsInUseForUser(actor);
                    _sleep.Sleep(actor, bed: null);
                    _activities.RemoveActivity(actor);
                }
            }
        }

        private void Resting_OnFinish(EntityUid activity, ActivityRestingComponent component, OnActivityFinishEvent args)
        {
            _mes.Display(Loc.GetString("Elona.Activity.Resting.Finish"));
        }
    }
}
