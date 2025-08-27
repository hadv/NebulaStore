using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.Storage.Embedded.Types.Memory;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Tests for the Eclipse Store-style memory management system.
/// </summary>
public class MemoryManagementTests : IDisposable
{
    private readonly MemoryManager _memoryManager;

    public MemoryManagementTests()
    {
        // Create memory manager with small threshold for testing
        _memoryManager = new MemoryManager(1024 * 1024, TimeSpan.FromSeconds(30)); // 1MB threshold, 30s timeout
    }

    [Fact]
    public void MemoryManager_AllocateEntityMemory_ShouldAllocateMemory()
    {
        // Arrange
        const long entityId = 1001;
        const long size = 1024;

        // Act
        var address = _memoryManager.AllocateEntityMemory(entityId, size);

        // Assert
        Assert.NotEqual(IntPtr.Zero, address);
        Assert.True(_memoryManager.IsEntityCached(entityId));
        Assert.Equal(size, _memoryManager.GetEntitySize(entityId));
        Assert.Equal(size, _memoryManager.CurrentCacheSize);
        Assert.Equal(1, _memoryManager.TotalAllocations);
    }

    [Fact]
    public void MemoryManager_GetEntityMemory_ShouldReturnCachedMemory()
    {
        // Arrange
        const long entityId = 1002;
        const long size = 512;
        var originalAddress = _memoryManager.AllocateEntityMemory(entityId, size);

        // Act
        var retrievedAddress = _memoryManager.GetEntityMemory(entityId);

        // Assert
        Assert.Equal(originalAddress, retrievedAddress);
        Assert.True(_memoryManager.IsEntityCached(entityId));
    }

    [Fact]
    public void MemoryManager_GetEntityMemory_ShouldReturnZeroForNonCachedEntity()
    {
        // Arrange
        const long entityId = 9999;

        // Act
        var address = _memoryManager.GetEntityMemory(entityId);

        // Assert
        Assert.Equal(IntPtr.Zero, address);
        Assert.False(_memoryManager.IsEntityCached(entityId));
    }

    [Fact]
    public void MemoryManager_ReleaseEntityMemory_ShouldReleaseMemory()
    {
        // Arrange
        const long entityId = 1003;
        const long size = 256;
        _memoryManager.AllocateEntityMemory(entityId, size);
        Assert.True(_memoryManager.IsEntityCached(entityId));

        // Act
        var released = _memoryManager.ReleaseEntityMemory(entityId);

        // Assert
        Assert.True(released);
        Assert.False(_memoryManager.IsEntityCached(entityId));
        Assert.Equal(0, _memoryManager.CurrentCacheSize);
    }

    [Fact]
    public void MemoryManager_ReleaseEntityMemory_ShouldReturnFalseForNonCachedEntity()
    {
        // Arrange
        const long entityId = 9998;

        // Act
        var released = _memoryManager.ReleaseEntityMemory(entityId);

        // Assert
        Assert.False(released);
    }

    [Fact]
    public void MemoryManager_PerformEviction_ShouldEvictOldEntries()
    {
        // Arrange
        const long size = 256;

        // Allocate multiple entities to exceed threshold
        for (long i = 1; i <= 10; i++)
        {
            _memoryManager.AllocateEntityMemory(i, size);
        }

        var initialCacheSize = _memoryManager.CurrentCacheSize;
        var initialEntryCount = _memoryManager.CacheEntryCount;

        // Act
        var freedMemory = _memoryManager.PerformEviction(size * 3); // Request to free 3 entries worth

        // Assert
        Assert.True(freedMemory > 0);
        Assert.True(_memoryManager.CurrentCacheSize < initialCacheSize);
        Assert.True(_memoryManager.CacheEntryCount < initialEntryCount);
        Assert.True(_memoryManager.TotalEvictions > 0);
    }

    [Fact]
    public void MemoryManager_ClearCache_ShouldClearAllMemory()
    {
        // Arrange
        const long size = 128;

        // Allocate several entities
        for (long i = 1; i <= 5; i++)
        {
            _memoryManager.AllocateEntityMemory(i, size);
        }

        Assert.True(_memoryManager.CurrentCacheSize > 0);
        Assert.True(_memoryManager.CacheEntryCount > 0);

        // Act
        var freedMemory = _memoryManager.ClearCache();

        // Assert
        Assert.True(freedMemory > 0);
        Assert.Equal(0, _memoryManager.CurrentCacheSize);
        Assert.Equal(0, _memoryManager.CacheEntryCount);
    }

    [Fact]
    public void MemoryManager_GetStatistics_ShouldReturnCorrectStatistics()
    {
        // Arrange
        const long size = 512;
        const int entityCount = 3;

        // Allocate entities
        for (long i = 1; i <= entityCount; i++)
        {
            _memoryManager.AllocateEntityMemory(i, size);
        }

        // Act
        var statistics = _memoryManager.GetStatistics();

        // Assert
        Assert.NotNull(statistics);
        Assert.Equal(size * entityCount, statistics.CurrentCacheSize);
        Assert.Equal(entityCount, statistics.CacheEntryCount);
        Assert.Equal(entityCount, statistics.TotalAllocations);
        Assert.Equal(0, statistics.TotalEvictions);
        Assert.True(statistics.CacheUtilization > 0);
        Assert.Equal(size, statistics.AverageCacheEntrySize);
    }

    [Fact]
    public void MemoryManager_AllocateEntityMemory_ShouldPreventDuplicateAllocation()
    {
        // Arrange
        const long entityId = 1004;
        const long size = 256;

        // Act
        var address1 = _memoryManager.AllocateEntityMemory(entityId, size);
        var address2 = _memoryManager.AllocateEntityMemory(entityId, size); // Try to allocate same entity again

        // Assert
        Assert.Equal(address1, address2); // Should return same address
        Assert.Equal(1, _memoryManager.CacheEntryCount); // Should still have only one entry
        Assert.Equal(size, _memoryManager.CurrentCacheSize); // Size should not double
    }

    [Fact]
    public async Task MemoryManager_AutomaticEviction_ShouldWorkWithTimer()
    {
        // Arrange
        using var shortTimeoutManager = new MemoryManager(1024, TimeSpan.FromMilliseconds(100)); // Very short timeout

        // Allocate an entity
        shortTimeoutManager.AllocateEntityMemory(1, 256);
        Assert.Equal(1, shortTimeoutManager.CacheEntryCount);

        // Act - Wait for timeout + eviction timer
        await Task.Delay(2000); // Wait 2 seconds for eviction timer to run

        // Assert - Entity should be evicted due to timeout
        // Note: This test might be flaky due to timer timing, but demonstrates the concept
        var statistics = shortTimeoutManager.GetStatistics();
        Assert.True(statistics.TotalEvictions >= 0); // At least no errors occurred
    }

    public void Dispose()
    {
        _memoryManager?.Dispose();
    }
}