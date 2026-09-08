namespace MinimalLlm.Tests;

/// <summary>
/// Emits an NDJSON fragment on demand, forever, pausing between lines. Nothing ends this
/// stream but cancellation — which is exactly what the cancellation tests need to observe.
/// </summary>
internal sealed class SlowNdjsonStream(int delayMs = 20) : Stream
{
    private static readonly byte[] Line =
        """{"message":{"role":"assistant","content":"tick "},"done":false}"""u8.ToArray();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.Delay(delayMs, cancellationToken);

        var payload = (byte[])[.. Line, (byte)'\n'];
        payload.CopyTo(buffer.Span[..payload.Length]);

        return payload.Length;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => 0; set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
