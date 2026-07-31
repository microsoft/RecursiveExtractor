// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using BenchmarkDotNet.Running;
using System.Reflection;

namespace Microsoft.CST.RecursiveExtractor.Benchmarks
{
    /// <summary>
    /// Entry point for the benchmark runner.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Runs the requested benchmarks. Pass <c>--filter *</c> to run everything, or
        /// <c>--list flat</c> to see the available benchmarks.
        /// </summary>
        /// <param name="args">BenchmarkDotNet command line arguments.</param>
        public static void Main(string[] args) =>
            BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args, BenchmarkConfig.Create());
    }
}
