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
    private readonly List<PendingWriteOperation> _pendingWrites = new();
    private readonly string _metadataFilePath;

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
        _metadataFilePath = Path.Combine(_storageDirectory, $"channel_{_channelIndex:D3}_metadata.json");

        EnsureStorageDirectoryExists();
        LoadExistingFiles();
        LoadMetadata();
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

                    // Record original length for rollback purposes
                    var originalLength = currentFile.Size;

                    // Write the chunk and get its position
                    positions[i] = _fileWriter.WriteStore(currentFile, new[] { buffer });

                    // Track this write operation for potential rollback
                    var pendingWrite = new PendingWriteOperation(currentFile, originalLength, positions[i], buffer);
                    _pendingWrites.Add(pendingWrite);
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
        lock (_lock)
        {
            try
            {
                // Rollback any pending write operations
                if (_pendingWrites.Count > 0)
                {
                    // Restore file positions to their pre-write state
                    foreach (var pendingWrite in _pendingWrites)
                    {
                        var file = pendingWrite.File;
                        var originalLength = pendingWrite.OriginalLength;

                        // Truncate file back to original length
                        if (file is StorageLiveDataFile dataFile)
                        {
                            dataFile.TruncateToLength(originalLength);
                        }
                    }

                    _pendingWrites.Clear();
                }

                // Reset current file state
                if (_currentDataFile != null)
                {
                    _currentDataFile.ResetToLastCommittedState();
                }
            }
            catch (Exception ex)
            {
                throw new StorageFileException($"Failed to rollback write operations for channel {_channelIndex}", ex);
            }
        }
    }

    public void CommitWrite()
    {
        lock (_lock)
        {
            try
            {
                // Commit all pending write operations
                if (_pendingWrites.Count > 0)
                {
                    foreach (var pendingWrite in _pendingWrites)
                    {
                        var file = pendingWrite.File;

                        // Flush and sync to disk
                        if (file is StorageLiveDataFile dataFile)
                        {
                            dataFile.FlushAndSync();
                            dataFile.CommitState();
                        }
                    }

                    _pendingWrites.Clear();
                }

                // Commit current file state
                if (_currentDataFile != null)
                {
                    _currentDataFile.FlushAndSync();
                    _currentDataFile.CommitState();
                }

                // Update metadata file with current state
                UpdateMetadataFile();
            }
            catch (Exception ex)
            {
                throw new StorageFileException($"Failed to commit write operations for channel {_channelIndex}", ex);
            }
        }
    }

    public IStorageInventory ReadStorage()
    {
        lock (_lock)
        {
            try
            {
                var typeDictionary = new StorageTypeDictionary();
                var inventory = new BasicStorageInventory(_channelIndex, typeDictionary);

                // Scan all data files and build inventory
                foreach (var file in _dataFiles.Values)
                {
                    if (file.HasContent)
                    {
                        // In a real implementation, this would parse the file contents
                        // and extract type information and object references
                        ScanFileForInventory(file, inventory);
                    }
                }

                return inventory;
            }
            catch (Exception ex)
            {
                throw new StorageFileException($"Failed to read storage inventory for channel {_channelIndex}", ex);
            }
        }
    }

    private void ScanFileForInventory(IStorageLiveDataFile file, BasicStorageInventory inventory)
    {
        try
        {
            // This is a simplified implementation
            // In a real system, this would parse the binary data format
            // and extract object metadata, type information, etc.

            var buffer = new byte[1024]; // Read in chunks
            var position = 0L;

            while (position < file.DataLength)
            {
                var bytesRead = file.ReadBytes(buffer, position);
                if (bytesRead == 0) break;

                // Parse chunk headers and extract metadata
                // This would involve understanding the binary format
                // For now, just advance position
                position += bytesRead;
            }
        }
        catch (Exception ex)
        {
            // Log warning but continue with other files
            Console.WriteLine($"Warning: Failed to scan file {file.Identifier}: {ex.Message}");
        }
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

        lock (_lock)
        {
            try
            {
                // Perform incremental file cleanup and validation
                var filesToCheck = _dataFiles.Values.ToList();

                foreach (var file in filesToCheck)
                {
                    if (DateTimeOffset.UtcNow > endTime)
                    {
                        return false; // Time budget exceeded
                    }

                    // Validate file integrity first
                    ValidateFileIntegrity(file);

                    // Check if file needs retirement
                    if (file.NeedsRetirement(_fileEvaluator))
                    {
                        // Mark file for cleanup or perform cleanup operations
                        // This would typically involve moving data to other files
                        // and then deleting the file
                        MarkFileForRetirement(file);
                    }

                    // Check file size optimization opportunities
                    CheckFileSizeOptimization(file);
                }

                // Validate metadata consistency if time permits
                if (DateTimeOffset.UtcNow < endTime)
                {
                    ValidateMetadataConsistency();
                }

                return true; // Completed within time budget
            }
            catch (Exception ex)
            {
                throw new StorageFileException($"File cleanup check failed for channel {_channelIndex}", ex);
            }
        }
    }

    private void ValidateFileIntegrity(IStorageLiveDataFile file)
    {
        try
        {
            // Check if file exists and is accessible
            if (!file.HasContent)
                return;

            // Validate file size consistency
            if (file.Size < 0 || file.DataLength < 0 || file.DataLength > file.Size)
            {
                throw new StorageFileException($"File {file.Identifier} has inconsistent size information");
            }

            // Basic read test to ensure file is not corrupted
            var buffer = new byte[Math.Min(1024, file.DataLength)];
            if (file.DataLength > 0)
            {
                var bytesRead = file.ReadBytes(buffer, 0);
                if (bytesRead < 0)
                {
                    throw new StorageFileException($"File {file.Identifier} failed basic read test");
                }
            }
        }
        catch (Exception ex) when (!(ex is StorageFileException))
        {
            throw new StorageFileException($"File integrity validation failed for {file.Identifier}", ex);
        }
    }

    private void CheckFileSizeOptimization(IStorageLiveDataFile file)
    {
        // Check if file is too small or too large for optimization
        // This matches Eclipse Store's file size optimization logic

        if (!file.HasContent)
            return;

        // Files that are too small might benefit from consolidation
        if (file.DataLength < 1024 && file.DataLength > 0)
        {
            // Mark for potential consolidation (would be implemented in housekeeping)
        }

        // Files that are too large might benefit from splitting
        if (file.Size > 1024 * 1024 * 1024) // 1GB threshold
        {
            // Mark for potential splitting (would be implemented in housekeeping)
        }
    }

    private void MarkFileForRetirement(IStorageLiveDataFile file)
    {
        // Mark file for retirement - in a real implementation this would
        // set flags or add to a retirement queue for later processing
        Console.WriteLine($"File {file.Identifier} marked for retirement");
    }

    private void ValidateMetadataConsistency()
    {
        try
        {
            // Validate that metadata file is consistent with actual files
            if (File.Exists(_metadataFilePath))
            {
                var json = File.ReadAllText(_metadataFilePath);
                var metadata = System.Text.Json.JsonSerializer.Deserialize<StorageFileMetadata>(json);

                if (metadata != null)
                {
                    // Check if file count matches
                    if (metadata.FileCount != _dataFiles.Count)
                    {
                        // Update metadata to match reality
                        UpdateMetadataFile();
                    }

                    // Validate individual file entries
                    foreach (var fileEntry in metadata.Files)
                    {
                        if (!_dataFiles.ContainsKey(fileEntry.Key))
                        {
                            // File referenced in metadata but not in memory - potential issue
                            Console.WriteLine($"Warning: Metadata references file {fileEntry.Key} not found in memory");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Metadata validation failed: {ex.Message}");
        }
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
        try
        {
            lock (_lock)
            {
                // Check transaction files integrity
                // In Eclipse Store, this validates transaction log files

                var transactionFilesValid = true;

                // Check if there are any pending writes that need validation
                if (_pendingWrites.Count > 0)
                {
                    foreach (var pendingWrite in _pendingWrites)
                    {
                        // Validate pending write integrity
                        if (pendingWrite.Data == null || pendingWrite.Data.Length == 0)
                        {
                            Console.WriteLine($"Warning: Invalid pending write detected");
                            transactionFilesValid = false;
                        }

                        // Check if write timestamp is reasonable
                        if (pendingWrite.Timestamp < DateTime.UtcNow.AddDays(-1))
                        {
                            Console.WriteLine($"Warning: Stale pending write detected from {pendingWrite.Timestamp}");
                        }
                    }
                }

                // Validate metadata file if size checking is enabled
                if (checkSize && File.Exists(_metadataFilePath))
                {
                    var fileInfo = new FileInfo(_metadataFilePath);
                    if (fileInfo.Length == 0)
                    {
                        Console.WriteLine("Warning: Metadata file is empty");
                        transactionFilesValid = false;
                    }
                    else if (fileInfo.Length > 10 * 1024 * 1024) // 10MB threshold
                    {
                        Console.WriteLine("Warning: Metadata file is unusually large");
                    }
                }

                return transactionFilesValid;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during transaction file check: {ex.Message}");
            return false;
        }
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

    private void LoadMetadata()
    {
        try
        {
            if (File.Exists(_metadataFilePath))
            {
                var json = File.ReadAllText(_metadataFilePath);
                var metadata = System.Text.Json.JsonSerializer.Deserialize<StorageFileMetadata>(json);
                if (metadata != null)
                {
                    _nextFileNumber = metadata.NextFileNumber;
                }
            }
        }
        catch (Exception ex)
        {
            // Log warning but continue - metadata will be rebuilt
            Console.WriteLine($"Warning: Failed to load metadata for channel {_channelIndex}: {ex.Message}");
        }
    }

    private void UpdateMetadataFile()
    {
        try
        {
            var metadata = new StorageFileMetadata
            {
                NextFileNumber = _nextFileNumber,
                LastUpdated = DateTime.UtcNow,
                FileCount = _dataFiles.Count,
                TotalDataSize = _dataFiles.Values.Sum(f => f.DataLength)
            };

            foreach (var kvp in _dataFiles)
            {
                var file = kvp.Value;
                metadata.Files[kvp.Key] = new StorageFileMetadata.FileInfo
                {
                    Number = file.Number,
                    Size = file.Size,
                    DataLength = file.DataLength,
                    Created = DateTime.UtcNow, // Would be stored in actual implementation
                    LastModified = DateTime.UtcNow,
                    IsActive = file.HasContent
                };
            }

            var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_metadataFilePath, json);
        }
        catch (Exception ex)
        {
            throw new StorageFileException($"Failed to update metadata file for channel {_channelIndex}", ex);
        }
    }

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

/// <summary>
/// Represents a pending write operation that can be rolled back.
/// </summary>
internal class PendingWriteOperation
{
    public IStorageLiveDataFile File { get; }
    public long OriginalLength { get; }
    public long WritePosition { get; }
    public byte[] Data { get; }
    public DateTime Timestamp { get; }

    public PendingWriteOperation(IStorageLiveDataFile file, long originalLength, long writePosition, byte[] data)
    {
        File = file ?? throw new ArgumentNullException(nameof(file));
        OriginalLength = originalLength;
        WritePosition = writePosition;
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Metadata for storage file management.
/// </summary>
internal class StorageFileMetadata
{
    public long NextFileNumber { get; set; } = 1;
    public Dictionary<long, FileInfo> Files { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public long TotalDataSize { get; set; }
    public int FileCount { get; set; }

    public class FileInfo
    {
        public long Number { get; set; }
        public long Size { get; set; }
        public long DataLength { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastModified { get; set; }
        public bool IsActive { get; set; }
    }
}

/// <summary>
/// Metadata for an object stored in the system.
/// </summary>
public class ObjectMetadata
{
    public long ObjectId { get; set; }
    public long TypeId { get; set; }
    public int ChannelIndex { get; set; }
    public long FileNumber { get; set; }
    public long Position { get; set; }
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public long AccessCount { get; set; }
}

/// <summary>
/// Metadata for a type registered in the system.
/// </summary>
public class TypeMetadata
{
    public long TypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string AssemblyQualifiedName { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
    public long ObjectCount { get; set; }
}

/// <summary>
/// Storage statistics for monitoring and management.
/// </summary>
public class StorageStatistics
{
    public long TotalObjects { get; set; }
    public long TotalTypes { get; set; }
    public long TotalDataSize { get; set; }
    public long TotalFileSize { get; set; }
    public int ChannelCount { get; set; }
    public long FileCount { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
