namespace Microsoft.CST.RecursiveExtractor
{
    /// <summary>
    /// Bag of File data used for Pass Filtering
    /// </summary>
    public class FileEntryInfo
    {
        /// <summary>
        /// Construct a FileEntryInfo
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parentPath"></param>
        /// <param name="size"></param>
        public FileEntryInfo(string name, string parentPath, long size)
        {
            Name = name;
            ParentPath = parentPath;
            Size = size;
        }
        /// <summary>
        /// The Relative Path in the Parent
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// The Parent Path
        /// </summary>
        public string ParentPath { get; }
        /// <summary>
        /// The Size of the File
        /// </summary>
        public long Size { get; }
    }
}
