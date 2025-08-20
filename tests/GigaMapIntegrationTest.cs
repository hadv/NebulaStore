using System;
using System.IO;
using System.Threading.Tasks;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;
using NebulaStore.GigaMap;
using Xunit;

namespace NebulaStore.Tests;

/// <summary>
/// Integration tests for GigaMap functionality with NebulaStore.
/// </summary>
public class GigaMapIntegrationTest : IDisposable
{
    private readonly string _testDirectory;
    private IEmbeddedStorageManager? _storage;

    public GigaMapIntegrationTest()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "NebulaStore_GigaMapTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void CreateGigaMap_ShouldReturnBuilder()
    {
        // Arrange
        using var storage = CreateStorage();

        // Act
        var builder = storage.CreateGigaMap<TestEntity>();

        // Assert
        Assert.NotNull(builder);
        Assert.IsAssignableFrom<IGigaMapBuilder<TestEntity>>(builder);
    }

    [Fact]
    public void BuildGigaMap_ShouldCreateWorkingInstance()
    {
        // Arrange
        using var storage = CreateStorage();

        // Act
        var gigaMap = storage.CreateGigaMap<TestEntity>()
            .WithBitmapIndex(Indexer.Property<TestEntity, string>("Name", e => e.Name))
            .WithBitmapIndex(Indexer.Property<TestEntity, int>("Age", e => e.Age))
            .Build();

        // Assert
        Assert.NotNull(gigaMap);
        Assert.Equal(0, gigaMap.Size);
    }

    [Fact]
    public void RegisterGigaMap_ShouldAllowRetrieval()
    {
        // Arrange
        using var storage = CreateStorage();
        var gigaMap = storage.CreateGigaMap<TestEntity>()
            .WithBitmapIndex(Indexer.Property<TestEntity, string>("Name", e => e.Name))
            .Build();

        // Act
        storage.RegisterGigaMap(gigaMap);
        var retrieved = storage.GetGigaMap<TestEntity>();

        // Assert
        Assert.NotNull(retrieved);
        Assert.Same(gigaMap, retrieved);
    }

    [Fact]
    public void GigaMap_BasicOperations_ShouldWork()
    {
        // Arrange
        using var storage = CreateStorage();
        var gigaMap = storage.CreateGigaMap<TestEntity>()
            .WithBitmapIndex(Indexer.Property<TestEntity, string>("Name", e => e.Name))
            .WithBitmapIndex(Indexer.Property<TestEntity, int>("Age", e => e.Age))
            .Build();

        storage.RegisterGigaMap(gigaMap);

        var entity1 = new TestEntity { Name = "John", Age = 30, Department = "Engineering" };
        var entity2 = new TestEntity { Name = "Jane", Age = 25, Department = "Marketing" };

        // Act
        var id1 = gigaMap.Add(entity1);
        var id2 = gigaMap.Add(entity2);

        // Assert
        Assert.True(id1 > 0);
        Assert.True(id2 > 0);
        Assert.NotEqual(id1, id2);
        Assert.Equal(2, gigaMap.Size);

        // Test retrieval
        var retrieved1 = gigaMap.Get(id1);
        var retrieved2 = gigaMap.Get(id2);

        Assert.NotNull(retrieved1);
        Assert.NotNull(retrieved2);
        Assert.Equal("John", retrieved1.Name);
        Assert.Equal("Jane", retrieved2.Name);
    }

    [Fact]
    public void GigaMap_Querying_ShouldWork()
    {
        // Arrange
        using var storage = CreateStorage();
        var nameIndexer = Indexer.Property<TestEntity, string>("Name", e => e.Name);
        var ageIndexer = Indexer.Property<TestEntity, int>("Age", e => e.Age);

        var gigaMap = storage.CreateGigaMap<TestEntity>()
            .WithBitmapIndex(nameIndexer)
            .WithBitmapIndex(ageIndexer)
            .Build();

        storage.RegisterGigaMap(gigaMap);

        // Add test data
        gigaMap.Add(new TestEntity { Name = "John", Age = 30, Department = "Engineering" });
        gigaMap.Add(new TestEntity { Name = "Jane", Age = 25, Department = "Marketing" });
        gigaMap.Add(new TestEntity { Name = "Bob", Age = 35, Department = "Engineering" });

        // Act & Assert
        var johnResults = gigaMap.Query()
            .And(nameIndexer.Is("John"))
            .Execute();
        Assert.Single(johnResults);
        Assert.Equal("John", johnResults[0].Name);

        var youngPeople = gigaMap.Query()
            .And(ageIndexer.IsLessThan(30))
            .Execute();
        Assert.Single(youngPeople);
        Assert.Equal("Jane", youngPeople[0].Name);

        var allResults = gigaMap.Query().Execute();
        Assert.Equal(3, allResults.Count);
    }

    [Fact]
    public async Task StoreGigaMapsAsync_ShouldNotThrow()
    {
        // Arrange
        using var storage = CreateStorage();
        var gigaMap = storage.CreateGigaMap<TestEntity>()
            .WithBitmapIndex(Indexer.Property<TestEntity, string>("Name", e => e.Name))
            .Build();

        storage.RegisterGigaMap(gigaMap);
        gigaMap.Add(new TestEntity { Name = "Test", Age = 25, Department = "Test" });

        // Act & Assert
        await storage.StoreGigaMapsAsync(); // Should not throw
    }

    [Fact]
    public async Task GigaMap_StoreAsync_ShouldNotThrow()
    {
        // Arrange
        using var storage = CreateStorage();
        var gigaMap = storage.CreateGigaMap<TestEntity>()
            .WithBitmapIndex(Indexer.Property<TestEntity, string>("Name", e => e.Name))
            .Build();

        gigaMap.Add(new TestEntity { Name = "Test", Age = 25, Department = "Test" });

        // Act & Assert
        var result = await gigaMap.StoreAsync(); // Should not throw
        Assert.True(result >= 0); // Should return non-negative value
    }

    private IEmbeddedStorageManager CreateStorage()
    {
        var config = EmbeddedStorageConfiguration.Builder()
            .SetStorageDirectory(_testDirectory)
            .SetGigaMapEnabled(true)
            .SetGigaMapDefaultSegmentSize(8) // Small segment size for testing
            .Build();

        _storage = EmbeddedStorage.Start(config);
        return _storage;
    }

    public void Dispose()
    {
        _storage?.Dispose();
        
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
/// Test entity for GigaMap integration tests.
/// </summary>
public class TestEntity
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Department { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{Name} (Age: {Age}, Dept: {Department})";
    }
}
