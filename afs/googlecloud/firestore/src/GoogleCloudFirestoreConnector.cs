using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.GoogleCloud.Firestore;

/// <summary>
/// Google Cloud Firestore implementation of IBlobStoreConnector.
/// Stores blobs as documents in Firestore collections.
/// </summary>
/// <remarks>
/// This connector stores files as numbered blob documents in Firestore collections.
/// Each blob is limited to 1MB (Firestore document size limit) and larger files
/// are split across multiple documents.
/// 
/// First create a Firestore connection:
/// <code>
/// FirestoreDb firestore = FirestoreDb.Create("your-project-id");
/// BlobStoreFileSystem fileSystem = BlobStoreFileSystem.New(
///     GoogleCloudFirestoreConnector.New(firestore)
/// );
/// </code>
/// </remarks>
public class GoogleCloudFirestoreConnector : BlobStoreConnectorBase
{
    private const string FieldKey = "key";
    private const string FieldSize = "size";
    private const string FieldData = "data";
    
    // Firestore limits: https://firebase.google.com/docs/firestore/quotas
    private const long MaxBlobSize = 1_000_000L; // 1MB per document
    private const long MaxRequestSize = 10_000_000L; // 10MB per batch
    
    private readonly FirestoreDb _firestore;
    private readonly bool _useCache;
    private readonly Dictionary<string, bool> _directoryExistsCache = new();
    private readonly Dictionary<string, bool> _fileExistsCache = new();
    private readonly Dictionary<string, long> _fileSizeCache = new();
    private readonly object _cacheLock = new();

    /// <summary>
    /// Creates a new GoogleCloudFirestoreConnector.
    /// </summary>
    /// <param name="firestore">Connection to the Google Firestore service</param>
    /// <returns>A new GoogleCloudFirestoreConnector</returns>
    public static GoogleCloudFirestoreConnector New(FirestoreDb firestore)
    {
        return new GoogleCloudFirestoreConnector(firestore, false);
    }

    /// <summary>
    /// Creates a new GoogleCloudFirestoreConnector with cache enabled.
    /// </summary>
    /// <param name="firestore">Connection to the Google Firestore service</param>
    /// <returns>A new GoogleCloudFirestoreConnector with caching</returns>
    public static GoogleCloudFirestoreConnector Caching(FirestoreDb firestore)
    {
        return new GoogleCloudFirestoreConnector(firestore, true);
    }

    /// <summary>
    /// Initializes a new instance of the GoogleCloudFirestoreConnector class.
    /// </summary>
    /// <param name="firestore">The Firestore database instance</param>
    /// <param name="useCache">Whether to enable caching for performance</param>
    private GoogleCloudFirestoreConnector(FirestoreDb firestore, bool useCache)
    {
        _firestore = firestore ?? throw new ArgumentNullException(nameof(firestore));
        _useCache = useCache;
    }

    /// <summary>
    /// Gets the collection reference for the specified path.
    /// </summary>
    /// <param name="path">The blob store path</param>
    /// <returns>The collection reference</returns>
    private CollectionReference GetCollection(BlobStorePath path)
    {
        ValidateCollectionName(path.Container);
        return _firestore.Collection(path.Container);
    }

    /// <summary>
    /// Validates the collection name according to Firestore rules.
    /// </summary>
    /// <param name="collectionName">The collection name to validate</param>
    private static void ValidateCollectionName(string collectionName)
    {
        if (collectionName.Contains('/'))
        {
            throw new ArgumentException("Collection name cannot contain a forward slash (/)");
        }
        
        if (collectionName == "." || collectionName == "..")
        {
            throw new ArgumentException("Collection name cannot solely consist of a single period (.) or double periods (..)");
        }
        
        if (Regex.IsMatch(collectionName, @"^__.*__$"))
        {
            throw new ArgumentException("Collection name cannot match the regular expression __.*__");
        }
    }

    /// <summary>
    /// Gets the document path for a blob.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <param name="blobNumber">The blob number</param>
    /// <returns>The document path</returns>
    private static string GetDocumentPath(BlobStorePath file, long blobNumber)
    {
        var pathElements = file.PathElements.Skip(1).ToArray();
        var pathString = string.Join("_", pathElements);
        return $"{pathString}{NumberSuffixSeparator}{blobNumber}";
    }

    /// <summary>
    /// Gets the blob key for a file and blob number.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <param name="blobNumber">The blob number</param>
    /// <returns>The blob key</returns>
    private static string GetBlobKey(BlobStorePath file, long blobNumber)
    {
        return $"{file.FullQualifiedName}{NumberSuffixSeparator}{blobNumber}";
    }

    /// <summary>
    /// Gets the blob key prefix for a file.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <returns>The blob key prefix</returns>
    private static string GetBlobKeyPrefix(BlobStorePath file)
    {
        return file.FullQualifiedName + NumberSuffixSeparator;
    }

    /// <summary>
    /// Gets all blob documents for a file, optionally with data.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <param name="withData">Whether to include blob data in the query</param>
    /// <returns>Stream of blob documents</returns>
    private async Task<List<DocumentSnapshot>> GetBlobsAsync(BlobStorePath file, bool withData = false)
    {
        var prefix = GetBlobKeyPrefix(file);
        var pattern = new Regex($"^{Regex.Escape(prefix)}\\d+$");
        
        var query = GetCollection(file).WhereGreaterThan(FieldKey, prefix);
        if (!withData)
        {
            query = query.Select(FieldKey, FieldSize);
        }

        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Documents
            .Where(doc => pattern.IsMatch(doc.GetValue<string>(FieldKey)))
            .OrderBy(doc => ExtractBlobNumber(doc.GetValue<string>(FieldKey)))
            .ToList();
    }

    /// <summary>
    /// Extracts the blob number from a blob key.
    /// </summary>
    /// <param name="blobKey">The blob key</param>
    /// <returns>The blob number</returns>
    private static long ExtractBlobNumber(string blobKey)
    {
        var lastDotIndex = blobKey.LastIndexOf(NumberSuffixSeparatorChar);
        if (lastDotIndex >= 0 && long.TryParse(blobKey.Substring(lastDotIndex + 1), out var number))
        {
            return number;
        }
        return 0;
    }

    public override long GetFileSize(BlobStorePath file)
    {
        EnsureNotDisposed();

        if (_useCache)
        {
            lock (_cacheLock)
            {
                if (_fileSizeCache.TryGetValue(file.FullQualifiedName, out var cachedSize))
                    return cachedSize;
            }
        }

        try
        {
            var blobs = GetBlobsAsync(file, false).Result;
            var totalSize = blobs.Sum(doc => doc.GetValue<long>(FieldSize));

            if (_useCache)
            {
                lock (_cacheLock)
                {
                    _fileSizeCache[file.FullQualifiedName] = totalSize;
                }
            }

            return totalSize;
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to get file size for {file.FullQualifiedName}", ex);
        }
    }

    public override bool DirectoryExists(BlobStorePath directory)
    {
        EnsureNotDisposed();

        if (_useCache)
        {
            lock (_cacheLock)
            {
                if (_directoryExistsCache.TryGetValue(directory.FullQualifiedName, out var cached))
                    return cached;
            }
        }

        try
        {
            var prefix = directory.FullQualifiedName + "/";
            var query = GetCollection(directory)
                .WhereGreaterThan(FieldKey, prefix)
                .Select(FieldKey)
                .Limit(1);

            var snapshot = query.GetSnapshotAsync().Result;
            var exists = snapshot.Documents.Any(doc => 
                doc.GetValue<string>(FieldKey).StartsWith(prefix));

            if (_useCache)
            {
                lock (_cacheLock)
                {
                    _directoryExistsCache[directory.FullQualifiedName] = exists;
                }
            }

            return exists;
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to check directory existence for {directory.FullQualifiedName}", ex);
        }
    }

    public override bool FileExists(BlobStorePath file)
    {
        EnsureNotDisposed();

        if (_useCache)
        {
            lock (_cacheLock)
            {
                if (_fileExistsCache.TryGetValue(file.FullQualifiedName, out var cached))
                    return cached;
            }
        }

        try
        {
            var blobs = GetBlobsAsync(file, false).Result;
            var exists = blobs.Count > 0;

            if (_useCache)
            {
                lock (_cacheLock)
                {
                    _fileExistsCache[file.FullQualifiedName] = exists;
                }
            }

            return exists;
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to check file existence for {file.FullQualifiedName}", ex);
        }
    }

    public override void VisitChildren(BlobStorePath directory, IBlobStorePathVisitor visitor)
    {
        EnsureNotDisposed();

        try
        {
            var prefix = directory.FullQualifiedName + "/";
            var query = GetCollection(directory)
                .WhereGreaterThan(FieldKey, prefix)
                .Select(FieldKey);

            var snapshot = query.GetSnapshotAsync().Result;
            var children = new HashSet<string>();

            foreach (var doc in snapshot.Documents)
            {
                var key = doc.GetValue<string>(FieldKey);
                if (key.StartsWith(prefix))
                {
                    var relativePath = key.Substring(prefix.Length);
                    var firstSegment = relativePath.Split('/')[0];

                    // Remove blob number suffix if present
                    if (firstSegment.Contains(NumberSuffixSeparatorChar))
                    {
                        firstSegment = RemoveNumberSuffix(firstSegment);
                    }

                    children.Add(firstSegment);
                }
            }

            foreach (var child in children.OrderBy(c => c))
            {
                if (child.Contains('/'))
                {
                    visitor.VisitDirectory(directory, child.Split('/')[0]);
                }
                else
                {
                    visitor.VisitFile(directory, child);
                }
            }
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to visit children for {directory.FullQualifiedName}", ex);
        }
    }

    public override bool IsEmpty(BlobStorePath directory)
    {
        EnsureNotDisposed();

        try
        {
            var prefix = directory.FullQualifiedName + "/";
            var query = GetCollection(directory)
                .WhereGreaterThan(FieldKey, prefix)
                .Select(FieldKey)
                .Limit(1);

            var snapshot = query.GetSnapshotAsync().Result;
            return !snapshot.Documents.Any(doc =>
                doc.GetValue<string>(FieldKey).StartsWith(prefix));
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to check if directory is empty for {directory.FullQualifiedName}", ex);
        }
    }

    public override bool CreateDirectory(BlobStorePath directory)
    {
        EnsureNotDisposed();

        // Firestore creates collections implicitly when documents are added
        // So we just mark it as existing in cache
        if (_useCache)
        {
            lock (_cacheLock)
            {
                _directoryExistsCache[directory.FullQualifiedName] = true;
            }
        }

        return true;
    }

    public override bool CreateFile(BlobStorePath file)
    {
        EnsureNotDisposed();

        // Files are created implicitly when data is written
        return true;
    }

    public override bool DeleteFile(BlobStorePath file)
    {
        EnsureNotDisposed();

        try
        {
            var blobs = GetBlobsAsync(file, false).Result;
            if (blobs.Count == 0)
                return false;

            var batch = _firestore.StartBatch();
            foreach (var blob in blobs)
            {
                batch.Delete(blob.Reference);
            }

            batch.CommitAsync().Wait();

            if (_useCache)
            {
                lock (_cacheLock)
                {
                    _fileExistsCache.Remove(file.FullQualifiedName);
                    _fileSizeCache.Remove(file.FullQualifiedName);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to delete file {file.FullQualifiedName}", ex);
        }
    }

    public override byte[] ReadData(BlobStorePath file, long offset, long length)
    {
        EnsureNotDisposed();

        try
        {
            var blobs = GetBlobsAsync(file, true).Result;
            if (blobs.Count == 0)
                return Array.Empty<byte>();

            var allData = new List<byte>();
            foreach (var blob in blobs)
            {
                var blobData = blob.GetValue<Blob>(FieldData);
                allData.AddRange(blobData.ByteString.ToByteArray());
            }

            if (allData.Count == 0)
                return Array.Empty<byte>();

            var startIndex = Math.Min((int)offset, allData.Count);
            var endIndex = length == -1 ? allData.Count : Math.Min(startIndex + (int)length, allData.Count);
            var resultLength = Math.Max(0, endIndex - startIndex);

            var result = new byte[resultLength];
            if (resultLength > 0)
            {
                allData.CopyTo(startIndex, result, 0, resultLength);
            }

            return result;
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to read data from {file.FullQualifiedName}", ex);
        }
    }

    public override long ReadData(BlobStorePath file, byte[] targetBuffer, long offset, long length)
    {
        var data = ReadData(file, offset, length);
        var bytesToCopy = Math.Min(data.Length, targetBuffer.Length);
        Array.Copy(data, 0, targetBuffer, 0, bytesToCopy);
        return bytesToCopy;
    }

    public override long WriteData(BlobStorePath file, IEnumerable<byte[]> sourceBuffers)
    {
        EnsureNotDisposed();

        try
        {
            var allData = sourceBuffers.SelectMany(buffer => buffer).ToArray();
            if (allData.Length == 0)
                return 0;

            var batch = _firestore.StartBatch();
            var nextBlobNumber = GetNextBlobNumber(file);
            var totalWritten = 0L;
            var currentBatchSize = 0L;

            for (int offset = 0; offset < allData.Length; offset += (int)MaxBlobSize)
            {
                var chunkSize = Math.Min(MaxBlobSize, allData.Length - offset);
                var chunk = new byte[chunkSize];
                Array.Copy(allData, offset, chunk, 0, (int)chunkSize);

                var docPath = GetDocumentPath(file, nextBlobNumber);
                var docRef = GetCollection(file).Document(docPath);

                var data = new Dictionary<string, object>
                {
                    [FieldKey] = GetBlobKey(file, nextBlobNumber),
                    [FieldSize] = chunkSize,
                    [FieldData] = Blob.CopyFrom(chunk)
                };

                batch.Set(docRef, data);
                nextBlobNumber++;
                totalWritten += chunkSize;
                currentBatchSize += chunkSize;

                // Commit batch if approaching size limit
                if (currentBatchSize >= MaxRequestSize - MaxBlobSize)
                {
                    batch.CommitAsync().Wait();
                    batch = _firestore.StartBatch();
                    currentBatchSize = 0;
                }
            }

            // Commit remaining operations
            if (currentBatchSize > 0)
            {
                batch.CommitAsync().Wait();
            }

            if (_useCache)
            {
                lock (_cacheLock)
                {
                    _fileExistsCache[file.FullQualifiedName] = true;
                    _fileSizeCache.TryGetValue(file.FullQualifiedName, out var currentSize);
                    _fileSizeCache[file.FullQualifiedName] = currentSize + totalWritten;
                }
            }

            return totalWritten;
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to write data to {file.FullQualifiedName}", ex);
        }
    }

    public override void MoveFile(BlobStorePath sourceFile, BlobStorePath targetFile)
    {
        EnsureNotDisposed();

        try
        {
            // Read source data
            var data = ReadData(sourceFile, 0, -1);

            // Write to target
            WriteData(targetFile, new[] { data });

            // Delete source
            DeleteFile(sourceFile);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to move file from {sourceFile.FullQualifiedName} to {targetFile.FullQualifiedName}", ex);
        }
    }

    public override long CopyFile(BlobStorePath sourceFile, BlobStorePath targetFile, long offset, long length)
    {
        EnsureNotDisposed();

        try
        {
            var data = ReadData(sourceFile, offset, length);
            return WriteData(targetFile, new[] { data });
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to copy file from {sourceFile.FullQualifiedName} to {targetFile.FullQualifiedName}", ex);
        }
    }

    public override void TruncateFile(BlobStorePath file, long newLength)
    {
        EnsureNotDisposed();

        try
        {
            if (newLength < 0)
                throw new ArgumentException("New length cannot be negative", nameof(newLength));

            // Read current data up to new length
            var data = ReadData(file, 0, newLength);

            // Delete the file
            DeleteFile(file);

            // Write truncated data back
            if (data.Length > 0)
            {
                WriteData(file, new[] { data });
            }
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to truncate file {file.FullQualifiedName}", ex);
        }
    }

    /// <summary>
    /// Gets the next blob number for a file.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <returns>The next blob number</returns>
    private long GetNextBlobNumber(BlobStorePath file)
    {
        try
        {
            var blobs = GetBlobsAsync(file, false).Result;
            if (blobs.Count == 0)
                return 0;

            var lastBlob = blobs.Last();
            var lastKey = lastBlob.GetValue<string>(FieldKey);
            return ExtractBlobNumber(lastKey) + 1;
        }
        catch
        {
            return 0;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_useCache)
            {
                lock (_cacheLock)
                {
                    _directoryExistsCache.Clear();
                    _fileExistsCache.Clear();
                    _fileSizeCache.Clear();
                }
            }
        }

        base.Dispose(disposing);
    }
}
