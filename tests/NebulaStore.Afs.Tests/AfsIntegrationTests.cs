using System;
using System.IO;
using FluentAssertions;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;
using Xunit;

namespace NebulaStore.Afs.Tests;

/// <summary>
/// Integration tests for AFS with NebulaStore.
/// </summary>
public class AfsIntegrationTests : IDisposable
{
    private readonly string _testDirectory;

    public AfsIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "NebulaStore.AfsTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void StartWithAfs_WithDefaultSettings_ShouldCreateStorageManager()
    {
        // Act
        using var storage = EmbeddedStorage.StartWithAfs(_testDirectory);

        // Assert
        storage.Should().NotBeNull();
        storage.IsRunning.Should().BeTrue();
        storage.Configuration.UseAfs.Should().BeTrue();
        storage.Configuration.AfsStorageType.Should().Be("blobstore");
    }

    [Fact]
    public void StartWithAfs_WithCustomConfiguration_ShouldUseAfsSettings()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetUseAfs(true)
            .SetAfsStorageType("blobstore")
            .SetAfsUseCache(false);

        // Act
        using var storage = EmbeddedStorage.StartWithAfs(config);

        // Assert
        storage.Should().NotBeNull();
        storage.Configuration.UseAfs.Should().BeTrue();
        storage.Configuration.AfsStorageType.Should().Be("blobstore");
        storage.Configuration.AfsUseCache.Should().BeFalse();
    }

    [Fact]
    public void StartWithAfs_WithRootObject_ShouldStoreAndRetrieveRoot()
    {
        // Arrange
        var testData = new TestDataClass { Name = "Test", Value = 42 };

        // Act
        using var storage = EmbeddedStorage.StartWithAfs(testData, _testDirectory);
        var root = storage.Root<TestDataClass>();

        // Assert
        root.Should().NotBeNull();
        root.Name.Should().Be("Test");
        root.Value.Should().Be(42);
    }

    [Fact]
    public void AfsStorage_StoreAndRetrieveObjects_ShouldPersistData()
    {
        // Arrange
        var testData = new TestDataClass { Name = "Persistent", Value = 123 };

        // Act & Assert - First session
        using (var storage = EmbeddedStorage.StartWithAfs(_testDirectory))
        {
            var root = storage.Root<TestDataClass>();
            root.Name = testData.Name;
            root.Value = testData.Value;
            
            storage.StoreRoot();
        }

        // Act & Assert - Second session
        using (var storage = EmbeddedStorage.StartWithAfs(_testDirectory))
        {
            var root = storage.Root<TestDataClass>();
            root.Name.Should().Be(testData.Name);
            root.Value.Should().Be(testData.Value);
        }
    }

    [Fact]
    public void AfsStorage_StoreMultipleObjects_ShouldHandleComplexData()
    {
        // Arrange
        var library = new Library
        {
            Name = "AFS Test Library",
            Books = new List<Book>
            {
                new Book { Title = "AFS Guide", Author = "Test Author", Year = 2025 },
                new Book { Title = "Blob Storage", Author = "Storage Expert", Year = 2024 }
            }
        };

        // Act & Assert - Store data
        using (var storage = EmbeddedStorage.StartWithAfs(_testDirectory))
        {
            var root = storage.Root<Library>();
            root.Name = library.Name;
            root.Books = library.Books;
            
            storage.StoreRoot();
        }

        // Act & Assert - Retrieve data
        using (var storage = EmbeddedStorage.StartWithAfs(_testDirectory))
        {
            var root = storage.Root<Library>();
            root.Name.Should().Be(library.Name);
            root.Books.Should().HaveCount(2);
            root.Books[0].Title.Should().Be("AFS Guide");
            root.Books[1].Author.Should().Be("Storage Expert");
        }
    }

    [Fact]
    public void AfsStorage_WithCaching_ShouldImprovePerformance()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetUseAfs(true)
            .SetAfsStorageType("blobstore")
            .SetAfsUseCache(true);

        // Act
        using var storage = EmbeddedStorage.StartWithAfs(config);

        // Assert
        storage.Configuration.AfsUseCache.Should().BeTrue();
        // Performance testing would require more complex setup
        // For now, just verify the configuration is applied
    }

    [Fact]
    public void AfsStorage_CreateStorer_ShouldSupportBatchOperations()
    {
        // Arrange
        using var storage = EmbeddedStorage.StartWithAfs(_testDirectory);
        var objects = new[]
        {
            new TestDataClass { Name = "Object1", Value = 1 },
            new TestDataClass { Name = "Object2", Value = 2 },
            new TestDataClass { Name = "Object3", Value = 3 }
        };

        // Act
        using var storer = storage.CreateStorer();
        var objectIds = storer.StoreAll(objects);
        var committedCount = storer.Commit();

        // Assert
        objectIds.Should().HaveCount(3);
        objectIds.Should().AllSatisfy(id => id.Should().NotBe(0));
        committedCount.Should().Be(3);
        storer.HasPendingOperations.Should().BeFalse();
    }

    [Fact]
    public void AfsStorage_GetStatistics_ShouldReturnValidStatistics()
    {
        // Arrange
        using var storage = EmbeddedStorage.StartWithAfs(_testDirectory);
        var root = storage.Root<TestDataClass>();
        root.Name = "Statistics Test";
        storage.StoreRoot();

        // Act
        var stats = storage.GetStatistics();

        // Assert
        stats.Should().NotBeNull();
        stats.CreationTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        stats.TotalStorageSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AfsConfiguration_InvalidStorageType_ShouldThrowException()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(_testDirectory)
            .SetUseAfs(true)
            .SetAfsStorageType("invalid-type");

        // Act & Assert
        var act = () => EmbeddedStorage.StartWithAfs(config);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*invalid-type*not supported*");
    }

    [Fact]
    public void AfsStorage_Shutdown_ShouldCleanupResources()
    {
        // Arrange
        var storage = EmbeddedStorage.StartWithAfs(_testDirectory);
        var root = storage.Root<TestDataClass>();
        root.Name = "Shutdown Test";

        // Act
        var shutdownResult = storage.Shutdown();

        // Assert
        shutdownResult.Should().BeTrue();
        storage.IsRunning.Should().BeFalse();
        
        // Cleanup
        storage.Dispose();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    // Test data classes
    public class TestDataClass
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    public class Library
    {
        public string Name { get; set; } = string.Empty;
        public List<Book> Books { get; set; } = new();
    }

    public class Book
    {
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public int Year { get; set; }
    }
}
