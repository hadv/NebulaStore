using System;
using System.Collections.Generic;

namespace NebulaStore.GigaMap;

/// <summary>
/// Interface representing a builder for constructing instances of GigaMap with various types
/// of bitmap indices and constraints.
/// </summary>
/// <typeparam name="T">The type of elements to be stored in the GigaMap</typeparam>
public interface IGigaMapBuilder<T> where T : class
{
    /// <summary>
    /// Configures the builder with a bitmap index using the provided indexer.
    /// </summary>
    /// <param name="indexer">The indexer to be used to create the bitmap index</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithBitmapIndex<TKey>(IIndexer<T, TKey> indexer);

    /// <summary>
    /// Configures the builder with multiple bitmap indices using the provided indexers.
    /// </summary>
    /// <param name="indexers">A collection of indexers to be used for creating bitmap indices</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithBitmapIndices(IEnumerable<IIndexer<T, object>> indexers);

    /// <summary>
    /// Configures the builder with multiple bitmap indices using the provided array of indexers.
    /// </summary>
    /// <param name="indexers">The indexers to be used for creating bitmap indices</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithBitmapIndices(params IIndexer<T, object>[] indexers);

    /// <summary>
    /// Configures the builder with a bitmap identity index using the provided indexer.
    /// </summary>
    /// <param name="indexer">The indexer to be used to create the bitmap identity index</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithBitmapIdentityIndex<TKey>(IIndexer<T, TKey> indexer);

    /// <summary>
    /// Configures the builder with multiple bitmap identity indices using the provided collection of indexers.
    /// </summary>
    /// <param name="indexers">The collection of indexers to be used for creating bitmap identity indices</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithBitmapIdentityIndices(IEnumerable<IIndexer<T, object>> indexers);

    /// <summary>
    /// Configures the builder with multiple bitmap identity indices using the provided array of indexers.
    /// </summary>
    /// <param name="indexers">An array of indexers to be used for creating bitmap identity indices</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithBitmapIdentityIndices(params IIndexer<T, object>[] indexers);

    /// <summary>
    /// Configures the builder with a bitmap unique index using the provided indexer.
    /// </summary>
    /// <param name="indexer">The indexer to be used for creating the bitmap unique index</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithBitmapUniqueIndex<TKey>(IIndexer<T, TKey> indexer);

    /// <summary>
    /// Configures the builder with multiple bitmap unique indices using the provided collection of indexers.
    /// </summary>
    /// <param name="indexers">A collection of indexers to be used for creating bitmap unique indices</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithBitmapUniqueIndices(IEnumerable<IIndexer<T, object>> indexers);

    /// <summary>
    /// Configures the builder with multiple bitmap unique indices using the provided array of indexers.
    /// </summary>
    /// <param name="indexers">An array of indexers to be used for creating bitmap unique indices</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithBitmapUniqueIndices(params IIndexer<T, object>[] indexers);

    /// <summary>
    /// Configures the builder with custom constraint using the provided customConstraint.
    /// </summary>
    /// <param name="customConstraint">The custom constraint to configure the builder with</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithCustomConstraint(ICustomConstraint<T> customConstraint);

    /// <summary>
    /// Configures the builder with multiple custom constraints.
    /// </summary>
    /// <param name="customConstraints">The custom constraints to configure the builder with</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithCustomConstraints(IEnumerable<ICustomConstraint<T>> customConstraints);

    /// <summary>
    /// Configures the builder with multiple custom constraints.
    /// </summary>
    /// <param name="customConstraints">The custom constraints to configure the builder with</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithCustomConstraints(params ICustomConstraint<T>[] customConstraints);

    /// <summary>
    /// Configures the builder to use value-based equality for comparing elements.
    /// </summary>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithValueEquality();

    /// <summary>
    /// Configures the builder to use identity-based equality for comparing elements.
    /// </summary>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithIdentityEquality();

    /// <summary>
    /// Configures the builder to use a custom equality comparer for comparing elements.
    /// </summary>
    /// <param name="equalityComparer">The equality comparer to use</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithEqualityComparer(IEqualityComparer<T> equalityComparer);

    /// <summary>
    /// Configures the segment size distribution for the GigaMap.
    /// </summary>
    /// <param name="lowLevelLengthExponent">Exponent for the lower segment (0-20)</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithSegmentSize(int lowLevelLengthExponent);

    /// <summary>
    /// Configures the segment size distribution for the GigaMap.
    /// </summary>
    /// <param name="lowLevelLengthExponent">Exponent for the lower segment (0-20)</param>
    /// <param name="midLevelLengthExponent">Exponent for the middle segment (8-20)</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithSegmentSize(int lowLevelLengthExponent, int midLevelLengthExponent);

    /// <summary>
    /// Configures the segment size distribution for the GigaMap.
    /// The sum of all exponents cannot be greater than 50.
    /// </summary>
    /// <param name="lowLevelLengthExponent">Exponent for the lower segment (0-20)</param>
    /// <param name="midLevelLengthExponent">Exponent for the middle segment (8-20)</param>
    /// <param name="highLevelMaximumLengthExponent">Maximum exponent for the higher segment (8-30)</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithSegmentSize(
        int lowLevelLengthExponent,
        int midLevelLengthExponent,
        int highLevelMaximumLengthExponent);

    /// <summary>
    /// Configures the segment size distribution for the GigaMap.
    /// The sum of lowLevelLengthExponent, midLevelLengthExponent and highLevelMaximumLengthExponent cannot be greater than 50.
    /// </summary>
    /// <param name="lowLevelLengthExponent">Exponent for the lower segment (0-20)</param>
    /// <param name="midLevelLengthExponent">Exponent for the middle segment (8-20)</param>
    /// <param name="highLevelMinimumLengthExponent">Minimum exponent for the higher segment (0-30)</param>
    /// <param name="highLevelMaximumLengthExponent">Maximum exponent for the higher segment (8-30)</param>
    /// <returns>The builder instance for method chaining</returns>
    IGigaMapBuilder<T> WithSegmentSize(
        int lowLevelLengthExponent,
        int midLevelLengthExponent,
        int highLevelMinimumLengthExponent,
        int highLevelMaximumLengthExponent);

    /// <summary>
    /// Builds and returns a GigaMap instance configured with the specified parameters
    /// and indices defined in the Builder.
    /// </summary>
    /// <returns>A new instance of GigaMap containing the configuration and indices provided to the Builder</returns>
    IGigaMap<T> Build();
}

/// <summary>
/// Provides factory methods for creating GigaMap builders and instances.
/// </summary>
public static class GigaMap
{
    /// <summary>
    /// Creates a new GigaMap builder.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>A new GigaMap builder</returns>
    public static IGigaMapBuilder<T> Builder<T>() where T : class
    {
        return new GigaMapBuilder<T>();
    }

    /// <summary>
    /// Creates a new empty GigaMap with default settings.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>A newly created GigaMap</returns>
    public static IGigaMap<T> New<T>() where T : class
    {
        return Builder<T>().Build();
    }

    /// <summary>
    /// Creates a new empty GigaMap with the specified equality comparer.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="equalityComparer">The equality comparer to use</param>
    /// <returns>A newly created GigaMap</returns>
    public static IGigaMap<T> New<T>(IEqualityComparer<T> equalityComparer) where T : class
    {
        return Builder<T>().WithEqualityComparer(equalityComparer).Build();
    }

    /// <summary>
    /// Creates a new empty GigaMap with value-based equality.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>A newly created GigaMap</returns>
    public static IGigaMap<T> NewWithValueEquality<T>() where T : class
    {
        return Builder<T>().WithValueEquality().Build();
    }

    /// <summary>
    /// Creates a new empty GigaMap with identity-based equality.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>A newly created GigaMap</returns>
    public static IGigaMap<T> NewWithIdentityEquality<T>() where T : class
    {
        return Builder<T>().WithIdentityEquality().Build();
    }
}