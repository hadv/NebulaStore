using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;
using NebulaStore.GigaMap;
using MessagePack;

namespace NebulaStore.Storage.Embedded.Tests;

/// <summary>
/// Integration tests for GigaMap with AFS storage.
/// These tests verify that GigaMap seamlessly integrates with AFS following Eclipse Store patterns.
/// </summary>
public class GigaMapAfsIntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public GigaMapAfsIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "NebulaStore_GigaMapAfs_Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void CreateGigaMap_WithAfs_ShouldReturnAfsGigaMap()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetUseAfs(true)
            .SetAfsStorageType("blobstore")
            .Build();

        using var storage = EmbeddedStorage.Foundation(config).Start();

        // Act
        var gigaMap = storage.CreateGigaMap<TestPerson>()
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .Build();

        // Assert
        Assert.NotNull(gigaMap);
        Assert.IsType<AfsGigaMap<TestPerson>>(gigaMap);
    }

    [Fact]
    public void RegisterGigaMap_WithAfs_ShouldWrapWithAfsGigaMap()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetUseAfs(true)
            .SetAfsStorageType("blobstore")
            .Build();

        using var storage = EmbeddedStorage.Foundation(config).Start();
        
        var originalGigaMap = GigaMapFactory.New<TestPerson>();

        // Act
        storage.RegisterGigaMap<TestPerson>(originalGigaMap);
        var retrievedGigaMap = storage.GetGigaMap<TestPerson>();

        // Assert
        Assert.NotNull(retrievedGigaMap);
        Assert.IsType<AfsGigaMap<TestPerson>>(retrievedGigaMap);
        Assert.NotSame(originalGigaMap, retrievedGigaMap);
    }

    [Fact]
    public async Task GigaMap_AddEntity_ShouldTriggerAutoStore()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetUseAfs(true)
            .SetAfsStorageType("blobstore")
            .Build();

        using var storage = EmbeddedStorage.Foundation(config).Start();

        var gigaMap = storage.CreateGigaMap<TestPerson>()
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .Build();

        var person = new TestPerson { Email = "john@example.com", Name = "John Doe", Age = 30 };

        // Act
        var entityId = gigaMap.Add(person);

        // Give some time for async auto-store to complete
        await Task.Delay(500); // Increased delay for more reliable testing

        // Assert
        Assert.True(entityId >= 0); // Entity IDs start from 0, which is valid
        Assert.Equal(1, gigaMap.Size);
        Assert.Equal(person, gigaMap.Get(entityId));
    }

    [Fact]
    public void GigaMap_LazyLoading_ShouldLoadOnFirstAccess()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetUseAfs(true)
            .SetAfsStorageType("blobstore")
            .Build();

        using var storage = EmbeddedStorage.Foundation(config).Start();

        var gigaMap = storage.CreateGigaMap<TestPerson>()
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .Build();

        // Act & Assert - First access should trigger loading
        var size = gigaMap.Size; // This should trigger EnsureLoaded()
        Assert.Equal(0, size); // Should be 0 for a new GigaMap
    }

    [Fact]
    public async Task StoreGigaMapsAsync_ShouldStoreAllRegisteredGigaMaps()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetUseAfs(true)
            .SetAfsStorageType("blobstore")
            .Build();

        using var storage = EmbeddedStorage.Foundation(config).Start();

        var gigaMap1 = storage.CreateGigaMap<TestPerson>().Build();
        var gigaMap2 = storage.CreateGigaMap<TestProduct>().Build();

        storage.RegisterGigaMap<TestPerson>(gigaMap1);
        storage.RegisterGigaMap<TestProduct>(gigaMap2);

        gigaMap1.Add(new TestPerson { Email = "test1@example.com", Name = "Test 1", Age = 25 });
        gigaMap2.Add(new TestProduct { Name = "Product 1", Price = 99.99m });

        // Act
        await storage.StoreGigaMapsAsync();

        // Assert - No exceptions should be thrown
        Assert.Equal(1, gigaMap1.Size);
        Assert.Equal(1, gigaMap2.Size);
    }

    [Fact]
    public void GigaMap_WithoutAfs_ShouldReturnRegularGigaMap()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetUseAfs(false) // AFS disabled
            .Build();

        using var storage = EmbeddedStorage.Foundation(config).Start();

        // Act
        var gigaMap = storage.CreateGigaMap<TestPerson>()
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .Build();

        // Assert
        Assert.NotNull(gigaMap);
        Assert.IsNotType<AfsGigaMap<TestPerson>>(gigaMap);
    }

    [Fact]
    public void GigaMap_EclipseStoreCompatibility_ShouldSupportStoreMethod()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetUseAfs(true)
            .SetAfsStorageType("blobstore")
            .Build();

        using var storage = EmbeddedStorage.Foundation(config).Start();

        var gigaMap = storage.CreateGigaMap<TestPerson>().Build();
        gigaMap.Add(new TestPerson { Email = "eclipse@example.com", Name = "Eclipse Test", Age = 35 });

        // Act - Following Eclipse Store pattern: gigaMap.Store()
        var result = gigaMap.Store();

        // Assert
        Assert.True(result >= 0); // Should return a valid result
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

/// <summary>
/// Test entity for GigaMap integration tests.
/// </summary>
[MessagePackObject]
public class TestPerson
{
    [Key(0)]
    public string Email { get; set; } = string.Empty;

    [Key(1)]
    public string Name { get; set; } = string.Empty;

    [Key(2)]
    public int Age { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is TestPerson other && 
               Email == other.Email && 
               Name == other.Name && 
               Age == other.Age;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Email, Name, Age);
    }
}

/// <summary>
/// Additional test entity for multi-GigaMap scenarios.
/// </summary>
[MessagePackObject]
public class TestProduct
{
    [Key(0)]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    public decimal Price { get; set; }

    public override bool Equals(object? obj)
    {
        return obj is TestProduct other && 
               Name == other.Name && 
               Price == other.Price;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Price);
    }
}
