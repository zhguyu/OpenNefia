﻿using OpenNefia.Core.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenNefia.Core.UI.Element
{
    public interface IUiElement : IDrawable
    {
        Vector2 PreferredSize { get; set; }
        Vector2 MinSize { get; set; }

        void GetPreferredSize(out Vector2 size);
        void SetPreferredSize();
    }
}
