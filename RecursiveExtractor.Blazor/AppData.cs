using System.Collections.Generic;

namespace Microsoft.CST.RecursiveExtractor.Blazor.Services
{
    public class AppData
    {
        public AppData()
        {
        }

        public Dictionary<string, FileEntry> GuidToFileEntryMap { get; set; } = new Dictionary<string, FileEntry>();
    }
}