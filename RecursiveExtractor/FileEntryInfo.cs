using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CST.OpenSource
{
    public class FileEntryInfo
    {
        public FileEntryInfo(string name, string parentpath, long size)
        {
            Name = name;
            ParentPath = parentpath;
            Size = size;
        }
        public string Name { get; }
        public string ParentPath { get; }
        public long Size { get; }
    }
}
