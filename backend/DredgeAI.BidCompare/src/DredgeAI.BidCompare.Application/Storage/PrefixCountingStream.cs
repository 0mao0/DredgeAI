using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.Storage;

/// <summary>
/// 上传直通流：先吐出已读取的嗅探头部，再继续转发请求流，并累计总字节数。
/// 使上传无需整文件内存缓冲（MemoryStream + ToArray 双份），IFormFile 流可直接落存储。
/// </summary>
internal sealed class PrefixCountingStream : Stream
{
    private readonly byte[] _prefix;
    private readonly int _prefixLength;
    private readonly Stream _inner;
    private int _prefixOffset;

    public PrefixCountingStream(byte[] prefix, int prefixLength, Stream inner)
    {
        _prefix = prefix;
        _prefixLength = prefixLength;
        _inner = inner;
    }

    /// <summary>已通过本流读出的总字节数（= 实际上传大小，上传完成后写入实体 FileSize）。</summary>
    public long TotalBytesRead { get; private set; }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => TotalBytesRead;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = ReadPrefix(buffer.AsSpan(offset, count));
        if (read < count)
        {
            read += _inner.Read(buffer, offset + read, count - read);
        }
        TotalBytesRead += read;
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = ReadPrefix(buffer);
        if (read < buffer.Length)
        {
            read += _inner.Read(buffer[read..]);
        }
        TotalBytesRead += read;
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = ReadPrefix(buffer.Span);
        if (read < buffer.Length)
        {
            read += await _inner.ReadAsync(buffer[read..], cancellationToken);
        }
        TotalBytesRead += read;
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = ReadPrefix(buffer.AsSpan(offset, count));
        if (read < count)
        {
            read += await _inner.ReadAsync(buffer, offset + read, count - read, cancellationToken);
        }
        TotalBytesRead += read;
        return read;
    }

    private int ReadPrefix(Span<byte> buffer)
    {
        if (_prefixOffset >= _prefixLength)
        {
            return 0;
        }
        var n = Math.Min(buffer.Length, _prefixLength - _prefixOffset);
        _prefix.AsSpan(_prefixOffset, n).CopyTo(buffer);
        _prefixOffset += n;
        return n;
    }

    public override void Flush()
    {
    }

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
