using System;
using System.Collections.Generic;

namespace NebulaStore.Storage.Embedded.Types.Memory;

/// <summary>
/// Represents cache performance ratings following Eclipse Store patterns.
/// </summary>
public enum CachePerformanceRating
{
    /// <summary>
    /// Cache performance is poor and needs immediate attention.
    /// </summary>
    Poor,

    /// <summary>
    /// Cache performance is fair but could be improved.
    /// </summary>
    Fair,

    /// <summary>
    /// Cache performance is good with minor optimization opportunities.
    /// </summary>
    Good,

    /// <summary>
    /// Cache performance is excellent and optimal.
    /// </summary>
    Excellent
}

/// <summary>
/// Contains the results of cache evaluation following Eclipse Store patterns.
/// </summary>
public class CacheEvaluationResult
{
    /// <summary>
    /// Gets or sets the evaluation time.
    /// </summary>
    public DateTime EvaluationTime { get; set; }

    /// <summary>
    /// Gets or sets the memory statistics at evaluation time.
    /// </summary>
    public MemoryStatistics Statistics { get; set; } = new MemoryStatistics();

    /// <summary>
    /// Gets or sets the overall performance rating.
    /// </summary>
    public CachePerformanceRating PerformanceRating { get; set; } = CachePerformanceRating.Fair;

    /// <summary>
    /// Gets the list of identified issues.
    /// </summary>
    public List<string> Issues { get; } = new List<string>();

    /// <summary>
    /// Gets the list of optimization recommendations.
    /// </summary>
    public List<string> Recommendations { get; } = new List<string>();

    /// <summary>
    /// Gets a value indicating whether the cache has issues.
    /// </summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>
    /// Gets a value indicating whether optimization is recommended.
    /// </summary>
    public bool NeedsOptimization => PerformanceRating != CachePerformanceRating.Excellent || HasIssues;

    /// <summary>
    /// Gets a summary of the evaluation result.
    /// </summary>
    public string Summary => $"Rating: {PerformanceRating}, " +
                           $"Issues: {Issues.Count}, " +
                           $"Recommendations: {Recommendations.Count}, " +
                           $"Hit Ratio: {Statistics.CacheHitRatio:P1}, " +
                           $"Utilization: {Statistics.CacheUtilization:F1}%";

    /// <summary>
    /// Returns a string representation of the evaluation result.
    /// </summary>
    /// <returns>A string representation of the evaluation result.</returns>
    public override string ToString()
    {
        return $"CacheEvaluationResult: {Summary}";
    }
}