using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Configuration;

/// <summary>
/// Interface for performance configuration management with dynamic updates and validation.
/// </summary>
public interface IPerformanceConfiguration : IDisposable
{
    /// <summary>
    /// Gets the configuration name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the current configuration version.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Gets whether dynamic updates are enabled.
    /// </summary>
    bool SupportsDynamicUpdates { get; }

    /// <summary>
    /// Gets a configuration value.
    /// </summary>
    /// <typeparam name="T">Type of the configuration value</typeparam>
    /// <param name="key">Configuration key</param>
    /// <param name="defaultValue">Default value if key not found</param>
    /// <returns>Configuration value</returns>
    T GetValue<T>(string key, T defaultValue = default(T)!);

    /// <summary>
    /// Sets a configuration value.
    /// </summary>
    /// <typeparam name="T">Type of the configuration value</typeparam>
    /// <param name="key">Configuration key</param>
    /// <param name="value">Configuration value</param>
    /// <param name="validate">Whether to validate the value</param>
    /// <returns>True if set successfully, false otherwise</returns>
    bool SetValue<T>(string key, T value, bool validate = true);

    /// <summary>
    /// Gets all configuration keys.
    /// </summary>
    /// <returns>Collection of configuration keys</returns>
    IEnumerable<string> GetKeys();

    /// <summary>
    /// Gets all configuration values.
    /// </summary>
    /// <returns>Dictionary of configuration values</returns>
    IReadOnlyDictionary<string, object?> GetAllValues();

    /// <summary>
    /// Validates the current configuration.
    /// </summary>
    /// <returns>Validation result</returns>
    ConfigurationValidationResult Validate();

    /// <summary>
    /// Reloads configuration from source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves configuration to source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets configuration to defaults.
    /// </summary>
    void ResetToDefaults();

    /// <summary>
    /// Creates a configuration snapshot.
    /// </summary>
    /// <returns>Configuration snapshot</returns>
    IConfigurationSnapshot CreateSnapshot();

    /// <summary>
    /// Restores configuration from a snapshot.
    /// </summary>
    /// <param name="snapshot">Configuration snapshot</param>
    void RestoreFromSnapshot(IConfigurationSnapshot snapshot);

    /// <summary>
    /// Event fired when configuration changes.
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
}

/// <summary>
/// Interface for configuration snapshots.
/// </summary>
public interface IConfigurationSnapshot
{
    /// <summary>
    /// Gets the snapshot timestamp.
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Gets the configuration version.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Gets the snapshot data.
    /// </summary>
    IReadOnlyDictionary<string, object?> Data { get; }

    /// <summary>
    /// Gets the snapshot metadata.
    /// </summary>
    IReadOnlyDictionary<string, string> Metadata { get; }
}

/// <summary>
/// Interface for performance tuning recommendations.
/// </summary>
public interface IPerformanceTuner : IDisposable
{
    /// <summary>
    /// Gets the tuner name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether auto-tuning is enabled.
    /// </summary>
    bool IsAutoTuningEnabled { get; }

    /// <summary>
    /// Analyzes current performance and provides tuning recommendations.
    /// </summary>
    /// <param name="metrics">Current performance metrics</param>
    /// <returns>Tuning recommendations</returns>
    IEnumerable<TuningRecommendation> AnalyzePerformance(IEnumerable<object> metrics);

    /// <summary>
    /// Applies tuning recommendations to configuration.
    /// </summary>
    /// <param name="recommendations">Recommendations to apply</param>
    /// <param name="configuration">Configuration to update</param>
    /// <returns>Applied recommendations</returns>
    IEnumerable<TuningRecommendation> ApplyRecommendations(
        IEnumerable<TuningRecommendation> recommendations,
        IPerformanceConfiguration configuration);

    /// <summary>
    /// Starts auto-tuning based on workload patterns.
    /// </summary>
    /// <param name="interval">Tuning interval</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StartAutoTuningAsync(TimeSpan interval, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops auto-tuning.
    /// </summary>
    Task StopAutoTuningAsync();

    /// <summary>
    /// Event fired when tuning recommendations are available.
    /// </summary>
    event EventHandler<TuningRecommendationsEventArgs> RecommendationsAvailable;
}

/// <summary>
/// Configuration validation result.
/// </summary>
public class ConfigurationValidationResult
{
    public ConfigurationValidationResult(bool isValid, IEnumerable<string>? errors = null, IEnumerable<string>? warnings = null)
    {
        IsValid = isValid;
        Errors = errors?.ToList() ?? new List<string>();
        Warnings = warnings?.ToList() ?? new List<string>();
    }

    /// <summary>
    /// Gets whether the configuration is valid.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Gets validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    /// Gets whether there are any issues.
    /// </summary>
    public bool HasIssues => Errors.Count > 0 || Warnings.Count > 0;

    public override string ToString()
    {
        if (IsValid && !HasIssues)
            return "Configuration is valid";

        var issues = new List<string>();
        if (Errors.Count > 0)
            issues.Add($"{Errors.Count} error(s)");
        if (Warnings.Count > 0)
            issues.Add($"{Warnings.Count} warning(s)");

        return $"Configuration validation: {string.Join(", ", issues)}";
    }
}

/// <summary>
/// Tuning recommendation.
/// </summary>
public class TuningRecommendation
{
    public TuningRecommendation(
        string id,
        string name,
        string description,
        TuningCategory category,
        TuningPriority priority,
        string configurationKey,
        object currentValue,
        object recommendedValue,
        double expectedImprovement,
        string reasoning)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Category = category;
        Priority = priority;
        ConfigurationKey = configurationKey ?? throw new ArgumentNullException(nameof(configurationKey));
        CurrentValue = currentValue;
        RecommendedValue = recommendedValue;
        ExpectedImprovement = expectedImprovement;
        Reasoning = reasoning ?? throw new ArgumentNullException(nameof(reasoning));
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the recommendation ID.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the recommendation name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the recommendation description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the tuning category.
    /// </summary>
    public TuningCategory Category { get; }

    /// <summary>
    /// Gets the recommendation priority.
    /// </summary>
    public TuningPriority Priority { get; }

    /// <summary>
    /// Gets the configuration key to modify.
    /// </summary>
    public string ConfigurationKey { get; }

    /// <summary>
    /// Gets the current value.
    /// </summary>
    public object CurrentValue { get; }

    /// <summary>
    /// Gets the recommended value.
    /// </summary>
    public object RecommendedValue { get; }

    /// <summary>
    /// Gets the expected performance improvement (0.0 to 1.0).
    /// </summary>
    public double ExpectedImprovement { get; }

    /// <summary>
    /// Gets the reasoning for this recommendation.
    /// </summary>
    public string Reasoning { get; }

    /// <summary>
    /// Gets when the recommendation was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    public override string ToString()
    {
        return $"[{Priority}] {Name}: Change '{ConfigurationKey}' from {CurrentValue} to {RecommendedValue} " +
               $"(Expected improvement: {ExpectedImprovement:P1}) - {Reasoning}";
    }
}

/// <summary>
/// Enumeration of tuning categories.
/// </summary>
public enum TuningCategory
{
    /// <summary>
    /// Memory-related tuning.
    /// </summary>
    Memory,

    /// <summary>
    /// CPU-related tuning.
    /// </summary>
    CPU,

    /// <summary>
    /// I/O-related tuning.
    /// </summary>
    IO,

    /// <summary>
    /// Cache-related tuning.
    /// </summary>
    Cache,

    /// <summary>
    /// Concurrency-related tuning.
    /// </summary>
    Concurrency,

    /// <summary>
    /// Index-related tuning.
    /// </summary>
    Indexing,

    /// <summary>
    /// Query-related tuning.
    /// </summary>
    Query,

    /// <summary>
    /// Network-related tuning.
    /// </summary>
    Network,

    /// <summary>
    /// General performance tuning.
    /// </summary>
    General
}

/// <summary>
/// Enumeration of tuning priorities.
/// </summary>
public enum TuningPriority
{
    /// <summary>
    /// Low priority recommendation.
    /// </summary>
    Low,

    /// <summary>
    /// Medium priority recommendation.
    /// </summary>
    Medium,

    /// <summary>
    /// High priority recommendation.
    /// </summary>
    High,

    /// <summary>
    /// Critical priority recommendation.
    /// </summary>
    Critical
}

/// <summary>
/// Event arguments for configuration changed events.
/// </summary>
public class ConfigurationChangedEventArgs : EventArgs
{
    public ConfigurationChangedEventArgs(string key, object? oldValue, object? newValue)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        OldValue = oldValue;
        NewValue = newValue;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the configuration key that changed.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the old value.
    /// </summary>
    public object? OldValue { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public object? NewValue { get; }

    /// <summary>
    /// Gets when the change occurred.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"Configuration changed: {Key} = {OldValue} -> {NewValue} @ {Timestamp:HH:mm:ss}";
    }
}

/// <summary>
/// Event arguments for tuning recommendations events.
/// </summary>
public class TuningRecommendationsEventArgs : EventArgs
{
    public TuningRecommendationsEventArgs(IEnumerable<TuningRecommendation> recommendations)
    {
        Recommendations = recommendations?.ToList() ?? new List<TuningRecommendation>();
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the tuning recommendations.
    /// </summary>
    public IReadOnlyList<TuningRecommendation> Recommendations { get; }

    /// <summary>
    /// Gets when the recommendations were generated.
    /// </summary>
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"Tuning recommendations available: {Recommendations.Count} recommendation(s) @ {Timestamp:HH:mm:ss}";
    }
}
