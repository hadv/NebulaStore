using System;
using System.IO;
using System.Threading;
using Xunit;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.Embedded.Types;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Integration tests for the EmbeddedStorage system.
/// </summary>
public class EmbeddedStorageIntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public EmbeddedStorageIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "NebulaStore_Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void CanStartAndStopStorage()
    {
        // Arrange & Act
        using var storage = EmbeddedStorage.Start(_testDirectory);

        // Assert
        Assert.NotNull(storage);
        Assert.True(storage.IsRunning);

        // Act - Shutdown
        var shutdownResult = storage.Shutdown();

        // Assert
        Assert.True(shutdownResult);
        Assert.False(storage.IsRunning);
    }

    [Fact]
    public void CanSetAndGetRoot()
    {
        // Arrange
        var testData = new TestData { Name = "Test", Value = 42 };
        
        using var storage = EmbeddedStorage.Start(testData, _testDirectory);

        // Act
        var root = storage.Root<TestData>();

        // Assert
        Assert.NotNull(root);
        Assert.IsType<TestData>(root);
        var retrievedData = root;
        Assert.Equal("Test", retrievedData.Name);
        Assert.Equal(42, retrievedData.Value);
    }

    [Fact]
    public void CanStoreAndRetrieveRoot()
    {
        // Arrange
        var testData = new TestData { Name = "Persistent", Value = 123 };
        
        using var storage = EmbeddedStorage.Start(testData, _testDirectory);

        // Act
        var objectId = storage.StoreRoot();

        // Assert
        Assert.True(objectId > 0);
    }

    [Fact]
    public void CanCreateConnection()
    {
        // Arrange
        using var storage = EmbeddedStorage.Start(_testDirectory);

        // Act
        using var connection = storage.CreateConnection();

        // Assert
        Assert.NotNull(connection);
        Assert.NotNull(connection.PersistenceManager);
    }

    [Fact]
    public void CanPerformGarbageCollection()
    {
        // Arrange
        using var storage = EmbeddedStorage.Start(_testDirectory);

        // Act & Assert - Should not throw
        storage.IssueFullGarbageCollection();
        
        var result = storage.IssueGarbageCollection(TimeSpan.FromSeconds(1).Ticks * 100); // Convert to nanoseconds
        Assert.True(result); // Should complete within time budget
    }

    [Fact]
    public void CanPerformFileCheck()
    {
        // Arrange
        using var storage = EmbeddedStorage.Start(_testDirectory);

        // Act & Assert - Should not throw
        storage.IssueFullFileCheck();
        
        var result = storage.IssueFileCheck(TimeSpan.FromSeconds(1));
        Assert.True(result); // Should complete within time budget
    }

    [Fact]
    public void CanPerformCacheCheck()
    {
        // Arrange
        using var storage = EmbeddedStorage.Start(_testDirectory);

        // Act & Assert - Should not throw
        storage.IssueFullCacheCheck();
        
        var result = storage.IssueCacheCheck(TimeSpan.FromSeconds(1));
        Assert.True(result); // Should complete within time budget
    }

    [Fact]
    public void CanCreateBackup()
    {
        // Arrange
        var backupDirectory = new DirectoryInfo(Path.Combine(_testDirectory, "backup"));
        using var storage = EmbeddedStorage.Start(_testDirectory);

        // Act & Assert - Should not throw
        storage.IssueFullBackup(backupDirectory);
        
        Assert.True(backupDirectory.Exists);
    }

    [Fact]
    public void CanGetStorageStatistics()
    {
        // Arrange
        using var storage = EmbeddedStorage.Start(_testDirectory);

        // Act
        var statistics = storage.CreateStorageStatistics();

        // Assert
        Assert.NotNull(statistics);
        Assert.True(statistics.DataFileCount >= 0);
        Assert.True(statistics.TotalStorageSize >= 0);
        Assert.True(statistics.LiveDataLength >= 0);
    }

    [Fact]
    public void CanExportAndImportChannels()
    {
        // Arrange
        var exportDirectory = new DirectoryInfo(Path.Combine(_testDirectory, "export"));
        var importDirectory = new DirectoryInfo(Path.Combine(_testDirectory, "import"));
        
        using var storage = EmbeddedStorage.Start(_testDirectory);

        // Act - Export
        storage.ExportChannels(exportDirectory);
        
        // Assert - Export
        Assert.True(exportDirectory.Exists);

        // Act - Import (to a different location)
        Directory.CreateDirectory(importDirectory.FullName);
        storage.ImportFiles(importDirectory);

        // Assert - Should not throw
        Assert.True(importDirectory.Exists);
    }

    [Fact]
    public void CanAccessTypeDictionary()
    {
        // Arrange
        using var storage = EmbeddedStorage.Start(_testDirectory);

        // Act
        var typeDictionary = storage.TypeDictionary;

        // Assert
        Assert.NotNull(typeDictionary);
        
        // Should have built-in types registered
        var stringHandler = typeDictionary.GetTypeHandler(typeof(string));
        Assert.NotNull(stringHandler);
        Assert.Equal(typeof(string), stringHandler.HandledType);
    }

    [Fact]
    public void CanAccessConfiguration()
    {
        // Arrange
        using var storage = EmbeddedStorage.Start(_testDirectory);

        // Act
        var configuration = storage.Configuration;

        // Assert
        Assert.NotNull(configuration);
        Assert.True(configuration.ChannelCount > 0);
        Assert.NotNull(configuration.StorageDirectory);
        Assert.True(configuration.EntityCacheThreshold > 0);
    }

    [Fact]
    public void CanAccessDatabase()
    {
        // Arrange
        using var storage = EmbeddedStorage.Start(_testDirectory);

        // Act
        var database = storage.Database();

        // Assert
        Assert.NotNull(database);
        Assert.Equal("NebulaStore", database.DatabaseName);
        Assert.Same(storage, database.StorageManager);
    }

    [Fact]
    public void CanViewRoots()
    {
        // Arrange
        var testData = new TestData { Name = "Root", Value = 999 };
        using var storage = EmbeddedStorage.Start(testData, _testDirectory);

        // Act
        var rootsView = storage.ViewRoots();

        // Assert
        Assert.NotNull(rootsView);
        
        var rootReference = rootsView.RootReference();
        Assert.NotNull(rootReference);
        Assert.Same(testData, rootReference);
        
        var allRoots = rootsView.AllRootReferences();
        Assert.NotNull(allRoots);
        Assert.Contains(testData, allRoots);
    }

    [Fact]
    public void MultipleStorageInstancesCanCoexist()
    {
        // Arrange
        var directory1 = Path.Combine(_testDirectory, "storage1");
        var directory2 = Path.Combine(_testDirectory, "storage2");
        
        var data1 = new TestData { Name = "Storage1", Value = 1 };
        var data2 = new TestData { Name = "Storage2", Value = 2 };

        // Act
        using var storage1 = EmbeddedStorage.Start(data1, directory1);
        using var storage2 = EmbeddedStorage.Start(data2, directory2);

        // Assert
        Assert.NotSame(storage1, storage2);
        Assert.True(storage1.IsRunning);
        Assert.True(storage2.IsRunning);
        
        var root1 = storage1.Root<TestData>()!;
        var root2 = storage2.Root<TestData>()!;
        
        Assert.Equal("Storage1", root1.Name);
        Assert.Equal("Storage2", root2.Name);
        Assert.Equal(1, root1.Value);
        Assert.Equal(2, root2.Value);
    }

    [Fact]
    public void StorageHandlesNullRoot()
    {
        // Arrange & Act
        using var storage = EmbeddedStorage.Start(null, _testDirectory);

        // Assert
        Assert.NotNull(storage);
        Assert.Null(storage.Root<TestData>());

        // Should be able to set root later
        var testData = new TestData { Name = "Later", Value = 456 };
        storage.SetRoot(testData);

        Assert.NotNull(storage.Root<TestData>());
        Assert.Same(testData, storage.Root<TestData>());
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
                // Ignore cleanup errors in tests
            }
        }
    }
}

/// <summary>
/// Test data class for storage tests.
/// </summary>
public class TestData
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public override string ToString()
    {
        return $"TestData[Name={Name}, Value={Value}, CreatedAt={CreatedAt}]";
    }
}
