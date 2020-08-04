using DiscUtils.Ntfs.Internals;
using Microsoft.CST.RecursiveExtractor;
using System.Collections;
using System.Collections.Generic;

namespace RecursiveExtractor.Blazor.Services
{
    public class AppData
    {
        public AppData()
        {
            
        }

        public Dictionary<string, FileEntry> GuidToFileEntryMap { get; set; } = new Dictionary<string, FileEntry>();
    }
}
