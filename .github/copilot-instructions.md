# GitHub Copilot Instructions for RecursiveExtractor

## Project Overview

RecursiveExtractor is a cross-platform .NET library and CLI tool for parsing archive files and disk images, including nested archives. It provides a unified interface to extract arbitrary archives using libraries like SharpCompress and DiscUtils.

## Tech Stack

- **Language**: C# 10.0
- **Target Frameworks**: .NET Standard 2.0, .NET Standard 2.1, .NET 8.0, .NET 9.0, .NET 10.0
- **Testing Framework**: xUnit (based on project structure)
- **Key Dependencies**: SharpCompress, LTRData.DiscUtils, NLog, Glob

## Building and Testing

### Build Commands
```bash
# Build the entire solution
dotnet build RecursiveExtractor.sln

# Build a specific project
dotnet build RecursiveExtractor/RecursiveExtractor.csproj
```

### Test Commands
```bash
# Run all tests
dotnet test RecursiveExtractor.sln

# Run tests for a specific project
dotnet test RecursiveExtractor.Tests/RecursiveExtractor.Tests.csproj
dotnet test RecursiveExtractor.Cli.Tests/RecursiveExtractor.Cli.Tests.csproj
```

### Restore Packages
```bash
dotnet restore RecursiveExtractor.sln
```

## NuGet Configuration

⚠️ **Important**: The repository uses a private NuGet feed configured in `nuget.config`:
- The `nuget.config` file points to a private Azure DevOps feed: `https://pkgs.dev.azure.com/microsoft-sdl/General/_packaging/PublicRegistriesFeed/nuget/v3/index.json`
- **When working as an agent, you may need to temporarily modify `nuget.config` to use public NuGet feeds** (e.g., `https://api.nuget.org/v3/index.json`) to restore packages successfully
- **ALWAYS restore the `nuget.config` to its original configuration before completing your work**
- The original configuration must be preserved to maintain consistency with the team's workflow

Example of temporarily switching to public feed:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

## Code Style Guidelines

### Follow .editorconfig Settings
- Use 4 spaces for indentation (no tabs)
- CRLF line endings
- Open braces on new lines
- Use `var` for local variables when type is apparent
- Follow PascalCase for types, methods, and properties
- Interfaces should begin with 'I'
- Do not use `this.` qualifier unless necessary

### Naming Conventions
- **Interfaces**: Start with 'I' (e.g., `ICustomAsyncExtractor`)
- **Classes**: PascalCase (e.g., `FileEntry`, `Extractor`)
- **Methods**: PascalCase (e.g., `Extract`, `ExtractAsync`)
- **Properties**: PascalCase (e.g., `FullPath`, `Content`)
- **Parameters**: camelCase (e.g., `fileEntry`, `options`)

### C# Best Practices
- Enable nullable reference types (project uses `<Nullable>Enable</Nullable>`)
- Prefer pattern matching over `as` with null checks
- Use expression-bodied members for simple properties and accessors
- Prefer `null` propagation (`?.`) when appropriate
- Use async/await for I/O operations
- Implement both synchronous and asynchronous versions of extraction methods

## Testing Practices

### Test Organization
- Unit tests go in `RecursiveExtractor.Tests` project
- CLI tests go in `RecursiveExtractor.Cli.Tests` project
- Use xUnit as the testing framework
- Test files should mirror the structure of source files

### Test Naming
- Use descriptive test names that explain what is being tested
- Follow pattern: `MethodName_StateUnderTest_ExpectedBehavior`

### Test Data
- Test archives and files should be placed in appropriate test data directories
- Include edge cases: nested archives, encrypted files, malformed content, zip bombs

## Security Considerations

- The library includes protections against ZipSlip, Quines, and Zip Bombs
- Always validate file paths to prevent directory traversal attacks
- Handle malformed archives gracefully without crashes
- Implement proper resource cleanup (dispose streams, file handles)

## Documentation

- Add XML documentation comments for public APIs
- Keep README.md updated with new features or changes
- Document breaking changes clearly
- Include code examples for new public APIs

## Project Structure

```
RecursiveExtractor/               # Main library project
RecursiveExtractor.Tests/         # Unit tests for library
RecursiveExtractor.Cli/           # Command-line interface project
RecursiveExtractor.Cli.Tests/     # Tests for CLI
```

## Common Patterns

### Extraction Pattern
- Use `Extractor` class as the main entry point
- Support both `Extract()` (sync) and `ExtractAsync()` (async) methods
- Return `IEnumerable<FileEntry>` or `IAsyncEnumerable<FileEntry>`
- Each `FileEntry` contains a Stream of content that should be disposed properly

### Custom Extractors
- Implement `ICustomAsyncExtractor` for new archive formats
- Include `CanExtract()` method to detect file format via magic bytes
- Preserve stream position in `CanExtract()`
- Support both sync and async extraction

### Error Handling
- Throw `OverflowException` for detected quines or zip bombs
- Throw `TimeoutException` when timing limits are exceeded
- Log errors and skip invalid files during extraction
- Use `ExtractSelfOnFail` option to return original archive on failure

## Important Notes

- Multi-targeting means code must be compatible with .NET Standard 2.0
- Some features (like WIM support) are Windows-only
- The library automatically detects archive types
- Streams in FileEntry objects should be disposed by consumers
- Avoid multiple enumeration of extraction results
- For parallel processing, use batching mechanism as documented in README
