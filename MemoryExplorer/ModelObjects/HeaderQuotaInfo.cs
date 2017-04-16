﻿using MemoryExplorer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MemoryExplorer.ModelObjects
{
    public class HeaderQuotaInfo : StructureBase
    {
        private dynamic _hqi;
        public HeaderQuotaInfo(DataModel model, ulong virtualAddress = 0, ulong physicalAddress = 0) : base(model, virtualAddress, physicalAddress)
        {
            _hqi = _profile.GetStructure("_OBJECT_HEADER_QUOTA_INFO", physicalAddress);
        }
        public dynamic dynamicObject
        {
            get { return _hqi; }
        }
    }
}
