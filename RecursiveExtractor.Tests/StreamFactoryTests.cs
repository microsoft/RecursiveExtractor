using Microsoft.CST.RecursiveExtractor;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace RecursiveExtractor.Tests;

public class StreamFactoryTests
{
    private const int Cutoff = 1024;

    /// <summary>
    /// Wraps a stream so it reports the same characteristics as an archive library's entry
    /// stream: readable, but not seekable and unable to report its length.
    /// </summary>
    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStream(Stream inner) => _inner = inner;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private static byte[] Payload(int size)
    {
        var bytes = new byte[size];
        new Random(42).NextBytes(bytes);
        return bytes;
    }

    [Fact]
    public void StreamOfKnownLengthUnderCutoffIsBackedInMemory()
    {
        using var source = new MemoryStream(Payload(Cutoff / 2));
        using var backing = StreamFactory.GenerateAppropriateBackingStream(Cutoff, source, 4096);
        Assert.IsType<MemoryStream>(backing);
    }

    [Fact]
    public void StreamOfKnownLengthOverCutoffIsBackedOnDisk()
    {
        using var source = new MemoryStream(Payload(Cutoff * 2));
        using var backing = StreamFactory.GenerateAppropriateBackingStream(Cutoff, source, 4096);
        Assert.IsType<FileStream>(backing);
    }

    /// <summary>
    /// The regression guard for the temporary file per entry problem: an entry stream that cannot
    /// report its length must not be assumed to be large.
    /// </summary>
    [Fact]
    public void StreamOfUnknownLengthStaysInMemoryWhileSmall()
    {
        var content = Payload(Cutoff / 2);
        using var source = new NonSeekableStream(new MemoryStream(content));
        using var backing = StreamFactory.GenerateAppropriateBackingStream(Cutoff, source, 4096);

        var spillOver = Assert.IsType<SpillOverStream>(backing);
        source.CopyTo(spillOver);

        Assert.False(spillOver.HasSpilledToDisk);
        Assert.Equal(content.Length, spillOver.Length);

        spillOver.Position = 0;
        using var readBack = new MemoryStream();
        spillOver.CopyTo(readBack);
        Assert.Equal(content, readBack.ToArray());
    }

    [Fact]
    public void StreamOfUnknownLengthMovesToDiskWhenItGrowsPastTheCutoff()
    {
        var content = Payload(Cutoff * 4);
        using var source = new NonSeekableStream(new MemoryStream(content));
        using var backing = StreamFactory.GenerateAppropriateBackingStream(Cutoff, source, 4096);

        var spillOver = Assert.IsType<SpillOverStream>(backing);
        source.CopyTo(spillOver);

        Assert.True(spillOver.HasSpilledToDisk);
        Assert.Equal(content.Length, spillOver.Length);

        spillOver.Position = 0;
        using var readBack = new MemoryStream();
        spillOver.CopyTo(readBack);
        Assert.Equal(content, readBack.ToArray());
    }

    [Fact]
    public async Task SpillOverStreamPreservesContentWrittenAsynchronously()
    {
        var content = Payload(Cutoff * 4);
        using var source = new NonSeekableStream(new MemoryStream(content));
        using var spillOver = new SpillOverStream(Cutoff, 4096);

        await source.CopyToAsync(spillOver);

        Assert.True(spillOver.HasSpilledToDisk);

        spillOver.Position = 0;
        using var readBack = new MemoryStream();
        await spillOver.CopyToAsync(readBack);
        Assert.Equal(content, readBack.ToArray());
    }

    [Fact]
    public void SpillOverStreamPreservesContentWrittenOneByteAtATime()
    {
        var content = Encoding.ASCII.GetBytes(new string('a', Cutoff * 2));
        using var spillOver = new SpillOverStream(Cutoff, 4096);

        foreach (var b in content)
        {
            spillOver.WriteByte(b);
        }

        Assert.True(spillOver.HasSpilledToDisk);
        Assert.Equal(content.Length, spillOver.Length);

        spillOver.Position = 0;
        Assert.Equal(content, Enumerable.Range(0, content.Length).Select(_ => (byte)spillOver.ReadByte()).ToArray());
    }

    [Fact]
    public void SpillOverStreamSpillsWhenLengthIsSetPastTheCutoff()
    {
        using var spillOver = new SpillOverStream(Cutoff, 4096);
        spillOver.SetLength(Cutoff * 2);

        Assert.True(spillOver.HasSpilledToDisk);
        Assert.Equal(Cutoff * 2, spillOver.Length);
    }

    /// <summary>
    /// A null cutoff means the caller has opted out of the memory limit entirely.
    /// </summary>
    [Fact]
    public void NoCutoffAlwaysUsesMemory()
    {
        using var source = new NonSeekableStream(new MemoryStream(Payload(Cutoff * 4)));
        using var backing = StreamFactory.GenerateAppropriateBackingStream(null, source, 4096);
        Assert.IsType<MemoryStream>(backing);
    }

#if !NETFRAMEWORK
    /// <summary>
    /// The span and memory overloads are what <see cref="Stream.CopyToAsync(Stream)"/> actually
    /// calls on modern targets, so the spill decision has to be made there too.
    /// </summary>
    [Fact]
    public void SpillOverStreamSpillsWhenWrittenThroughSpanOverload()
    {
        var content = Payload(Cutoff * 4);
        using var spillOver = new SpillOverStream(Cutoff, 4096);

        spillOver.Write(new ReadOnlySpan<byte>(content));

        Assert.True(spillOver.HasSpilledToDisk);
        Assert.Equal(content.Length, spillOver.Length);

        spillOver.Position = 0;
        var readBack = new byte[content.Length];
        Assert.Equal(content.Length, spillOver.Read(new Span<byte>(readBack)));
        Assert.Equal(content, readBack);
    }

    [Fact]
    public async Task SpillOverStreamSpillsWhenWrittenThroughMemoryOverload()
    {
        var content = Payload(Cutoff * 4);
        using var spillOver = new SpillOverStream(Cutoff, 4096);

        await spillOver.WriteAsync(new ReadOnlyMemory<byte>(content));

        Assert.True(spillOver.HasSpilledToDisk);

        spillOver.Position = 0;
        var readBack = new byte[content.Length];
        Assert.Equal(content.Length, await spillOver.ReadAsync(new Memory<byte>(readBack)));
        Assert.Equal(content, readBack);
    }
#endif

    /// <summary>
    /// Content written before the spill has to survive it, including bytes that were overwritten
    /// in place while the stream was still in memory.
    /// </summary>
    [Fact]
    public void SpillOverStreamPreservesRewrittenContentAcrossTheSpill()
    {
        var content = Payload(Cutoff / 2);
        using var spillOver = new SpillOverStream(Cutoff, 4096);

        spillOver.Write(content, 0, content.Length);
        spillOver.Position = 4;
        spillOver.Write(new byte[] { 1, 2, 3 }, 0, 3);
        Assert.False(spillOver.HasSpilledToDisk);

        spillOver.Position = spillOver.Length;
        var tail = Payload(Cutoff * 2);
        spillOver.Write(tail, 0, tail.Length);
        Assert.True(spillOver.HasSpilledToDisk);

        content[4] = 1;
        content[5] = 2;
        content[6] = 3;

        spillOver.Position = 0;
        using var readBack = new MemoryStream();
        spillOver.CopyTo(readBack);
        Assert.Equal(content.Concat(tail).ToArray(), readBack.ToArray());
    }
}
