# About
![CodeQL](https://github.com/microsoft/RecursiveExtractor/workflows/CodeQL/badge.svg) ![Nuget](https://img.shields.io/nuget/v/Microsoft.CST.RecursiveExtractor?link=https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/&link=https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/) ![Nuget](https://img.shields.io/nuget/dt/Microsoft.CST.RecursiveExtractor?link=https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/&link=https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/)

Recursive Extractor is a Cross-Platform [.NET Standard 2.0 Library](#library) and [Command Line Program](#cli) for parsing archive files and disk images, including nested archives and disk images.

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

## Command Line
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
    <li><i>allow-globs</i>: A comma separated list of glob patterns to require each extracted file match.</li>
    <li><i>deny-globs</i>: A comma separated list of glob patterns to require each extracted file not match.</li>
    <li><i>raw-extensions</i>: A comma separated list of file extensions to not recurse into.</li>
    <li><i>no-recursion</i>: Don't recurse into sub-archives.</li>
    <li><i>single-thread</i>: Don't attempt to parallelize extraction.</li>
    <li><i>printnames</i>: Output the name of each file extracted.</li>
    
</ul>

For example, to extract only ".cs" files:
```
RecursiveExtractor --input archive.ext --output outputDirectory --allow-globs **/*.cs
```

Run `RecursiveExtractor --help` for more details.
</details>

## .NET Standard Library
Recursive Extractor is available on NuGet as [Microsoft.CST.RecursiveExtractor](https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/). Recursive Extractor targets netstandard2.0+ and the latest .NET, currently .NET 6.0 and .NET 7.0.

### Usage

The most basic usage is to enumerate through all the files in the archive provided and do something with their contents as a Stream.

```csharp
using Microsoft.CST.RecursiveExtractor;

var path = "path/to/file";
var extractor = new Extractor();
foreach(var file in extractor.Extract(path))
{
    doSomething(file.Content); //Do Something with the file contents (a Stream)
}
```

<details>
<summary>Extracting to Disk</summary>
<br/>
This code adapted from the Cli extracts the contents of given archive located at `options.Input`
to a directory located at `options.Output`, including extracting failed archives as themselves.

```csharp
using Microsoft.CST.RecursiveExtractor;

var extractor = new Extractor();
var extractorOptions = new ExtractorOptions()
{
    ExtractSelfOnFail = true,
};
extractor.ExtractToDirectory(options.Output, options.Input, extractorOptions);
```
</details>
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
public DateTime CreateTime { get; }
public DateTime ModifyTime { get; }
public DateTime AccessTime { get; }
```
</details>

<details>
<summary>Extracting Encrypted Archives</summary>
<br/>
You can provide passwords to use to decrypt archives, paired with a Regex that will operate against the Name of the Archive to determine on which archies to try the password

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
Calls to Extract will throw an `OverflowException` when a Quine or Zip bomb is detected and a `TimeOutException` if `EnableTiming` is set and the specified time period has elapsed before completion.

Otherwise, invalid files found while crawling will emit a logger message and be skipped.  RecursiveExtractor uses NLog for logging.

## Notes on Enumeration

### Multiple Enumeration
You should not iterate the Enumeration returned from the `Extract` and `ExtractAsync` interfaces multiple times, if you need to do so, convert the Enumeration to the collection of your choice first.

### Parallel Enumeration
If you want to enumerate the output with parallelization you should use a batching mechanism, for example:

```csharp
var extractedEnumeration = Extract(fileEntry, opts);
using var enumerator = extractedEnumeration.GetEnumerator();
ConcurrentBag<FileEntry> entryBatch = new();
bool moreAvailable = enumerator.MoveNext();
while (moreAvailable)
{
    entryBatch = new();
    for (int i = 0; i < BatchSize; i++)
    {
        entryBatch.Add(enumerator.Current);
        moreAvailable = enumerator.MoveNext();
        if (!moreAvailable)
        {
            break;
        }
    }

    if (entryBatch.Count == 0)
    {
        break;
    }

    // Run your parallel processing on the batch
    Parallel.ForEach(entryBatch, new ParallelOptions() { CancellationToken = cts.Token }, entry =>
    {
        // Do something with each FileEntry
    }
}
```

### Disposing During Enumeration
If you are working with a very large archive or in particularly constrained environment you can reduce memory/file handle usage for the Content streams in each FileEntry by disposing as you iterate.

```csharp
var results = extractor.Extract(path);
foreach(var file in results)
{
    using var theStream = file.Content;
    // Do something with the stream.
    _ = theStream.ReadByte();
    // The stream is disposed here from the using statement
} 
```

# Feedback

If you have any issues or feature requests (for example, supporting other formats) you can open a new [Issue](https://github.com/microsoft/RecursiveExtractor/issues/new).  

If you are having trouble parsing a specific archive of one of the supported formats, it is helpful if you can include an sample archive with your report that demonstrates the issue.

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
