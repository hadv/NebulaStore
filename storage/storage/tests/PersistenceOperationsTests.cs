using System;
using System.IO;
using Xunit;
using NebulaStore.Storage.Embedded.Types;
using NebulaStore.Storage.Embedded.Types.Exceptions;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Tests for enhanced persistence operations including object storage and retrieval.
/// </summary>
public class PersistenceOperationsTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly IStorageManager _storageManager;

    public PersistenceOperationsTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "NebulaStore_PersistenceTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);

        var configuration = new TestStorageConfiguration(_testDirectory);
        _storageManager = StorageManager.Create(configuration);
        _storageManager.Start();
    }

    [Fact]
    public void Store_And_Retrieve_Object_Successfully()
    {
        // Arrange
        var testObject = new TestDataClass
        {
            Id = 42,
            Name = "Test Object",
            Value = 123.45
        };

        // Act - Set as root and store the object
        _storageManager.SetRoot(testObject);
        var objectId = _storageManager.Store(testObject);

        // Assert - Object ID should be valid
        Assert.True(objectId > 0);

        // Act - Retrieve the object from root (Eclipse Store approach)
        var retrievedObject = _storageManager.Root() as TestDataClass;

        // Assert - Retrieved object should match original
        Assert.NotNull(retrievedObject);
        Assert.IsType<TestDataClass>(retrievedObject);
        
        var retrievedTestObject = (TestDataClass)retrievedObject;
        Assert.Equal(testObject.Id, retrievedTestObject.Id);
        Assert.Equal(testObject.Name, retrievedTestObject.Name);
        Assert.Equal(testObject.Value, retrievedTestObject.Value);
    }

    [Fact]
    public void Root_With_No_Data_Returns_Null()
    {
        // Arrange - No root set

        // Act & Assert - Root should be null when no data is stored
        var root = _storageManager.Root() as TestDataClass;
        Assert.Null(root);
    }

    [Fact]
    public void Store_Multiple_Objects_And_Retrieve_All()
    {
        // Arrange
        var objects = new[]
        {
            new TestDataClass { Id = 1, Name = "Object 1", Value = 1.1 },
            new TestDataClass { Id = 2, Name = "Object 2", Value = 2.2 },
            new TestDataClass { Id = 3, Name = "Object 3", Value = 3.3 }
        };

        // Act - Store array as root (Eclipse Store approach)
        _storageManager.SetRoot(objects);
        var objectIds = _storageManager.StoreAll(objects);

        // Assert - All objects should have valid IDs
        Assert.Equal(3, objectIds.Length);
        Assert.All(objectIds, id => Assert.True(id > 0));

        // Act & Assert - Retrieve all objects from root
        var retrievedObjects = _storageManager.Root() as TestDataClass[];
        Assert.NotNull(retrievedObjects);
        Assert.Equal(3, retrievedObjects.Length);

        for (int i = 0; i < objects.Length; i++)
        {
            Assert.NotNull(retrievedObjects[i]);
            Assert.Equal(objects[i].Id, retrievedObjects[i].Id);
            Assert.Equal(objects[i].Name, retrievedObjects[i].Name);
            Assert.Equal(objects[i].Value, retrievedObjects[i].Value);
        }
    }

    [Fact]
    public void Store_Root_Object_And_Retrieve()
    {
        // Arrange
        var rootObject = new TestDataClass
        {
            Id = 100,
            Name = "Root Object",
            Value = 999.99
        };

        // Act - Set and store root
        _storageManager.SetRoot(rootObject);
        var rootObjectId = _storageManager.StoreRoot();

        // Assert - Root should be stored successfully
        Assert.True(rootObjectId > 0);

        // Act - Retrieve root
        var retrievedRoot = _storageManager.Root();

        // Assert - Retrieved root should match original
        Assert.NotNull(retrievedRoot);
        Assert.IsType<TestDataClass>(retrievedRoot);
        
        var retrievedRootObject = (TestDataClass)retrievedRoot;
        Assert.Equal(rootObject.Id, retrievedRootObject.Id);
        Assert.Equal(rootObject.Name, retrievedRootObject.Name);
        Assert.Equal(rootObject.Value, retrievedRootObject.Value);
    }

    [Fact]
    public void Persistence_Survives_Storage_Restart()
    {
        // Arrange
        var testObject = new TestDataClass
        {
            Id = 200,
            Name = "Persistent Object",
            Value = 456.78
        };

        // Act - Set as root and store object
        _storageManager.SetRoot(testObject);
        var objectId = _storageManager.Store(testObject);
        Assert.True(objectId > 0);

        // Shutdown and restart storage
        _storageManager.Shutdown();
        var newStorageManager = StorageManager.Create(new TestStorageConfiguration(_testDirectory));
        newStorageManager.Start();

        try
        {
            // Act - Try to retrieve object from root after restart
            var retrievedObject = newStorageManager.Root() as TestDataClass;

            // Assert - Object should still be retrievable
            Assert.NotNull(retrievedObject);
            Assert.IsType<TestDataClass>(retrievedObject);

            Assert.Equal(testObject.Id, retrievedObject.Id);
            Assert.Equal(testObject.Name, retrievedObject.Name);
            Assert.Equal(testObject.Value, retrievedObject.Value);
        }
        finally
        {
            newStorageManager.Shutdown();
        }
    }

    public void Dispose()
    {
        _storageManager?.Shutdown();
        
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

/// <summary>
/// Test data class for persistence operations tests.
/// </summary>
public class TestDataClass
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
}

/// <summary>
/// Test storage configuration for unit tests.
/// </summary>
internal class TestStorageConfiguration : IStorageConfiguration
{
    public string StorageDirectory { get; }
    public int ChannelCount { get; } = 1;
    public TimeSpan HousekeepingInterval { get; } = TimeSpan.FromSeconds(1);
    public long EntityCacheThreshold { get; } = 1000000;
    public TimeSpan EntityCacheTimeout { get; } = TimeSpan.FromDays(1);

    public TestStorageConfiguration(string storageDirectory)
    {
        StorageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));
    }
}
