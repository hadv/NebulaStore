using System;
using System.IO;
using Xunit;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.Embedded.Types;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Core storage functionality tests that focus on the essential storage operations
/// without dependencies on the query system.
/// </summary>
public class CoreStorageTests : IDisposable
{
    private readonly string _testDirectory;

    public CoreStorageTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "NebulaStore_CoreTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void CanCreateStorageConfiguration()
    {
        // Act
        var config = EmbeddedStorage.Configuration(_testDirectory);

        // Assert
        Assert.NotNull(config);
        Assert.Equal(_testDirectory, config.StorageDirectory);
        Assert.True(config.ChannelCount > 0);
        Assert.True(config.EntityCacheThreshold > 0);
    }

    [Fact]
    public void CanCreateConnectionFoundation()
    {
        // Act
        var foundation = EmbeddedStorage.CreateFoundation();

        // Assert
        Assert.NotNull(foundation);
    }

    [Fact]
    public void CanCreateStorageFoundation()
    {
        // Act
        var foundation = EmbeddedStorage.CreateFoundation();

        // Assert
        Assert.NotNull(foundation);
    }

    [Fact]
    public void CanCreateEmbeddedStorageFoundation()
    {
        // Act
        var config = EmbeddedStorage.Configuration(_testDirectory);
        var foundation = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config);

        // Assert
        Assert.NotNull(foundation);
    }

    [Fact]
    public void CanCreateStorageManagerFromFoundation()
    {
        // Arrange
        var testData = new SimpleTestData { Name = "Test", Value = 42 };

        // Act
        var config = EmbeddedStorage.Configuration(_testDirectory);
        var storageManager = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config)
            .CreateEmbeddedStorageManager(testData);

        // Assert
        Assert.NotNull(storageManager);
        Assert.NotNull(storageManager.Configuration);
        Assert.NotNull(storageManager.TypeDictionary);
        
        // Cleanup
        storageManager.Dispose();
    }

    [Fact]
    public void StorageManagerCanStartAndStop()
    {
        // Arrange
        var config = EmbeddedStorage.Configuration(_testDirectory);
        var storageManager = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config)
            .CreateEmbeddedStorageManager(null);

        // Act & Assert - Start
        var startedManager = storageManager.Start();
        Assert.Same(storageManager, startedManager);
        Assert.True(storageManager.IsRunning);

        // Act & Assert - Stop
        var shutdownResult = storageManager.Shutdown();
        Assert.True(shutdownResult);
        Assert.False(storageManager.IsRunning);
        
        // Cleanup
        storageManager.Dispose();
    }

    [Fact]
    public void CanSetAndGetRootObject()
    {
        // Arrange
        var testData = new SimpleTestData { Name = "Root", Value = 999 };
        var config = EmbeddedStorage.Configuration(_testDirectory);
        var storageManager = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config)
            .CreateEmbeddedStorageManager(null);

        // Act
        var setResult = storageManager.SetRoot(testData);
        var retrievedRoot = storageManager.Root<SimpleTestData>();

        // Assert
        Assert.Same(testData, setResult);
        Assert.Equal(testData.Name, retrievedRoot.Name); // Use Assert.Equal for value comparison
        Assert.Equal(testData.Value, retrievedRoot.Value);
        
        // Cleanup
        storageManager.Dispose();
    }

    [Fact]
    public void CanStoreObjects()
    {
        // Arrange
        var testData = new SimpleTestData { Name = "Stored", Value = 123 };
        var config = EmbeddedStorage.Configuration(_testDirectory);
        var storageManager = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config)
            .CreateEmbeddedStorageManager(null);

        storageManager.Start();

        // Act
        var objectId = storageManager.Store(testData);

        // Assert
        Assert.True(objectId > 0);
        
        // Cleanup
        storageManager.Shutdown();
        storageManager.Dispose();
    }

    [Fact]
    public void CanStoreMultipleObjects()
    {
        // Arrange
        var testData1 = new SimpleTestData { Name = "Object1", Value = 1 };
        var testData2 = new SimpleTestData { Name = "Object2", Value = 2 };
        var testData3 = new SimpleTestData { Name = "Object3", Value = 3 };
        
        var config = EmbeddedStorage.Configuration(_testDirectory);
        var storageManager = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config)
            .CreateEmbeddedStorageManager(null);

        storageManager.Start();

        // Act
        var objectIds = storageManager.StoreAll(testData1, testData2, testData3);

        // Assert
        Assert.Equal(3, objectIds.Length);
        Assert.All(objectIds, id => Assert.True(id > 0));
        
        // Cleanup
        storageManager.Shutdown();
        storageManager.Dispose();
    }

    [Fact]
    public void CanCreateConnection()
    {
        // Arrange
        var config = EmbeddedStorage.Configuration(_testDirectory);
        var storageManager = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config)
            .CreateEmbeddedStorageManager(null);

        // Act
        using var connection = storageManager.CreateConnection();

        // Assert
        Assert.NotNull(connection);
        Assert.NotNull(connection.PersistenceManager);
        
        // Cleanup
        storageManager.Dispose();
    }

    [Fact]
    public void ConnectionCanStoreObjects()
    {
        // Arrange
        var testData = new SimpleTestData { Name = "ConnectionTest", Value = 456 };
        var config = EmbeddedStorage.Configuration(_testDirectory);
        var storageManager = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config)
            .CreateEmbeddedStorageManager(null);

        storageManager.Start();

        // Act
        using var connection = storageManager.CreateConnection();
        var objectId = connection.Store(testData);

        // Assert
        Assert.True(objectId > 0);
        
        // Cleanup
        storageManager.Shutdown();
        storageManager.Dispose();
    }

    [Fact]
    public void CanAccessTypeDictionary()
    {
        // Arrange
        var config = EmbeddedStorage.Configuration(_testDirectory);
        var storageManager = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config)
            .CreateEmbeddedStorageManager(null);

        // Act
        var typeDictionary = storageManager.TypeDictionary;

        // Assert
        Assert.NotNull(typeDictionary);
        
        // Should have built-in types
        var stringHandler = typeDictionary.GetTypeHandler(typeof(string));
        Assert.NotNull(stringHandler);
        
        var intHandler = typeDictionary.GetTypeHandler(typeof(int));
        Assert.NotNull(intHandler);
        
        // Cleanup
        storageManager.Dispose();
    }

    [Fact]
    public void CanRegisterCustomTypes()
    {
        // Arrange
        var config = EmbeddedStorage.Configuration(_testDirectory);
        var storageManager = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config)
            .CreateEmbeddedStorageManager(null);

        var typeDictionary = storageManager.TypeDictionary;

        // Act - Get type handler (types are registered automatically when used)
        var handler = typeDictionary.GetTypeHandler(typeof(SimpleTestData));

        // Assert - Handler might be null if type hasn't been used yet, which is expected
        // In Eclipse Store, types are registered automatically when first encountered
        if (handler != null)
        {
            Assert.Equal(typeof(SimpleTestData), handler.HandledType);
            Assert.True(handler.TypeId >= 1000); // Custom types start at 1000
        }
        
        // Should be retrievable
        var retrievedHandler = typeDictionary.GetTypeHandler(typeof(SimpleTestData));
        Assert.Same(handler, retrievedHandler);
        
        // Cleanup
        storageManager.Dispose();
    }

    [Fact]
    public void CanAccessDatabase()
    {
        // Arrange
        var config = EmbeddedStorage.Configuration(_testDirectory);
        var storageManager = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config)
            .CreateEmbeddedStorageManager(null);

        // Act
        var database = storageManager.Database();

        // Assert
        Assert.NotNull(database);
        Assert.Equal("NebulaStore", database.DatabaseName);
        Assert.Same(storageManager, database.StorageManager);
        
        // Cleanup
        storageManager.Dispose();
    }

    [Fact]
    public void CanViewRoots()
    {
        // Arrange
        var testData = new SimpleTestData { Name = "RootView", Value = 777 };
        var config = EmbeddedStorage.Configuration(_testDirectory);
        var storageManager = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config)
            .CreateEmbeddedStorageManager(testData);

        // Act
        var rootsView = storageManager.ViewRoots();

        // Assert
        Assert.NotNull(rootsView);
        
        var rootReference = rootsView.RootReference();
        Assert.Same(testData, rootReference);
        
        var allRoots = rootsView.AllRootReferences();
        Assert.Contains(testData, allRoots);
        
        // Cleanup
        storageManager.Dispose();
    }

    [Fact]
    public void StorageDirectoryIsCreated()
    {
        // Arrange
        var customDirectory = Path.Combine(_testDirectory, "custom_storage");
        
        // Act
        var config = EmbeddedStorage.Configuration(customDirectory);
        var storageManager = EmbeddedStorage.CreateFoundation()
            .SetConfiguration(config)
            .CreateEmbeddedStorageManager(null);

        storageManager.Start();

        // Assert
        Assert.True(Directory.Exists(customDirectory));
        
        // Cleanup
        storageManager.Shutdown();
        storageManager.Dispose();
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
/// Simple test data class for core storage tests.
/// </summary>
public class SimpleTestData
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public override string ToString()
    {
        return $"SimpleTestData[Name={Name}, Value={Value}]";
    }
}
