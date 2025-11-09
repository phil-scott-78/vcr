using System.Threading.Channels;

namespace VcrSharp.Infrastructure.Recording;

/// <summary>
/// High-performance async queue for writing frame data to disk in background.
/// Decouples frame capture from file I/O to prevent blocking the capture loop.
/// </summary>
public class FrameWriteQueue : IAsyncDisposable
{
    private readonly Channel<FrameData> _channel;
    private readonly Task _writerTask;
    private readonly CancellationTokenSource _cancellationTokenSource;

    /// <summary>
    /// Represents frame data to be written to disk.
    /// </summary>
    private record FrameData(string Path, byte[] Data);

    /// <summary>
    /// Initializes a new instance of FrameWriteQueue.
    /// </summary>
    /// <param name="capacity">Maximum queue capacity (default: 1000 frames).</param>
    public FrameWriteQueue(int capacity = 1000)
    {
        // Use bounded channel with drop-write policy to prevent memory explosion
        _channel = Channel.CreateBounded<FrameData>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        _cancellationTokenSource = new CancellationTokenSource();

        // Start background writer task
        _writerTask = Task.Run(async () => await WriterLoopAsync(_cancellationTokenSource.Token));
    }

    /// <summary>
    /// Enqueues frame data for background writing.
    /// </summary>
    /// <param name="path">File path to write to.</param>
    /// <param name="data">Frame data bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask EnqueueAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(new FrameData(path, data), cancellationToken);
    }

    /// <summary>
    /// Background writer loop that consumes queued frames and writes them to disk.
    /// </summary>
    private async Task WriterLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var frame in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await File.WriteAllBytesAsync(frame.Path, frame.Data, cancellationToken);
            }
            catch (Exception)
            {
                // Log error but continue processing (don't crash writer thread)
                // In production, use proper logging framework
            }
        }
    }

    /// <summary>
    /// Completes writing and waits for all queued frames to be flushed to disk.
    /// </summary>
    public async Task CompleteAsync()
    {
        // Signal no more writes
        _channel.Writer.Complete();

        // Wait for all queued frames to be written
        await _writerTask;
    }

    /// <summary>
    /// Disposes the queue and cancels any pending writes.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            // Try to complete gracefully first
            _channel.Writer.Complete();

            // Wait for writer with timeout
            await _writerTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // If timeout or error, cancel forcefully
            _cancellationTokenSource.Cancel();
        }
        finally
        {
            _cancellationTokenSource.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}