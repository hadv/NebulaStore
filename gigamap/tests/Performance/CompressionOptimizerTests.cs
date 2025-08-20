using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.GigaMap;
using NebulaStore.GigaMap.Performance;

namespace NebulaStore.GigaMap.Tests.Performance;

public class CompressionOptimizerTests
{
    public class TestEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public List<string> Tags { get; set; } = new();
    }

    private static List<TestEntity> CreateTestEntities(int count)
    {
        var entities = new List<TestEntity>();
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            entities.Add(new TestEntity
            {
                Name = $"Entity_{i:D4}",
                Value = random.Next(1, 1000),
                Description = $"This is a test entity with index {i} and some additional text to make it more realistic for compression testing.",
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                Tags = new List<string> { $"tag_{i % 10}", $"category_{i % 5}", "test" }
            });
        }

        return entities;
    }

    [Fact]
    public async Task CompressEntitiesAsync_ShouldCompressData()
    {
        // Arrange
        var entities = CreateTestEntities(100);

        // Act
        var compressedData = await CompressionOptimizer.CompressEntitiesAsync(entities);

        // Assert
        Assert.NotNull(compressedData);
        Assert.True(compressedData.CompressedSize > 0);
        Assert.True(compressedData.OriginalSize > 0);
        Assert.True(compressedData.CompressedSize < compressedData.OriginalSize);
        Assert.Equal(entities.Count, compressedData.EntityCount);
        Assert.True(compressedData.CompressionRatio > 0 && compressedData.CompressionRatio < 1.0);
        Assert.Equal(CompressionLevel.Optimal, compressedData.CompressionLevel);
        Assert.True(compressedData.SpaceSavings > 0);
        Assert.True(compressedData.CompressionPercentage > 0);
    }

    [Fact]
    public async Task DecompressEntitiesAsync_ShouldRestoreOriginalData()
    {
        // Arrange
        var originalEntities = CreateTestEntities(50);
        var compressedData = await CompressionOptimizer.CompressEntitiesAsync(originalEntities);

        // Act
        var decompressedEntities = await CompressionOptimizer.DecompressEntitiesAsync(compressedData);

        // Assert
        Assert.NotNull(decompressedEntities);
        Assert.Equal(originalEntities.Count, decompressedEntities.Count);

        // Verify data integrity
        for (int i = 0; i < originalEntities.Count; i++)
        {
            Assert.Equal(originalEntities[i].Name, decompressedEntities[i].Name);
            Assert.Equal(originalEntities[i].Value, decompressedEntities[i].Value);
            Assert.Equal(originalEntities[i].Description, decompressedEntities[i].Description);
            Assert.Equal(originalEntities[i].Tags.Count, decompressedEntities[i].Tags.Count);
        }
    }

    [Fact]
    public async Task CompressQueryResultAsync_ShouldCompressQueryData()
    {
        // Arrange
        var entities = CreateTestEntities(100);
        var querySignature = "test_query_signature";

        // Act
        var compressedResult = await CompressionOptimizer.CompressQueryResultAsync(
            entities, querySignature);

        // Assert
        Assert.NotNull(compressedResult);
        Assert.Equal(querySignature, compressedResult.QuerySignature);
        Assert.True(compressedResult.CompressedSize > 0);
        Assert.True(compressedResult.OriginalSize > 0);
        Assert.True(compressedResult.CompressedSize < compressedResult.OriginalSize);
        Assert.Equal(entities.Count, compressedResult.ResultCount);
        Assert.True(compressedResult.CompressionRatio > 0 && compressedResult.CompressionRatio < 1.0);
        Assert.False(compressedResult.IsExpired); // Should not be expired immediately
    }

    [Fact]
    public async Task DecompressQueryResultAsync_ShouldRestoreQueryData()
    {
        // Arrange
        var originalEntities = CreateTestEntities(30);
        var querySignature = "test_query";
        var compressedResult = await CompressionOptimizer.CompressQueryResultAsync(
            originalEntities, querySignature);

        // Act
        var decompressedResult = await CompressionOptimizer.DecompressQueryResultAsync(compressedResult);

        // Assert
        Assert.NotNull(decompressedResult);
        Assert.Equal(querySignature, decompressedResult.QuerySignature);
        Assert.Equal(originalEntities.Count, decompressedResult.Results.Count);
        Assert.Equal(originalEntities.Count, decompressedResult.ResultCount);

        // Verify data integrity
        for (int i = 0; i < originalEntities.Count; i++)
        {
            Assert.Equal(originalEntities[i].Name, decompressedResult.Results[i].Name);
            Assert.Equal(originalEntities[i].Value, decompressedResult.Results[i].Value);
        }
    }

    [Fact]
    public async Task AnalyzeCompressionAsync_ShouldAnalyzeAllLevels()
    {
        // Arrange
        var entities = CreateTestEntities(200);

        // Act
        var analysis = await CompressionOptimizer.AnalyzeCompressionAsync(entities);

        // Assert
        Assert.NotNull(analysis);
        Assert.Equal(entities.Count, analysis.EntityCount);
        Assert.True(analysis.OriginalSize > 0);
        Assert.True(analysis.LevelResults.Count >= 4); // Should have all compression levels

        // Verify all compression levels are analyzed
        Assert.True(analysis.LevelResults.ContainsKey(CompressionLevel.NoCompression));
        Assert.True(analysis.LevelResults.ContainsKey(CompressionLevel.Fastest));
        Assert.True(analysis.LevelResults.ContainsKey(CompressionLevel.Optimal));
        Assert.True(analysis.LevelResults.ContainsKey(CompressionLevel.SmallestSize));

        // Verify compression ratios make sense
        var noCompression = analysis.LevelResults[CompressionLevel.NoCompression];
        var optimal = analysis.LevelResults[CompressionLevel.Optimal];
        
        Assert.True(noCompression.CompressionRatio >= optimal.CompressionRatio);
        Assert.True(analysis.BestCompressionRatio > 0);
        Assert.True(analysis.FastestCompressionTime.TotalMilliseconds >= 0);
    }

    [Fact]
    public async Task EstimateMemorySavingsAsync_ShouldProvideEstimate()
    {
        // Arrange
        var gigaMap = new DefaultGigaMap<TestEntity>(EqualityComparer<TestEntity>.Default, 8, 12, 16, 20);
        var entities = CreateTestEntities(500);
        
        foreach (var entity in entities)
        {
            gigaMap.Add(entity);
        }

        // Act
        var estimate = await CompressionOptimizer.EstimateMemorySavingsAsync(gigaMap, sampleSize: 100);

        // Assert
        Assert.NotNull(estimate);
        Assert.Equal(entities.Count, estimate.TotalEntities);
        Assert.True(estimate.SampleSize > 0);
        Assert.True(estimate.EstimatedCurrentMemoryUsage > 0);
        Assert.True(estimate.EstimatedCompressedMemoryUsage > 0);
        Assert.True(estimate.EstimatedMemorySavings >= 0);
        Assert.True(estimate.EstimatedCompressionRatio > 0);
        Assert.True(estimate.SavingsPercentage >= 0);
    }

    [Fact]
    public void CompressedData_Properties_ShouldCalculateCorrectly()
    {
        // Arrange
        var compressedData = new CompressedData<TestEntity>
        {
            OriginalSize = 1000,
            CompressedSize = 300,
            EntityCount = 10,
            CompressionLevel = CompressionLevel.Optimal,
            CompressionRatio = 0.3 // 300/1000 = 0.3
        };

        // Act & Assert
        Assert.Equal(700, compressedData.SpaceSavings);
        Assert.Equal(70.0, compressedData.CompressionPercentage); // (1 - 0.3) * 100 = 70%
    }

    [Fact]
    public void CompressedQueryResult_IsExpired_ShouldWorkCorrectly()
    {
        // Arrange
        var result = new CompressedQueryResult<TestEntity>
        {
            CompressedAt = DateTime.UtcNow.AddMinutes(-5),
            CacheExpiry = TimeSpan.FromMinutes(10)
        };

        var expiredResult = new CompressedQueryResult<TestEntity>
        {
            CompressedAt = DateTime.UtcNow.AddMinutes(-15),
            CacheExpiry = TimeSpan.FromMinutes(10)
        };

        // Act & Assert
        Assert.False(result.IsExpired);
        Assert.True(expiredResult.IsExpired);
    }

    [Fact]
    public void CompressionAnalysis_Properties_ShouldCalculateCorrectly()
    {
        // Arrange
        var analysis = new CompressionAnalysis
        {
            OriginalSize = 1000,
            EntityCount = 100,
            LevelResults = new Dictionary<CompressionLevel, CompressionLevelResult>
            {
                [CompressionLevel.Fastest] = new CompressionLevelResult 
                { 
                    CompressionRatio = 0.8, 
                    CompressionTime = TimeSpan.FromMilliseconds(10) 
                },
                [CompressionLevel.Optimal] = new CompressionLevelResult 
                { 
                    CompressionRatio = 0.6, 
                    CompressionTime = TimeSpan.FromMilliseconds(20) 
                }
            }
        };

        // Act & Assert
        Assert.Equal(0.6, analysis.BestCompressionRatio);
        Assert.Equal(TimeSpan.FromMilliseconds(10), analysis.FastestCompressionTime);
    }

    [Fact]
    public void CompressionLevelResult_CompressionSpeed_ShouldCalculateCorrectly()
    {
        // Arrange
        var result = new CompressionLevelResult
        {
            SpaceSavings = 1024 * 1024, // 1 MB
            CompressionTime = TimeSpan.FromSeconds(1)
        };

        // Act
        var speed = result.CompressionSpeed;

        // Assert
        Assert.Equal(1.0, speed, 2); // 1 MB/s with 2 decimal precision
    }

    [Fact]
    public void MemorySavingsEstimate_SavingsPercentage_ShouldCalculateCorrectly()
    {
        // Arrange
        var estimate = new MemorySavingsEstimate
        {
            EstimatedCurrentMemoryUsage = 1000,
            EstimatedMemorySavings = 300
        };

        // Act
        var percentage = estimate.SavingsPercentage;

        // Assert
        Assert.Equal(30.0, percentage);
    }

    [Theory]
    [InlineData(CompressionLevel.NoCompression)]
    [InlineData(CompressionLevel.Fastest)]
    [InlineData(CompressionLevel.Optimal)]
    [InlineData(CompressionLevel.SmallestSize)]
    public async Task CompressEntitiesAsync_WithDifferentLevels_ShouldWork(CompressionLevel level)
    {
        // Arrange
        var entities = CreateTestEntities(50);

        // Act
        var compressedData = await CompressionOptimizer.CompressEntitiesAsync(entities, level);

        // Assert
        Assert.NotNull(compressedData);
        Assert.Equal(level, compressedData.CompressionLevel);
        Assert.True(compressedData.OriginalSize > 0);
        Assert.True(compressedData.CompressedSize > 0);
        
        if (level == CompressionLevel.NoCompression)
        {
            // No compression should result in similar or larger size
            Assert.True(compressedData.CompressionRatio >= 1.0);
        }
        else
        {
            // Other levels should compress
            Assert.True(compressedData.CompressionRatio < 1.0);
        }
    }

    [Fact]
    public async Task CompressEntitiesAsync_WithEmptyList_ShouldHandleGracefully()
    {
        // Arrange
        var entities = new List<TestEntity>();

        // Act
        var compressedData = await CompressionOptimizer.CompressEntitiesAsync(entities);

        // Assert
        Assert.NotNull(compressedData);
        Assert.Equal(0, compressedData.EntityCount);
        Assert.True(compressedData.OriginalSize >= 0);
        Assert.True(compressedData.CompressedSize >= 0);
    }
}
