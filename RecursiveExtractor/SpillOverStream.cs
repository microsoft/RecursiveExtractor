using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CST.RecursiveExtractor;

/// <summary>
/// A seekable, read-write backing stream that starts in memory and moves itself to a
/// delete-on-close <see cref="FileStream"/> once the content written to it exceeds a cutoff.
/// </summary>
/// <remarks>
/// This exists for the case where the size of the content being buffered is not knowable up
/// front. Most archive libraries expose entry streams that cannot seek and throw when
/// <see cref="Stream.Length"/> is read, so choosing a backing store before the copy means
/// either assuming everything is small (risking memory exhaustion) or assuming everything is
/// large (paying for a temporary file per entry). Deferring the decision until the content
/// actually crosses the cutoff avoids both.
/// </remarks>
internal sealed class SpillOverStream : Stream
{
    private readonly long _memoryStreamCutoff;
    private readonly int _fileStreamBufferSize;
    private Stream _backingStream;

    /// <summary>
    /// Creates a stream backed by memory until <paramref name="memoryStreamCutoff"/> bytes are
    /// written to it.
    /// </summary>
    /// <param name="memoryStreamCutoff">Largest size in bytes to keep in memory.</param>
    /// <param name="fileStreamBufferSize">Buffer size for the FileStream spilled to.</param>
    internal SpillOverStream(long memoryStreamCutoff, int fileStreamBufferSize)
    {
        _memoryStreamCutoff = memoryStreamCutoff;
        _fileStreamBufferSize = fileStreamBufferSize;
        _backingStream = new MemoryStream();
    }

    /// <summary>
    /// True once the content has grown past the cutoff and moved to a file on disk.
    /// </summary>
    internal bool HasSpilledToDisk { get; private set; }

    /// <inheritdoc />
    public override bool CanRead => _backingStream.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => _backingStream.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => _backingStream.CanWrite;

    /// <inheritdoc />
    public override long Length => _backingStream.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _backingStream.Position;
        set => _backingStream.Position = value;
    }

    /// <inheritdoc />
    public override void Flush() => _backingStream.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _backingStream.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => _backingStream.Read(buffer, offset, count);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        _backingStream.ReadAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    public override int ReadByte() => _backingStream.ReadByte();

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => _backingStream.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value)
    {
        SpillIfNeeded(value);
        _backingStream.SetLength(value);
    }

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count)
    {
        SpillIfNeeded(_backingStream.Position + count);
        _backingStream.Write(buffer, offset, count);
    }

    /// <inheritdoc />
    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        SpillIfNeeded(_backingStream.Position + count);
        await _backingStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void WriteByte(byte value)
    {
        SpillIfNeeded(_backingStream.Position + 1);
        _backingStream.WriteByte(value);
    }

#if !NETSTANDARD2_0
    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => _backingStream.Read(buffer);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _backingStream.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        SpillIfNeeded(_backingStream.Position + buffer.Length);
        _backingStream.Write(buffer);
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        SpillIfNeeded(_backingStream.Position + buffer.Length);
        await _backingStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
#endif

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _backingStream.Dispose();
        }

        base.Dispose(disposing);
    }

    private void SpillIfNeeded(long requiredLength)
    {
        if (HasSpilledToDisk || requiredLength <= _memoryStreamCutoff)
        {
            return;
        }

        // Only reachable while the backing store is still the MemoryStream created in the
        // constructor, since HasSpilledToDisk flips at the same time the field is replaced.
        var memoryStream = (MemoryStream)_backingStream;
        var fileStream = StreamFactory.GenerateDeleteOnCloseFileStream(_fileStreamBufferSize);

        try
        {
            memoryStream.WriteTo(fileStream);
            fileStream.Position = memoryStream.Position;
        }
        catch
        {
            fileStream.Dispose();
            throw;
        }

        _backingStream = fileStream;
        HasSpilledToDisk = true;
        memoryStream.Dispose();
    }
}
