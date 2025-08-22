using System;
using System.Runtime.InteropServices;
using System.Threading;
using NebulaStore.Storage.Embedded.Types.Exceptions;

namespace NebulaStore.Storage.Embedded.Types;

/// <summary>
/// Default implementation of IStorageEntity representing a stored object in the storage system.
/// </summary>
public class StorageEntity : IStorageEntity
{
    #region Private Fields

    private readonly long _objectId;
    private readonly bool _hasReferences;
    private readonly int _simpleReferenceDataCount;
    
    private long _typeId;
    private long _length;
    private long _storagePosition;
    private long _lastTouched;
    private long _cachedDataLength;
    private IntPtr _cachedDataAddress;
    private bool _isDeleted;
    private GcMark _gcMark;

    // Chain references
    private IStorageEntity? _fileNext;
    private IStorageEntity? _typeNext;
    private IStorageEntity? _hashNext;
    private IStorageEntityTypeInFile? _typeInFile;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the StorageEntity class.
    /// </summary>
    /// <param name="objectId">The object ID.</param>
    /// <param name="typeInFile">The type in file reference.</param>
    /// <param name="hashNext">The next entity in the hash chain.</param>
    /// <param name="hasReferences">Whether this entity has references to other entities.</param>
    /// <param name="simpleReferenceDataCount">The simple reference data count.</param>
    public StorageEntity(
        long objectId,
        IStorageEntityTypeInFile typeInFile,
        IStorageEntity? hashNext,
        bool hasReferences,
        int simpleReferenceDataCount)
    {
        _objectId = objectId;
        _typeInFile = typeInFile ?? throw new ArgumentNullException(nameof(typeInFile));
        _hashNext = hashNext;
        _hasReferences = hasReferences;
        _simpleReferenceDataCount = simpleReferenceDataCount;
        _gcMark = GcMark.White;
        _lastTouched = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _typeId = typeInFile.Type.TypeId;
    }

    #endregion

    #region IStorageEntity Implementation

    public long ObjectId => _objectId;

    public long TypeId => _typeId;

    public long Length => _length;

    public long StoragePosition => _storagePosition;

    public long LastTouched => _lastTouched;

    public long CachedDataLength => _cachedDataLength;

    public bool HasReferences => _hasReferences;

    public bool IsLive => _cachedDataAddress != IntPtr.Zero;

    public bool IsDeleted => _isDeleted;

    public bool IsProper => true; // This is a proper entity, not a head/tail dummy

    public IStorageEntityTypeInFile TypeInFile => _typeInFile ?? throw new InvalidOperationException("TypeInFile is null");

    public IStorageEntity? FileNext
    {
        get => _fileNext;
        set => _fileNext = value;
    }

    public IStorageEntity? TypeNext
    {
        get => _typeNext;
        set => _typeNext = value;
    }

    public IStorageEntity? HashNext
    {
        get => _hashNext;
        set => _hashNext = value;
    }

    public bool IsGcMarked => _gcMark != GcMark.White;

    public bool IsGcBlack => _gcMark == GcMark.Black;

    #endregion

    #region Public Methods

    public void Touch()
    {
        _lastTouched = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public void UpdateStorageInformation(long length, long storagePosition)
    {
        _length = length;
        _storagePosition = storagePosition;
    }

    public void PutCacheData(IntPtr dataAddress, long length)
    {
        if (length < 0)
            throw new ArgumentException("Length cannot be negative", nameof(length));

        // Clear existing cache data if any
        ClearCache();

        if (length > 0 && dataAddress != IntPtr.Zero)
        {
            // Allocate new memory and copy data
            _cachedDataAddress = Marshal.AllocHGlobal((int)length);
            unsafe
            {
                Buffer.MemoryCopy(dataAddress.ToPointer(), _cachedDataAddress.ToPointer(), length, length);
            }
            _cachedDataLength = length;
        }

        Touch();
    }

    public long ClearCache()
    {
        var clearedSize = _cachedDataLength;
        
        if (_cachedDataAddress != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_cachedDataAddress);
            _cachedDataAddress = IntPtr.Zero;
        }
        
        _cachedDataLength = 0;
        return clearedSize;
    }

    public void DetachFromFile()
    {
        _typeInFile = null;
        _fileNext = null;
    }

    public void SetDeleted()
    {
        _isDeleted = true;
        ClearCache();
    }

    public void CopyCachedData(IChunksBuffer dataCollector)
    {
        if (!IsLive)
            return;

        // Copy cached data to the data collector
        var buffer = new byte[_cachedDataLength];
        if (_cachedDataAddress != IntPtr.Zero)
        {
            Marshal.Copy(_cachedDataAddress, buffer, 0, (int)_cachedDataLength);
            // Add buffer to data collector (implementation would depend on IChunksBuffer)
            // For now, we'll assume the interface has an AddBuffer method
            // dataCollector.AddBuffer(buffer);
        }
    }

    public bool IterateReferenceIds(IStorageReferenceMarker referenceMarker)
    {
        if (!HasReferences)
            return false;

        // If entity is not live, we need to load it first
        bool requiresLoading = !IsLive;
        
        if (requiresLoading)
        {
            // This would typically trigger loading from storage
            // For now, we'll return true to indicate loading was required
            return true;
        }

        // Iterate through reference IDs in the cached data
        // This would parse the binary data and extract object IDs
        // For now, this is a placeholder implementation
        if (IsLive && _cachedDataAddress != IntPtr.Zero)
        {
            // Parse binary data and extract reference IDs
            // This would be implemented based on the serialization format
            // referenceMarker.Mark(referenceObjectId);
        }

        return requiresLoading;
    }

    public void MarkWhite()
    {
        _gcMark = GcMark.White;
    }

    public void MarkGray()
    {
        _gcMark = GcMark.Gray;
    }

    public void MarkBlack()
    {
        _gcMark = GcMark.Black;
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a new StorageEntity instance.
    /// </summary>
    /// <param name="objectId">The object ID.</param>
    /// <param name="typeInFile">The type in file reference.</param>
    /// <param name="hashNext">The next entity in the hash chain.</param>
    /// <param name="hasReferences">Whether this entity has references to other entities.</param>
    /// <param name="simpleReferenceDataCount">The simple reference data count.</param>
    /// <returns>A new StorageEntity instance.</returns>
    public static StorageEntity New(
        long objectId,
        IStorageEntityTypeInFile typeInFile,
        IStorageEntity? hashNext,
        bool hasReferences,
        int simpleReferenceDataCount)
    {
        return new StorageEntity(objectId, typeInFile, hashNext, hasReferences, simpleReferenceDataCount);
    }

    #endregion

    #region IDisposable Implementation

    private bool _disposed = false;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
            }

            // Free unmanaged resources
            ClearCache();
            _disposed = true;
        }
    }

    ~StorageEntity()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Private Enums

    /// <summary>
    /// Garbage collection mark states.
    /// </summary>
    private enum GcMark : byte
    {
        /// <summary>
        /// Entity is unmarked (eligible for collection).
        /// </summary>
        White = 0,

        /// <summary>
        /// Entity is marked but references not processed yet.
        /// </summary>
        Gray = 1,

        /// <summary>
        /// Entity is marked and references processed.
        /// </summary>
        Black = 2
    }

    #endregion
}

/// <summary>
/// Implementation of IStorageEntityTypeInFile.
/// </summary>
public class StorageEntityTypeInFile : IStorageEntityTypeInFile
{
    public IStorageEntityType Type { get; }
    public IStorageLiveDataFile File { get; }

    public StorageEntityTypeInFile(IStorageEntityType type, IStorageLiveDataFile file)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        File = file ?? throw new ArgumentNullException(nameof(file));
    }
}
