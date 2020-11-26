# About
![CodeQL](https://github.com/microsoft/RecursiveExtractor/workflows/CodeQL/badge.svg) ![Nuget](https://img.shields.io/nuget/v/Microsoft.CST.RecursiveExtractor?link=https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/&link=https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/) ![Nuget](https://img.shields.io/nuget/dt/Microsoft.CST.RecursiveExtractor?link=https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/&link=https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/)

Recursive Extractor is a Cross-Platform [.NET Standard 2.0 Library](#library), [Progressive Web App](#browser) and [Command Line Program](#cli) for parsing archive files and disk images, including nested archives and disk images.

# Supported File Types
| | | |
|-|-|-|
| 7zip+ | ar    | bzip2 |
| deb   | gzip  | iso   |
| rar^  | tar   | vhd   |
| vhdx  | vmdk  | wim*  |
| xzip  | zip+  |       |

<details>
<summary>Details</summary>
<br/>
* Windows only<br/>
+ Encryption Supported<br/>
^ Rar version 4 Encryption supported<br/>
</details>

# Variants

## Browser
You can run Recursive Extractor directly [in your browser](https://microsoft.github.io/RecursiveExtractor/) and install it as a Progressive Web App.  

It runs entirely locally using [WebAssembly](https://docs.microsoft.com/en-us/aspnet/core/blazor/progressive-web-app?view=aspnetcore-3.1&tabs=visual-studio) with no connectivity requirement.

## Cli
### Installing
1. Ensure you have the latest [.NET SDK](https://dotnet.microsoft.com/download).
2. run `dotnet tool install -g Microsoft.CST.RecursiveExtractor.Cli`

This adds `RecursiveExtractor` to your path so you can run it directly from the shell.

### Running
Basic usage is: `RecursiveExtractor --input archive.ext --output outputDirectory`

<details>
<summary>Detailed Usage</summary>
<br/>
<ul>
    <li><i>input</i>: The path to the Archive to extract.</li>
    <li><i>output</i>: The path a directory to extract into.</li>
    <li><i>passwords</i>: A comma separated list of passwords to use for archives.</li>
    <li><i>allow-filters</i>: A comma separated list of regexes to require each extracted file match.</li>
    <li><i>deny-filters</i>: A comma separated list of regexes to require each extracted file not match.</li>
</ul>

For example, to extract only ".cs" files:
```
RecursiveExtractor --input archive.ext --output outputDirectory --allow-filters .cs$
```

Run "RecursiveExtractor --help" for more details.
</details>

## Library
Recursive Extractor is available on NuGet as [Microsoft.CST.RecursiveExtractor](https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/).

### Usage
This code adapted from the Cli extracts the contents of given archive located at `options.Input`
to a directory located at `options.Output`.

```csharp
using Microsoft.CST.RecursiveExtractor;

var extractor = new Extractor();
var extractorOptions = new ExtractorOptions()
{
    ExtractSelfOnFail = true,
    Parallel = true,
};
extractor.ExtractToDirectory(options.Output, options.Input, extractorOptions);
```
<details>
<summary>Async Usage</summary>
<br/>
This example of using the async API prints out all the file names found from the archive located at the path.

```csharp
var path = "/Path/To/Your/Archive"
var extractor = new Extractor();
try {
    IEnumerable<FileEntry> results = extractor.ExtractFileAsync(path);
    await foreach(var found in results)
    {
        Console.WriteLine(found.FullPath);
    }
}
catch(OverflowException)
{
    // This means Recursive Extractor has detected a Quine or Zip Bomb
}
```
</details>

<details>
<summary>The FileEntry Object</summary>
<br/>
The Extractor returns `FileEntry` objects.  These objects contain a `Content` Stream of the file contents.

```csharp
public Stream Content { get; }
public string FullPath { get; }
public string Name { get; }
public FileEntry? Parent { get; }
public string? ParentPath { get; }
```
</details>

<details>
<summary>Extracting Encrypted Archives</summary>
<br/>
You can provide passwords to use to decrypt archives, paired with a Regex that will operate against the Name of the Archive.

```csharp
var path = "/Path/To/Your/Archive"
var directory
var extractor = new Extractor();
try {
    IEnumerable<FileEntry> results = extractor.ExtractFile(path, new ExtractorOptions()
    {
        Passwords = new Dictionary<Regex, List<string>>()
        {
            { new Regex("\.zip"), new List<string>(){ "PasswordForZipFiles" } },
            { new Regex("\.7z"), new List<string>(){ "PasswordFor7zFiles" } },
            { new Regex(".*"), new List<string>(){ "PasswordForAllFiles" } }

        }
    });
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
</details>

## Exceptions
RecursiveExtractor protects against [ZipSlip](https://snyk.io/research/zip-slip-vulnerability), [Quines, and Zip Bombs](https://en.wikipedia.org/wiki/Zip_bomb).
Calls to Extract will throw an `OverflowException` when a Quine or Zip bomb is detected.

Otherwise, invalid files found while crawling will emit a logger message and be skipped.  RecursiveExtractor uses NLog for logging.

# Feedback

If you have any issues or feature requests you can open a new [Issue](https://github.com/microsoft/RecursiveExtractor/issues/new).  

If you have an archive you are having trouble parsing, please include it in your feedback.

# Dependencies

Recursive Extractor uses a number of libraries to parse archives.

* [SharpZipLib](https://github.com/icsharpcode/SharpZipLib)
* [SharpCompress](https://github.com/adamhathcock/sharpcompress)
* [DiscUtils](https://github.com/discutils/discutils)

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
