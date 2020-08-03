namespace Microsoft.CST.RecursiveExtractor
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
