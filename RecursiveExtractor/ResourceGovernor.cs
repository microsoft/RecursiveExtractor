﻿using NLog;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Microsoft.CST.RecursiveExtractor
{
    /// <summary>
    /// Class that keeps track of bytes processed and time spent processing. Used to Detect ZipBombs etc.
    /// </summary>
    public class ResourceGovernor
    {
        private readonly ExtractorOptions options;
        private readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Create a governor with the given options.
        /// </summary>
        /// <param name="opts"></param>
        public ResourceGovernor(ExtractorOptions opts)
        {
            options = opts ?? new ExtractorOptions();
            GovernorStopwatch = new Stopwatch();
        }

        internal void ResetResourceGovernor(Stream stream)
        {
            Logger.Trace("ResetResourceGovernor()");
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream), "Stream must not be null.");
            }

            GovernorStopwatch = Stopwatch.StartNew();

            // Default value is we take MaxExtractedBytes (meaning, ratio is not defined)
            CurrentOperationProcessedBytesLeft = options.MaxExtractedBytes;
            if (options.MaxExtractedBytesRatio > 0)
            {
                long streamLength;
                try
                {
                    streamLength = stream.Length;
                }
                catch (Exception)
                {
                    throw new ArgumentException("Unable to get length of stream.");
                }

                // Ratio *is* defined, so the max value would be based on the stream length
                var maxViaRatio = (long)(options.MaxExtractedBytesRatio * streamLength);
                // Assign the samller of the two, accounting for MaxExtractedBytes == 0 means, 'no limit'.
                CurrentOperationProcessedBytesLeft = Math.Min(maxViaRatio, options.MaxExtractedBytes > 0 ? options.MaxExtractedBytes : long.MaxValue);
            }
        }

        /// <summary>
        ///     Stores the number of bytes left before we abort (denial of service).
        /// </summary>
        private long CurrentOperationProcessedBytesLeft = -1;

        /// <summary>
        /// Adjust the amount of bytes remaining.
        /// </summary>
        /// <param name="ChangeAmount"></param>
        internal void AdjustRemainingBytes(long ChangeAmount)
        {
            lock (this)
            {
                CurrentOperationProcessedBytesLeft += ChangeAmount;
            }
        }
        
        /// <summary>
        ///     Times extraction operations to avoid denial of service.
        /// </summary>
        internal Stopwatch GovernorStopwatch;

        /// <summary>
        ///     Checks to ensure we haven't extracted too many bytes, or taken too long. This exists primarily
        ///     to mitigate the risks of quines (archives that contain themselves) and zip bombs (specially
        ///     constructed to expand to huge sizes).
        ///     Ref: https://alf.nu/ZipQuine
        /// </summary>
        /// <param name="additionalBytes"> </param>
        internal void CheckResourceGovernor(long additionalBytes = 0)
        {
            Logger.ConditionalTrace("CheckResourceGovernor(duration={0}, bytes={1})", GovernorStopwatch.Elapsed.TotalMilliseconds, CurrentOperationProcessedBytesLeft);

            if (options.EnableTiming && GovernorStopwatch.Elapsed > options.Timeout)
            {
                throw new TimeoutException(string.Format($"Processing timeout exceeded: {GovernorStopwatch.Elapsed.TotalMilliseconds} ms."));
            }

            if (CurrentOperationProcessedBytesLeft - additionalBytes < 0)
            {
                throw new OverflowException("Too many bytes extracted, exceeding limit.");
            }
        }
    }
}
