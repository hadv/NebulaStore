using System;

namespace NebulaStore.Storage.Embedded.Types;

/// <summary>
/// Function type that evaluates if a live entity (entity with cached data) shall be unloaded (its cache cleared).
/// 
/// Note that any implementation of this type must be safe enough to never throw an exception as this would doom
/// the storage thread that executes it. Catching any exception would not prevent the problem for the channel thread
/// as the function has to work in order for the channel to work properly.
/// It is therefore strongly suggested that implementations only use "exception free" logic (like simple arithmetic)
/// or handle any possible exception internally.
/// </summary>
public interface IStorageEntityCacheEvaluator
{
    /// <summary>
    /// Evaluates whether the entity's cache should be cleared.
    /// </summary>
    /// <param name="totalCacheSize">The total cache size in bytes.</param>
    /// <param name="evaluationTime">The current evaluation time in milliseconds.</param>
    /// <param name="entity">The entity to evaluate.</param>
    /// <returns>True if the entity's cache should be cleared, false otherwise.</returns>
    bool ClearEntityCache(long totalCacheSize, long evaluationTime, IStorageEntity entity);

    /// <summary>
    /// Evaluates whether the entity should initially be cached.
    /// </summary>
    /// <param name="totalCacheSize">The total cache size in bytes.</param>
    /// <param name="evaluationTime">The current evaluation time in milliseconds.</param>
    /// <param name="entity">The entity to evaluate.</param>
    /// <returns>True if the entity should initially be cached, false otherwise.</returns>
    bool InitiallyCacheEntity(long totalCacheSize, long evaluationTime, IStorageEntity entity)
    {
        return !ClearEntityCache(totalCacheSize, evaluationTime, entity);
    }
}

/// <summary>
/// Default values for storage entity cache evaluator.
/// </summary>
public static class StorageEntityCacheEvaluatorDefaults
{
    /// <summary>
    /// Gets the default cache threshold (~1 GB).
    /// </summary>
    /// <returns>The default cache threshold.</returns>
    public static long DefaultCacheThreshold() => 1_000_000_000;

    /// <summary>
    /// Gets the default timeout in milliseconds (1 day).
    /// </summary>
    /// <returns>The default timeout in milliseconds.</returns>
    public static long DefaultTimeoutMs() => 86_400_000;
}

/// <summary>
/// Validation utilities for storage entity cache evaluator parameters.
/// </summary>
public static class StorageEntityCacheEvaluatorValidation
{
    /// <summary>
    /// Gets the minimum timeout in milliseconds.
    /// </summary>
    /// <returns>The minimum timeout in milliseconds.</returns>
    public static long MinimumTimeoutMs() => 1;

    /// <summary>
    /// Gets the minimum threshold.
    /// </summary>
    /// <returns>The minimum threshold.</returns>
    public static long MinimumThreshold() => 1;

    /// <summary>
    /// Validates the specified parameters.
    /// </summary>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <param name="threshold">The threshold.</param>
    /// <exception cref="ArgumentException">Thrown when parameters are invalid.</exception>
    public static void ValidateParameters(long timeoutMs, long threshold)
    {
        if (timeoutMs < MinimumTimeoutMs())
        {
            throw new ArgumentException(
                $"Specified millisecond timeout of {timeoutMs} is lower than the minimum value {MinimumTimeoutMs()}.");
        }

        if (threshold < MinimumThreshold())
        {
            throw new ArgumentException(
                $"Specified threshold of {threshold} is lower than the minimum value {MinimumThreshold()}.");
        }
    }
}

/// <summary>
/// Factory methods for creating storage entity cache evaluators.
/// </summary>
public static class StorageEntityCacheEvaluatorFactory
{
    /// <summary>
    /// Creates a new storage entity cache evaluator using default values.
    /// </summary>
    /// <returns>A new storage entity cache evaluator instance.</returns>
    public static IStorageEntityCacheEvaluator New()
    {
        return New(
            StorageEntityCacheEvaluatorDefaults.DefaultTimeoutMs(),
            StorageEntityCacheEvaluatorDefaults.DefaultCacheThreshold());
    }

    /// <summary>
    /// Creates a new storage entity cache evaluator using the specified timeout and default threshold.
    /// </summary>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <returns>A new storage entity cache evaluator instance.</returns>
    public static IStorageEntityCacheEvaluator New(long timeoutMs)
    {
        return New(timeoutMs, StorageEntityCacheEvaluatorDefaults.DefaultCacheThreshold());
    }

    /// <summary>
    /// Creates a new storage entity cache evaluator using the specified values.
    /// 
    /// In the default implementation, two values are combined to calculate an entity's "cache weight":
    /// its "age" (the time in milliseconds of not being read) and its size in bytes. The resulting value is
    /// in turn compared to an abstract "free space" value, calculated by subtracting the current total cache size
    /// in bytes from the abstract threshold value defined here. If this comparison deems the tested entity
    /// to be "too heavy" for the cache, its data is cleared from the cache. It is also cleared from the cache if its
    /// "age" is greater than the timeout defined here.
    /// 
    /// This is a relatively simple and extremely fast algorithm to create the following behavior:
    /// 1. Cached data that seems to not be used currently ("too old") is cleared.
    /// 2. Apart from that, as long as there is "enough space", nothing is cleared.
    /// 3. The old and bigger an entity's data is, the more likely it is to be cleared.
    /// 4. The less free space there is in the cache, the sooner cached entity data is cleared.
    /// 
    /// This combination of rules is relatively accurate on keeping cached what is needed and dropping the rest,
    /// while being easily tailorable to suit an application's needs.
    /// </summary>
    /// <param name="timeoutMs">The time (in milliseconds, greater than 0) of not being read (the "age"), after which a particular entity's data will be cleared from the Storage's internal cache.</param>
    /// <param name="threshold">An abstract value (greater than 0) to evaluate the product of size and age of an entity in relation to the current cache size in order to determine if the entity's data shall be cleared from the cache.</param>
    /// <returns>A new storage entity cache evaluator instance.</returns>
    /// <exception cref="ArgumentException">Thrown when any of the passed values is equal to or lower than 0.</exception>
    public static IStorageEntityCacheEvaluator New(long timeoutMs, long threshold)
    {
        StorageEntityCacheEvaluatorValidation.ValidateParameters(timeoutMs, threshold);
        return new DefaultStorageEntityCacheEvaluator(timeoutMs, threshold);
    }
}

/// <summary>
/// Default implementation of storage entity cache evaluator.
/// </summary>
internal class DefaultStorageEntityCacheEvaluator : IStorageEntityCacheEvaluator
{
    // To satisfy CheckStyle. See algorithm comment below.
    // Shifting by 16 means roughly age in minutes and is fast.
    private const int C16 = 16;

    private readonly long _timeoutMs;
    private readonly long _threshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultStorageEntityCacheEvaluator"/> class.
    /// </summary>
    /// <param name="timeoutMs">The timeout in milliseconds.</param>
    /// <param name="threshold">The threshold.</param>
    public DefaultStorageEntityCacheEvaluator(long timeoutMs, long threshold)
    {
        _timeoutMs = timeoutMs;
        _threshold = threshold;
    }

    /// <summary>
    /// Gets the timeout in milliseconds.
    /// </summary>
    public long Timeout => _timeoutMs;

    /// <summary>
    /// Gets the threshold.
    /// </summary>
    public long Threshold => _threshold;

    /// <summary>
    /// Evaluates whether the entity's cache should be cleared.
    /// </summary>
    /// <param name="cacheSize">The total cache size in bytes.</param>
    /// <param name="evalTime">The current evaluation time in milliseconds.</param>
    /// <param name="entity">The entity to evaluate.</param>
    /// <returns>True if the entity's cache should be cleared, false otherwise.</returns>
    public bool ClearEntityCache(long cacheSize, long evalTime, IStorageEntity entity)
    {
        /* Simple default algorithm to take cache size, entity cached data length, age and reference into account:
         *
         * - subtract current cache size from threshold, resulting in an abstract value how "free" the cache is.
         *   This also means that if the current cache size alone reaches the threshold, the entity will definitely
         *   be cleared from the cache, no matter what (panic mode to avoid out of memory situations).
         *
         * - calculate "weight" of the entity and compare it to the memory's "freeness" to evaluate.
         *
         * "weight" calculation:
         * - the entity's memory consumption (cached data length)
         * - multiplied by its "cache age" in ms, divided by 2^16 (roughly equals age in minutes)
         *   the division is crucial to give newly loaded entities a kind of "grace time" in order to
         *   not constantly unload and load entities in filled systems (avoid live lock)
         * - multiply weight by 2 if entity has no references.
         *   Non-reference entities tend to be huge (text, binaries) and won't be needed by GC, so they have
         *   less priority to stay in cache or higher priority to be cleared before entities with references.
         *
         * In conclusion, this algorithm means:
         * The more the cache approaches the threshold, the more likely it gets that entities will be
         * cleared from the cache, especially large, old, non-reference entities.
         * And the older (not recently used) entities become, the more likely it gets that they will be cleared,
         * to a point where eventually every entity will be unloaded in a system without activity, resulting in
         * dormant systems automatically having an empty cache.
         */
        var ageInMs = evalTime - entity.LastTouched;

        /*
         * Note on ">>":
         * Cannot use ">>>" here, because some entities are touched "in the future", making age negative.
         * Unsigned shifting makes that a giant positive age, causing an unwanted unload.
         * For the formula to be correct, the signed shift has to be used.
         */
        return ageInMs >= _timeoutMs
            || _threshold - cacheSize < entity.CachedDataLength * (ageInMs >> C16) << (entity.HasReferences ? 0 : 1);
    }

    /// <summary>
    /// Returns a string representation of this cache evaluator.
    /// </summary>
    /// <returns>A string representation of this cache evaluator.</returns>
    public override string ToString()
    {
        return $"{GetType().Name}:\n  threshold = {_threshold}\n  timeout   = {_timeoutMs}";
    }
}

// IStorageEntity interface is already defined in IStorageEntityCache.cs
