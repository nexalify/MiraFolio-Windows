using System.Buffers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MiraFolio.Core.Models;
using MiraFolio.Core.Utilities;

namespace MiraFolio.Core.Services;

public class ImageSelector : IImageSelector, IDisposable
{
    private static readonly HashSet<string> SupportedExtensions = new(
        [".jpg", ".jpeg", ".png", ".bmp", ".webp"],
        StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ImageSelector> _logger;
    private readonly Random _random = new();
    private readonly string _cachePath;
    private readonly ISettingsService? _settingsService;
    private HashSet<string>? _configuredRootFolders;

    // Per-folder cache: file list + pre-categorized orientation buckets + watcher
    private readonly Dictionary<string, FolderCache> _folderCaches = new(StringComparer.OrdinalIgnoreCase);
    // Per-file dimension cache — keyed by absolute file path, persisted to disk as folder -> relative path maps
    private readonly Dictionary<string, CachedDimension> _dimensionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private readonly object _cachePersistenceLock = new();

    private const int ScanBatchSize = 100;
    private const int MaxCandidatePoolsPerFolder = 8;
    private const int MaxPersistedDimensionEntries = 200_000;
    private const int CacheReadBufferSize = 64 * 1024;

    // Debounce timers: coalesce rapid FileSystemWatcher events into one scan per folder
    private readonly Dictionary<string, Timer> _debounceTimers = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(2);

    public ImageSelector(ILogger<ImageSelector> logger, ISettingsService settingsService)
        : this(logger, settingsService, cachePath: null)
    {
    }

    internal ImageSelector(
        ILogger<ImageSelector> logger,
        ISettingsService settingsService,
        string? cachePath)
        : this(logger, cachePath, BuildConfiguredRootSet(settingsService.Load()))
    {
        _settingsService = settingsService;
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    internal ImageSelector(ILogger<ImageSelector> logger, string? cachePath = null)
        : this(logger, cachePath, configuredRootFolders: null)
    {
    }

    internal ImageSelector(
        ILogger<ImageSelector> logger,
        string cachePath,
        IEnumerable<string> configuredRootFolders)
        : this(logger, cachePath, BuildConfiguredRootSet(configuredRootFolders))
    {
    }

    private ImageSelector(
        ILogger<ImageSelector> logger,
        string? cachePath,
        HashSet<string>? configuredRootFolders)
    {
        _logger = logger;
        _cachePath = cachePath ?? AppDataPaths.PrepareFile("image_dim_cache.json");
        _configuredRootFolders = configuredRootFolders;
        LoadDimensionCache(GetExistingConfiguredRoots(configuredRootFolders));
    }

    public void PrewarmFolder(string folderPath)
    {
        var normalizedFolderPath = NormalizeFolderPath(folderPath);
        if (!Directory.Exists(normalizedFolderPath)) return;

        lock (_lock)
        {
            if (_configuredRootFolders != null && !_configuredRootFolders.Contains(normalizedFolderPath))
                return;
            if (_folderCaches.ContainsKey(normalizedFolderPath)) return;  // already scanning or complete
            var cache = new FolderCache(CreateWatcher(normalizedFolderPath));
            _folderCaches[normalizedFolderPath] = cache;
            _ = Task.Run(() => ScanFolder(normalizedFolderPath, cache));
            _logger.LogInformation("Pre-warming folder: {Folder}", normalizedFolderPath);
        }
    }

    public string? SelectImage(
        string folderPath,
        ImageOrientation targetOrientation,
        RandomPlaybackState randomPlayback,
        WallpaperPlaybackOrder playbackOrder,
        string? currentWallpaperPath,
        int? minimumImageSideLength = null,
        IReadOnlyCollection<string>? excludedImagePaths = null)
    {
        var normalizedFolderPath = NormalizeFolderPath(folderPath);
        if (!Directory.Exists(normalizedFolderPath))
        {
            _logger.LogWarning("Wallpaper folder not found: {Folder}", normalizedFolderPath);
            return null;
        }

        int? normalizedMinimumImageSideLength = minimumImageSideLength is null
            ? null
            : Math.Max(1, minimumImageSideLength.Value);
        string? selected = null;
        lock (_lock)
        {
            if (_configuredRootFolders != null && !_configuredRootFolders.Contains(normalizedFolderPath))
                return null;

            if (!_folderCaches.TryGetValue(normalizedFolderPath, out var cache))
            {
                // First access: register empty cache and kick off background scan
                var newCache = new FolderCache(CreateWatcher(normalizedFolderPath));
                _folderCaches[normalizedFolderPath] = newCache;
                _ = Task.Run(() => ScanFolder(normalizedFolderPath, newCache));
                _logger.LogWarning("Scan started for {Folder}, no images available yet", normalizedFolderPath);
                return null;
            }

            var pool = GetOrCreateCandidatePool(cache, targetOrientation, normalizedMinimumImageSideLength);
            var eligibleItems = FilterExcludedImages(pool.Items, excludedImagePaths);
            if (eligibleItems.Count > 0)
            {
                selected = playbackOrder switch
                {
                    WallpaperPlaybackOrder.Sequential => SelectSequential(
                        GetOrderedPool(pool, eligibleItems, reverse: false, hasExclusions: excludedImagePaths?.Count > 0),
                        currentWallpaperPath),
                    WallpaperPlaybackOrder.ReverseSequential => SelectSequential(
                        GetOrderedPool(pool, eligibleItems, reverse: true, hasExclusions: excludedImagePaths?.Count > 0),
                        currentWallpaperPath),
                    _ => SelectRandom(
                        eligibleItems,
                        randomPlayback,
                        BuildRandomCandidateKey(normalizedFolderPath, targetOrientation, normalizedMinimumImageSideLength),
                        currentWallpaperPath)
                };
            }
        }

        if (selected == null)
        {
            if (normalizedMinimumImageSideLength is not null)
            {
                _logger.LogWarning(
                    "No images met the minimum side length {MinimumImageSideLength} in folder: {Folder}",
                    normalizedMinimumImageSideLength.Value,
                    normalizedFolderPath);
            }
            else
            {
                _logger.LogWarning("No images found in folder: {Folder}", normalizedFolderPath);
            }
            return null;
        }

        _logger.LogDebug("Selected image: {Image}", Path.GetFileName(selected));
        return selected;
    }

    private static IReadOnlyList<string> FilterExcludedImages(
        IReadOnlyList<string> pool,
        IReadOnlyCollection<string>? excludedImagePaths)
    {
        if (excludedImagePaths == null || excludedImagePaths.Count == 0)
            return pool;

        var excluded = excludedImagePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return pool.Where(path => !excluded.Contains(path)).ToList();
    }

    private string? SelectRandom(
        IReadOnlyList<string> pool,
        RandomPlaybackState state,
        string candidateKey,
        string? currentWallpaperPath)
    {
        state.RemainingPaths ??= [];
        state.SeenPaths ??= [];

        var uniquePool = pool
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (uniquePool.Count == 0)
            return null;

        if (!string.Equals(state.CandidateKey, candidateKey, StringComparison.OrdinalIgnoreCase))
        {
            state.CandidateKey = candidateKey;
            state.SeenPaths.Clear();
            state.RemainingPaths.Clear();
            AppendShuffled(state.RemainingPaths, uniquePool);
        }
        else
        {
            ReconcileRandomCycle(state, uniquePool);
        }

        if (state.RemainingPaths.Count == 0)
            StartNewRandomCycle(state, uniquePool);

        AvoidRepeatingCurrentAtCycleBoundary(state, uniquePool, currentWallpaperPath);
        if (state.RemainingPaths.Count == 0)
            return null;

        var selected = state.RemainingPaths[0];
        state.RemainingPaths.RemoveAt(0);
        if (!state.SeenPaths.Contains(selected, StringComparer.OrdinalIgnoreCase))
            state.SeenPaths.Add(selected);
        return selected;
    }

    private void ReconcileRandomCycle(RandomPlaybackState state, IReadOnlyList<string> pool)
    {
        var poolSet = pool.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seenSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        state.SeenPaths.RemoveAll(path => !poolSet.Contains(path) || !seenSet.Add(path));

        var remainingSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        state.RemainingPaths.RemoveAll(path =>
            !poolSet.Contains(path) || seenSet.Contains(path) || !remainingSet.Add(path));

        var addedPaths = pool
            .Where(path => !seenSet.Contains(path) && !remainingSet.Contains(path))
            .ToList();
        Shuffle(addedPaths);
        foreach (var addedPath in addedPaths)
        {
            var insertAt = _random.Next(state.RemainingPaths.Count + 1);
            state.RemainingPaths.Insert(insertAt, addedPath);
        }
    }

    private void StartNewRandomCycle(RandomPlaybackState state, IReadOnlyList<string> pool)
    {
        state.SeenPaths.Clear();
        state.RemainingPaths.Clear();
        AppendShuffled(state.RemainingPaths, pool);
    }

    private void AvoidRepeatingCurrentAtCycleBoundary(
        RandomPlaybackState state,
        IReadOnlyList<string> pool,
        string? currentWallpaperPath)
    {
        if (string.IsNullOrEmpty(currentWallpaperPath) ||
            state.RemainingPaths.Count == 0 ||
            !string.Equals(state.RemainingPaths[0], currentWallpaperPath, StringComparison.OrdinalIgnoreCase))
            return;

        var alternativeIndex = state.RemainingPaths.FindIndex(
            1,
            path => !string.Equals(path, currentWallpaperPath, StringComparison.OrdinalIgnoreCase));
        if (alternativeIndex >= 0)
        {
            (state.RemainingPaths[0], state.RemainingPaths[alternativeIndex]) =
                (state.RemainingPaths[alternativeIndex], state.RemainingPaths[0]);
            return;
        }

        if (pool.Count <= 1)
            return;

        // The current wallpaper is the only item left in this cycle because it was applied
        // outside this random queue (for example after switching playback modes). Count it as
        // seen, then start the next cycle so the same image is not applied twice in a row.
        var current = state.RemainingPaths[0];
        state.RemainingPaths.Clear();
        if (!state.SeenPaths.Contains(current, StringComparer.OrdinalIgnoreCase))
            state.SeenPaths.Add(current);
        StartNewRandomCycle(state, pool);
        AvoidRepeatingCurrentAtCycleBoundary(state, pool, currentWallpaperPath);
    }

    private void AppendShuffled(List<string> destination, IReadOnlyList<string> source)
    {
        var shuffled = source.ToList();
        Shuffle(shuffled);
        destination.AddRange(shuffled);
    }

    private void Shuffle(List<string> paths)
    {
        for (var i = paths.Count - 1; i > 0; i--)
        {
            var swapIndex = _random.Next(i + 1);
            (paths[i], paths[swapIndex]) = (paths[swapIndex], paths[i]);
        }
    }

    private static string BuildRandomCandidateKey(
        string normalizedFolderPath,
        ImageOrientation targetOrientation,
        int? minimumImageSideLength) =>
        $"{normalizedFolderPath}\n{targetOrientation}\n{minimumImageSideLength?.ToString() ?? "none"}";

    private static string SelectSequential(IReadOnlyList<string> orderedPool, string? currentWallpaperPath)
    {
        if (orderedPool.Count == 1)
            return orderedPool[0];

        if (string.IsNullOrEmpty(currentWallpaperPath))
            return orderedPool[0];

        var currentIndex = -1;
        for (var i = 0; i < orderedPool.Count; i++)
        {
            if (!string.Equals(orderedPool[i], currentWallpaperPath, StringComparison.OrdinalIgnoreCase))
                continue;

            currentIndex = i;
            break;
        }

        if (currentIndex < 0)
            return orderedPool[0];

        return orderedPool[(currentIndex + 1) % orderedPool.Count];
    }

    private CandidatePool GetOrCreateCandidatePool(
        FolderCache cache,
        ImageOrientation targetOrientation,
        int? minimumImageSideLength)
    {
        var key = new CandidatePoolKey(targetOrientation, minimumImageSideLength ?? 0);
        if (cache.CandidatePools.TryGetValue(key, out var existing))
            return existing;

        if (cache.CandidatePools.Count >= MaxCandidatePoolsPerFolder)
            cache.CandidatePools.Clear();

        var created = new CandidatePool(BuildPoolSnapshot(cache, targetOrientation, minimumImageSideLength));
        cache.CandidatePools[key] = created;
        return created;
    }

    /// <summary>
    /// Builds a stable candidate pool snapshot from the current folder cache.
    /// Falls back to all files if no orientation-matched images exist yet.
    /// Must be called under _lock.
    /// </summary>
    private List<string> BuildPoolSnapshot(
        FolderCache cache,
        ImageOrientation target,
        int? minimumImageSideLength)
    {
        int? threshold = minimumImageSideLength is null
            ? null
            : Math.Max(1, minimumImageSideLength.Value);

        if (target == ImageOrientation.Square)
            return CopyEligibleFiles(cache.Files, threshold);

        var matched = cache.ByOrientation[target];
        var squares = cache.ByOrientation[ImageOrientation.Square];

        if (matched.Count == 0 && squares.Count == 0)
            return CopyEligibleFiles(cache.Files, threshold);

        var result = new List<string>(matched.Count + squares.Count);
        AppendEligibleFiles(result, matched, threshold);
        AppendEligibleFiles(result, squares, threshold);
        return result;
    }

    private List<string> CopyEligibleFiles(List<string> source, int? minimumImageSideLength)
    {
        var result = new List<string>(source.Count);
        AppendEligibleFiles(result, source, minimumImageSideLength);
        return result;
    }

    private void AppendEligibleFiles(
        List<string> destination,
        List<string> source,
        int? minimumImageSideLength)
    {
        if (minimumImageSideLength is null)
        {
            destination.AddRange(source);
            return;
        }

        var threshold = minimumImageSideLength.Value;
        foreach (var file in source)
        {
            if (MeetsMinimumSideLength(file, threshold))
                destination.Add(file);
        }
    }

    private bool MeetsMinimumSideLength(string filePath, int minimumImageSideLength)
    {
        if (!_dimensionCache.TryGetValue(filePath, out var dim))
            return false;

        return dim.Width >= minimumImageSideLength && dim.Height >= minimumImageSideLength;
    }

    private static IReadOnlyList<string> GetOrderedPool(
        CandidatePool pool,
        IReadOnlyList<string> eligibleItems,
        bool reverse,
        bool hasExclusions)
    {
        if (hasExclusions)
        {
            return reverse
                ? eligibleItems.OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase).ToList()
                : eligibleItems.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (reverse)
        {
            pool.Descending ??= pool.Items
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return pool.Descending;
        }

        pool.Ascending ??= pool.Items
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return pool.Ascending;
    }

    /// <summary>
    /// Incrementally enumerates the folder on a background thread, appending files in
    /// batches and pre-warming dimensions + orientation buckets for each batch.
    /// </summary>
    private void ScanFolder(string folderPath, FolderCache cache)
    {
        var completed = false;
        try
        {
            var batch = new List<string>();
            var enumerationOptions = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false
            };
            foreach (var file in Directory.EnumerateFiles(folderPath, "*", enumerationOptions))
            {
                if (cache.IsRetired)
                    break;

                if (!SupportedExtensions.Contains(Path.GetExtension(file)))
                    continue;

                var normalizedFile = NormalizeAbsoluteFilePath(file);

                // Reuse the path instance stored in the cached value so the folder lists and
                // the dictionary key do not retain separate strings for the same file.
                string canonical;
                lock (_lock)
                {
                    canonical = _dimensionCache.TryGetValue(normalizedFile, out var cachedEntry)
                        ? cachedEntry.FilePath
                        : normalizedFile;
                }
                batch.Add(canonical);
                if (batch.Count >= ScanBatchSize)
                {
                    var snapshot = new List<string>(batch);
                    PrewarmDimensionsBatch(folderPath, snapshot, cache);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
                PrewarmDimensionsBatch(folderPath, batch, cache);
            completed = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Image folder scan failed for {Folder}", folderPath);
        }
        finally
        {
            int total;
            bool pendingRescan;
            bool shouldSave;
            lock (_lock)
            {
                shouldSave = !cache.IsRetired &&
                    _folderCaches.TryGetValue(folderPath, out var currentCache) &&
                    ReferenceEquals(cache, currentCache);
                if (completed && shouldSave)
                    PruneStaleDimensions(folderPath, cache.Files);
                cache.IsScanning = false;
                pendingRescan = shouldSave && cache.PendingRescan;
                cache.PendingRescan = false;
                total = cache.Files.Count;
            }
            if (shouldSave)
            {
                _logger.LogInformation("Image scan complete for {Folder}: {Count} images", folderPath, total);
                SaveDimensionCache();
            }

            if (pendingRescan)
                DoRescan(folderPath);
        }
    }

    /// <summary>
    /// For each file: looks up or reads dimensions in parallel, then batch-writes to
    /// _dimensionCache and populates the orientation buckets.
    /// Does NOT trigger intermediate disk saves — ScanFolder.finally handles that.
    /// </summary>
    private void PrewarmDimensionsBatch(string folderPath, List<string> files, FolderCache cache)
    {
        // Separate files that are already cached from those that need I/O
        var toRead   = new List<string>(files.Count);
        var cached   = new List<(string File, CachedDimension Entry)>(files.Count);

        lock (_lock)
        {
            if (cache.IsRetired ||
                !_folderCaches.TryGetValue(folderPath, out var currentCache) ||
                !ReferenceEquals(cache, currentCache))
                return;

            foreach (var file in files)
            {
                if (_dimensionCache.TryGetValue(file, out var entry) && IsCacheEntryCurrent(file, entry))
                    cached.Add((entry.FilePath, entry));
                else
                {
                    _dimensionCache.Remove(file);
                    toRead.Add(file);
                }
            }
        }

        // Read dimensions in parallel (pure I/O, no shared state)
        var readResults = new (string File, (int W, int H) Dim)[toRead.Count];
        Parallel.For(0, toRead.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
        {
            var file = toRead[i];
            (int W, int H) dim;
            try { dim = ReadImageDimensions(file); }
            catch { dim = (0, 0); }
            readResults[i] = (file, dim);
        });

        // Batch-write results under a single lock acquisition
        lock (_lock)
        {
            if (cache.IsRetired ||
                !_folderCaches.TryGetValue(folderPath, out var currentCache) ||
                !ReferenceEquals(cache, currentCache))
                return;

            // Publish the file list together with its orientation buckets so selectors never
            // observe files before their cached dimensions have been categorized.
            cache.Files.AddRange(files);
            foreach (var (file, dim) in readResults)
            {
                if (!TryCreateCachedDimension(folderPath, file, dim, out var entry))
                    continue;

                _dimensionCache[file] = entry;
            }
            foreach (var (file, entry) in cached)
                AddToOrientationBucket(cache, file, (entry.Width, entry.Height));
            foreach (var (file, dim) in readResults)
                AddToOrientationBucket(cache, file, dim);
            cache.CandidatePools.Clear();
        }
    }

    /// <summary>
    /// Adds a file to the appropriate orientation bucket. Files with unreadable dimensions
    /// are not added to any bucket; they remain accessible via the Files fallback in BuildPool.
    /// Must be called under _lock.
    /// </summary>
    private static void AddToOrientationBucket(FolderCache cache, string filePath, (int W, int H) dim)
    {
        if (dim.W <= 0 || dim.H <= 0) return;

        var orientation = dim.W > dim.H ? ImageOrientation.Landscape
            : dim.W < dim.H ? ImageOrientation.Portrait
            : ImageOrientation.Square;

        cache.ByOrientation[orientation].Add(filePath);
    }

    private FileSystemWatcher CreateWatcher(string folderPath)
    {
        var watcher = new FileSystemWatcher(folderPath)
        {
            NotifyFilter = NotifyFilters.FileName |
                           NotifyFilters.DirectoryName |
                           NotifyFilters.LastWrite |
                           NotifyFilters.Size,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        watcher.Created += (_, _) => InvalidateFileList(folderPath);
        watcher.Changed += (_, e) =>
        {
            var changedPath = NormalizeAbsoluteFilePath(e.FullPath);
            lock (_lock)
            {
                _dimensionCache.Remove(changedPath);
            }
            InvalidateFileList(folderPath);
        };
        watcher.Deleted += (_, e) =>
        {
            InvalidateFileList(folderPath);
            var deletedPath = NormalizeAbsoluteFilePath(e.FullPath);
            lock (_lock)
            {
                _dimensionCache.Remove(deletedPath);
            }
        };
        watcher.Renamed += (sender, e) =>
        {
            InvalidateFileList(folderPath);
            var oldPath = NormalizeAbsoluteFilePath(e.OldFullPath);
            var newPath = NormalizeAbsoluteFilePath(e.FullPath);
            lock (_lock)
            {
                if (_dimensionCache.TryGetValue(oldPath, out var entry))
                {
                    _dimensionCache.Remove(oldPath);

                    if (TryGetRelativePath(folderPath, newPath, out _))
                    {
                        var updatedEntry = entry with { FilePath = newPath };
                        _dimensionCache[newPath] = updatedEntry;
                    }
                }
            }
        };
        watcher.Error += (_, e) =>
        {
            _logger.LogWarning(
                e.GetException(),
                "Wallpaper folder watcher overflowed for {Folder}; scheduling a full rescan",
                folderPath);
            InvalidateFileList(folderPath);
        };

        return watcher;
    }

    /// <summary>
    /// Schedules a debounced rescan. Rapid bursts of FileSystemWatcher events (e.g. bulk
    /// copy) are coalesced: the timer resets on every call and the scan only starts after
    /// DebounceDelay of silence.
    /// </summary>
    private void InvalidateFileList(string folderPath)
    {
        lock (_lock)
        {
            if (!_folderCaches.ContainsKey(folderPath))
                return;

            if (_debounceTimers.TryGetValue(folderPath, out var existing))
            {
                existing.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
                return;
            }

            _debounceTimers[folderPath] = new Timer(_ => DoRescan(folderPath),
                null, DebounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void DoRescan(string folderPath)
    {
        lock (_lock)
        {
            if (_debounceTimers.TryGetValue(folderPath, out var t)) t.Dispose();
            _debounceTimers.Remove(folderPath);

            if (!_folderCaches.TryGetValue(folderPath, out var cache)) return;
            if (cache.IsScanning)
            {
                cache.PendingRescan = true;
                return;
            }

            cache.Files.Clear();
            foreach (var list in cache.ByOrientation.Values) list.Clear();
            cache.CandidatePools.Clear();
            cache.IsScanning = true;
            _ = Task.Run(() => ScanFolder(folderPath, cache));
            _logger.LogDebug("File list invalidated, re-scanning: {Folder}", folderPath);
        }
    }

    // ── Persistent cache ──────────────────────────────────────────────────────

    private void LoadDimensionCache(IReadOnlySet<string>? retainedRootFolders)
    {
        if (!File.Exists(_cachePath))
            return;

        var buffer = ArrayPool<byte>.Shared.Rent(CacheReadBufferSize);
        try
        {
            using var stream = new FileStream(
                _cachePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CacheReadBufferSize,
                FileOptions.SequentialScan);

            var readerState = new JsonReaderState(new JsonReaderOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            var parser = new DimensionCacheParser(this, retainedRootFolders);
            var bufferedByteCount = 0;

            while (true)
            {
                var bytesRead = stream.Read(
                    buffer,
                    bufferedByteCount,
                    buffer.Length - bufferedByteCount);
                var totalByteCount = bufferedByteCount + bytesRead;
                var isFinalBlock = bytesRead == 0;
                var reader = new Utf8JsonReader(
                    buffer.AsSpan(0, totalByteCount),
                    isFinalBlock,
                    readerState);

                while (reader.Read())
                    parser.ProcessToken(ref reader);

                var consumedByteCount = checked((int)reader.BytesConsumed);
                bufferedByteCount = totalByteCount - consumedByteCount;
                if (bufferedByteCount > 0)
                {
                    Buffer.BlockCopy(
                        buffer,
                        consumedByteCount,
                        buffer,
                        0,
                        bufferedByteCount);
                }

                readerState = reader.CurrentState;
                if (isFinalBlock)
                {
                    if (bufferedByteCount != 0)
                        throw new JsonException("Dimension cache ended with an incomplete JSON token.");
                    break;
                }

                if (bufferedByteCount == buffer.Length)
                {
                    var largerBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                    Buffer.BlockCopy(buffer, 0, largerBuffer, 0, bufferedByteCount);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = largerBuffer;
                }
            }

            _logger.LogInformation(
                "Loaded {Loaded} dimension cache entries ({Skipped} invalid, stale, or excess skipped)",
                parser.LoadedCount,
                parser.SkippedCount);
        }
        catch (Exception ex)
        {
            lock (_lock)
                _dimensionCache.Clear();
            _logger.LogWarning(ex, "Failed to load dimension cache; starting empty");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void SaveDimensionCache()
    {
        lock (_cachePersistenceLock)
            SaveDimensionCacheCore();
    }

    private void SaveDimensionCacheCore()
    {
        var tempPath = _cachePath + ".tmp";
        try
        {
            List<CachedDimension> snapshot;
            lock (_lock)
            {
                snapshot = _dimensionCache.Values
                    .Where(entry => _configuredRootFolders == null ||
                        _configuredRootFolders.Contains(entry.RootFolder))
                    .ToList();
            }
            snapshot.Sort(CachedDimensionComparer.Instance);

            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                CacheReadBufferSize,
                FileOptions.SequentialScan))
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                string? currentRoot = null;
                var rootIsOpen = false;
                var savedCount = 0;

                foreach (var entry in snapshot)
                {
                    if (savedCount >= MaxPersistedDimensionEntries)
                        break;
                    if (!TryGetRelativePath(entry.RootFolder, entry.FilePath, out var relativePath))
                        continue;

                    if (!string.Equals(currentRoot, entry.RootFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        if (rootIsOpen)
                            writer.WriteEndObject();
                        currentRoot = entry.RootFolder;
                        writer.WritePropertyName(currentRoot);
                        writer.WriteStartObject();
                        rootIsOpen = true;
                    }

                    writer.WritePropertyName(relativePath);
                    writer.WriteStartObject();
                    writer.WriteNumber("width", entry.Width);
                    writer.WriteNumber("height", entry.Height);
                    writer.WriteNumber("length", entry.Length);
                    writer.WriteNumber("lastWriteTimeUtcTicks", entry.LastWriteTimeUtcTicks);
                    writer.WriteEndObject();
                    savedCount++;
                }

                if (rootIsOpen)
                    writer.WriteEndObject();
                writer.WriteEndObject();
                writer.Flush();
                _logger.LogDebug(
                    "Saved {Count} dimension cache entries (limit {Limit})",
                    savedCount,
                    MaxPersistedDimensionEntries);
            }
            File.Move(tempPath, _cachePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save dimension cache");
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to clean up temporary image cache file");
            }
        }
    }

    // ── Header parsers ────────────────────────────────────────────────────────

    private static (int Width, int Height) ReadImageDimensions(string filePath) =>
        ImageMetadataHelper.TryReadDimensions(filePath, out var width, out var height)
            ? (width, height)
            : (0, 0);

    private static string NormalizeFolderPath(string folderPath)
    {
        var fullPath = Path.GetFullPath(folderPath);
        var root = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string NormalizeAbsoluteFilePath(string filePath) => Path.GetFullPath(filePath);

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

    private static bool TryGetRelativePath(string rootFolder, string filePath, out string relativePath)
    {
        var relative = Path.GetRelativePath(rootFolder, filePath);
        if (string.IsNullOrWhiteSpace(relative) ||
            relative == "." ||
            relative == ".." ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            relativePath = string.Empty;
            return false;
        }

        relativePath = NormalizeRelativePath(relative);
        return true;
    }

    private static bool TryCreateCachedDimension(
        string rootFolder,
        string filePath,
        (int W, int H) dim,
        out CachedDimension entry)
    {
        // ScanFolder already supplies normalized absolute paths. Re-normalizing every file
        // would allocate another full path string for large libraries.
        if (!TryGetRelativePath(rootFolder, filePath, out _))
        {
            entry = default;
            return false;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            entry = new CachedDimension(
                filePath,
                rootFolder,
                dim.W,
                dim.H,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc.Ticks);
            return true;
        }
        catch
        {
            entry = default;
            return false;
        }
    }

    private static bool IsCacheEntryCurrent(string filePath, CachedDimension entry)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Exists &&
                   fileInfo.Length == entry.Length &&
                   fileInfo.LastWriteTimeUtc.Ticks == entry.LastWriteTimeUtcTicks;
        }
        catch
        {
            return false;
        }
    }

    private void PruneStaleDimensions(string folderPath, IReadOnlyCollection<string> currentFiles)
    {
        var currentFileSet = currentFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stalePaths = _dimensionCache
            .Where(entry =>
                string.Equals(entry.Value.RootFolder, folderPath, StringComparison.OrdinalIgnoreCase) &&
                !currentFileSet.Contains(entry.Key))
            .Select(entry => entry.Key)
            .ToList();

        foreach (var stalePath in stalePaths)
            _dimensionCache.Remove(stalePath);
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        var configuredRoots = BuildConfiguredRootSet(settings);
        var retainedRoots = GetExistingConfiguredRoots(configuredRoots)!;
        var cacheChanged = false;

        lock (_lock)
        {
            _configuredRootFolders = configuredRoots;

            foreach (var removedRoot in _folderCaches.Keys
                .Where(root => !retainedRoots.Contains(root))
                .ToArray())
            {
                var cache = _folderCaches[removedRoot];
                cache.IsRetired = true;
                cache.Watcher.Dispose();
                cache.Files.Clear();
                foreach (var bucket in cache.ByOrientation.Values)
                    bucket.Clear();
                cache.CandidatePools.Clear();
                _folderCaches.Remove(removedRoot);

                if (_debounceTimers.Remove(removedRoot, out var timer))
                    timer.Dispose();
                cacheChanged = true;
            }

            var stalePaths = _dimensionCache
                .Where(entry => !retainedRoots.Contains(entry.Value.RootFolder))
                .Select(entry => entry.Key)
                .ToArray();
            foreach (var stalePath in stalePaths)
                _dimensionCache.Remove(stalePath);
            cacheChanged |= stalePaths.Length > 0;
        }

        if (cacheChanged)
            SaveDimensionCache();
    }

    private static HashSet<string> BuildConfiguredRootSet(AppSettings settings) =>
        BuildConfiguredRootSet(settings.MonitorAssignments
            .Where(assignment => assignment.Enabled && !string.IsNullOrWhiteSpace(assignment.FolderPath))
            .Select(assignment => assignment.FolderPath));

    private static HashSet<string> BuildConfiguredRootSet(IEnumerable<string> folderPaths)
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folderPath in folderPaths)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(folderPath))
                    roots.Add(NormalizeFolderPath(folderPath));
            }
            catch
            {
                // Invalid configured paths are ignored and will not retain cache entries.
            }
        }
        return roots;
    }

    private static HashSet<string>? GetExistingConfiguredRoots(HashSet<string>? configuredRoots) =>
        configuredRoots?
            .Where(Directory.Exists)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (_settingsService != null)
            _settingsService.SettingsChanged -= OnSettingsChanged;

        lock (_lock)
        {
            foreach (var t in _debounceTimers.Values) t.Dispose();
            _debounceTimers.Clear();
            foreach (var cache in _folderCaches.Values)
            {
                cache.IsRetired = true;
                cache.Watcher.Dispose();
            }
            _folderCaches.Clear();
        }
        SaveDimensionCache();
    }

    // ── Inner type ────────────────────────────────────────────────────────────

    private sealed class DimensionCacheParser(
        ImageSelector owner,
        IReadOnlySet<string>? retainedRootFolders)
    {
        private string? _rootFolder;
        private string? _relativePath;
        private bool _includeRoot;
        private MetadataProperty _metadataProperty;
        private int _width;
        private int _height;
        private long _length;
        private long _lastWriteTimeUtcTicks;

        public int LoadedCount { get; private set; }
        public int SkippedCount { get; private set; }

        public void ProcessToken(ref Utf8JsonReader reader)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                ProcessPropertyName(ref reader);
                return;
            }

            if (reader.TokenType == JsonTokenType.Number && reader.CurrentDepth == 3 && _includeRoot)
            {
                ProcessMetadataValue(ref reader);
                return;
            }

            if (reader.TokenType != JsonTokenType.EndObject)
                return;

            if (reader.CurrentDepth == 2)
                CompleteEntry();
            else if (reader.CurrentDepth == 1)
            {
                _rootFolder = null;
                _includeRoot = false;
            }
        }

        private void ProcessPropertyName(ref Utf8JsonReader reader)
        {
            if (reader.CurrentDepth == 1)
            {
                try
                {
                    var rootFolder = reader.GetString();
                    _rootFolder = string.IsNullOrWhiteSpace(rootFolder)
                        ? null
                        : NormalizeFolderPath(rootFolder);
                    _includeRoot = _rootFolder != null &&
                        (retainedRootFolders == null || retainedRootFolders.Contains(_rootFolder));
                }
                catch
                {
                    _rootFolder = null;
                    _includeRoot = false;
                }
                return;
            }

            if (reader.CurrentDepth == 2)
            {
                _relativePath = _includeRoot ? reader.GetString() : null;
                _width = 0;
                _height = 0;
                _length = 0;
                _lastWriteTimeUtcTicks = 0;
                _metadataProperty = MetadataProperty.None;
                return;
            }

            if (reader.CurrentDepth != 3 || !_includeRoot)
                return;

            _metadataProperty = reader.ValueTextEquals("width"u8)
                ? MetadataProperty.Width
                : reader.ValueTextEquals("height"u8)
                    ? MetadataProperty.Height
                    : reader.ValueTextEquals("length"u8) || reader.ValueTextEquals("fileSize"u8)
                        ? MetadataProperty.Length
                        : reader.ValueTextEquals("lastWriteTimeUtcTicks"u8)
                            ? MetadataProperty.LastWriteTimeUtcTicks
                            : MetadataProperty.None;
        }

        private void ProcessMetadataValue(ref Utf8JsonReader reader)
        {
            switch (_metadataProperty)
            {
                case MetadataProperty.Width when reader.TryGetInt32(out var width):
                    _width = width;
                    break;
                case MetadataProperty.Height when reader.TryGetInt32(out var height):
                    _height = height;
                    break;
                case MetadataProperty.Length when reader.TryGetInt64(out var length):
                    _length = length;
                    break;
                case MetadataProperty.LastWriteTimeUtcTicks when reader.TryGetInt64(out var ticks):
                    _lastWriteTimeUtcTicks = ticks;
                    break;
            }
        }

        private void CompleteEntry()
        {
            if (!_includeRoot || _rootFolder == null || string.IsNullOrWhiteSpace(_relativePath) ||
                LoadedCount >= MaxPersistedDimensionEntries)
            {
                SkippedCount++;
                return;
            }

            try
            {
                var relativePath = NormalizeRelativePath(_relativePath);
                var fullPath = NormalizeAbsoluteFilePath(Path.Combine(
                    _rootFolder,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!TryGetRelativePath(_rootFolder, fullPath, out _))
                {
                    SkippedCount++;
                    return;
                }

                owner._dimensionCache[fullPath] = new CachedDimension(
                    fullPath,
                    _rootFolder,
                    _width,
                    _height,
                    _length,
                    _lastWriteTimeUtcTicks);
                LoadedCount++;
            }
            catch
            {
                SkippedCount++;
            }
        }
    }

    private enum MetadataProperty
    {
        None,
        Width,
        Height,
        Length,
        LastWriteTimeUtcTicks
    }

    private sealed class CachedDimensionComparer : IComparer<CachedDimension>
    {
        public static CachedDimensionComparer Instance { get; } = new();

        public int Compare(CachedDimension x, CachedDimension y)
        {
            var rootComparison = StringComparer.OrdinalIgnoreCase.Compare(x.RootFolder, y.RootFolder);
            return rootComparison != 0
                ? rootComparison
                : StringComparer.OrdinalIgnoreCase.Compare(x.FilePath, y.FilePath);
        }
    }

    private sealed class FolderCache(FileSystemWatcher watcher)
    {
        public List<string> Files        { get; } = new();
        // Pre-categorized by orientation — populated during background scan
        public Dictionary<ImageOrientation, List<string>> ByOrientation { get; } = new()
        {
            [ImageOrientation.Landscape] = new(),
            [ImageOrientation.Portrait]  = new(),
            [ImageOrientation.Square]    = new(),
        };
        public Dictionary<CandidatePoolKey, CandidatePool> CandidatePools { get; } = new();
        public FileSystemWatcher Watcher { get; } = watcher;
        public bool IsScanning           { get; set; } = true;
        public bool PendingRescan        { get; set; }
        public volatile bool IsRetired;
    }

    private readonly record struct CandidatePoolKey(ImageOrientation TargetOrientation, int MinimumImageSideLength);

    private sealed class CandidatePool(List<string> items)
    {
        public List<string> Items { get; } = items;
        public List<string>? Ascending { get; set; }
        public List<string>? Descending { get; set; }
    }

    private readonly record struct CachedDimension(
        string FilePath,
        string RootFolder,
        int Width,
        int Height,
        long Length,
        long LastWriteTimeUtcTicks);
}
