using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.GigaMap;
using NebulaStore.GigaMap.Performance;

namespace NebulaStore.GigaMap.Tests.Performance;

public class SimplePerformanceOptimizationsTests
{
    public class TestEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Department { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    private static List<TestEntity> CreateTestEntities(int count)
    {
        var entities = new List<TestEntity>();
        var departments = new[] { "Engineering", "Marketing", "Sales", "HR" };
        var random = new Random(42); // Fixed seed for reproducible tests

        for (int i = 0; i < count; i++)
        {
            entities.Add(new TestEntity
            {
                Name = $"Entity {i}",
                Age = 20 + (i % 50),
                Department = departments[i % departments.Length],
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            });
        }

        return entities;
    }

    [Fact]
    public void OptimizeIndices_ShouldNotThrow()
    {
        // Arrange
        var gigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);
        var entities = CreateTestEntities(100);
        
        foreach (var entity in entities)
        {
            gigaMap.Add(entity);
        }

        // Act & Assert
        var exception = Record.Exception(() => 
            SimplePerformanceOptimizations.OptimizeIndices(gigaMap));
        
        Assert.Null(exception);
    }

    [Fact]
    public async Task BulkAddAsync_ShouldAddAllEntities()
    {
        // Arrange
        var gigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);
        var entities = CreateTestEntities(1000);

        // Act
        var entityIds = await SimplePerformanceOptimizations.BulkAddAsync(gigaMap, entities);

        // Assert
        Assert.Equal(entities.Count, entityIds.Count);
        Assert.Equal(entities.Count, gigaMap.Size);
        Assert.All(entityIds, id => Assert.True(id >= 0));
        Assert.Equal(entityIds.Count, entityIds.Distinct().Count()); // All IDs should be unique
    }

    [Fact]
    public async Task ExecuteOptimizedQueryAsync_ShouldReturnResults()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestEntity>()
            .WithBitmapIndex(Indexer.Property<TestEntity, string>("Department", e => e.Department))
            .Build();
        
        var entities = CreateTestEntities(100);
        await SimplePerformanceOptimizations.BulkAddAsync(gigaMap, entities);

        var query = gigaMap.Query("Department", "Engineering");

        // Act
        var results = await SimplePerformanceOptimizations.ExecuteOptimizedQueryAsync(query);

        // Assert
        Assert.NotNull(results);
        Assert.True(results.Count > 0);
        Assert.All(results, entity => Assert.Equal("Engineering", entity.Department));
    }

    [Fact]
    public void GetPerformanceStats_ShouldReturnValidStats()
    {
        // Arrange
        var gigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);
        var entities = CreateTestEntities(50);
        
        foreach (var entity in entities)
        {
            gigaMap.Add(entity);
        }

        // Act
        var stats = SimplePerformanceOptimizations.GetPerformanceStats(gigaMap);

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(entities.Count, stats.EntityCount);
        Assert.False(stats.IsEmpty);
        Assert.False(stats.IsReadOnly);
        Assert.True(stats.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void GetOptimizationRecommendations_ShouldReturnRecommendations()
    {
        // Arrange
        var gigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);
        var entities = CreateTestEntities(100); // Small dataset for test

        foreach (var entity in entities)
        {
            gigaMap.Add(entity);
        }

        // Act
        var recommendations = SimplePerformanceOptimizations.GetOptimizationRecommendations(gigaMap);

        // Assert
        Assert.NotNull(recommendations);
        Assert.NotEmpty(recommendations);
        Assert.All(recommendations, rec => Assert.False(string.IsNullOrWhiteSpace(rec)));
    }

    [Fact]
    public async Task ApplyBasicOptimizationsAsync_ShouldComplete()
    {
        // Arrange
        var gigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);
        var entities = CreateTestEntities(100);
        
        foreach (var entity in entities)
        {
            gigaMap.Add(entity);
        }

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () => 
            await SimplePerformanceOptimizations.ApplyBasicOptimizationsAsync(gigaMap));
        
        Assert.Null(exception);
    }

    [Fact]
    public async Task ApplyComprehensiveOptimizationsAsync_ShouldComplete()
    {
        // Arrange
        var gigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);
        var entities = CreateTestEntities(100);
        
        foreach (var entity in entities)
        {
            gigaMap.Add(entity);
        }

        // Act & Assert
        var exception = await Record.ExceptionAsync(async () => 
            await SimplePerformanceOptimizations.ApplyComprehensiveOptimizationsAsync(gigaMap));
        
        Assert.Null(exception);
    }

    [Fact]
    public async Task CreateCompressedBackupAsync_ShouldCreateValidBackup()
    {
        // Arrange
        var gigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);
        var entities = CreateTestEntities(100);
        await SimplePerformanceOptimizations.BulkAddAsync(gigaMap, entities);

        // Act
        var backup = await SimplePerformanceOptimizations.CreateCompressedBackupAsync(gigaMap);

        // Assert
        Assert.NotNull(backup);
        Assert.True(backup.CompressedSize > 0);
        Assert.True(backup.OriginalSize > 0);
        Assert.True(backup.CompressedSize <= backup.OriginalSize);
        Assert.Equal(entities.Count, backup.EntityCount);
        Assert.True(backup.CompressionRatio > 0 && backup.CompressionRatio <= 1.0);
    }

    [Fact]
    public async Task RestoreFromCompressedBackupAsync_ShouldRestoreAllEntities()
    {
        // Arrange
        var originalGigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);
        var entities = CreateTestEntities(50);
        await SimplePerformanceOptimizations.BulkAddAsync(originalGigaMap, entities);

        var backup = await SimplePerformanceOptimizations.CreateCompressedBackupAsync(originalGigaMap);
        var newGigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);

        // Act
        var restoredIds = await SimplePerformanceOptimizations.RestoreFromCompressedBackupAsync(newGigaMap, backup);

        // Assert
        Assert.Equal(entities.Count, restoredIds.Count);
        Assert.Equal(entities.Count, newGigaMap.Size);
        
        // Verify data integrity
        var originalEntities = originalGigaMap.ToList();
        var restoredEntities = newGigaMap.ToList();
        
        Assert.Equal(originalEntities.Count, restoredEntities.Count);
        
        // Sort both lists by name for comparison
        var sortedOriginal = originalEntities.OrderBy(e => e.Name).ToList();
        var sortedRestored = restoredEntities.OrderBy(e => e.Name).ToList();
        
        for (int i = 0; i < sortedOriginal.Count; i++)
        {
            Assert.Equal(sortedOriginal[i].Name, sortedRestored[i].Name);
            Assert.Equal(sortedOriginal[i].Age, sortedRestored[i].Age);
            Assert.Equal(sortedOriginal[i].Department, sortedRestored[i].Department);
        }
    }

    [Fact]
    public void PerformanceStats_ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var stats = new PerformanceStats
        {
            EntityCount = 1000,
            IndexCount = 5,
            IsEmpty = false,
            IsReadOnly = false,
            HasIndices = true
        };

        // Act
        var result = stats.ToString();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("1000", result);
        Assert.Contains("5", result);
        Assert.Contains("False", result);
    }

    [Fact]
    public async Task QueryOptimizer_ExecuteWithMonitoringAsync_ShouldReturnValidResult()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestEntity>()
            .WithBitmapIndex(Indexer.Property<TestEntity, string>("Department", e => e.Department))
            .Build();
        
        var entities = CreateTestEntities(100);
        await SimplePerformanceOptimizations.BulkAddAsync(gigaMap, entities);

        var query = gigaMap.Query("Department", "Engineering");

        // Act
        var result = await QueryOptimizer.ExecuteWithMonitoringAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.True(result.ExecutionTime.TotalMilliseconds >= 0);
        Assert.True(result.ResultCount >= 0);
        Assert.NotNull(result.Results);
        Assert.Null(result.Error);
    }

    [Fact]
    public void MemoryOptimizer_ForceGarbageCollection_ShouldReturnMemoryInfo()
    {
        // Act
        var memoryInfo = MemoryOptimizer.ForceGarbageCollection();

        // Assert
        Assert.NotNull(memoryInfo);
        Assert.True(memoryInfo.MemoryAfterGC >= 0);
        Assert.True(memoryInfo.MemoryBeforeGC >= 0);
        Assert.True(memoryInfo.Generation0Collections >= 0);
        Assert.True(memoryInfo.Generation1Collections >= 0);
        Assert.True(memoryInfo.Generation2Collections >= 0);
    }

    [Fact]
    public void MemoryInfo_ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var memoryInfo = new MemoryInfo
        {
            MemoryAfterGC = 1024 * 1024 * 10, // 10 MB
            MemoryBeforeGC = 1024 * 1024 * 15, // 15 MB
            MemoryFreed = 1024 * 1024 * 5, // 5 MB
            Generation0Collections = 10,
            Generation1Collections = 5,
            Generation2Collections = 2
        };

        // Act
        var result = memoryInfo.ToString();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("10.0 MB", result);
        Assert.Contains("5.0 MB", result);
        Assert.Contains("Gen0=10", result);
        Assert.Contains("Gen1=5", result);
        Assert.Contains("Gen2=2", result);
    }
}
