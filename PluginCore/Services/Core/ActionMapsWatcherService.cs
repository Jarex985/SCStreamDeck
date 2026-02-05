using System.Collections.Concurrent;
using SCStreamDeck.Common;
using SCStreamDeck.Logging;
using SCStreamDeck.Models;

namespace SCStreamDeck.Services.Core;

/// <summary>
///     Watches for changes to Star Citizen user keybinding overrides (actionmaps.xml) and emits debounced notifications.
/// </summary>
public sealed class ActionMapsWatcherService : IDisposable
{
    private readonly ConcurrentDictionary<SCChannel, FileSystemWatcher> _watchersByChannel = new();
    private readonly ConcurrentDictionary<SCChannel, Timer> _debounceTimersByChannel = new();
    private readonly ConcurrentDictionary<SCChannel, object> _debounceLocksByChannel = new();

    private volatile bool _disposed;

    /// <summary>
    ///     Fired after a debounce window when actionmaps.xml changes for a channel.
    /// </summary>
    public event Func<SCChannel, Task>? ActionMapsChanged;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach ((SCChannel _, Timer timer) in _debounceTimersByChannel)
        {
            try
            {
                timer.Dispose();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        foreach ((SCChannel _, FileSystemWatcher watcher) in _watchersByChannel)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        _debounceTimersByChannel.Clear();
        _watchersByChannel.Clear();
        _debounceLocksByChannel.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Starts or updates a watcher for the specified channel.
    ///     Watches the entire channel folder (IncludeSubdirectories) but filters events to actionmaps.xml only.
    /// </summary>
    public void StartOrUpdate(SCChannel channel, string channelPath)
    {
        if (_disposed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(channelPath) || !Directory.Exists(channelPath))
        {
            Log.Debug($"[{nameof(ActionMapsWatcherService)}] Skip watch for {channel}: channelPath missing");
            Stop(channel);
            return;
        }

        if (!SecurePathValidator.TryNormalizePath(channelPath, out string normalizedChannelPath))
        {
            Log.Warn($"[{nameof(ActionMapsWatcherService)}] Skip watch for {channel}: invalid channelPath '{channelPath}'");
            Stop(channel);
            return;
        }

        if (_watchersByChannel.TryGetValue(channel, out FileSystemWatcher? existing))
        {
            // If the watch root hasn't changed, keep the existing watcher.
            if (string.Equals(existing.Path, normalizedChannelPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Stop(channel);
        }

        FileSystemWatcher watcher = new(normalizedChannelPath)
        {
            Filter = SCConstants.Files.ActionMapsFileName,
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        watcher.Changed += (_, e) => OnRawEvent(channel, e);
        watcher.Created += (_, e) => OnRawEvent(channel, e);
        watcher.Deleted += (_, e) => OnRawEvent(channel, e);
        watcher.Renamed += (_, e) => OnRawEvent(channel, e);

        watcher.EnableRaisingEvents = true;
        _watchersByChannel[channel] = watcher;

        Log.Info(
            $"[{nameof(ActionMapsWatcherService)}] Watching {channel} for '{SCConstants.Files.ActionMapsFileName}' changes under: {normalizedChannelPath}");
    }

    public void StartOrUpdate(IReadOnlyList<SCInstallCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        foreach (SCInstallCandidate candidate in candidates)
        {
            StartOrUpdate(candidate.Channel, candidate.ChannelPath);
        }
    }

    public void Stop(SCChannel channel)
    {
        if (_watchersByChannel.TryRemove(channel, out FileSystemWatcher? watcher))
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }

        if (_debounceTimersByChannel.TryRemove(channel, out Timer? timer))
        {
            try
            {
                timer.Dispose();
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    private void OnRawEvent(SCChannel channel, FileSystemEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        // This watcher is already filtered to actionmaps.xml, but log defensively.
        Log.Debug($"[{nameof(ActionMapsWatcherService)}] FS event for {channel}: {e.ChangeType} {e.FullPath}");

        ScheduleDebouncedNotification(channel, TimeSpan.FromMilliseconds(1000));
    }

    private void ScheduleDebouncedNotification(SCChannel channel, TimeSpan debounce)
    {
        object gate = _debounceLocksByChannel.GetOrAdd(channel, _ => new object());
        lock (gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_debounceTimersByChannel.TryGetValue(channel, out Timer? existing))
            {
                existing.Change(debounce, Timeout.InfiniteTimeSpan);
                return;
            }

            Timer timer = new(_ =>
            {
                _ = Task.Run(async () => await FireDebouncedAsync(channel).ConfigureAwait(false));
            }, null, debounce, Timeout.InfiniteTimeSpan);

            _debounceTimersByChannel[channel] = timer;
        }
    }

    private async Task FireDebouncedAsync(SCChannel channel)
    {
        try
        {
            if (_disposed)
            {
                return;
            }

            Log.Info($"[{nameof(ActionMapsWatcherService)}] Debounced actionmaps.xml change detected for {channel}");

            Func<SCChannel, Task>? handler = ActionMapsChanged;
            if (handler == null)
            {
                return;
            }

            await handler(channel).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Err($"[{nameof(ActionMapsWatcherService)}] Debounced change handling failed for {channel}", ex);
        }
    }
}
