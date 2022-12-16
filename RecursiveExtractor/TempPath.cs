// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.IO;

namespace Microsoft.CST.RecursiveExtractor;

/// <summary>
/// Helper class to get temporary file path
/// </summary>
public static class TempPath
{
    /// <summary>
    /// Use this instead of Path.GetTempFileName()
    /// Path.GetTempFileName() generates predictable file names and is inherently unreliable and insecure.
    /// Additionally, the method will raise an IOException if it is used to create more than 65535 files
    /// without deleting previous temporary files.
    /// </summary>
    /// <returns></returns>
    public static string GetTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    }
}