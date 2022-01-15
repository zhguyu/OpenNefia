﻿using OpenNefia.Content.UI.Element;
using OpenNefia.Content.UI.Element.List;
using OpenNefia.Content.Prototypes;
using OpenNefia.Core.Audio;
using OpenNefia.Core.Locale;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenNefia.Core.Rendering;
using OpenNefia.Content.Charas;
using OpenNefia.Core.Maths;
using OpenNefia.Core.UI;
using static OpenNefia.Content.Charas.CharaAppearanceWindow;

namespace OpenNefia.Content.CharaMake
{
    /// <summary>
    /// Still very WIP atm, unsure how the data will be stored. Essentially just a placeholder.
    /// </summary>
    [Localize("Elona.CharaMake.AppearanceSelect")]
    public class CharaMakeAppearanceLayer : CharaMakeLayer
    {
        private CharaAppearanceWindow AppearanceWindow = new();

        public CharaMakeAppearanceLayer()
        {
            AddChild(AppearanceWindow);

            AppearanceWindow.List.OnActivated += OnListActivated;
        }

        private void OnListActivated(object? sender, UiListEventArgs<UiAppearanceData> args)
        {
            switch (args.SelectedCell.Data)
            {
                case UiAppearanceData.Done:
                    Finish(new CharaMakeResult(new Dictionary<string, object>
                    {

                    }));
                    break;
            }
        }

        public override void Initialize(CharaMakeData args)
        {
            base.Initialize(args);
        }

        public override void OnQuery()
        {
            base.OnQuery();
            Sounds.Play(Protos.Sound.Port);
        }

        public override void GrabFocus()
        {
            base.GrabFocus();
            AppearanceWindow.GrabFocus();
        }

        public override void GetPreferredBounds(out UIBox2i bounds)
        {
            AppearanceWindow.GetPreferredSize(out var size);
            UiUtils.GetCenteredParams(size, out bounds, yOffset: -15);
        }

        public override void SetSize(int width, int height)
        {
            base.SetSize(width, height);
            AppearanceWindow.SetSize(Width, Height);
        }

        public override void SetPosition(int x, int y)
        {
            base.SetPosition(x, y);
            AppearanceWindow.SetPosition(X, Y);
        }

        public override void Draw()
        {
            base.Draw();
            AppearanceWindow.Draw();
        }

        public override void Update(float dt)
        {
            base.Update(dt);
            AppearanceWindow.Update(dt);
        }

        public override void Dispose()
        {
            base.Dispose();
            AppearanceWindow.Dispose();
        }
    }
}
