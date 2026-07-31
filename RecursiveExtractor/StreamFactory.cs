using System;
using System.IO;

namespace Microsoft.CST.RecursiveExtractor;

/// <summary>
/// Static class with static Methods to generate the correct type of backing stream based on the length of hte target stream and the <see cref="ExtractorOptions"/>.
/// </summary>
public static class StreamFactory
{
    /// <summary>
    /// Generate the appropriate backing Stream to copy a target Stream to.
    /// </summary>
    /// <param name="opts">Extractor Options</param>
    /// <param name="targetStream">The target stream to be backed</param>
    /// <returns></returns>
    public static Stream GenerateAppropriateBackingStream(ExtractorOptions opts, Stream targetStream)
    {
        return GenerateAppropriateBackingStream(opts.MemoryStreamCutoff, targetStream, opts.FileStreamBufferSize);
    }
    
    /// <summary>
    /// Generate the appropriate backing Stream to copy a target Stream to.
    /// </summary>
    /// <param name="opts">Extractor Options</param>
    /// <param name="targetStreamLength">The length of stream to be backed</param>
    /// <returns></returns>
    public static Stream GenerateAppropriateBackingStream(ExtractorOptions opts, long targetStreamLength)
    {
        return GenerateAppropriateBackingStream(opts.MemoryStreamCutoff, targetStreamLength, opts.FileStreamBufferSize);
    }

    /// <summary>
    /// Generate the appropriate backing Stream to copy a target Stream to.
    /// </summary>
    /// <param name="memoryStreamCutoff">Largest size in bytes to use a memory stream for backing</param>
    /// <param name="targetStream">Stream to be copied to the new backer</param>
    /// <param name="fileStreamBufferSize">Size of FileStream's buffers</param>
    /// <returns></returns>
    internal static Stream GenerateAppropriateBackingStream(int? memoryStreamCutoff, Stream targetStream, int fileStreamBufferSize)
    {
        if (memoryStreamCutoff is not int cutoff)
        {
            return new MemoryStream();
        }

        try
        {
            if (targetStream.Length > cutoff)
            {
                return GenerateDeleteOnCloseFileStream(fileStreamBufferSize);
            }
            return new MemoryStream();
        }
        catch (Exception)
        {
            // The length isn't knowable without draining the stream, which is the normal case for
            // archive entry streams. Rather than assume the content is large and pay for a
            // temporary file on every entry, buffer in memory and move to disk only if the content
            // turns out to exceed the cutoff.
            return new SpillOverStream(cutoff, fileStreamBufferSize);
        }
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="memoryStreamCutoff"></param>
    /// <param name="targetStreamLength"></param>
    /// <param name="fileStreamBufferSize"></param>
    /// <returns></returns>
    internal static Stream GenerateAppropriateBackingStream(int? memoryStreamCutoff, long targetStreamLength, int fileStreamBufferSize)
    {
        try
        {
            if (targetStreamLength > memoryStreamCutoff)
            {
                return GenerateDeleteOnCloseFileStream(fileStreamBufferSize);
            }
            return new MemoryStream();
        }
        catch (Exception)
        {
            return GenerateDeleteOnCloseFileStream(fileStreamBufferSize);
        }
    }

    internal static Stream GenerateDeleteOnCloseFileStream(int fileStreamBufferSize)
    {
        return new FileStream(TempPath.GetTempFilePath(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, fileStreamBufferSize, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
    }
}