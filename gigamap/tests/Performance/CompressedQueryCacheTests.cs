using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.GigaMap.Performance;

namespace NebulaStore.GigaMap.Tests.Performance;

public class CompressedQueryCacheTests : IDisposable
{
    public class TestEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    private readonly CompressedQueryCache<TestEntity> _cache;

    public CompressedQueryCacheTests()
    {
        _cache = new CompressedQueryCache<TestEntity>(
            defaultExpiry: TimeSpan.FromMinutes(5),
            maxCacheSize: 10);
    }

    public void Dispose()
    {
        _cache?.Dispose();
    }

    private static List<TestEntity> CreateTestEntities(int count)
    {
        var entities = new List<TestEntity>();
        for (int i = 0; i < count; i++)
        {
            entities.Add(new TestEntity
            {
                Name = $"Entity_{i}",
                Value = i * 10,
                Category = $"Category_{i % 3}"
            });
        }
        return entities;
    }

    [Fact]
    public async Task CacheResultAsync_ShouldCacheResults()
    {
        // Arrange
        var entities = CreateTestEntities(50);
        var querySignature = "test_query_1";

        // Act
        await _cache.CacheResultAsync(querySignature, entities);

        // Assert
        var stats = _cache.GetStatistics();
        Assert.Equal(1, stats.EntryCount);
        Assert.Equal(entities.Count, stats.TotalCachedResults);
        Assert.True(stats.TotalCompressedSize > 0);
        Assert.True(stats.TotalOriginalSize > 0);
        Assert.True(stats.CompressionRatio > 0);
        Assert.True(stats.MemorySavings > 0);
    }

    [Fact]
    public async Task TryGetCachedResultAsync_WithValidCache_ShouldReturnResults()
    {
        // Arrange
        var entities = CreateTestEntities(30);
        var querySignature = "test_query_2";
        await _cache.CacheResultAsync(querySignature, entities);

        // Act
        var cachedResult = await _cache.TryGetCachedResultAsync(querySignature);

        // Assert
        Assert.NotNull(cachedResult);
        Assert.Equal(entities.Count, cachedResult.Count);
        
        // Verify data integrity
        for (int i = 0; i < entities.Count; i++)
        {
            Assert.Equal(entities[i].Name, cachedResult[i].Name);
            Assert.Equal(entities[i].Value, cachedResult[i].Value);
            Assert.Equal(entities[i].Category, cachedResult[i].Category);
        }
    }

    [Fact]
    public async Task TryGetCachedResultAsync_WithNonExistentKey_ShouldReturnNull()
    {
        // Act
        var result = await _cache.TryGetCachedResultAsync("non_existent_key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CacheResultAsync_WithCustomExpiry_ShouldRespectExpiry()
    {
        // Arrange
        var entities = CreateTestEntities(20);
        var querySignature = "expiry_test";
        var customExpiry = TimeSpan.FromMilliseconds(100);

        // Act
        await _cache.CacheResultAsync(querySignature, entities, customExpiry);
        
        // Wait for expiry
        await Task.Delay(150);

        var result = await _cache.TryGetCachedResultAsync(querySignature);

        // Assert
        Assert.Null(result); // Should be expired and return null
    }

    [Fact]
    public async Task CacheResultAsync_ExceedingMaxSize_ShouldEvictOldest()
    {
        // Arrange
        var maxSize = 3;
        using var smallCache = new CompressedQueryCache<TestEntity>(
            defaultExpiry: TimeSpan.FromMinutes(10),
            maxCacheSize: maxSize);

        // Act - Add more entries than max size
        for (int i = 0; i < maxSize + 2; i++)
        {
            var entities = CreateTestEntities(10);
            await smallCache.CacheResultAsync($"query_{i}", entities);
        }

        // Assert
        var stats = smallCache.GetStatistics();
        Assert.Equal(maxSize, stats.EntryCount);

        // The first entry should be evicted
        var firstResult = await smallCache.TryGetCachedResultAsync("query_0");
        Assert.Null(firstResult);

        // The last entries should still be there
        var lastResult = await smallCache.TryGetCachedResultAsync($"query_{maxSize + 1}");
        Assert.NotNull(lastResult);
    }

    [Fact]
    public void GetStatistics_WithEmptyCache_ShouldReturnZeroStats()
    {
        // Act
        var stats = _cache.GetStatistics();

        // Assert
        Assert.Equal(0, stats.EntryCount);
        Assert.Equal(0, stats.TotalCachedResults);
        Assert.Equal(0, stats.TotalCompressedSize);
        Assert.Equal(0, stats.TotalOriginalSize);
        Assert.Equal(0, stats.CompressionRatio);
        Assert.Equal(0, stats.MemorySavings);
    }

    [Fact]
    public async Task GetStatistics_WithMultipleEntries_ShouldAggregateCorrectly()
    {
        // Arrange
        var entities1 = CreateTestEntities(20);
        var entities2 = CreateTestEntities(30);
        var entities3 = CreateTestEntities(25);

        await _cache.CacheResultAsync("query_1", entities1);
        await _cache.CacheResultAsync("query_2", entities2);
        await _cache.CacheResultAsync("query_3", entities3);

        // Act
        var stats = _cache.GetStatistics();

        // Assert
        Assert.Equal(3, stats.EntryCount);
        Assert.Equal(75, stats.TotalCachedResults); // 20 + 30 + 25
        Assert.True(stats.TotalCompressedSize > 0);
        Assert.True(stats.TotalOriginalSize > 0);
        Assert.True(stats.CompressionRatio > 0 && stats.CompressionRatio < 1.0);
        Assert.True(stats.MemorySavings > 0);
    }

    [Fact]
    public async Task Clear_ShouldRemoveAllEntries()
    {
        // Arrange
        var entities = CreateTestEntities(10);
        await _cache.CacheResultAsync("test_query", entities);

        // Act
        _cache.Clear();

        // Assert
        var stats = _cache.GetStatistics();
        Assert.Equal(0, stats.EntryCount);
        Assert.Equal(0, stats.TotalCachedResults);
    }

    [Fact]
    public async Task CacheResultAsync_WithNullResults_ShouldHandleGracefully()
    {
        // Arrange
        var querySignature = "null_test";

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () =>
            await _cache.CacheResultAsync(querySignature, null!));

        // Should not throw, but handle gracefully
        Assert.Null(exception);
    }

    [Fact]
    public async Task CacheResultAsync_WithEmptyResults_ShouldCache()
    {
        // Arrange
        var emptyResults = new List<TestEntity>();
        var querySignature = "empty_test";

        // Act
        await _cache.CacheResultAsync(querySignature, emptyResults);

        // Assert
        var stats = _cache.GetStatistics();
        Assert.Equal(1, stats.EntryCount);
        Assert.Equal(0, stats.TotalCachedResults);

        var cachedResult = await _cache.TryGetCachedResultAsync(querySignature);
        Assert.NotNull(cachedResult);
        Assert.Empty(cachedResult);
    }

    [Fact]
    public void CacheStatistics_ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var stats = new CacheStatistics
        {
            EntryCount = 5,
            TotalCachedResults = 150,
            TotalCompressedSize = 1024,
            TotalOriginalSize = 2048,
            CompressionRatio = 0.5,
            MemorySavings = 1024
        };

        // Act
        var result = stats.ToString();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("5 entries", result);
        Assert.Contains("150 results", result);
        Assert.Contains("1.0 KB compressed", result);
        Assert.Contains("1.0 KB", result);
        Assert.Contains("50.0%", result);
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var entityCount = 20;

        // Act - Perform concurrent cache operations
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var entities = CreateTestEntities(entityCount);
                await _cache.CacheResultAsync($"concurrent_query_{index}", entities);
            }));
        }

        for (int i = 0; i < 5; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                await _cache.TryGetCachedResultAsync($"concurrent_query_{index}");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var stats = _cache.GetStatistics();
        Assert.True(stats.EntryCount > 0);
        Assert.True(stats.TotalCachedResults > 0);
    }

    [Fact]
    public async Task Dispose_ShouldCleanupResources()
    {
        // Arrange
        var entities = CreateTestEntities(10);
        await _cache.CacheResultAsync("dispose_test", entities);

        // Act
        _cache.Dispose();

        // Assert
        var stats = _cache.GetStatistics();
        Assert.Equal(0, stats.EntryCount);
    }

    [Fact]
    public async Task CacheResultAsync_WithLargeData_ShouldCompress()
    {
        // Arrange
        var largeEntities = CreateTestEntities(1000);
        var querySignature = "large_data_test";

        // Act
        await _cache.CacheResultAsync(querySignature, largeEntities);

        // Assert
        var stats = _cache.GetStatistics();
        Assert.Equal(1, stats.EntryCount);
        Assert.Equal(1000, stats.TotalCachedResults);
        Assert.True(stats.CompressionRatio < 0.8); // Should achieve good compression
        Assert.True(stats.MemorySavings > 0);
    }

    [Fact]
    public async Task TryGetCachedResultAsync_AfterClear_ShouldReturnNull()
    {
        // Arrange
        var entities = CreateTestEntities(15);
        var querySignature = "clear_test";
        await _cache.CacheResultAsync(querySignature, entities);

        // Act
        _cache.Clear();
        var result = await _cache.TryGetCachedResultAsync(querySignature);

        // Assert
        Assert.Null(result);
    }
}
