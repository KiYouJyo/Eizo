using Eizo.Cache;

namespace Eizo.Cache.Tests;

public sealed class DiskCacheStoreTests
{
    [Fact]
    public async Task WriteReadAndSnapshot_TracksRealFileSize()
    {
        var root = CreateTempDirectory();

        try
        {
            var store = new DiskCacheStore(root);
            var entry = await store.WriteTextAsync(
                CacheCategory.Metadata,
                "bangumi:calendar",
                "cached payload",
                new CacheWriteOptions(
                    "Bangumi API · calendar",
                    "Bangumi",
                    ".json"));

            Assert.True(File.Exists(entry.Path));

            var text = await store.ReadTextAsync(
                CacheCategory.Metadata,
                "bangumi:calendar");
            Assert.Equal("cached payload", text);

            var snapshot =
                await store.GetSnapshotAsync();

            Assert.Single(snapshot.Entries);
            Assert.Equal(
                new FileInfo(entry.Path).Length,
                snapshot.TotalBytes);
            Assert.Equal(
                snapshot.TotalBytes,
                snapshot.CategoryBytes[
                    CacheCategory.Metadata]);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task EnforceLimit_RemovesLeastRecentUnpinnedEntry()
    {
        var root = CreateTempDirectory();

        try
        {
            var store = new DiskCacheStore(root);

            var pinned = await store.WriteBytesAsync(
                CacheCategory.Media,
                "pinned",
                new byte[60],
                new CacheWriteOptions(
                    "Pinned",
                    "WebDAV",
                    ".bin",
                    Pinned: true));

            await Task.Delay(20);

            var disposable = await store.WriteBytesAsync(
                CacheCategory.Media,
                "disposable",
                new byte[60],
                new CacheWriteOptions(
                    "Disposable",
                    "WebDAV",
                    ".bin"));

            var result = await store.EnforceLimitAsync(
                new CachePolicy(
                    LimitBytes: 70,
                    AutomaticCleanup: true,
                    PreservePinned: true,
                    RemotePrecacheBytes:
                        CacheDefaults.RemotePrecacheBytes));

            Assert.Equal(120L, result.BytesBefore);
            Assert.Equal(60L, result.BytesAfter);
            Assert.Equal(1, result.EntriesRemoved);
            Assert.True(File.Exists(pinned.Path));
            Assert.False(File.Exists(disposable.Path));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ClearCategory_DoesNotDeleteOtherCategories()
    {
        var root = CreateTempDirectory();

        try
        {
            var store = new DiskCacheStore(root);

            await store.WriteBytesAsync(
                CacheCategory.Artwork,
                "poster",
                new byte[16]);

            await store.WriteBytesAsync(
                CacheCategory.Metadata,
                "subject",
                new byte[8]);

            var removed = await store.ClearAsync(
                CacheCategory.Artwork);

            Assert.Equal(1, removed);

            var snapshot =
                await store.GetSnapshotAsync();

            Assert.Single(snapshot.Entries);
            Assert.Equal(
                CacheCategory.Metadata,
                snapshot.Entries[0].Category);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "Eizo-Cache-Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(
        string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
