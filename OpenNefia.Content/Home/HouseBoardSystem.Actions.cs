﻿using OpenNefia.Core.GameObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenNefia.Core.IoC;
using OpenNefia.Content.Home;
using OpenNefia.Core.Locale;
using OpenNefia.Content.UI;

namespace OpenNefia.Content.Home
{
    public sealed partial class HouseBoardSystem
    {
        [Dependency] private readonly IHomeSystem _homes = default!;

        private void HouseBoard_Design(EntityUid user)
        {
            _mes.Newline();
            _mes.Display(Loc.GetString("Elona.Home.Design.Help"));
            var args = new MapDesignerLayer.Args(user);
            _uiManager.Query<MapDesignerLayer, MapDesignerLayer.Args>(args);
        }

        private void HouseBoard_ViewHomeRank(EntityUid user)
        {
            _mes.Display("TODO", UiColors.MesYellow);
        }

        private void HouseBoard_AlliesInYourHome(EntityUid user)
        {
            _mes.Display("TODO", UiColors.MesYellow);
        }

        private void HouseBoard_RecruitServant(EntityUid user)
        {
            _mes.Display("TODO", UiColors.MesYellow);
        }

        private void HouseBoard_MoveAStayer(EntityUid user)
        {
            _mes.Display("TODO", UiColors.MesYellow);
        }

        private void GetDefaultHouseBoardActions(EntityUid houseBoard, HouseBoardComponent component, HouseBoardGetActionsEvent args)
        {
            // >>>>>>>> shade2/map_user.hsp:224 	if areaId(gArea)=areaShop{ ..
            // TODO zones
            // TODO shops
            // TODO ranches
            var map = GetMap(houseBoard);

            args.OutActions.Add(new(Loc.GetString("Elona.Item.HouseBoard.Actions.Design"), HouseBoard_Design));

            if (_homes.ActiveHomeID == map.Id)
            {
                args.OutActions.Add(new(Loc.GetString("Elona.Item.HouseBoard.Actions.HomeRank"), HouseBoard_ViewHomeRank));
                args.OutActions.Add(new(Loc.GetString("Elona.Item.HouseBoard.Actions.AlliesInYourHome"), HouseBoard_AlliesInYourHome));
                args.OutActions.Add(new(Loc.GetString("Elona.Item.HouseBoard.Actions.RecruitAServant"), HouseBoard_RecruitServant));
                args.OutActions.Add(new(Loc.GetString("Elona.Item.HouseBoard.Actions.MoveAStayer"), HouseBoard_MoveAStayer));
            }
            // <<<<<<<< shade2 / map_user.hsp:240      } ..
        }
    }
}
