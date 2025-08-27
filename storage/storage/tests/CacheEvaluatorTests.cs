using System;
using Xunit;
using NebulaStore.Storage.Embedded.Types.Memory;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Tests for the Eclipse Store-style cache evaluation system.
/// </summary>
public class CacheEvaluatorTests : IDisposable
{
    private readonly MemoryManager _memoryManager;
    private readonly StorageEntityCacheEvaluator _evaluator;

    public CacheEvaluatorTests()
    {
        _memoryManager = new MemoryManager(1024 * 1024, TimeSpan.FromMinutes(5)); // 1MB threshold
        _evaluator = new StorageEntityCacheEvaluator();
    }

    [Fact]
    public void CacheEvaluator_EvaluateCache_ShouldReturnEvaluationResult()
    {
        // Arrange
        // Allocate some entities to have data to evaluate
        for (long i = 1; i <= 5; i++)
        {
            _memoryManager.AllocateEntityMemory(i, 1024);
        }

        // Act
        var result = _evaluator.EvaluateCache(_memoryManager);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.EvaluationTime <= DateTime.UtcNow);
        Assert.NotNull(result.Statistics);
        Assert.True(Enum.IsDefined(typeof(CachePerformanceRating), result.PerformanceRating));
    }

    [Fact]
    public void CacheEvaluator_EvaluateCache_WithGoodPerformance_ShouldReturnExcellentRating()
    {
        // Arrange
        // Create a scenario with good cache performance (low utilization, good hit ratio)
        _memoryManager.AllocateEntityMemory(1, 1024); // Small allocation, well within threshold

        // Act
        var result = _evaluator.EvaluateCache(_memoryManager);

        // Assert
        Assert.Equal(CachePerformanceRating.Excellent, result.PerformanceRating);
        Assert.False(result.HasIssues);
        Assert.Contains("optimal", result.Recommendations[0].ToLower());
    }

    [Fact]
    public void CacheEvaluator_ShouldEvict_WithHighUtilization_ShouldReturnTrue()
    {
        // Arrange
        // Fill cache to high utilization
        var entitySize = 200 * 1024; // 200KB per entity
        for (long i = 1; i <= 6; i++) // This should exceed 85% of 1MB threshold
        {
            _memoryManager.AllocateEntityMemory(i, entitySize);
        }

        // Act
        var shouldEvict = _evaluator.ShouldEvict(_memoryManager);

        // Assert
        Assert.True(shouldEvict);
    }

    [Fact]
    public void CacheEvaluator_ShouldEvict_WithLowUtilization_ShouldReturnFalse()
    {
        // Arrange
        // Allocate small amount
        _memoryManager.AllocateEntityMemory(1, 1024); // 1KB out of 1MB

        // Act
        var shouldEvict = _evaluator.ShouldEvict(_memoryManager);

        // Assert
        Assert.False(shouldEvict);
    }

    [Fact]
    public void CacheEvaluator_CalculateOptimalCacheSize_ShouldReturnReasonableSize()
    {
        // Arrange
        _memoryManager.AllocateEntityMemory(1, 1024);

        // Act
        var optimalSize = _evaluator.CalculateOptimalCacheSize(_memoryManager);

        // Assert
        Assert.True(optimalSize > 0);
        Assert.True(optimalSize >= _memoryManager.CacheThreshold); // Should be at least current threshold
    }

    [Fact]
    public void CacheEvaluator_EstimateMemorySavings_ShouldReturnSavingsForHighUtilization()
    {
        // Arrange
        // Fill cache to high utilization
        var entitySize = 200 * 1024; // 200KB per entity
        for (long i = 1; i <= 6; i++) // This should exceed 85% threshold
        {
            _memoryManager.AllocateEntityMemory(i, entitySize);
        }

        // Act
        var savings = _evaluator.EstimateMemorySavings(_memoryManager);

        // Assert
        Assert.True(savings > 0); // Should suggest some savings for high utilization
    }

    [Fact]
    public void CacheEvaluator_EstimateMemorySavings_ShouldReturnZeroForLowUtilization()
    {
        // Arrange
        _memoryManager.AllocateEntityMemory(1, 1024); // Low utilization

        // Act
        var savings = _evaluator.EstimateMemorySavings(_memoryManager);

        // Assert
        Assert.Equal(0, savings); // No savings needed for low utilization
    }

    [Fact]
    public void CacheEvaluator_EvaluateCache_WithNullManager_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _evaluator.EvaluateCache(null!));
    }

    [Fact]
    public void CacheEvaluator_ShouldEvict_WithNullManager_ShouldReturnFalse()
    {
        // Act
        var shouldEvict = _evaluator.ShouldEvict(null!);

        // Assert
        Assert.False(shouldEvict);
    }

    [Fact]
    public void CacheEvaluator_CalculateOptimalCacheSize_WithNullManager_ShouldReturnZero()
    {
        // Act
        var optimalSize = _evaluator.CalculateOptimalCacheSize(null!);

        // Assert
        Assert.Equal(0, optimalSize);
    }

    [Fact]
    public void CacheEvaluationResult_Properties_ShouldWorkCorrectly()
    {
        // Arrange
        var result = new CacheEvaluationResult
        {
            PerformanceRating = CachePerformanceRating.Good,
            Statistics = new MemoryStatistics
            {
                TotalAllocations = 100,
                TotalEvictions = 15,
                CacheUtilization = 75.0
            }
        };

        result.Issues.Add("Test issue");
        result.Recommendations.Add("Test recommendation");

        // Act & Assert
        Assert.True(result.HasIssues);
        Assert.True(result.NeedsOptimization); // Good rating but has issues
        Assert.Contains("Good", result.Summary);
        Assert.Contains("75.0%", result.Summary);
    }

    [Fact]
    public void MemoryStatistics_Properties_ShouldCalculateCorrectly()
    {
        // Arrange
        var statistics = new MemoryStatistics
        {
            CurrentCacheSize = 1024,
            CacheEntryCount = 4,
            TotalAllocations = 10,
            TotalEvictions = 2,
            CacheUtilization = 50.0
        };

        // Act & Assert
        Assert.Equal(0.8, statistics.CacheHitRatio); // (10-2)/10 = 0.8
        Assert.Equal(256.0, statistics.AverageCacheEntrySize); // 1024/4 = 256
        Assert.False(statistics.IsUnderPressure); // 50% < 90%
        Assert.Contains("1,024", statistics.Summary);
    }

    public void Dispose()
    {
        _memoryManager?.Dispose();
    }
}