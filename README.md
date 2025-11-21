# About
![CodeQL](https://github.com/microsoft/RecursiveExtractor/workflows/CodeQL/badge.svg) ![Nuget](https://img.shields.io/nuget/v/Microsoft.CST.RecursiveExtractor?link=https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/&link=https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/) ![Nuget](https://img.shields.io/nuget/dt/Microsoft.CST.RecursiveExtractor?link=https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/&link=https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/)

Recursive Extractor is a Cross-Platform [.NET Standard 2.0 Library](#library) and [Command Line Program](#cli) for parsing archive files and disk images, including nested archives and disk images.

# Supported File Types
| | | |
|-|-|-|
| 7zip+ | ar    | bzip2 |
| deb   | dmg** | gzip  | 
| iso   | rar^  | tar   | 
| vhd   | vhdx  | vmdk  | 
| wim*  | xzip  | zip+  |

<details>
<summary>Details</summary>
<br/>
* Windows only<br/>
+ Encryption Supported<br/>
^ Encryption supported for Rar version 4 only<br/>
** Limited support. Unencrypted HFS+ volumes with certain compression schemes.
</details>

# Variants

## Command Line
### Installing
1. Ensure you have the latest [.NET SDK](https://dotnet.microsoft.com/download).
2. Run `dotnet tool install -g Microsoft.CST.RecursiveExtractor.Cli`

This adds `RecursiveExtractor` to your path so you can run it directly from your shell.

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
Recursive Extractor is available on NuGet as [Microsoft.CST.RecursiveExtractor](https://www.nuget.org/packages/Microsoft.CST.RecursiveExtractor/). Recursive Extractor targets netstandard2.0+ and the latest .NET, currently .NET 6.0, .NET 7.0 and .NET 8.0.

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
This code adapted from the Cli extracts the contents of given archive located at `options.Input` to a directory located at `options.Output`, including extracting failed archives as themselves.

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
You can provide passwords to use to decrypt archives, paired with a Regular Expression that will operate against the Name of the Archive to determine on which archives to try the passwords in each List.

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

<details>
<summary>Custom Extractors for Additional File Types</summary>
<br/>
You can extend RecursiveExtractor with custom extractors to support additional archive or file formats not natively supported. This is useful for formats like MSI, MSP, or other proprietary archive formats.

To create a custom extractor, implement the `ICustomAsyncExtractor` interface and register it with the extractor:

```csharp
using Microsoft.CST.RecursiveExtractor;
using Microsoft.CST.RecursiveExtractor.Extractors;
using System.IO;
using System.Collections.Generic;
using System.Linq;

// Example: Custom extractor for a hypothetical archive format with magic bytes "MYARC"
public class MyCustomExtractor : ICustomAsyncExtractor
{
    private readonly Extractor context;
    private static readonly byte[] MAGIC_BYTES = System.Text.Encoding.ASCII.GetBytes("MYARC");

    public MyCustomExtractor(Extractor ctx)
    {
        context = ctx;
    }

    // Check if this extractor can handle the file based on binary signatures
    public bool CanExtract(Stream stream)
    {
        if (stream == null || !stream.CanRead || !stream.CanSeek || stream.Length < MAGIC_BYTES.Length)
        {
            return false;
        }

        var initialPosition = stream.Position;
        try
        {
            stream.Position = 0;
            var buffer = new byte[MAGIC_BYTES.Length];
            var bytesRead = stream.Read(buffer, 0, MAGIC_BYTES.Length);
            
            return bytesRead == MAGIC_BYTES.Length && buffer.SequenceEqual(MAGIC_BYTES);
        }
        finally
        {
            // Always restore the original position
            stream.Position = initialPosition;
        }
    }

    // Implement extraction logic
    public IEnumerable<FileEntry> Extract(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
    {
        // Your extraction logic here
        // For example, parse the archive and yield FileEntry objects for each contained file
        yield break;
    }

    public async IAsyncEnumerable<FileEntry> ExtractAsync(FileEntry fileEntry, ExtractorOptions options, ResourceGovernor governor, bool topLevel = true)
    {
        // Your async extraction logic here
        yield break;
    }
}

// Register the custom extractor via constructor
var customExtractor = new MyCustomExtractor(null);
var extractor = new Extractor(new[] { customExtractor });

// Now the extractor will use your custom extractor for files matching your CanExtract criteria
var results = extractor.Extract("path/to/custom/archive.myarc");
```

Key points:
- The `CanExtract` method should check the stream's binary signature (like MiniMagic does) and return true if this extractor can handle the format
- Always preserve the stream's original position in `CanExtract`
- Custom extractors are provided via the constructor as an `IEnumerable<ICustomAsyncExtractor>`
- Custom extractors are only checked when the file type is UNKNOWN (not recognized by built-in extractors)
- Multiple custom extractors can be registered; they are checked in the order provided
- Custom extractors are invoked for both synchronous and asynchronous extraction paths

</details>

## Exceptions
RecursiveExtractor protects against [ZipSlip](https://snyk.io/research/zip-slip-vulnerability), [Quines, and Zip Bombs](https://en.wikipedia.org/wiki/Zip_bomb).
Calls to Extract will throw an `OverflowException` when a Quine or Zip bomb is detected and a `TimeOutException` if `EnableTiming` is set and the specified time period has elapsed before completion.

Otherwise, invalid files found while crawling will emit a logger message and be skipped.  You can also enable `ExtractSelfOnFail` to return the original archive file on an extraction failure.

## Notes on Enumeration

### Multiple Enumeration
You should not iterate the Enumeration returned from the `Extract` and `ExtractAsync` interfaces multiple times, if you need to do so, convert the Enumeration to an in memory collection first.

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
If you are working with a very large archive or in particularly constrained environment you can reduce memory and file handle usage for the Content streams in each FileEntry by disposing as you iterate.

```csharp
var results = extractor.Extract(path);
foreach(var file in results)
{
    using var theStream = file.Content;
    // Do something with the stream.
    _ = theStream.ReadByte();
// The stream is disposed here by the using statement
} 
```

# Feedback

If you have any issues or feature requests (for example, supporting other formats) you can open a new [Issue](https://github.com/microsoft/RecursiveExtractor/issues/new).  

If you are having trouble parsing a specific archive of one of the supported formats, it is helpful if you can include an sample archive with your report that demonstrates the issue.

# Dependencies

Recursive Extractor aims to provide a unified interface to extract arbitrary archives and relies on a number of libraries to parse the archives.

* [SharpCompress](https://github.com/adamhathcock/sharpcompress)
* [LTRData/DiscUtils](https://github.com/LTRData/discutils)

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
