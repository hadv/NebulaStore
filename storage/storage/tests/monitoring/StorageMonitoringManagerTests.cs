using Xunit;
using NebulaStore.Storage.Monitoring;

namespace NebulaStore.Storage.Tests.Monitoring;

public class StorageMonitoringManagerTests
{
    [Fact]
    public void StorageMonitoringManager_WithValidMonitors_ShouldInitializeCorrectly()
    {
        // Arrange
        var storageManagerMonitor = new TestStorageManagerMonitor();
        var entityCacheSummaryMonitor = new TestEntityCacheSummaryMonitor();
        var objectRegistryMonitor = new TestObjectRegistryMonitor();
        var entityCacheMonitors = new[] { new TestEntityCacheMonitor(1), new TestEntityCacheMonitor(2) };
        var housekeepingMonitors = new[] { new TestHousekeepingMonitor(1), new TestHousekeepingMonitor(2) };

        // Act
        var manager = new StorageMonitoringManager(
            storageManagerMonitor,
            entityCacheSummaryMonitor,
            objectRegistryMonitor,
            entityCacheMonitors,
            housekeepingMonitors);

        // Assert
        Assert.Same(storageManagerMonitor, manager.StorageManagerMonitor);
        Assert.Same(entityCacheSummaryMonitor, manager.EntityCacheSummaryMonitor);
        Assert.Same(objectRegistryMonitor, manager.ObjectRegistryMonitor);
        Assert.Equal(2, manager.EntityCacheMonitors.Count);
        Assert.Equal(2, manager.HousekeepingMonitors.Count);
        Assert.Equal(7, manager.AllMonitors.Count); // 1 + 1 + 1 + 2 + 2
    }

    [Fact]
    public void StorageMonitoringManager_GetMonitor_ShouldReturnCorrectMonitor()
    {
        // Arrange
        var storageManagerMonitor = new TestStorageManagerMonitor();
        var entityCacheSummaryMonitor = new TestEntityCacheSummaryMonitor();
        var objectRegistryMonitor = new TestObjectRegistryMonitor();
        var entityCacheMonitors = new[] { new TestEntityCacheMonitor(1) };
        var housekeepingMonitors = new[] { new TestHousekeepingMonitor(1) };

        var manager = new StorageMonitoringManager(
            storageManagerMonitor,
            entityCacheSummaryMonitor,
            objectRegistryMonitor,
            entityCacheMonitors,
            housekeepingMonitors);

        // Act & Assert
        Assert.Same(storageManagerMonitor, manager.GetMonitor("TestStorageManager"));
        Assert.Same(entityCacheSummaryMonitor, manager.GetMonitor("TestEntityCacheSummary"));
        Assert.Same(objectRegistryMonitor, manager.GetMonitor("TestObjectRegistry"));
        Assert.Null(manager.GetMonitor("NonExistent"));
    }

    [Fact]
    public void StorageMonitoringManager_GetMonitors_ShouldReturnCorrectTypes()
    {
        // Arrange
        var storageManagerMonitor = new TestStorageManagerMonitor();
        var entityCacheSummaryMonitor = new TestEntityCacheSummaryMonitor();
        var objectRegistryMonitor = new TestObjectRegistryMonitor();
        var entityCacheMonitors = new[] { new TestEntityCacheMonitor(1), new TestEntityCacheMonitor(2) };
        var housekeepingMonitors = new[] { new TestHousekeepingMonitor(1) };

        var manager = new StorageMonitoringManager(
            storageManagerMonitor,
            entityCacheSummaryMonitor,
            objectRegistryMonitor,
            entityCacheMonitors,
            housekeepingMonitors);

        // Act
        var entityCacheMonitorResults = manager.GetMonitors<IEntityCacheMonitor>().ToList();
        var housekeepingMonitorResults = manager.GetMonitors<IStorageChannelHousekeepingMonitor>().ToList();

        // Assert
        Assert.Equal(2, entityCacheMonitorResults.Count);
        Assert.Single(housekeepingMonitorResults);
    }

    [Fact]
    public void StorageMonitoringManager_WithNullArguments_ShouldThrowArgumentNullException()
    {
        // Arrange
        var storageManagerMonitor = new TestStorageManagerMonitor();
        var entityCacheSummaryMonitor = new TestEntityCacheSummaryMonitor();
        var objectRegistryMonitor = new TestObjectRegistryMonitor();
        var entityCacheMonitors = new[] { new TestEntityCacheMonitor(1) };
        var housekeepingMonitors = new[] { new TestHousekeepingMonitor(1) };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StorageMonitoringManager(
            null!, entityCacheSummaryMonitor, objectRegistryMonitor, entityCacheMonitors, housekeepingMonitors));
        
        Assert.Throws<ArgumentNullException>(() => new StorageMonitoringManager(
            storageManagerMonitor, null!, objectRegistryMonitor, entityCacheMonitors, housekeepingMonitors));
        
        Assert.Throws<ArgumentNullException>(() => new StorageMonitoringManager(
            storageManagerMonitor, entityCacheSummaryMonitor, null!, entityCacheMonitors, housekeepingMonitors));
        
        Assert.Throws<ArgumentNullException>(() => new StorageMonitoringManager(
            storageManagerMonitor, entityCacheSummaryMonitor, objectRegistryMonitor, null!, housekeepingMonitors));
        
        Assert.Throws<ArgumentNullException>(() => new StorageMonitoringManager(
            storageManagerMonitor, entityCacheSummaryMonitor, objectRegistryMonitor, entityCacheMonitors, null!));
    }
}

// Test helper classes
internal class TestStorageManagerMonitor : IStorageManagerMonitor
{
    public string Name => "TestStorageManager";
    public StorageStatistics StorageStatistics => new(1, 1, 1000, 800, Array.Empty<ChannelStatistics>());
    public void IssueFullGarbageCollection() { }
    public void IssueFullFileCheck() { }
    public void IssueFullCacheCheck() { }
}

internal class TestEntityCacheSummaryMonitor : IEntityCacheSummaryMonitor
{
    public string Name => "TestEntityCacheSummary";
    public long UsedCacheSize => 5000;
    public long EntityCount => 100;
}

internal class TestObjectRegistryMonitor : IObjectRegistryMonitor
{
    public string Name => "TestObjectRegistry";
    public long Size => 250;
    public long Capacity => 1000;
}

internal class TestEntityCacheMonitor : IEntityCacheMonitor
{
    public string Name { get; }
    public int ChannelIndex { get; }
    public long LastSweepStart => 1000;
    public long LastSweepEnd => 2000;
    public long EntityCount => 50;
    public long UsedCacheSize => 2500;

    public TestEntityCacheMonitor(int channelIndex)
    {
        ChannelIndex = channelIndex;
        Name = $"TestEntityCache-{channelIndex}";
    }
}

internal class TestHousekeepingMonitor : IStorageChannelHousekeepingMonitor
{
    public string Name { get; }
    public bool EntityCacheCheckResult => true;
    public long EntityCacheCheckStartTime => 1000;
    public long EntityCacheCheckDuration => 500;
    public long EntityCacheCheckBudget => 2000;
    public bool GarbageCollectionResult => true;
    public long GarbageCollectionStartTime => 1500;
    public long GarbageCollectionDuration => 750;
    public long GarbageCollectionBudget => 3000;
    public bool FileCleanupCheckResult => false;
    public long FileCleanupCheckStartTime => 2000;
    public long FileCleanupCheckDuration => 1000;
    public long FileCleanupCheckBudget => 4000;

    public TestHousekeepingMonitor(int channelIndex)
    {
        Name = $"TestHousekeeping-{channelIndex}";
    }
}
