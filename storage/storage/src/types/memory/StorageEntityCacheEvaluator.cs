using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.Storage.Embedded.Types.Memory;

/// <summary>
/// Evaluates storage entity cache performance and provides optimization recommendations following Eclipse Store patterns.
/// </summary>
public class StorageEntityCacheEvaluator
{
    #region Private Fields

    private readonly TimeSpan _evaluationPeriod;
    private readonly double _targetHitRatio;
    private readonly double _maxUtilization;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the StorageEntityCacheEvaluator class.
    /// </summary>
    /// <param name="evaluationPeriod">The evaluation period for cache analysis.</param>
    /// <param name="targetHitRatio">The target cache hit ratio (0.0 to 1.0).</param>
    /// <param name="maxUtilization">The maximum cache utilization percentage (0.0 to 100.0).</param>
    public StorageEntityCacheEvaluator(
        TimeSpan? evaluationPeriod = null,
        double targetHitRatio = 0.85,
        double maxUtilization = 85.0)
    {
        _evaluationPeriod = evaluationPeriod ?? TimeSpan.FromMinutes(5);
        _targetHitRatio = Math.Clamp(targetHitRatio, 0.0, 1.0);
        _maxUtilization = Math.Clamp(maxUtilization, 0.0, 100.0);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Evaluates cache performance and provides recommendations following Eclipse Store patterns.
    /// </summary>
    /// <param name="memoryManager">The memory manager to evaluate.</param>
    /// <returns>The cache evaluation result.</returns>
    public CacheEvaluationResult EvaluateCache(MemoryManager memoryManager)
    {
        if (memoryManager == null)
            throw new ArgumentNullException(nameof(memoryManager));

        var statistics = memoryManager.GetStatistics();
        var result = new CacheEvaluationResult
        {
            EvaluationTime = DateTime.UtcNow,
            Statistics = statistics
        };

        // Analyze cache performance
        AnalyzeCachePerformance(result, statistics);

        // Generate recommendations
        GenerateRecommendations(result, statistics);

        return result;
    }

    /// <summary>
    /// Determines if cache eviction is needed following Eclipse Store patterns.
    /// </summary>
    /// <param name="memoryManager">The memory manager to check.</param>
    /// <returns>True if eviction is recommended, false otherwise.</returns>
    public bool ShouldEvict(MemoryManager memoryManager)
    {
        if (memoryManager == null)
            return false;

        var statistics = memoryManager.GetStatistics();

        // Evict if utilization is too high
        if (statistics.CacheUtilization > _maxUtilization)
            return true;

        // Evict if hit ratio is too low (cache is not effective)
        if (statistics.CacheHitRatio < _targetHitRatio && statistics.TotalAllocations > 100)
            return true;

        return false;
    }

    /// <summary>
    /// Calculates optimal cache size following Eclipse Store patterns.
    /// </summary>
    /// <param name="memoryManager">The memory manager to analyze.</param>
    /// <returns>The recommended cache size in bytes.</returns>
    public long CalculateOptimalCacheSize(MemoryManager memoryManager)
    {
        if (memoryManager == null)
            return 0;

        var statistics = memoryManager.GetStatistics();

        // If hit ratio is good, current size is probably fine
        if (statistics.CacheHitRatio >= _targetHitRatio)
        {
            return statistics.CacheThreshold;
        }

        // If hit ratio is low, consider increasing cache size
        if (statistics.CacheHitRatio < _targetHitRatio * 0.8)
        {
            return (long)(statistics.CacheThreshold * 1.5);
        }

        // If utilization is too high, consider increasing cache size
        if (statistics.CacheUtilization > _maxUtilization)
        {
            return (long)(statistics.CacheThreshold * 1.2);
        }

        return statistics.CacheThreshold;
    }

    /// <summary>
    /// Estimates memory savings from cache optimization following Eclipse Store patterns.
    /// </summary>
    /// <param name="memoryManager">The memory manager to analyze.</param>
    /// <returns>The estimated memory savings in bytes.</returns>
    public long EstimateMemorySavings(MemoryManager memoryManager)
    {
        if (memoryManager == null)
            return 0;

        var statistics = memoryManager.GetStatistics();

        // Estimate savings from removing least recently used entries
        var potentialSavings = 0L;

        // If cache is over-utilized, we could save by evicting old entries
        if (statistics.CacheUtilization > _maxUtilization)
        {
            var excessUtilization = statistics.CacheUtilization - _maxUtilization;
            potentialSavings = (long)(statistics.CurrentCacheSize * (excessUtilization / 100.0));
        }

        return potentialSavings;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Analyzes cache performance metrics.
    /// </summary>
    /// <param name="result">The evaluation result to update.</param>
    /// <param name="statistics">The memory statistics.</param>
    private void AnalyzeCachePerformance(CacheEvaluationResult result, MemoryStatistics statistics)
    {
        // Determine overall performance rating
        if (statistics.CacheHitRatio >= _targetHitRatio && statistics.CacheUtilization <= _maxUtilization)
        {
            result.PerformanceRating = CachePerformanceRating.Excellent;
        }
        else if (statistics.CacheHitRatio >= _targetHitRatio * 0.8 && statistics.CacheUtilization <= _maxUtilization * 1.1)
        {
            result.PerformanceRating = CachePerformanceRating.Good;
        }
        else if (statistics.CacheHitRatio >= _targetHitRatio * 0.6 && statistics.CacheUtilization <= _maxUtilization * 1.2)
        {
            result.PerformanceRating = CachePerformanceRating.Fair;
        }
        else
        {
            result.PerformanceRating = CachePerformanceRating.Poor;
        }

        // Identify specific issues
        if (statistics.CacheUtilization > _maxUtilization)
        {
            result.Issues.Add("Cache utilization is too high");
        }

        if (statistics.CacheHitRatio < _targetHitRatio)
        {
            result.Issues.Add("Cache hit ratio is below target");
        }

        if (statistics.IsUnderPressure)
        {
            result.Issues.Add("Cache is under memory pressure");
        }
    }

    /// <summary>
    /// Generates optimization recommendations.
    /// </summary>
    /// <param name="result">The evaluation result to update.</param>
    /// <param name="statistics">The memory statistics.</param>
    private void GenerateRecommendations(CacheEvaluationResult result, MemoryStatistics statistics)
    {
        if (statistics.CacheUtilization > _maxUtilization)
        {
            result.Recommendations.Add("Consider increasing cache threshold or performing more frequent evictions");
        }

        if (statistics.CacheHitRatio < _targetHitRatio)
        {
            result.Recommendations.Add("Consider increasing cache size or adjusting cache timeout");
        }

        if (statistics.TotalEvictions > statistics.TotalAllocations * 0.5)
        {
            result.Recommendations.Add("High eviction rate detected - consider increasing cache size");
        }

        if (statistics.CacheEntryCount > 0 && statistics.AverageCacheEntrySize < 1024)
        {
            result.Recommendations.Add("Many small cache entries detected - consider cache consolidation");
        }

        if (result.PerformanceRating == CachePerformanceRating.Excellent)
        {
            result.Recommendations.Add("Cache performance is optimal - no changes needed");
        }
    }

    #endregion
}