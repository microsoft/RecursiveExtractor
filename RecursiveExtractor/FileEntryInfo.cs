using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.CST.OpenSource
{
    public class FileEntryInfo
    {
        public FileEntryInfo(string name, string parentPath, long size)
        {
            Name = name;
            ParentPath = parentPath;
            Size = size;
        }
        public string Name { get; }
        public string ParentPath { get; }
        public long Size { get; }
    }
}
