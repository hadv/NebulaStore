using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NebulaStore.Storage.Embedded.Types.Exceptions;

namespace NebulaStore.Storage.Embedded.Types.Files;

/// <summary>
/// Default implementation of IStorageFileWriter for writing data to storage files.
/// </summary>
public class StorageFileWriter : IStorageFileWriter
{
    #region Private Fields

    private readonly object _lock = new object();

    #endregion

    #region IStorageFileWriter Implementation

    public long WriteStore(IStorageLiveDataFile file, IEnumerable<byte[]> dataBuffers)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));
        if (dataBuffers == null)
            throw new ArgumentNullException(nameof(dataBuffers));

        lock (_lock)
        {
            var totalBytesWritten = 0L;
            var startPosition = file.Size;

            foreach (var buffer in dataBuffers)
            {
                if (buffer != null && buffer.Length > 0)
                {
                    if (file is StorageLiveDataFile dataFile)
                    {
                        var bytesWritten = dataFile.WriteData(buffer);
                        totalBytesWritten += bytesWritten;
                    }
                    else
                    {
                        throw new StorageFileException($"Unsupported file type: {file.GetType()}");
                    }
                }
            }

            return startPosition; // Return the position where writing started
        }
    }

    public long WriteTransfer(IStorageLiveDataFile sourceFile, long sourceOffset, long length, IStorageLiveDataFile targetFile)
    {
        if (sourceFile == null)
            throw new ArgumentNullException(nameof(sourceFile));
        if (targetFile == null)
            throw new ArgumentNullException(nameof(targetFile));
        if (sourceOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceOffset));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        lock (_lock)
        {
            var buffer = new byte[Math.Min(length, 64 * 1024)]; // 64KB buffer
            var totalBytesTransferred = 0L;
            var remainingBytes = length;

            while (remainingBytes > 0)
            {
                var bytesToRead = (int)Math.Min(remainingBytes, buffer.Length);
                var bytesRead = sourceFile.ReadBytes(buffer, sourceOffset + totalBytesTransferred);
                
                if (bytesRead == 0)
                    break;

                if (targetFile is StorageLiveDataFile targetDataFile)
                {
                    var bytesToWrite = Math.Min(bytesRead, bytesToRead);
                    var writeBuffer = new byte[bytesToWrite];
                    Array.Copy(buffer, 0, writeBuffer, 0, bytesToWrite);
                    
                    targetDataFile.WriteData(writeBuffer);
                    totalBytesTransferred += bytesToWrite;
                    remainingBytes -= bytesToWrite;
                }
                else
                {
                    throw new StorageFileException($"Unsupported target file type: {targetFile.GetType()}");
                }
            }

            return totalBytesTransferred;
        }
    }

    public void WriteImport(IStorageImportSource importSource, long position, long length, IStorageLiveDataFile targetFile)
    {
        if (importSource == null)
            throw new ArgumentNullException(nameof(importSource));
        if (targetFile == null)
            throw new ArgumentNullException(nameof(targetFile));
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        lock (_lock)
        {
            // This would typically read from the import source and write to the target file
            // For now, this is a placeholder implementation
            var buffer = new byte[length];
            
            // Read from import source (implementation would depend on the source type)
            // For now, we'll create dummy data
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)(i % 256);
            }

            if (targetFile is StorageLiveDataFile targetDataFile)
            {
                targetDataFile.WriteDataAt(buffer, position);
            }
            else
            {
                throw new StorageFileException($"Unsupported target file type: {targetFile.GetType()}");
            }
        }
    }

    public void Truncate(IStorageLiveDataFile file, long length, IStorageLiveFileProvider fileProvider)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        lock (_lock)
        {
            file.Truncate(length);
        }
    }

    public void Delete(IStorageLiveDataFile file, IStorageWriteController writeController, IStorageLiveFileProvider fileProvider)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));
        if (writeController == null)
            throw new ArgumentNullException(nameof(writeController));

        lock (_lock)
        {
            // Validate that file cleanup is enabled
            writeController.ValidateIsFileCleanupEnabled();
            
            // Delete the file
            file.Delete();
        }
    }

    public void WriteTransactionEntryCreate(IStorageLiveTransactionsFile transactionsFile, IEnumerable<byte[]> entryData, IStorageLiveDataFile dataFile)
    {
        if (transactionsFile == null)
            throw new ArgumentNullException(nameof(transactionsFile));
        if (entryData == null)
            throw new ArgumentNullException(nameof(entryData));
        if (dataFile == null)
            throw new ArgumentNullException(nameof(dataFile));

        lock (_lock)
        {
            // Write transaction entry for file creation
            // This would typically write a transaction log entry
            // For now, this is a placeholder
        }
    }

    public void WriteTransactionEntryStore(IStorageLiveTransactionsFile transactionsFile, IEnumerable<byte[]> entryData, IStorageLiveDataFile dataFile, long offset, long length)
    {
        if (transactionsFile == null)
            throw new ArgumentNullException(nameof(transactionsFile));
        if (entryData == null)
            throw new ArgumentNullException(nameof(entryData));
        if (dataFile == null)
            throw new ArgumentNullException(nameof(dataFile));

        lock (_lock)
        {
            // Write transaction entry for store operation
            // This would typically write a transaction log entry
            // For now, this is a placeholder
        }
    }

    public void WriteTransactionEntryTransfer(IStorageLiveTransactionsFile transactionsFile, IEnumerable<byte[]> entryData, IStorageLiveDataFile sourceFile, long sourceOffset, long length)
    {
        if (transactionsFile == null)
            throw new ArgumentNullException(nameof(transactionsFile));
        if (entryData == null)
            throw new ArgumentNullException(nameof(entryData));
        if (sourceFile == null)
            throw new ArgumentNullException(nameof(sourceFile));

        lock (_lock)
        {
            // Write transaction entry for transfer operation
            // This would typically write a transaction log entry
            // For now, this is a placeholder
        }
    }

    public void WriteTransactionEntryDelete(IStorageLiveTransactionsFile transactionsFile, IEnumerable<byte[]> entryData, IStorageLiveDataFile dataFile)
    {
        if (transactionsFile == null)
            throw new ArgumentNullException(nameof(transactionsFile));
        if (entryData == null)
            throw new ArgumentNullException(nameof(entryData));
        if (dataFile == null)
            throw new ArgumentNullException(nameof(dataFile));

        lock (_lock)
        {
            // Write transaction entry for file deletion
            // This would typically write a transaction log entry
            // For now, this is a placeholder
        }
    }

    public void WriteTransactionEntryTruncate(IStorageLiveTransactionsFile transactionsFile, IEnumerable<byte[]> entryData, IStorageLiveDataFile dataFile, long newLength)
    {
        if (transactionsFile == null)
            throw new ArgumentNullException(nameof(transactionsFile));
        if (entryData == null)
            throw new ArgumentNullException(nameof(entryData));
        if (dataFile == null)
            throw new ArgumentNullException(nameof(dataFile));

        lock (_lock)
        {
            // Write transaction entry for file truncation
            // This would typically write a transaction log entry
            // For now, this is a placeholder
        }
    }

    public long Write(IStorageFile file, IEnumerable<byte[]> data)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        lock (_lock)
        {
            var totalBytesWritten = 0L;

            foreach (var buffer in data)
            {
                if (buffer != null && buffer.Length > 0)
                {
                    if (file is StorageLiveDataFile dataFile)
                    {
                        var bytesWritten = dataFile.WriteData(buffer);
                        totalBytesWritten += bytesWritten;
                    }
                    else
                    {
                        throw new StorageFileException($"Unsupported file type: {file.GetType()}");
                    }
                }
            }

            return totalBytesWritten;
        }
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a new StorageFileWriter instance.
    /// </summary>
    /// <returns>A new StorageFileWriter instance.</returns>
    public static StorageFileWriter Create()
    {
        return new StorageFileWriter();
    }

    #endregion
}

/// <summary>
/// Default implementation of IStorageDataFileEvaluator.
/// </summary>
public class DefaultStorageDataFileEvaluator : IStorageDataFileEvaluator
{
    #region Private Fields

    private readonly long _fileMaximumSize;
    private readonly long _transactionFileMaximumSize;
    private readonly bool _isFileCleanupEnabled;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the DefaultStorageDataFileEvaluator class.
    /// </summary>
    /// <param name="fileMaximumSize">The maximum file size.</param>
    /// <param name="transactionFileMaximumSize">The maximum transaction file size.</param>
    /// <param name="isFileCleanupEnabled">Whether file cleanup is enabled.</param>
    public DefaultStorageDataFileEvaluator(
        long fileMaximumSize = 100 * 1024 * 1024, // 100 MB default
        long transactionFileMaximumSize = 10 * 1024 * 1024, // 10 MB default
        bool isFileCleanupEnabled = true)
    {
        _fileMaximumSize = fileMaximumSize;
        _transactionFileMaximumSize = transactionFileMaximumSize;
        _isFileCleanupEnabled = isFileCleanupEnabled;
    }

    #endregion

    #region IStorageDataFileEvaluator Implementation

    public long FileMaximumSize => _fileMaximumSize;

    public long TransactionFileMaximumSize => _transactionFileMaximumSize;

    public bool IsFileCleanupEnabled => _isFileCleanupEnabled;

    public bool NeedsDissolving(IStorageLiveDataFile file)
    {
        if (file == null)
            return false;

        // A file needs dissolving if it's too large or has too little live data
        var fileSize = file.Size;
        var liveDataSize = file.DataLength;
        
        // File is too large
        if (fileSize > _fileMaximumSize)
            return true;

        // File has too little live data (less than 50% utilization)
        if (fileSize > 0 && liveDataSize < fileSize * 0.5)
            return true;

        return false;
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a new DefaultStorageDataFileEvaluator with default settings.
    /// </summary>
    /// <returns>A new DefaultStorageDataFileEvaluator instance.</returns>
    public static DefaultStorageDataFileEvaluator Create()
    {
        return new DefaultStorageDataFileEvaluator();
    }

    #endregion
}

/// <summary>
/// Default implementation of IStorageWriteController.
/// </summary>
public class DefaultStorageWriteController : IStorageWriteController
{
    public bool IsFileCleanupEnabled { get; }

    public DefaultStorageWriteController(bool isFileCleanupEnabled = true)
    {
        IsFileCleanupEnabled = isFileCleanupEnabled;
    }

    public void ValidateIsFileCleanupEnabled()
    {
        if (!IsFileCleanupEnabled)
        {
            throw new InvalidOperationException("File cleanup is disabled");
        }
    }

    public static DefaultStorageWriteController Create(bool isFileCleanupEnabled = true)
    {
        return new DefaultStorageWriteController(isFileCleanupEnabled);
    }
}
