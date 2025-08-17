using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using MessagePack;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Storage.Embedded.Tests;

public class EmbeddedStorageTests : IDisposable
{
    private readonly string _testDirectory;

    public EmbeddedStorageTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void Start_WithDefaultConfiguration_ShouldCreateStorageManager()
    {
        using var storage = EmbeddedStorage.Start();
        
        Assert.NotNull(storage);
        Assert.True(storage.IsRunning);
        Assert.True(storage.IsActive);
    }

    [Fact]
    public void Start_WithCustomDirectory_ShouldUseSpecifiedDirectory()
    {
        using var storage = EmbeddedStorage.Start(_testDirectory);
        
        Assert.NotNull(storage);
        Assert.Equal(_testDirectory, storage.Configuration.StorageDirectory);
    }

    [Fact]
    public void Start_WithRootObject_ShouldSetRoot()
    {
        var rootData = new TestData { Name = "Root", Value = 42 };
        
        using var storage = EmbeddedStorage.Start(rootData, _testDirectory);
        var retrievedRoot = storage.Root<TestData>();
        
        Assert.Equal(rootData.Name, retrievedRoot.Name);
        Assert.Equal(rootData.Value, retrievedRoot.Value);
    }

    [Fact]
    public void Foundation_WithConfiguration_ShouldCreateFoundation()
    {
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetChannelCount(2)
            .Build();

        var foundation = EmbeddedStorage.Foundation(config);
        
        Assert.NotNull(foundation);
        Assert.Equal(_testDirectory, foundation.GetConfiguration().StorageDirectory);
        Assert.Equal(2, foundation.GetConfiguration().ChannelCount);
    }

    [Fact]
    public void StorageManager_StoreAndRetrieve_ShouldPersistObjects()
    {
        using var storage = EmbeddedStorage.Start(_testDirectory);
        
        var testData = new TestData { Name = "Test", Value = 123 };
        var objectId = storage.Store(testData);
        
        Assert.True(objectId > 0);
    }

    [Fact]
    public void StorageManager_Query_ShouldFindStoredObjects()
    {
        using var storage = EmbeddedStorage.Start(_testDirectory);
        
        var root = storage.Root<TestContainer>();
        root.Items.Add(new TestData { Name = "Item1", Value = 1 });
        root.Items.Add(new TestData { Name = "Item2", Value = 2 });
        
        storage.StoreRoot();
        
        var items = storage.Query<TestData>().ToList();
        
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Name == "Item1");
        Assert.Contains(items, i => i.Name == "Item2");
    }

    [Fact]
    public void StorageManager_CreateStorer_ShouldAllowBatchOperations()
    {
        using var storage = EmbeddedStorage.Start(_testDirectory);
        using var storer = storage.CreateStorer();
        
        var objects = new[]
        {
            new TestData { Name = "Batch1", Value = 1 },
            new TestData { Name = "Batch2", Value = 2 },
            new TestData { Name = "Batch3", Value = 3 }
        };
        
        var objectIds = storer.StoreAll(objects);
        var committedCount = storer.Commit();
        
        Assert.Equal(3, objectIds.Length);
        Assert.Equal(3, committedCount);
        Assert.All(objectIds, id => Assert.True(id > 0));
    }

    [Fact]
    public void StorageManager_Shutdown_ShouldStopManager()
    {
        var storage = EmbeddedStorage.Start(_testDirectory);
        
        Assert.True(storage.IsRunning);
        
        var shutdownResult = storage.Shutdown();
        
        Assert.True(shutdownResult);
        Assert.False(storage.IsRunning);
        
        storage.Dispose();
    }

    [Fact]
    public async Task StorageManager_CreateBackup_ShouldCreateBackupFiles()
    {
        var backupDirectory = Path.Combine(_testDirectory, "backup");
        
        using var storage = EmbeddedStorage.Start(_testDirectory);
        var root = storage.Root<TestData>();
        root.Name = "BackupTest";
        root.Value = 999;
        storage.StoreRoot();
        
        await storage.CreateBackupAsync(backupDirectory);
        
        Assert.True(Directory.Exists(backupDirectory));
        // Backup should contain some files
        var backupFiles = Directory.GetFiles(backupDirectory, "*", SearchOption.AllDirectories);
        Assert.NotEmpty(backupFiles);
    }

    [Fact]
    public void StorageManager_GetStatistics_ShouldReturnStatistics()
    {
        using var storage = EmbeddedStorage.Start(_testDirectory);
        
        var stats = storage.GetStatistics();
        
        Assert.NotNull(stats);
        Assert.True(stats.CreationTime <= DateTime.UtcNow);
    }

    [Fact]
    public void ConfigurationBuilder_WithCustomSettings_ShouldBuildCorrectConfiguration()
    {
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetChannelCount(4)
            .SetEntityCacheThreshold(2000000)
            .SetEntityCacheTimeout(3600000)
            .SetDataFileSize(2048, 2147483648)
            .SetHousekeepingOnStartup(false)
            .SetHousekeepingInterval(5000)
            .SetValidateOnStartup(false)
            .Build();

        Assert.Equal(_testDirectory, config.StorageDirectory);
        Assert.Equal(4, config.ChannelCount);
        Assert.Equal(2000000, config.EntityCacheThreshold);
        Assert.Equal(3600000, config.EntityCacheTimeoutMs);
        Assert.Equal(2048, config.DataFileMinimumSize);
        Assert.Equal(2147483648, config.DataFileMaximumSize);
        Assert.False(config.HousekeepingOnStartup);
        Assert.Equal(5000, config.HousekeepingIntervalMs);
        Assert.False(config.ValidateOnStartup);
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

[MessagePackObject(AllowPrivate = true)]
internal class TestData
{
    [Key(0)]
    public string Name { get; set; } = string.Empty;
    
    [Key(1)]
    public int Value { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
internal class TestContainer
{
    [Key(0)]
    public List<TestData> Items { get; set; } = new();
}
