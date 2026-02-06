using Xunit;

namespace RecursiveExtractor.Tests.ExtractorTests;

/// <summary>
/// Defines a shared test collection so that all extractor test classes share a single
/// <see cref="BaseExtractorTestClass"/> fixture instance. The fixture is created once
/// before the first test runs and disposed once after the last test completes,
/// avoiding the race condition where parallel class-level fixtures prematurely
/// delete the shared temp directory while other classes are still running.
/// </summary>
[CollectionDefinition(Name)]
public class ExtractorTestCollection : ICollectionFixture<BaseExtractorTestClass>
{
    public const string Name = "Extractor Tests";
}
