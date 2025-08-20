using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.GigaMap;

/// <summary>
/// Default implementation of the GigaMap builder.
/// </summary>
/// <typeparam name="T">The type of elements to be stored in the GigaMap</typeparam>
public class GigaMapBuilder<T> : IGigaMapBuilder<T> where T : class
{
    private readonly List<IIndexer<T, object>> _bitmapIndices = new();
    private readonly List<IIndexer<T, object>> _identityIndices = new();
    private readonly List<IIndexer<T, object>> _uniqueIndices = new();
    private readonly List<ICustomConstraint<T>> _customConstraints = new();

    private IEqualityComparer<T>? _equalityComparer;
    private bool _useValueEquality;
    private bool _useIdentityEquality;

    // Segment size configuration
    private int _lowLevelLengthExponent = 8;  // Default: 2^8 = 256
    private int _midLevelLengthExponent = 10; // Default: 2^10 = 1024
    private int _highLevelMinimumLengthExponent = 0; // Default: 2^0 = 1
    private int _highLevelMaximumLengthExponent = 30; // Default: 2^30 = ~1 billion

    public IGigaMapBuilder<T> WithBitmapIndex<TKey>(IIndexer<T, TKey> indexer)
    {
        if (indexer == null)
            throw new ArgumentNullException(nameof(indexer));

        _bitmapIndices.Add(Indexer.AsObjectIndexer(indexer));
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapIndices(IEnumerable<IIndexer<T, object>> indexers)
    {
        if (indexers == null)
            throw new ArgumentNullException(nameof(indexers));

        _bitmapIndices.AddRange(indexers);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapIndices(params IIndexer<T, object>[] indexers)
    {
        if (indexers == null)
            throw new ArgumentNullException(nameof(indexers));

        _bitmapIndices.AddRange(indexers);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapIdentityIndex<TKey>(IIndexer<T, TKey> indexer)
    {
        if (indexer == null)
            throw new ArgumentNullException(nameof(indexer));

        _identityIndices.Add(indexer as IIndexer<T, object> ??
                            throw new ArgumentException("Indexer must be compatible with object key type"));
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapIdentityIndices(IEnumerable<IIndexer<T, object>> indexers)
    {
        if (indexers == null)
            throw new ArgumentNullException(nameof(indexers));

        _identityIndices.AddRange(indexers);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapIdentityIndices(params IIndexer<T, object>[] indexers)
    {
        if (indexers == null)
            throw new ArgumentNullException(nameof(indexers));

        _identityIndices.AddRange(indexers);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapUniqueIndex<TKey>(IIndexer<T, TKey> indexer)
    {
        if (indexer == null)
            throw new ArgumentNullException(nameof(indexer));

        var objectIndexer = Indexer.AsObjectIndexer(indexer);
        _bitmapIndices.Add(objectIndexer);  // Add as bitmap index
        _uniqueIndices.Add(objectIndexer);  // Add as unique constraint
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapUniqueIndices(IEnumerable<IIndexer<T, object>> indexers)
    {
        if (indexers == null)
            throw new ArgumentNullException(nameof(indexers));

        _uniqueIndices.AddRange(indexers);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapUniqueIndices(params IIndexer<T, object>[] indexers)
    {
        if (indexers == null)
            throw new ArgumentNullException(nameof(indexers));

        _uniqueIndices.AddRange(indexers);
        return this;
    }

    public IGigaMapBuilder<T> WithCustomConstraint(ICustomConstraint<T> customConstraint)
    {
        if (customConstraint == null)
            throw new ArgumentNullException(nameof(customConstraint));

        _customConstraints.Add(customConstraint);
        return this;
    }

    public IGigaMapBuilder<T> WithCustomConstraints(IEnumerable<ICustomConstraint<T>> customConstraints)
    {
        if (customConstraints == null)
            throw new ArgumentNullException(nameof(customConstraints));

        _customConstraints.AddRange(customConstraints);
        return this;
    }

    public IGigaMapBuilder<T> WithCustomConstraints(params ICustomConstraint<T>[] customConstraints)
    {
        if (customConstraints == null)
            throw new ArgumentNullException(nameof(customConstraints));

        _customConstraints.AddRange(customConstraints);
        return this;
    }

    public IGigaMapBuilder<T> WithValueEquality()
    {
        _useValueEquality = true;
        _useIdentityEquality = false;
        _equalityComparer = null;
        return this;
    }

    public IGigaMapBuilder<T> WithIdentityEquality()
    {
        _useIdentityEquality = true;
        _useValueEquality = false;
        _equalityComparer = null;
        return this;
    }

    public IGigaMapBuilder<T> WithEqualityComparer(IEqualityComparer<T> equalityComparer)
    {
        _equalityComparer = equalityComparer ?? throw new ArgumentNullException(nameof(equalityComparer));
        _useValueEquality = false;
        _useIdentityEquality = false;
        return this;
    }

    public IGigaMapBuilder<T> WithSegmentSize(int lowLevelLengthExponent)
    {
        ValidateSegmentExponent(lowLevelLengthExponent, 0, 20, nameof(lowLevelLengthExponent));
        _lowLevelLengthExponent = lowLevelLengthExponent;
        return this;
    }

    public IGigaMapBuilder<T> WithSegmentSize(int lowLevelLengthExponent, int midLevelLengthExponent)
    {
        ValidateSegmentExponent(lowLevelLengthExponent, 0, 20, nameof(lowLevelLengthExponent));
        ValidateSegmentExponent(midLevelLengthExponent, 8, 20, nameof(midLevelLengthExponent));

        _lowLevelLengthExponent = lowLevelLengthExponent;
        _midLevelLengthExponent = midLevelLengthExponent;
        return this;
    }

    public IGigaMapBuilder<T> WithSegmentSize(
        int lowLevelLengthExponent,
        int midLevelLengthExponent,
        int highLevelMaximumLengthExponent)
    {
        ValidateSegmentExponent(lowLevelLengthExponent, 0, 20, nameof(lowLevelLengthExponent));
        ValidateSegmentExponent(midLevelLengthExponent, 8, 20, nameof(midLevelLengthExponent));
        ValidateSegmentExponent(highLevelMaximumLengthExponent, 8, 30, nameof(highLevelMaximumLengthExponent));
        ValidateSegmentSizeDistribution(lowLevelLengthExponent, midLevelLengthExponent, highLevelMaximumLengthExponent);

        _lowLevelLengthExponent = lowLevelLengthExponent;
        _midLevelLengthExponent = midLevelLengthExponent;
        _highLevelMaximumLengthExponent = highLevelMaximumLengthExponent;
        return this;
    }

    public IGigaMapBuilder<T> WithSegmentSize(
        int lowLevelLengthExponent,
        int midLevelLengthExponent,
        int highLevelMinimumLengthExponent,
        int highLevelMaximumLengthExponent)
    {
        ValidateSegmentExponent(lowLevelLengthExponent, 0, 20, nameof(lowLevelLengthExponent));
        ValidateSegmentExponent(midLevelLengthExponent, 8, 20, nameof(midLevelLengthExponent));
        ValidateSegmentExponent(highLevelMinimumLengthExponent, 0, 30, nameof(highLevelMinimumLengthExponent));
        ValidateSegmentExponent(highLevelMaximumLengthExponent, 8, 30, nameof(highLevelMaximumLengthExponent));
        ValidateSegmentSizeDistribution(lowLevelLengthExponent, midLevelLengthExponent, highLevelMaximumLengthExponent);

        if (highLevelMinimumLengthExponent > highLevelMaximumLengthExponent)
        {
            throw new ArgumentException(
                "High level minimum length exponent cannot be greater than maximum length exponent");
        }

        _lowLevelLengthExponent = lowLevelLengthExponent;
        _midLevelLengthExponent = midLevelLengthExponent;
        _highLevelMinimumLengthExponent = highLevelMinimumLengthExponent;
        _highLevelMaximumLengthExponent = highLevelMaximumLengthExponent;
        return this;
    }

    public IGigaMap<T> Build()
    {
        // Determine the equality comparer to use
        IEqualityComparer<T> equalityComparer;
        if (_equalityComparer != null)
        {
            equalityComparer = _equalityComparer;
        }
        else if (_useValueEquality)
        {
            equalityComparer = EqualityComparer<T>.Default;
        }
        else if (_useIdentityEquality)
        {
            equalityComparer = ReferenceEqualityComparer.Instance as IEqualityComparer<T>
                              ?? throw new InvalidOperationException("Reference equality comparer not available");
        }
        else
        {
            // Default to reference equality for reference types
            equalityComparer = ReferenceEqualityComparer.Instance as IEqualityComparer<T>
                              ?? EqualityComparer<T>.Default;
        }

        // Create the GigaMap instance
        var gigaMap = new DefaultGigaMap<T>(
            equalityComparer,
            _lowLevelLengthExponent,
            _midLevelLengthExponent,
            _highLevelMinimumLengthExponent,
            _highLevelMaximumLengthExponent);

        // Configure bitmap indices
        var bitmapIndices = gigaMap.Index.Bitmap;

        if (_bitmapIndices.Any())
        {
            bitmapIndices.EnsureAll(_bitmapIndices);
        }

        if (_identityIndices.Any())
        {
            bitmapIndices.EnsureAll(_identityIndices);
            bitmapIndices.SetIdentityIndices(_identityIndices);
        }

        if (_uniqueIndices.Any())
        {
            bitmapIndices.EnsureAll(_uniqueIndices);
            gigaMap.Constraints.Unique.AddConstraints(_uniqueIndices);
        }

        // Configure custom constraints
        if (_customConstraints.Any())
        {
            gigaMap.Constraints.Custom.AddConstraints(_customConstraints);
        }

        return gigaMap;
    }

    private static void ValidateSegmentExponent(int exponent, int min, int max, string parameterName)
    {
        if (exponent < min || exponent > max)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                exponent,
                $"Segment exponent must be between {min} and {max}");
        }
    }

    private static void ValidateSegmentSizeDistribution(int level1, int level2, int level3Maximum)
    {
        const int maxSum = 50; // Maximum total entity capacity exponent

        if (level1 + level2 + level3Maximum > maxSum)
        {
            throw new ArgumentException(
                $"Specified total entity capacity of 2^{level1 + level2 + level3Maximum} " +
                $"exceeds the technical limit of 2^{maxSum}");
        }
    }
}