using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NebulaStore.Storage.Embedded.Types.Exceptions;

namespace NebulaStore.Storage.Embedded.Types.Files;

/// <summary>
/// Default implementation of IStorageFileManager that manages storage files for a channel.
/// </summary>
public class StorageFileManager : IStorageFileManager
{
    #region Private Fields

    private readonly int _channelIndex;
    private readonly string _storageDirectory;
    private readonly IStorageDataFileEvaluator _fileEvaluator;
    private readonly IStorageFileWriter _fileWriter;
    private readonly object _lock = new object();
    private readonly ConcurrentDictionary<long, IStorageLiveDataFile> _dataFiles = new();

    private IStorageLiveDataFile? _currentDataFile;
    private long _nextFileNumber = 1;
    private bool _disposed = false;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the StorageFileManager class.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="storageDirectory">The storage directory.</param>
    /// <param name="fileEvaluator">The file evaluator.</param>
    /// <param name="fileWriter">The file writer.</param>
    public StorageFileManager(
        int channelIndex,
        string storageDirectory,
        IStorageDataFileEvaluator fileEvaluator,
        IStorageFileWriter fileWriter)
    {
        _channelIndex = channelIndex;
        _storageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));
        _fileEvaluator = fileEvaluator ?? throw new ArgumentNullException(nameof(fileEvaluator));
        _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));

        EnsureStorageDirectoryExists();
        LoadExistingFiles();
    }

    #endregion

    #region IStorageFileManager Implementation

    public int ChannelIndex => _channelIndex;

    public IStorageLiveDataFile CurrentStorageFile
    {
        get
        {
            lock (_lock)
            {
                if (_currentDataFile == null)
                {
                    _currentDataFile = CreateNewDataFile();
                }
                return _currentDataFile;
            }
        }
    }

    public long[] StoreChunks(long timestamp, byte[][] dataBuffers)
    {
        if (dataBuffers == null)
            throw new ArgumentNullException(nameof(dataBuffers));

        var positions = new long[dataBuffers.Length];
        
        lock (_lock)
        {
            var currentFile = CurrentStorageFile;
            
            for (int i = 0; i < dataBuffers.Length; i++)
            {
                var buffer = dataBuffers[i];
                if (buffer != null && buffer.Length > 0)
                {
                    // Check if we need a new file
                    if (ShouldCreateNewFile(currentFile, buffer.Length))
                    {
                        currentFile = CreateNewDataFile();
                        _currentDataFile = currentFile;
                    }

                    // Write the chunk and get its position
                    positions[i] = _fileWriter.WriteStore(currentFile, new[] { buffer });
                }
                else
                {
                    positions[i] = -1; // Invalid position for empty/null buffers
                }
            }
        }

        return positions;
    }

    public void RollbackWrite()
    {
        // In a real implementation, this would rollback any pending write operations
        // For now, this is a placeholder
    }

    public void CommitWrite()
    {
        // In a real implementation, this would commit any pending write operations
        // For now, this is a placeholder
        lock (_lock)
        {
            _currentDataFile?.Close();
        }
    }

    public IStorageInventory ReadStorage()
    {
        // Read storage inventory from existing files
        // This would typically scan all files and build an inventory
        // For now, return a basic inventory
        return new BasicStorageInventory(_channelIndex, new StorageTypeDictionary());
    }

    public IStorageIdAnalysis InitializeStorage(
        long taskTimestamp,
        long consistentStoreTimestamp,
        IStorageInventory storageInventory,
        IStorageChannel parentChannel)
    {
        if (storageInventory == null)
            throw new ArgumentNullException(nameof(storageInventory));
        if (parentChannel == null)
            throw new ArgumentNullException(nameof(parentChannel));

        // Initialize storage with the provided inventory
        // This would typically set up the initial state based on existing data
        // For now, return a basic analysis
        return new StorageIdAnalysis(0, 0, 0);
    }

    public void IterateStorageFiles(Action<IStorageLiveDataFile> procedure)
    {
        if (procedure == null)
            throw new ArgumentNullException(nameof(procedure));

        var files = _dataFiles.Values.ToList();
        foreach (var file in files)
        {
            procedure(file);
        }
    }

    public bool IncrementalFileCleanupCheck(TimeSpan timeBudget)
    {
        var endTime = DateTimeOffset.UtcNow.Add(timeBudget);
        
        // Perform incremental file cleanup
        var filesToCheck = _dataFiles.Values.ToList();
        
        foreach (var file in filesToCheck)
        {
            if (DateTimeOffset.UtcNow > endTime)
            {
                return false; // Time budget exceeded
            }

            if (file.NeedsRetirement(_fileEvaluator))
            {
                // Mark file for cleanup or perform cleanup operations
                // This would typically involve moving data to other files
                // and then deleting the file
            }
        }

        return true; // Completed within time budget
    }

    public bool IssuedFileCleanupCheck(TimeSpan timeBudget)
    {
        return IncrementalFileCleanupCheck(timeBudget);
    }

    public void ExportData(IStorageLiveFileProvider fileProvider)
    {
        if (fileProvider == null)
            throw new ArgumentNullException(nameof(fileProvider));

        // Export data using the file provider
        foreach (var file in _dataFiles.Values)
        {
            using var exportStream = fileProvider.ProvideFile(file.Identifier);
            file.CopyTo(exportStream);
        }
    }

    public IStorageRawFileStatistics CreateRawFileStatistics()
    {
        var fileCount = _dataFiles.Count;
        var totalSize = _dataFiles.Values.Sum(f => f.Size);
        var liveDataSize = _dataFiles.Values.Sum(f => f.DataLength);

        return new BasicStorageRawFileStatistics(fileCount, totalSize, liveDataSize);
    }

    public void RestartFileCleanupCursor()
    {
        // Restart the file cleanup cursor
        // This would typically reset any internal state used for incremental cleanup
    }

    public void LoadData(IStorageLiveDataFile dataFile, IStorageEntity entity, long length, long cacheChange)
    {
        if (dataFile == null)
            throw new ArgumentNullException(nameof(dataFile));
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // Load entity data from the file
        var buffer = new byte[length];
        var bytesRead = dataFile.ReadBytes(buffer, entity.StoragePosition);
        
        if (bytesRead > 0)
        {
            // Put the data in the entity's cache
            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    entity.PutCacheData(new IntPtr(ptr), bytesRead);
                }
            }
        }
    }

    public void PrepareImport()
    {
        // Prepare for data import operations
        // This would typically set up temporary structures for import
    }

    public void CopyData(IStorageImportSource importSource)
    {
        if (importSource == null)
            throw new ArgumentNullException(nameof(importSource));

        // Copy data from the import source
        foreach (var file in importSource.GetAvailableFiles())
        {
            // Create corresponding data file and copy content
            var dataFile = CreateNewDataFile();
            using var sourceStream = file.OpenRead();
            using var targetStream = new FileStream(((StorageLiveDataFile)dataFile).FilePath, FileMode.Create);
            sourceStream.CopyTo(targetStream);
        }
    }

    public void CommitImport(long taskTimestamp)
    {
        // Commit the import operation
        // This would typically finalize the imported data
    }

    public void RollbackImport()
    {
        // Rollback the import operation
        // This would typically clean up any partially imported data
    }

    public bool IssuedTransactionFileCheck(bool checkSize)
    {
        // Check transaction files
        // This would typically verify the integrity of transaction log files
        return true; // Placeholder
    }

    public void Reset()
    {
        lock (_lock)
        {
            foreach (var file in _dataFiles.Values)
            {
                file.Dispose();
            }
            
            _dataFiles.Clear();
            _currentDataFile = null;
            _nextFileNumber = 1;
        }
    }

    #endregion

    #region Private Methods

    private void EnsureStorageDirectoryExists()
    {
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    private void LoadExistingFiles()
    {
        var pattern = $"channel_{_channelIndex:D3}_file_*.dat";
        var existingFiles = Directory.GetFiles(_storageDirectory, pattern);
        
        foreach (var filePath in existingFiles)
        {
            var fileName = Path.GetFileName(filePath);
            if (TryParseFileNumber(fileName, out var fileNumber))
            {
                var dataFile = new StorageLiveDataFile(fileNumber, _channelIndex, filePath);
                _dataFiles.TryAdd(fileNumber, dataFile);
                
                if (fileNumber >= _nextFileNumber)
                {
                    _nextFileNumber = fileNumber + 1;
                }
            }
        }
    }

    private bool TryParseFileNumber(string fileName, out long fileNumber)
    {
        fileNumber = 0;
        
        // Parse file name like "channel_001_file_000001.dat"
        var parts = fileName.Split('_');
        if (parts.Length >= 4 && parts[0] == "channel" && parts[2] == "file")
        {
            var numberPart = parts[3].Split('.')[0];
            return long.TryParse(numberPart, out fileNumber);
        }
        
        return false;
    }

    private IStorageLiveDataFile CreateNewDataFile()
    {
        var fileNumber = Interlocked.Increment(ref _nextFileNumber) - 1;
        var dataFile = StorageLiveDataFile.Create(fileNumber, _channelIndex, _storageDirectory);
        
        dataFile.EnsureExists();
        _dataFiles.TryAdd(fileNumber, dataFile);
        
        return dataFile;
    }

    private bool ShouldCreateNewFile(IStorageLiveDataFile currentFile, long additionalDataSize)
    {
        var currentSize = currentFile.Size;
        var maxSize = _fileEvaluator.FileMaximumSize;
        
        return currentSize + additionalDataSize > maxSize;
    }

    #endregion

    #region IDisposable Implementation

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Reset();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a new StorageFileManager instance.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="storageDirectory">The storage directory.</param>
    /// <param name="fileEvaluator">The file evaluator.</param>
    /// <param name="fileWriter">The file writer.</param>
    /// <returns>A new StorageFileManager instance.</returns>
    public static StorageFileManager Create(
        int channelIndex,
        string storageDirectory,
        IStorageDataFileEvaluator fileEvaluator,
        IStorageFileWriter fileWriter)
    {
        return new StorageFileManager(channelIndex, storageDirectory, fileEvaluator, fileWriter);
    }

    #endregion
}

/// <summary>
/// Basic implementation of IStorageInventory.
/// </summary>
internal class BasicStorageInventory : IStorageInventory
{
    public int ChannelCount { get; }
    public IStorageTypeDictionary TypeDictionary { get; }
    public long CreationTimestamp { get; }

    public BasicStorageInventory(int channelCount, IStorageTypeDictionary typeDictionary)
    {
        ChannelCount = channelCount;
        TypeDictionary = typeDictionary ?? throw new ArgumentNullException(nameof(typeDictionary));
        CreationTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}

/// <summary>
/// Basic implementation of IStorageRawFileStatistics.
/// </summary>
internal class BasicStorageRawFileStatistics : IStorageRawFileStatistics
{
    public int FileCount { get; }
    public long TotalFileSize { get; }
    public long LiveDataSize { get; }

    public BasicStorageRawFileStatistics(int fileCount, long totalFileSize, long liveDataSize)
    {
        FileCount = fileCount;
        TotalFileSize = totalFileSize;
        LiveDataSize = liveDataSize;
    }
}
