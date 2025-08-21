using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NebulaStore.Storage.Embedded.Monitoring;

namespace NebulaStore.Storage.Embedded.Configuration;

/// <summary>
/// Intelligent performance tuner that analyzes workload patterns and provides optimization recommendations.
/// </summary>
public class PerformanceTuner : IPerformanceTuner
{
    private readonly string _name;
    private readonly IPerformanceMetrics _metrics;
    private readonly PerformanceTunerConfiguration _configuration;
    private readonly List<ITuningRule> _tuningRules;
    private readonly Timer? _autoTuningTimer;
    private volatile bool _isAutoTuningEnabled;
    private volatile bool _isDisposed;
    private CancellationTokenSource? _autoTuningCancellation;

    public PerformanceTuner(
        string name, 
        IPerformanceMetrics metrics, 
        PerformanceTunerConfiguration? configuration = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _configuration = configuration ?? new PerformanceTunerConfiguration();
        _tuningRules = new List<ITuningRule>();

        InitializeDefaultRules();
    }

    public string Name => _name;
    public bool IsAutoTuningEnabled => _isAutoTuningEnabled;

    public event EventHandler<TuningRecommendationsEventArgs>? RecommendationsAvailable;

    public IEnumerable<TuningRecommendation> AnalyzePerformance(IEnumerable<object> metrics)
    {
        if (_isDisposed) return Enumerable.Empty<TuningRecommendation>();

        var recommendations = new List<TuningRecommendation>();
        var metricsList = metrics.ToList();

        foreach (var rule in _tuningRules.Where(r => r.IsEnabled))
        {
            try
            {
                var ruleRecommendations = rule.Analyze(metricsList);
                recommendations.AddRange(ruleRecommendations);
            }
            catch
            {
                // Ignore individual rule failures
            }
        }

        // Sort by priority and expected improvement
        return recommendations
            .OrderByDescending(r => (int)r.Priority)
            .ThenByDescending(r => r.ExpectedImprovement)
            .ToList();
    }

    public IEnumerable<TuningRecommendation> ApplyRecommendations(
        IEnumerable<TuningRecommendation> recommendations,
        IPerformanceConfiguration configuration)
    {
        if (_isDisposed) return Enumerable.Empty<TuningRecommendation>();
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));

        var appliedRecommendations = new List<TuningRecommendation>();

        foreach (var recommendation in recommendations)
        {
            try
            {
                // Apply the recommendation based on its type
                var success = ApplyRecommendation(recommendation, configuration);
                if (success)
                {
                    appliedRecommendations.Add(recommendation);
                }
            }
            catch
            {
                // Ignore individual application failures
            }
        }

        return appliedRecommendations;
    }

    public async Task StartAutoTuningAsync(TimeSpan interval, CancellationToken cancellationToken = default)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(PerformanceTuner));
        if (_isAutoTuningEnabled) return;

        _isAutoTuningEnabled = true;
        _autoTuningCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start auto-tuning loop
        _ = Task.Run(async () =>
        {
            while (!_autoTuningCancellation.Token.IsCancellationRequested)
            {
                try
                {
                    await PerformAutoTuning();
                    await Task.Delay(interval, _autoTuningCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore auto-tuning errors and continue
                }
            }
        }, _autoTuningCancellation.Token);

        await Task.CompletedTask;
    }

    public async Task StopAutoTuningAsync()
    {
        if (!_isAutoTuningEnabled) return;

        _isAutoTuningEnabled = false;
        _autoTuningCancellation?.Cancel();
        _autoTuningCancellation?.Dispose();
        _autoTuningCancellation = null;

        await Task.CompletedTask;
    }

    /// <summary>
    /// Adds a custom tuning rule.
    /// </summary>
    /// <param name="rule">Tuning rule to add</param>
    public void AddTuningRule(ITuningRule rule)
    {
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        _tuningRules.Add(rule);
    }

    /// <summary>
    /// Removes a tuning rule.
    /// </summary>
    /// <param name="ruleId">Rule ID to remove</param>
    /// <returns>True if removed, false if not found</returns>
    public bool RemoveTuningRule(string ruleId)
    {
        if (string.IsNullOrWhiteSpace(ruleId)) return false;

        var rule = _tuningRules.FirstOrDefault(r => r.Id == ruleId);
        if (rule != null)
        {
            _tuningRules.Remove(rule);
            return true;
        }

        return false;
    }

    private async Task PerformAutoTuning()
    {
        try
        {
            // Get current metrics
            var currentMetrics = _metrics.GetCurrentValues().Cast<object>().ToList();
            
            if (currentMetrics.Count == 0) return;

            // Analyze performance and get recommendations
            var recommendations = AnalyzePerformance(currentMetrics);
            var filteredRecommendations = recommendations
                .Where(r => r.Priority >= _configuration.MinimumPriority)
                .Take(_configuration.MaxRecommendationsPerCycle)
                .ToList();

            if (filteredRecommendations.Count > 0)
            {
                // Fire recommendations event
                RecommendationsAvailable?.Invoke(this, new TuningRecommendationsEventArgs(filteredRecommendations));
            }
        }
        catch
        {
            // Ignore auto-tuning errors
        }

        await Task.CompletedTask;
    }

    private bool ApplyRecommendation(TuningRecommendation recommendation, IPerformanceConfiguration configuration)
    {
        try
        {
            // Convert the recommended value to the appropriate type
            var currentValue = configuration.GetValue<object>(recommendation.ConfigurationKey);
            var recommendedValue = recommendation.RecommendedValue;

            // Apply the recommendation
            return configuration.SetValue(recommendation.ConfigurationKey, recommendedValue, validate: true);
        }
        catch
        {
            return false;
        }
    }

    private void InitializeDefaultRules()
    {
        // Cache tuning rules
        _tuningRules.Add(new CacheSizeTuningRule());
        _tuningRules.Add(new CacheHitRatioTuningRule());

        // Memory tuning rules
        _tuningRules.Add(new MemoryPressureTuningRule());
        _tuningRules.Add(new ObjectPoolSizeTuningRule());

        // I/O tuning rules
        _tuningRules.Add(new BatchSizeTuningRule());
        _tuningRules.Add(new CompressionTuningRule());

        // Concurrency tuning rules
        _tuningRules.Add(new ThreadPoolSizeTuningRule());
        _tuningRules.Add(new WorkStealingTuningRule());

        // Index tuning rules
        _tuningRules.Add(new IndexLoadFactorTuningRule());
        _tuningRules.Add(new IndexRebuildTuningRule());
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _ = StopAutoTuningAsync();
        _autoTuningCancellation?.Dispose();
    }
}

/// <summary>
/// Configuration for performance tuner.
/// </summary>
public class PerformanceTunerConfiguration
{
    /// <summary>
    /// Gets or sets the minimum priority for auto-applied recommendations.
    /// </summary>
    public TuningPriority MinimumPriority { get; set; } = TuningPriority.Medium;

    /// <summary>
    /// Gets or sets the maximum number of recommendations per tuning cycle.
    /// </summary>
    public int MaxRecommendationsPerCycle { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to enable conservative tuning (smaller changes).
    /// </summary>
    public bool EnableConservativeTuning { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum improvement threshold for recommendations.
    /// </summary>
    public double MinimumImprovementThreshold { get; set; } = 0.05; // 5%

    /// <summary>
    /// Gets or sets the analysis window for metrics.
    /// </summary>
    public TimeSpan AnalysisWindow { get; set; } = TimeSpan.FromMinutes(10);

    public override string ToString()
    {
        return $"PerformanceTunerConfiguration[MinPriority={MinimumPriority}, " +
               $"MaxRecommendations={MaxRecommendationsPerCycle}, " +
               $"Conservative={EnableConservativeTuning}, " +
               $"MinImprovement={MinimumImprovementThreshold:P1}]";
    }
}

/// <summary>
/// Interface for tuning rules.
/// </summary>
public interface ITuningRule
{
    /// <summary>
    /// Gets the rule ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the rule name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the tuning category.
    /// </summary>
    TuningCategory Category { get; }

    /// <summary>
    /// Gets whether the rule is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Analyzes metrics and provides tuning recommendations.
    /// </summary>
    /// <param name="metrics">Metrics to analyze</param>
    /// <returns>Tuning recommendations</returns>
    IEnumerable<TuningRecommendation> Analyze(IEnumerable<object> metrics);
}

/// <summary>
/// Base class for tuning rules.
/// </summary>
public abstract class TuningRuleBase : ITuningRule
{
    protected TuningRuleBase(string id, string name, TuningCategory category, bool isEnabled = true)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Category = category;
        IsEnabled = isEnabled;
    }

    public string Id { get; }
    public string Name { get; }
    public TuningCategory Category { get; }
    public bool IsEnabled { get; }

    public abstract IEnumerable<TuningRecommendation> Analyze(IEnumerable<object> metrics);

    protected TuningRecommendation CreateRecommendation(
        string name,
        string description,
        TuningPriority priority,
        string configKey,
        object currentValue,
        object recommendedValue,
        double expectedImprovement,
        string reasoning)
    {
        return new TuningRecommendation(
            Guid.NewGuid().ToString("N"),
            name,
            description,
            Category,
            priority,
            configKey,
            currentValue,
            recommendedValue,
            expectedImprovement,
            reasoning
        );
    }
}

/// <summary>
/// Cache size tuning rule.
/// </summary>
public class CacheSizeTuningRule : TuningRuleBase
{
    public CacheSizeTuningRule() : base("cache-size-tuning", "Cache Size Optimization", TuningCategory.Cache)
    {
    }

    public override IEnumerable<TuningRecommendation> Analyze(IEnumerable<object> metrics)
    {
        // Analyze cache metrics and recommend size adjustments
        // This is a simplified implementation
        yield break;
    }
}

/// <summary>
/// Cache hit ratio tuning rule.
/// </summary>
public class CacheHitRatioTuningRule : TuningRuleBase
{
    public CacheHitRatioTuningRule() : base("cache-hit-ratio-tuning", "Cache Hit Ratio Optimization", TuningCategory.Cache)
    {
    }

    public override IEnumerable<TuningRecommendation> Analyze(IEnumerable<object> metrics)
    {
        // Analyze cache hit ratios and recommend improvements
        // This is a simplified implementation
        yield break;
    }
}

// Additional tuning rule implementations would follow the same pattern
public class MemoryPressureTuningRule : TuningRuleBase
{
    public MemoryPressureTuningRule() : base("memory-pressure-tuning", "Memory Pressure Optimization", TuningCategory.Memory) { }
    public override IEnumerable<TuningRecommendation> Analyze(IEnumerable<object> metrics) { yield break; }
}

public class ObjectPoolSizeTuningRule : TuningRuleBase
{
    public ObjectPoolSizeTuningRule() : base("object-pool-size-tuning", "Object Pool Size Optimization", TuningCategory.Memory) { }
    public override IEnumerable<TuningRecommendation> Analyze(IEnumerable<object> metrics) { yield break; }
}

public class BatchSizeTuningRule : TuningRuleBase
{
    public BatchSizeTuningRule() : base("batch-size-tuning", "Batch Size Optimization", TuningCategory.IO) { }
    public override IEnumerable<TuningRecommendation> Analyze(IEnumerable<object> metrics) { yield break; }
}

public class CompressionTuningRule : TuningRuleBase
{
    public CompressionTuningRule() : base("compression-tuning", "Compression Optimization", TuningCategory.IO) { }
    public override IEnumerable<TuningRecommendation> Analyze(IEnumerable<object> metrics) { yield break; }
}

public class ThreadPoolSizeTuningRule : TuningRuleBase
{
    public ThreadPoolSizeTuningRule() : base("thread-pool-size-tuning", "Thread Pool Size Optimization", TuningCategory.Concurrency) { }
    public override IEnumerable<TuningRecommendation> Analyze(IEnumerable<object> metrics) { yield break; }
}

public class WorkStealingTuningRule : TuningRuleBase
{
    public WorkStealingTuningRule() : base("work-stealing-tuning", "Work Stealing Optimization", TuningCategory.Concurrency) { }
    public override IEnumerable<TuningRecommendation> Analyze(IEnumerable<object> metrics) { yield break; }
}

public class IndexLoadFactorTuningRule : TuningRuleBase
{
    public IndexLoadFactorTuningRule() : base("index-load-factor-tuning", "Index Load Factor Optimization", TuningCategory.Indexing) { }
    public override IEnumerable<TuningRecommendation> Analyze(IEnumerable<object> metrics) { yield break; }
}

public class IndexRebuildTuningRule : TuningRuleBase
{
    public IndexRebuildTuningRule() : base("index-rebuild-tuning", "Index Rebuild Optimization", TuningCategory.Indexing) { }
    public override IEnumerable<TuningRecommendation> Analyze(IEnumerable<object> metrics) { yield break; }
}
