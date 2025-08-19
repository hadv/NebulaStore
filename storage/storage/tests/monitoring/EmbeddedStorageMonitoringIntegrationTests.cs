using Xunit;
using NebulaStore.Storage.Monitoring;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Storage.Tests.Monitoring;

public class EmbeddedStorageMonitoringIntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public EmbeddedStorageMonitoringIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "NebulaStore_MonitoringTests_" + Guid.NewGuid().ToString("N")[..8]);
    }

    [Fact]
    public void EmbeddedStorageManager_GetMonitoringManager_ShouldReturnValidManager()
    {
        // Arrange
        using var storage = EmbeddedStorage.Start(_testDirectory);

        // Act
        var monitoringManager = storage.GetMonitoringManager();

        // Assert
        Assert.NotNull(monitoringManager);
        Assert.NotNull(monitoringManager.StorageManagerMonitor);
        Assert.NotNull(monitoringManager.EntityCacheSummaryMonitor);
        Assert.NotNull(monitoringManager.ObjectRegistryMonitor);
        Assert.NotEmpty(monitoringManager.AllMonitors);
    }

    [Fact]
    public void EmbeddedStorageManager_MonitoringManager_ShouldHaveCorrectChannelCount()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetChannelCount(3)
            .Build();

        using var storage = EmbeddedStorage.Start(config);

        // Act
        var monitoringManager = storage.GetMonitoringManager();

        // Assert
        Assert.Equal(3, monitoringManager.EntityCacheMonitors.Count);
        Assert.Equal(3, monitoringManager.HousekeepingMonitors.Count);
        
        // Verify channel indices
        var channelIndices = monitoringManager.EntityCacheMonitors.Select(m => m.ChannelIndex).ToArray();
        Assert.Contains(0, channelIndices);
        Assert.Contains(1, channelIndices);
        Assert.Contains(2, channelIndices);
    }

    [Fact]
    public void StorageManagerMonitor_ShouldProvideValidStatistics()
    {
        // Arrange
        using var storage = EmbeddedStorage.Start(_testDirectory);
        var monitoringManager = storage.GetMonitoringManager();

        // Act
        var storageManagerMonitor = monitoringManager.StorageManagerMonitor;
        var statistics = storageManagerMonitor.StorageStatistics;

        // Assert
        Assert.NotNull(statistics);
        Assert.Equal("name=EmbeddedStorage", storageManagerMonitor.Name);
        Assert.True(statistics.ChannelCount >= 1);
        Assert.True(statistics.UsageRatio >= 0.0 && statistics.UsageRatio <= 1.0);
    }

    [Fact]
    public void StorageManagerMonitor_IssueFullGarbageCollection_ShouldNotThrow()
    {
        // Arrange
        using var storage = EmbeddedStorage.Start(_testDirectory);
        var monitoringManager = storage.GetMonitoringManager();
        var storageManagerMonitor = monitoringManager.StorageManagerMonitor;

        // Act & Assert - Should not throw
        storageManagerMonitor.IssueFullGarbageCollection();
        storageManagerMonitor.IssueFullFileCheck();
        storageManagerMonitor.IssueFullCacheCheck();
    }

    [Fact]
    public void EntityCacheMonitors_ShouldHaveUniqueNames()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetChannelCount(2)
            .Build();

        using var storage = EmbeddedStorage.Start(config);
        var monitoringManager = storage.GetMonitoringManager();

        // Act
        var entityCacheMonitors = monitoringManager.EntityCacheMonitors;
        var names = entityCacheMonitors.Select(m => m.Name).ToArray();

        // Assert
        Assert.Equal(2, names.Length);
        Assert.Contains("channel=channel-0,group=Entity cache", names);
        Assert.Contains("channel=channel-1,group=Entity cache", names);
        Assert.Equal(names.Length, names.Distinct().Count()); // All names should be unique
    }

    [Fact]
    public void HousekeepingMonitors_ShouldHaveUniqueNames()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetChannelCount(2)
            .Build();

        using var storage = EmbeddedStorage.Start(config);
        var monitoringManager = storage.GetMonitoringManager();

        // Act
        var housekeepingMonitors = monitoringManager.HousekeepingMonitors;
        var names = housekeepingMonitors.Select(m => m.Name).ToArray();

        // Assert
        Assert.Equal(2, names.Length);
        Assert.Contains("channel=channel-0,group=housekeeping", names);
        Assert.Contains("channel=channel-1,group=housekeeping", names);
        Assert.Equal(names.Length, names.Distinct().Count()); // All names should be unique
    }

    [Fact]
    public void MonitoringManager_GetMonitor_ShouldFindMonitorsByName()
    {
        // Arrange
        using var storage = EmbeddedStorage.Start(_testDirectory);
        var monitoringManager = storage.GetMonitoringManager();

        // Act
        var storageManagerMonitor = monitoringManager.GetMonitor("name=EmbeddedStorage");
        var entityCacheSummaryMonitor = monitoringManager.GetMonitor("name=EntityCacheSummary");
        var objectRegistryMonitor = monitoringManager.GetMonitor("name=ObjectRegistry");

        // Assert
        Assert.NotNull(storageManagerMonitor);
        Assert.NotNull(entityCacheSummaryMonitor);
        Assert.NotNull(objectRegistryMonitor);
        Assert.IsType<StorageManagerMonitor>(storageManagerMonitor);
        Assert.IsType<EntityCacheSummaryMonitor>(entityCacheSummaryMonitor);
        Assert.IsType<ObjectRegistryMonitor>(objectRegistryMonitor);
    }

    [Fact]
    public void MonitoringManager_GetMonitors_ShouldReturnCorrectTypes()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetChannelCount(2)
            .Build();

        using var storage = EmbeddedStorage.Start(config);
        var monitoringManager = storage.GetMonitoringManager();

        // Act
        var entityCacheMonitors = monitoringManager.GetMonitors<IEntityCacheMonitor>().ToList();
        var housekeepingMonitors = monitoringManager.GetMonitors<IStorageChannelHousekeepingMonitor>().ToList();
        var storageManagerMonitors = monitoringManager.GetMonitors<IStorageManagerMonitor>().ToList();

        // Assert
        Assert.Equal(2, entityCacheMonitors.Count);
        Assert.Equal(2, housekeepingMonitors.Count);
        Assert.Single(storageManagerMonitors);
    }

    [Fact]
    public void EntityCacheSummaryMonitor_ShouldAggregateFromAllChannels()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetChannelCount(3)
            .Build();

        using var storage = EmbeddedStorage.Start(config);
        var monitoringManager = storage.GetMonitoringManager();

        // Act
        var entityCacheSummaryMonitor = monitoringManager.EntityCacheSummaryMonitor;
        var totalEntityCount = entityCacheSummaryMonitor.EntityCount;
        var totalCacheSize = entityCacheSummaryMonitor.UsedCacheSize;

        // Assert
        Assert.Equal("name=EntityCacheSummary", entityCacheSummaryMonitor.Name);
        Assert.True(totalEntityCount >= 0);
        Assert.True(totalCacheSize >= 0);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
