# RecursiveExtractor.Benchmarks

[BenchmarkDotNet](https://benchmarkdotnet.org/) harness for measuring RecursiveExtractor's
throughput and, more importantly, for separating RecursiveExtractor's own overhead from the cost
of the underlying format libraries (SharpCompress, DiscUtils).

All archives are generated in memory at setup time by `SyntheticArchives`, so the benchmarks are
deterministic and do not depend on the test project's assets.

## Running

Benchmarks must be run against an optimized build:

```bash
# Everything (slow)
dotnet run -c Release -f net8.0 --project RecursiveExtractor.Benchmarks -- --filter "*"

# A single class
dotnet run -c Release -f net8.0 --project RecursiveExtractor.Benchmarks -- --filter "*ZipExtractionBenchmarks*"

# Fewer iterations, good enough for triage
dotnet run -c Release -f net8.0 --project RecursiveExtractor.Benchmarks -- --filter "*" --job short

# List what is available
dotnet run -c Release -f net8.0 --project RecursiveExtractor.Benchmarks -- --list flat
```

The project multi-targets `net8.0`, `net9.0` and `net10.0`, so `-f` is required. Results are
written to `BenchmarkDotNet.Artifacts/results` as markdown, CSV and HTML.

## What each class measures

| Class | Question it answers |
| --- | --- |
| `ZipExtractionBenchmarks` | How much does RecursiveExtractor add on top of raw SharpCompress for the same zip? Isolates quine detection and archive type detection so their share of total time can be read directly. |
| `ArchiveFormatBenchmarks` | How much of the cost is the container/compression itself? Same payload through zip (stored and deflate), tar, tar.gz, tar.bz2 and 7z, each against a raw SharpCompress reader baseline. |
| `QuineDetectionBenchmarks` | What does `Extractor.IsQuine` cost per entry — on the fast path, when lengths collide, and in the worst case where an ancestor has the same name and length and a full byte comparison is required. |
| `NestedArchiveBenchmarks` | How does the pipeline scale as archives nest inside archives? |
| `FileTypeDetectionBenchmarks` | What does `MiniMagic.DetectFileType` cost per entry, and how much worse is it when the entry is backed by a `FileStream` instead of a `MemoryStream`? |
| `ExtractToDirectoryBenchmarks` | What does `ExtractorOptions.Parallel` buy when writing extracted entries to disk? |

## Interpreting the numbers

- `SharpCompress: decompress only` is the floor. Nothing RecursiveExtractor does can go faster.
- `SharpCompress: decompress + buffer` adds the seekable copy of each entry that
  `FileEntry.Content` requires, which is the price of type detection and quine detection.
- The gap between `SharpCompress: decompress + buffer` and `RE: Extract` is RecursiveExtractor's
  actual overhead.
- `RE: Extract (no recurse)` skips the resource governor, quine check and type detection for each
  entry, so the delta against `RE: Extract (recurse)` bounds the total cost of recursion bookkeeping.
- Absolute times are hardware and noise dependent; compare the `Ratio` column, not the means.

## What these benchmarks found

Measured on a 1000 entry x 4 KiB deflate zip, ShortRun, net8.0:

- **Quine detection is not a bottleneck.** Running `IsQuine` over all 1000 entries costs ~20 us
  with zero allocations: roughly 0.06% of the ~30 ms extraction, because `AreIdentical` compares
  `Length` and `Name` before it ever compares bytes. The full content comparison only runs when an
  ancestor has both the same size and the same name.
- **Archive type detection is ~1%** of extraction over the same entries.
- **RecursiveExtractor's own overhead is roughly 10-20%** over a raw SharpCompress reader that does
  the same buffering. The remainder is the format library.
- **The format dominates everything else.** The same 200 x 8 KiB payload spans two orders of
  magnitude between containers, from ~0.3 ms for stored zip to ~100 ms for tar.bz2.
- **`ExtractAsync` used to be ~28x slower than `Extract` for zip.** `ZipExtractor`'s async path
  handed SharpCompress's non-seekable entry stream to `StreamFactory`, whose `Length` access threw
  and fell back to a delete-on-close temporary file, one per entry. Fixed by passing the known
  `zipEntry.Size` and by making the unknown-length fallback a `SpillOverStream` that only moves to
  disk if the content actually exceeds `MemoryStreamCutoff`. Async is now within ~15% of sync, and
  7z (which hits the same fallback through `FileEntry`) got ~6x faster.
- **7z and rar used to allocate ~26x the baseline.** `SevenZipExtractor` and `RarExtractor` never
  disposed the stream returned by `entry.OpenEntryStream()`. Because 7z is a solid format,
  SharpCompress builds a fresh decoder chain per entry, and each one holds a 1 MiB LZMA dictionary
  plus a 128 KiB read cache. At 200 entries that is ~230 MiB retained, nearly all of it on the large
  object heap. The equivalent baseline in `ArchiveFormatBenchmarks.ReadWithArchiveApi` used `using`,
  which is what made the gap visible. `FileEntry` copies the content into its own backing stream, so
  the source can be disposed before yielding. 7z is now 1.10x the baseline on both time and
  allocations, down from 26.35x: 320 ms -> 31 ms and 240 MB -> 10 MB.
  `EntryStreamDisposalTests` guards against a regression.
