// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Reports;

namespace Microsoft.CST.RecursiveExtractor.Benchmarks
{
    /// <summary>
    /// Shared BenchmarkDotNet configuration.
    /// </summary>
    public static class BenchmarkConfig
    {
        /// <summary>
        /// Builds the configuration used by every benchmark in this assembly.
        /// </summary>
        /// <returns>The configuration.</returns>
        public static IConfig Create() =>
            DefaultConfig.Instance
                .AddDiagnoser(MemoryDiagnoser.Default)
                .AddColumn(RankColumn.Arabic)
                .WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(40));
    }
}
