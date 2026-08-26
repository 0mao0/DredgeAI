using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare;

/// <summary>带属主流：Dispose 时一并释放底层 HTTP/S3 响应句柄，供流式下载直通使用。</summary>
public sealed class OwnedStream : Stream
{
    private readonly Stream _inner;
    private readonly IDisposable _owner;

    public OwnedStream(Stream inner, IDisposable owner)
    {
        _inner = inner;
        _owner = owner;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => _inner.Read(buffer);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void SetLength(long value) => _inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _owner.Dispose();
        }
        base.Dispose(disposing);
    }
}
