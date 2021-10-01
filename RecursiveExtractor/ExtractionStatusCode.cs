// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.RecursiveExtractor
{
    /// <summary>
    /// Status codes for the ExtractToDirectory functions. 
    /// </summary>
    public enum ExtractionStatusCode
    {
        /// <summary>
        /// Extraction generally successful.
        /// </summary>
        Ok,
        /// <summary>
        /// One of the arguments provided was invalid.
        /// </summary>
        BadArgument,
        /// <summary>
        /// There was a critical error extracting.
        /// </summary>
        Failure
    }
}