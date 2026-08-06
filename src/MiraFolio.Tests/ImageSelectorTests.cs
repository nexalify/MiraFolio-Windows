using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MiraFolio.Core.Models;
using MiraFolio.Core.Services;
using Xunit;

namespace MiraFolio.Tests;

public class ImageSelectorTests : IDisposable
{
    private readonly string _testFolder;
    private readonly string _cacheFolder;
    private readonly string _cachePath;
    private readonly ImageSelector _selector;

    public ImageSelectorTests()
    {
        _testFolder = Path.Combine(Path.GetTempPath(), "MiraFolioTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testFolder);
        _cacheFolder = Path.Combine(Path.GetTempPath(), "MiraFolioTests_Cache_" + Guid.NewGuid());
        Directory.CreateDirectory(_cacheFolder);
        _cachePath = Path.Combine(_cacheFolder, "image_dim_cache.json");
        _selector = CreateSelector();
    }

    [Fact]
    public void SelectImage_EmptyFolder_ReturnsNull()
    {
        var result = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.Random,
            null);
        Assert.Null(result);
    }

    [Fact]
    public void SelectImage_NonExistentFolder_ReturnsNull()
    {
        var result = _selector.SelectImage(
            @"C:\NonExistentFolder_12345",
            ImageOrientation.Landscape,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.Random,
            null);
        Assert.Null(result);
    }

    [Fact]
    public void SelectImage_SingleImage_ReturnsIt()
    {
        // Create a fake PNG file with valid header
        var pngPath = Path.Combine(_testFolder, "test.png");
        CreateFakePng(pngPath, 1920, 1080);

        _selector.PrewarmFolder(_testFolder);
        WaitForScan();

        var result = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.Random,
            null);
        Assert.NotNull(result);
        Assert.Equal(pngPath, result);
    }

    [Fact]
    public void SelectImage_Random_UsesEveryImageBeforeStartingNextCycle()
    {
        var paths = Enumerable.Range(1, 6)
            .Select(index => Path.Combine(_testFolder, $"{index}.png"))
            .ToArray();
        foreach (var path in paths)
            CreateFakePng(path, 1920, 1080);
        _selector.PrewarmFolder(_testFolder);
        WaitForScan();

        var state = new RandomPlaybackState();
        var firstCycle = new List<string>();
        string? current = null;
        for (var i = 0; i < paths.Length; i++)
        {
            current = _selector.SelectImage(
                _testFolder,
                ImageOrientation.Landscape,
                state,
                WallpaperPlaybackOrder.Random,
                current);
            Assert.NotNull(current);
            firstCycle.Add(current);
        }

        Assert.Equal(paths.Length, firstCycle.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var nextCycleFirst = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            state,
            WallpaperPlaybackOrder.Random,
            current);
        Assert.NotNull(nextCycleFirst);
        Assert.NotEqual(current, nextCycleFirst);
    }

    [Fact]
    public void SelectImage_Random_NewFileJoinsRemainingCurrentCycle()
    {
        var originalPaths = new[]
        {
            Path.Combine(_testFolder, "a.png"),
            Path.Combine(_testFolder, "b.png")
        };
        foreach (var path in originalPaths)
            CreateFakePng(path, 1920, 1080);
        _selector.PrewarmFolder(_testFolder);
        WaitForScan();
        WaitForFolderScanToCompleteWithoutSelection();

        var state = new RandomPlaybackState();
        var first = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            state,
            WallpaperPlaybackOrder.Random,
            null);
        Assert.NotNull(first);

        var cacheTimestamp = File.GetLastWriteTimeUtc(_cachePath);
        var addedPath = Path.Combine(_testFolder, "c.png");
        CreateFakePng(addedPath, 1920, 1080);
        WaitForCacheRewrite(cacheTimestamp);

        var second = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            state,
            WallpaperPlaybackOrder.Random,
            first);
        var third = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            state,
            WallpaperPlaybackOrder.Random,
            second);

        var currentCycle = new[] { first, second, third };
        Assert.Equal(3, currentCycle.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(addedPath, currentCycle);
    }

    [Fact]
    public void SelectImage_Random_DeletedFileIsRemovedWithoutResettingCycle()
    {
        var paths = Enumerable.Range(1, 3)
            .Select(index => Path.Combine(_testFolder, $"{index}.png"))
            .ToArray();
        foreach (var path in paths)
            CreateFakePng(path, 1920, 1080);
        _selector.PrewarmFolder(_testFolder);
        WaitForScan();
        WaitForFolderScanToCompleteWithoutSelection();

        var state = new RandomPlaybackState();
        var first = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            state,
            WallpaperPlaybackOrder.Random,
            null);
        Assert.NotNull(first);
        var deletedPath = state.RemainingPaths[0];

        var cacheTimestamp = File.GetLastWriteTimeUtc(_cachePath);
        File.Delete(deletedPath);
        WaitForCacheRewrite(cacheTimestamp);

        var next = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            state,
            WallpaperPlaybackOrder.Random,
            first);

        Assert.NotNull(next);
        Assert.NotEqual(deletedPath, next);
        Assert.DoesNotContain(state.RemainingPaths, path =>
            string.Equals(path, deletedPath, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(state.SeenPaths, path =>
            string.Equals(path, deletedPath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SelectImage_Random_OrientationChangeStartsNewCycle()
    {
        var landscapePath = Path.Combine(_testFolder, "landscape.png");
        var portraitPath = Path.Combine(_testFolder, "portrait.png");
        CreateFakePng(landscapePath, 1920, 1080);
        CreateFakePng(portraitPath, 1080, 1920);
        _selector.PrewarmFolder(_testFolder);
        WaitForScan();

        var state = new RandomPlaybackState();
        var landscape = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            state,
            WallpaperPlaybackOrder.Random,
            null);
        var landscapeKey = state.CandidateKey;

        var portrait = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Portrait,
            state,
            WallpaperPlaybackOrder.Random,
            landscape);

        Assert.Equal(landscapePath, landscape);
        Assert.Equal(portraitPath, portrait);
        Assert.NotEqual(landscapeKey, state.CandidateKey);
        Assert.Equal(portraitPath, state.SeenPaths.Single());
    }

    [Fact]
    public void SelectImage_WithMinimumSideLength_ExcludesLowResolutionImages()
    {
        var small = Path.Combine(_testFolder, "small.png");
        var large = Path.Combine(_testFolder, "large.png");
        CreateFakePng(small, 800, 1200);
        CreateFakePng(large, 1920, 1080);

        _selector.PrewarmFolder(_testFolder);
        WaitForScan();

        var result = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.Sequential,
            null,
            1024);

        Assert.Equal(large, result);
    }

    [Fact]
    public void SelectImage_WithMinimumSideLength_RequiresBothDimensionsToMeetThreshold()
    {
        var tooNarrow = Path.Combine(_testFolder, "too-narrow.png");
        var tooShort = Path.Combine(_testFolder, "too-short.png");
        CreateFakePng(tooNarrow, 900, 1600);
        CreateFakePng(tooShort, 1600, 900);

        _selector.PrewarmFolder(_testFolder);
        WaitForScan();

        var result = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.Random,
            null,
            1024);

        Assert.Null(result);
    }

    [Fact]
    public void SelectImage_WithMinimumSideLength_ExcludesUnreadableImages()
    {
        var invalid = Path.Combine(_testFolder, "broken.png");
        CreateInvalidImageFile(invalid, 64);

        _selector.PrewarmFolder(_testFolder);
        WaitForFolderScanToCompleteWithoutSelection();

        var result = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.Random,
            null,
            1024);

        Assert.Null(result);
    }

    [Fact]
    public void SelectImage_WithMinimumSideLength_SequentialUsesFilteredPool()
    {
        var low = Path.Combine(_testFolder, "a.png");
        var high1 = Path.Combine(_testFolder, "b.png");
        var high2 = Path.Combine(_testFolder, "c.png");
        CreateFakePng(low, 900, 900);
        CreateFakePng(high1, 1200, 1200);
        CreateFakePng(high2, 1400, 1400);

        _selector.PrewarmFolder(_testFolder);
        WaitForScan();

        var expected = new[] { high1, high2 }.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();

        var first = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Square,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.Sequential,
            null,
            1024);

        var second = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Square,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.Sequential,
            first,
            1024);

        Assert.Equal(expected[0], first);
        Assert.Equal(expected[1], second);
    }

    [Fact]
    public void SelectImage_Sequential_UsesFullPathAscending()
    {
        var alpha = Path.Combine(_testFolder, "b", "01.png");
        var beta = Path.Combine(_testFolder, "a", "02.png");
        Directory.CreateDirectory(Path.GetDirectoryName(alpha)!);
        Directory.CreateDirectory(Path.GetDirectoryName(beta)!);
        CreateFakePng(alpha, 1920, 1080);
        CreateFakePng(beta, 1920, 1080);

        _selector.PrewarmFolder(_testFolder);
        WaitForScan();

        var ordered = new[] { alpha, beta }.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();

        var first = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.Sequential,
            null);

        var second = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.Sequential,
            first);

        Assert.Equal(ordered[0], first);
        Assert.Equal(ordered[1], second);
    }

    [Fact]
    public void SelectImage_ReverseSequential_UsesFullPathDescending()
    {
        var alpha = Path.Combine(_testFolder, "b", "01.png");
        var beta = Path.Combine(_testFolder, "a", "02.png");
        Directory.CreateDirectory(Path.GetDirectoryName(alpha)!);
        Directory.CreateDirectory(Path.GetDirectoryName(beta)!);
        CreateFakePng(alpha, 1920, 1080);
        CreateFakePng(beta, 1920, 1080);

        _selector.PrewarmFolder(_testFolder);
        WaitForScan();

        var ordered = new[] { alpha, beta }.OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase).ToArray();

        var first = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.ReverseSequential,
            null);

        var second = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.ReverseSequential,
            first);

        Assert.Equal(ordered[0], first);
        Assert.Equal(ordered[1], second);
    }

    [Fact]
    public void SelectImage_Sequential_ExcludesRemovedImageCaseInsensitively()
    {
        var removed = Path.Combine(_testFolder, "a.png");
        var eligible = Path.Combine(_testFolder, "b.png");
        CreateFakePng(removed, 1920, 1080);
        CreateFakePng(eligible, 1920, 1080);
        _selector.PrewarmFolder(_testFolder);
        WaitForScan();

        var result = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.Sequential,
            null,
            excludedImagePaths: [removed.ToUpperInvariant()]);

        Assert.Equal(eligible, result);
    }

    [Fact]
    public void SelectImage_Random_RemovedImageIsPurgedFromCurrentCycle()
    {
        var paths = Enumerable.Range(1, 3)
            .Select(index => Path.Combine(_testFolder, $"{index}.png"))
            .ToArray();
        foreach (var path in paths)
            CreateFakePng(path, 1920, 1080);
        _selector.PrewarmFolder(_testFolder);
        WaitForScan();

        var state = new RandomPlaybackState();
        var first = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            state,
            WallpaperPlaybackOrder.Random,
            null);
        Assert.NotNull(first);

        var removed = state.RemainingPaths[0];
        var next = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            state,
            WallpaperPlaybackOrder.Random,
            first,
            excludedImagePaths: [removed]);

        Assert.NotNull(next);
        Assert.NotEqual(removed, next);
        Assert.DoesNotContain(state.RemainingPaths, path =>
            string.Equals(path, removed, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(state.SeenPaths, path =>
            string.Equals(path, removed, StringComparison.OrdinalIgnoreCase));

        var restored = _selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            state,
            WallpaperPlaybackOrder.Random,
            next);

        Assert.Equal(removed, restored);
    }

    [Fact]
    public void DimensionCache_SavesAsFolderMapWithRelativePaths()
    {
        var nestedPath = Path.Combine(_testFolder, "nested", "test.png");
        Directory.CreateDirectory(Path.GetDirectoryName(nestedPath)!);
        CreateFakePng(nestedPath, 1920, 1080);

        _selector.PrewarmFolder(_testFolder);
        WaitForScan(_selector, _testFolder);
        _selector.Dispose();

        Assert.True(File.Exists(_cachePath));

        using var doc = JsonDocument.Parse(File.ReadAllText(_cachePath));
        Assert.True(doc.RootElement.TryGetProperty(NormalizeFolderPath(_testFolder), out var folderNode));
        Assert.True(folderNode.TryGetProperty("nested/test.png", out var fileNode));
        Assert.Equal(1920, fileNode.GetProperty("width").GetInt32());
        Assert.Equal(1080, fileNode.GetProperty("height").GetInt32());
        Assert.Equal(new FileInfo(nestedPath).Length, fileNode.GetProperty("length").GetInt64());
        Assert.True(fileNode.GetProperty("lastWriteTimeUtcTicks").GetInt64() > 0);
        Assert.False(fileNode.TryGetProperty("filePath", out _));
        Assert.False(fileNode.TryGetProperty("fileSize", out _));
    }

    [Fact]
    public void DimensionCache_LoadsFolderMapAndUsesRelativePathsForOrientationBuckets()
    {
        var portraitPath = Path.Combine(_testFolder, "a.png");
        var landscapePath = Path.Combine(_testFolder, "b.png");
        CreateInvalidImageFile(portraitPath, 32);
        CreateInvalidImageFile(landscapePath, 48);
        var portraitInfo = new FileInfo(portraitPath);
        var landscapeInfo = new FileInfo(landscapePath);

        var rootFolderJson = JsonSerializer.Serialize(NormalizeFolderPath(_testFolder));
        var cacheJson = $$"""
        {
          {{rootFolderJson}}: {
            "a.png": {
              "width": 800,
              "height": 1200,
              "length": {{portraitInfo.Length}},
              "lastWriteTimeUtcTicks": {{portraitInfo.LastWriteTimeUtc.Ticks}}
            },
            "b.png": {
              "width": 1600,
              "height": 900,
              "length": {{landscapeInfo.Length}},
              "lastWriteTimeUtcTicks": {{landscapeInfo.LastWriteTimeUtc.Ticks}}
            }
          }
        }
        """;
        File.WriteAllText(_cachePath, cacheJson);

        using var selector = CreateSelector();
        selector.PrewarmFolder(_testFolder);
        WaitForScan(selector, _testFolder);

        var result = selector.SelectImage(
            _testFolder,
            ImageOrientation.Landscape,
            new RandomPlaybackState(),
            WallpaperPlaybackOrder.Sequential,
            null);

        Assert.Equal(landscapePath, result);
    }

    [Fact]
    public void DimensionCache_LargeHistory_LoadsAndRetainsOnlyConfiguredExistingRoots()
    {
        var activePath = Path.Combine(_testFolder, "active.png");
        CreateInvalidImageFile(activePath, 32);
        var activeInfo = new FileInfo(activePath);
        var staleRoot = Path.Combine(_cacheFolder, "stale-root");
        Directory.CreateDirectory(staleRoot);

        using (var stream = File.Create(_cachePath))
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName(NormalizeFolderPath(staleRoot));
            writer.WriteStartObject();
            for (var index = 0; index < 2_000; index++)
            {
                writer.WritePropertyName($"history/{index:D5}.png");
                WriteDimensionMetadata(writer, 1920, 1080, 24, 1);
            }
            writer.WriteEndObject();

            writer.WritePropertyName(NormalizeFolderPath(_testFolder));
            writer.WriteStartObject();
            writer.WritePropertyName("active.png");
            WriteDimensionMetadata(
                writer,
                1600,
                900,
                activeInfo.Length,
                activeInfo.LastWriteTimeUtc.Ticks);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        using (var selector = new ImageSelector(
            NullLogger<ImageSelector>.Instance,
            _cachePath,
            [_testFolder]))
        {
            selector.PrewarmFolder(_testFolder);
            WaitForScan(selector, _testFolder);

            var selected = selector.SelectImage(
                _testFolder,
                ImageOrientation.Landscape,
                new RandomPlaybackState(),
                WallpaperPlaybackOrder.Sequential,
                null);

            Assert.Equal(activePath, selected);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(_cachePath));
        var root = Assert.Single(doc.RootElement.EnumerateObject());
        Assert.Equal(NormalizeFolderPath(_testFolder), root.Name, ignoreCase: true);
        Assert.True(root.Value.TryGetProperty("active.png", out _));
    }

    [Fact]
    public void DimensionCache_ConfiguredButUnavailableRoot_IsNotLoadedOrPersisted()
    {
        var unavailableRoot = Path.Combine(_cacheFolder, "unavailable-root");
        var rootJson = JsonSerializer.Serialize(NormalizeFolderPath(unavailableRoot));
        File.WriteAllText(
            _cachePath,
            $$"""
            {
              {{rootJson}}: {
                "old.png": {
                  "width": 1920,
                  "height": 1080,
                  "length": 24,
                  "lastWriteTimeUtcTicks": 1
                }
              }
            }
            """);

        using (var selector = new ImageSelector(
            NullLogger<ImageSelector>.Instance,
            _cachePath,
            [unavailableRoot]))
        {
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(_cachePath));
        Assert.Empty(doc.RootElement.EnumerateObject());
    }

    [Fact]
    public void DimensionCache_RemovedRuntimeAssignment_ReleasesAndRemovesFolderCache()
    {
        var imagePath = Path.Combine(_testFolder, "active.png");
        CreateFakePng(imagePath, 1920, 1080);
        var settingsService = new FakeSettingsService(new AppSettings
        {
            MonitorAssignments =
            [
                new WallpaperAssignment
                {
                    MonitorDevicePath = "display-1",
                    FolderPath = _testFolder,
                    Enabled = true
                }
            ]
        });

        using (var selector = new ImageSelector(
            NullLogger<ImageSelector>.Instance,
            settingsService,
            _cachePath))
        {
            selector.PrewarmFolder(_testFolder);
            WaitForScan(selector, _testFolder);

            settingsService.Save(new AppSettings());

            var selected = selector.SelectImage(
                _testFolder,
                ImageOrientation.Landscape,
                new RandomPlaybackState(),
                WallpaperPlaybackOrder.Sequential,
                null);

            Assert.Null(selected);
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(_cachePath));
        Assert.Empty(doc.RootElement.EnumerateObject());
    }

    [Fact]
    public void DimensionCache_RenameUpdatesSavedRelativePath()
    {
        var originalPath = Path.Combine(_testFolder, "nested", "old.png");
        var renamedPath = Path.Combine(_testFolder, "nested", "new.png");
        Directory.CreateDirectory(Path.GetDirectoryName(originalPath)!);
        CreateFakePng(originalPath, 1920, 1080);

        _selector.PrewarmFolder(_testFolder);
        WaitForScan(_selector, _testFolder);

        File.Move(originalPath, renamedPath);
        WaitForSelectedImage(_selector, _testFolder, renamedPath, timeoutMs: 8000);
        _selector.Dispose();

        using var doc = JsonDocument.Parse(File.ReadAllText(_cachePath));
        var folderNode = doc.RootElement.GetProperty(NormalizeFolderPath(_testFolder));
        Assert.True(folderNode.TryGetProperty("nested/new.png", out _));
        Assert.False(folderNode.TryGetProperty("nested/old.png", out _));
    }

    private ImageSelector CreateSelector() => new(NullLogger<ImageSelector>.Instance, _cachePath);

    private void WaitForScan() => WaitForScan(_selector, _testFolder);

    private static void WaitForScan(ImageSelector selector, string folderPath)
    {
        for (var i = 0; i < 20; i++)
        {
            var result = selector.SelectImage(
                folderPath,
                ImageOrientation.Landscape,
                new RandomPlaybackState(),
                WallpaperPlaybackOrder.Random,
                null);
            if (result != null)
                return;

            Thread.Sleep(50);
        }

        Assert.Fail("Timed out waiting for ImageSelector folder scan to complete.");
    }

    private static void WaitForSelectedImage(
        ImageSelector selector,
        string folderPath,
        string expectedPath,
        int timeoutMs)
    {
        var attempts = Math.Max(1, timeoutMs / 100);
        for (var i = 0; i < attempts; i++)
        {
            var result = selector.SelectImage(
                folderPath,
                ImageOrientation.Landscape,
                new RandomPlaybackState(),
                WallpaperPlaybackOrder.Sequential,
                null);
            if (string.Equals(result, expectedPath, StringComparison.OrdinalIgnoreCase))
                return;

            Thread.Sleep(100);
        }

        Assert.Fail($"Timed out waiting for ImageSelector to return '{expectedPath}'.");
    }

    private void WaitForFolderScanToCompleteWithoutSelection()
    {
        for (var i = 0; i < 20; i++)
        {
            if (File.Exists(_cachePath))
                return;

            Thread.Sleep(50);
        }

        Assert.Fail("Timed out waiting for ImageSelector folder scan to complete.");
    }

    private void WaitForCacheRewrite(DateTime previousTimestamp)
    {
        for (var i = 0; i < 100; i++)
        {
            if (File.Exists(_cachePath) && File.GetLastWriteTimeUtc(_cachePath) > previousTimestamp)
                return;

            Thread.Sleep(100);
        }

        Assert.Fail("Timed out waiting for ImageSelector to rescan after a file change.");
    }

    private static void CreateFakePng(string path, int width, int height)
    {
        // Minimal PNG header (signature + IHDR)
        var bytes = new byte[24];
        // PNG signature
        bytes[0] = 0x89; bytes[1] = 0x50; bytes[2] = 0x4E; bytes[3] = 0x47;
        bytes[4] = 0x0D; bytes[5] = 0x0A; bytes[6] = 0x1A; bytes[7] = 0x0A;
        // IHDR length (13)
        bytes[8] = 0; bytes[9] = 0; bytes[10] = 0; bytes[11] = 13;
        // "IHDR"
        bytes[12] = 0x49; bytes[13] = 0x48; bytes[14] = 0x44; bytes[15] = 0x52;
        // Width (big-endian)
        bytes[16] = (byte)(width >> 24); bytes[17] = (byte)(width >> 16);
        bytes[18] = (byte)(width >> 8); bytes[19] = (byte)(width);
        // Height (big-endian)
        bytes[20] = (byte)(height >> 24); bytes[21] = (byte)(height >> 16);
        bytes[22] = (byte)(height >> 8); bytes[23] = (byte)(height);
        File.WriteAllBytes(path, bytes);
    }

    private static void CreateInvalidImageFile(string path, int size)
    {
        File.WriteAllBytes(path, Enumerable.Repeat((byte)0x42, size).ToArray());
    }

    private static void WriteDimensionMetadata(
        Utf8JsonWriter writer,
        int width,
        int height,
        long length,
        long lastWriteTimeUtcTicks)
    {
        writer.WriteStartObject();
        writer.WriteNumber("width", width);
        writer.WriteNumber("height", height);
        writer.WriteNumber("length", length);
        writer.WriteNumber("lastWriteTimeUtcTicks", lastWriteTimeUtcTicks);
        writer.WriteEndObject();
    }

    private static string NormalizeFolderPath(string folderPath)
    {
        var fullPath = Path.GetFullPath(folderPath);
        var root = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private sealed class FakeSettingsService(AppSettings settings) : ISettingsService
    {
        private AppSettings _settings = settings;

        public event EventHandler<AppSettings>? SettingsChanged;

        public AppSettings Load() => _settings;

        public void Save(AppSettings newSettings, bool notifyChanged = true)
        {
            _settings = newSettings;
            if (notifyChanged)
                SettingsChanged?.Invoke(this, newSettings);
        }

        public RuntimeState LoadState() => new();

        public void UpdateState(Action<RuntimeState> update)
        {
            var state = new RuntimeState();
            update(state);
        }
    }

    public void Dispose()
    {
        _selector.Dispose();

        if (Directory.Exists(_testFolder))
            Directory.Delete(_testFolder, recursive: true);
        if (Directory.Exists(_cacheFolder))
            Directory.Delete(_cacheFolder, recursive: true);
    }
}
