# About

Recursive Extractor is a .NET Standard 2.0/2.1 Library for parsing archive files and disk images, including nested archives and disk images.

Recursive Extractor is available on NuGet as [Microsoft.CST.RecursiveExtractor](https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/).
# Using
This example will print out the paths of all the files in the archive.
```csharp
var path = "/Path/To/Your/Archive"
var extractor = new Extractor();
try {
    IEnumerable<FileEntry> results = extractor.ExtractFile(path, parallel);
    foreach(var found in results)
    {
        Console.WriteLine(found.FullPath);
    }
}
catch(OverflowException)
{
    // This means Recursive Extractor has detected a Quine or Zip Bomb
}
```

## Exceptions

`ExtractFile` will throw an overflow exception when a quine or zip bomb is detected.

Otherwise, invalid files found while crawling will emit a logger message.

# Supported File Types
* 7zip
* ar
* bzip2
* deb
* gzip
* iso
* rar
* tar
* vhd
* vhdx
* vmdk
* wim
* xzip
* zip

# Feedback

If you have any issues or feature requests please open a new [Issue](https://github.com/microsoft/RecursiveExtractor/issues/new)

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
