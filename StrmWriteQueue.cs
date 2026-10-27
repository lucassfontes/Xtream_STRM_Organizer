using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

namespace XtreamStrmOrganizer;

/// <summary>
/// Bounded, concurrent, non-destructive .strm writer. Producers do not wait for each disk write.
/// Each target is created by an atomic rename from a temporary file, with no overwrite.
/// </summary>
internal sealed class StrmWriteQueue : IAsyncDisposable
{
    private sealed record Request(string Folder, string Path, string Content, string Target);
    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private readonly Channel<Request> _channel;
    private readonly Task[] _workers;
    private readonly ConcurrentDictionary<string, Lazy<bool>> _createdFolders = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _owners = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string> _onExisting;
    private readonly Action _onCreated;
    private readonly Action<Exception> _onError;
    private readonly Action<int> _onPendingChange;
    private readonly CancellationToken _cancel;
    private bool _completed;

    public StrmWriteQueue(int concurrency, CancellationToken cancel,
        Action<string> onExisting, Action onCreated, Action<Exception> onError, Action<int> onPendingChange)
    {
        _cancel = cancel;
        _onExisting = onExisting;
        _onCreated = onCreated;
        _onError = onError;
        _onPendingChange = onPendingChange;
        _channel = Channel.CreateBounded<Request>(new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _workers = Enumerable.Range(0,concurrency).Select(_ => Task.Run(ConsumeAsync)).ToArray();
    }

    public async ValueTask<string> EnqueueAsync(string folder, string name, string id, string url, CancellationToken token, string? target = null)
    {
        token.ThrowIfCancellationRequested();
        var path = Path.Combine(folder, name + ".strm");
        var firstOwner = _owners.GetOrAdd(path, id);
        if (firstOwner != id)
            path = Path.Combine(folder, name + " [" + id + "].strm");

        // Exactly one filesystem existence check, in a consumer. This avoids
        // two disk round-trips for every new file and preserves any existing STRM.
        _onPendingChange(1);
        try
        {
            await _channel.Writer.WriteAsync(new Request(folder, path, url + "\n", target ?? WarningText.Item("item", name)), token);
        }
        catch
        {
            _onPendingChange(-1);
            throw;
        }
        return path;
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var job in _channel.Reader.ReadAllAsync(_cancel))
            {
                try
                {
                    await WriteOneAsync(job, _cancel);
                }
                catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { throw; }
                catch (Exception ex) { _onError(new MediaWarningException("o STRM " + job.Target, "não foi criado", ex)); }
                finally { _onPendingChange(-1); }
            }
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
        {
            // On cancellation, never touch files not yet dequeued.
        }
    }

    private async Task WriteOneAsync(Request job, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (File.Exists(job.Path))
        {
            _onExisting(job.Path);
            return;
        }

        // Create a directory just once, safely, even when several workers share it.
        // On failure, remove its cache entry so subsequent attempts may retry.
        var folderEntry = _createdFolders.GetOrAdd(job.Folder,
            static path => new Lazy<bool>(() => { Directory.CreateDirectory(path); return true; },
                LazyThreadSafetyMode.ExecutionAndPublication));
        try { _ = folderEntry.Value; }
        catch
        {
            _createdFolders.TryRemove(job.Folder, out _);
            throw;
        }

        string? temporary = Path.Combine(job.Folder, ".xtream-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, job.Content, Utf8NoBom, token);
            token.ThrowIfCancellationRequested();
            try
            {
                // The overload without overwrite never replaces an existing destination.
                File.Move(temporary, job.Path);
                _onCreated();
            }
            catch (IOException) when (File.Exists(job.Path))
            {
                _onExisting(job.Path);
            }
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public async Task CompleteAsync()
    {
        if (!_completed)
        {
            _completed = true;
            _channel.Writer.TryComplete();
        }
        await Task.WhenAll(_workers);
    }

    public async ValueTask DisposeAsync()
    {
        // CompleteAsync may already have been called by a successful run.
        await CompleteAsync();
    }
}
