﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenNefia.Core.CommandLine
{
    public interface ICommandLineController
    {
        public void Run(string[] args);
    }
}
