﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleCloudExtension.CloudExplorer
{
    public interface ICloudExplorerSource
    {
        TreeHierarchy GetRoot();

        void Refresh();
    }
}