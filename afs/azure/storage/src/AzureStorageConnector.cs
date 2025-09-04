using System.Text.RegularExpressions;
using System.Threading;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Afs.Blobstore.Types;

namespace NebulaStore.Afs.Azure.Storage;

/// <summary>
/// Azure Blob Storage implementation of IBlobStoreConnector.
/// Stores blobs as objects in Azure Blob Storage containers.
/// </summary>
/// <remarks>
/// This connector stores files as numbered blob objects in Azure containers.
/// Each blob can be up to 5TB (Azure block blob limit) and larger files
/// are split across multiple blobs for optimal performance.
/// 
/// First create a BlobServiceClient and configuration:
/// <code>
/// var config = AzureStorageConfiguration.New()
///     .SetConnectionString("DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net")
///     .SetUseCache(true);
/// 
/// var blobServiceClient = AzureStorageClientFactory.CreateBlobServiceClient(config);
/// var connector = AzureStorageConnector.New(blobServiceClient, config);
/// var fileSystem = BlobStoreFileSystem.New(connector);
/// </code>
/// </remarks>
public class AzureStorageConnector : BlobStoreConnectorBase
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly AzureStorageConfiguration _configuration;
    private readonly IAzureStoragePathValidator _pathValidator;
    private readonly Dictionary<string, bool> _containerExistsCache = new();
    private readonly Dictionary<string, bool> _blobExistsCache = new();
    private readonly Dictionary<string, long> _blobSizeCache = new();
    private readonly object _cacheLock = new();

    /// <summary>
    /// Initializes a new instance of the AzureStorageConnector class.
    /// </summary>
    /// <param name="blobServiceClient">The Azure blob service client</param>
    /// <param name="configuration">The Azure storage configuration</param>
    /// <param name="pathValidator">The path validator (optional)</param>
    public AzureStorageConnector(
        BlobServiceClient blobServiceClient, 
        AzureStorageConfiguration configuration,
        IAzureStoragePathValidator? pathValidator = null)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _pathValidator = pathValidator ?? IAzureStoragePathValidator.New();
        
        _configuration.Validate();
    }

    /// <summary>
    /// Creates a new Azure Storage connector.
    /// </summary>
    /// <param name="blobServiceClient">The Azure blob service client</param>
    /// <param name="configuration">The Azure storage configuration</param>
    /// <returns>A new Azure Storage connector</returns>
    public static AzureStorageConnector New(BlobServiceClient blobServiceClient, AzureStorageConfiguration configuration)
    {
        return new AzureStorageConnector(blobServiceClient, configuration);
    }

    /// <summary>
    /// Creates a new Azure Storage connector with caching enabled.
    /// </summary>
    /// <param name="blobServiceClient">The Azure blob service client</param>
    /// <param name="configuration">The Azure storage configuration</param>
    /// <returns>A new Azure Storage connector with caching</returns>
    public static AzureStorageConnector Caching(BlobServiceClient blobServiceClient, AzureStorageConfiguration configuration)
    {
        configuration.SetUseCache(true);
        return new AzureStorageConnector(blobServiceClient, configuration);
    }

    /// <summary>
    /// Creates a new Azure Storage connector from a connection string.
    /// </summary>
    /// <param name="connectionString">The Azure storage connection string</param>
    /// <param name="useCache">Whether to enable caching</param>
    /// <returns>A new Azure Storage connector</returns>
    public static AzureStorageConnector FromConnectionString(string connectionString, bool useCache = true)
    {
        var configuration = AzureStorageConfiguration.New()
            .SetConnectionString(connectionString)
            .SetUseCache(useCache);
        
        var blobServiceClient = AzureStorageClientFactory.CreateBlobServiceClient(configuration);
        return new AzureStorageConnector(blobServiceClient, configuration);
    }

    public override long GetFileSize(BlobStorePath file)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        if (_configuration.UseCache)
        {
            lock (_cacheLock)
            {
                var cacheKey = file.ToString();
                if (_blobSizeCache.TryGetValue(cacheKey, out var cachedSize))
                {
                    return cachedSize;
                }
            }
        }

        try
        {
            var blobs = GetBlobs(file);
            var totalSize = blobs.Sum(blob => blob.Properties.ContentLength ?? 0);

            if (_configuration.UseCache)
            {
                lock (_cacheLock)
                {
                    _blobSizeCache[file.ToString()] = totalSize;
                }
            }

            return totalSize;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return 0;
        }
    }

    public override bool DirectoryExists(BlobStorePath directory)
    {
        EnsureNotDisposed();
        directory.Validate(_pathValidator);

        if (_configuration.UseCache)
        {
            lock (_cacheLock)
            {
                var cacheKey = directory.ToString();
                if (_containerExistsCache.TryGetValue(cacheKey, out var cachedExists))
                {
                    return cachedExists;
                }
            }
        }

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(directory.Container);
            
            // Check if container exists
            if (!containerClient.Exists())
            {
                return false;
            }

            // If this is just the container root, it exists
            if (directory.PathElements.Length == 1)
            {
                return true;
            }

            // Check if any blobs exist with this prefix
            var prefix = ToContainerKey(directory);
            var blobs = containerClient.GetBlobs(prefix: prefix).Take(1);
            var exists = blobs.Any();

            if (_configuration.UseCache)
            {
                lock (_cacheLock)
                {
                    _containerExistsCache[directory.ToString()] = exists;
                }
            }

            return exists;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public override bool FileExists(BlobStorePath file)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        if (_configuration.UseCache)
        {
            lock (_cacheLock)
            {
                var cacheKey = file.ToString();
                if (_blobExistsCache.TryGetValue(cacheKey, out var cachedExists))
                {
                    return cachedExists;
                }
            }
        }

        try
        {
            var blobs = GetBlobs(file);
            var exists = blobs.Any();

            if (_configuration.UseCache)
            {
                lock (_cacheLock)
                {
                    _blobExistsCache[file.ToString()] = exists;
                }
            }

            return exists;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public override void VisitChildren(BlobStorePath directory, IBlobStorePathVisitor visitor)
    {
        EnsureNotDisposed();
        directory.Validate(_pathValidator);

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(directory.Container);
            var prefix = ToContainerKey(directory);
            var delimiter = BlobStorePath.Separator;

            var blobs = containerClient.GetBlobsByHierarchy(prefix: prefix, delimiter: delimiter);
            var visitedDirectories = new HashSet<string>();
            var visitedFiles = new HashSet<string>();

            foreach (var item in blobs)
            {
                if (item.IsPrefix)
                {
                    // This is a directory
                    var dirName = item.Prefix.TrimEnd('/');
                    if (dirName.StartsWith(prefix))
                    {
                        dirName = dirName.Substring(prefix.Length);
                    }
                    
                    if (!string.IsNullOrEmpty(dirName) && visitedDirectories.Add(dirName))
                    {
                        visitor.VisitDirectory(directory, dirName);
                    }
                }
                else if (item.Blob != null)
                {
                    // This is a file
                    var fileName = GetFileNameFromBlobName(item.Blob.Name, prefix);
                    if (!string.IsNullOrEmpty(fileName) && visitedFiles.Add(fileName))
                    {
                        visitor.VisitFile(directory, fileName);
                    }
                }
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Directory doesn't exist, nothing to visit
        }
    }

    public override bool IsEmpty(BlobStorePath directory)
    {
        EnsureNotDisposed();
        directory.Validate(_pathValidator);

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(directory.Container);
            var prefix = ToContainerKey(directory);
            var blobs = containerClient.GetBlobs(prefix: prefix).Take(1);
            return !blobs.Any();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return true;
        }
    }

    public override bool CreateDirectory(BlobStorePath directory)
    {
        EnsureNotDisposed();
        directory.Validate(_pathValidator);

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(directory.Container);
            
            // Create container if it doesn't exist
            containerClient.CreateIfNotExists(PublicAccessType.None);

            // Azure Blob Storage doesn't have explicit directories, they're implicit
            // We'll create a marker blob to represent the directory
            if (directory.PathElements.Length > 1)
            {
                var markerBlobName = ToContainerKey(directory) + ".directory";
                var blobClient = containerClient.GetBlobClient(markerBlobName);
                
                if (!blobClient.Exists())
                {
                    using var stream = new MemoryStream(Array.Empty<byte>());
                    blobClient.Upload(stream, overwrite: false);
                }
            }

            InvalidateCache(directory.ToString());
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public override bool CreateFile(BlobStorePath file)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        try
        {
            // Ensure parent directory exists
            var parentPath = file.ParentPath;
            if (parentPath != null && parentPath is BlobStorePath blobParentPath)
            {
                CreateDirectory(blobParentPath);
            }

            // Create an empty blob
            var containerClient = _blobServiceClient.GetBlobContainerClient(file.Container);
            var blobName = ToBlobKey(file, 0);
            var blobClient = containerClient.GetBlobClient(blobName);
            
            using var stream = new MemoryStream(Array.Empty<byte>());
            blobClient.Upload(stream, overwrite: false);

            InvalidateCache(file.ToString());
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    // Helper methods will be added in the next part due to line limit
    private List<BlobItem> GetBlobs(BlobStorePath file)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(file.Container);
        var prefix = ToBlobKeyPrefix(file);
        var pattern = new Regex($"^{Regex.Escape(prefix)}\\d+$");
        
        return containerClient.GetBlobs(prefix: prefix)
            .Where(blob => pattern.IsMatch(blob.Name))
            .OrderBy(blob => GetBlobNumber(blob.Name))
            .ToList();
    }

    private new string ToContainerKey(BlobStorePath path)
    {
        if (path.PathElements.Length <= 1)
            return string.Empty;
        
        var elements = path.PathElements.Skip(1).ToArray();
        return string.Join(BlobStorePath.Separator, elements) + BlobStorePath.Separator;
    }

    private new string ToBlobKeyPrefix(BlobStorePath file)
    {
        if (file.PathElements.Length <= 1)
            return file.Identifier + NumberSuffixSeparator;
        
        var elements = file.PathElements.Skip(1).ToArray();
        return string.Join(BlobStorePath.Separator, elements) + NumberSuffixSeparator;
    }

    private new string ToBlobKey(BlobStorePath file, long blobNumber)
    {
        return ToBlobKeyPrefix(file) + blobNumber;
    }

    private long GetBlobNumber(string blobName)
    {
        var lastDot = blobName.LastIndexOf(NumberSuffixSeparatorChar);
        if (lastDot > 0 && long.TryParse(blobName.Substring(lastDot + 1), out var number))
        {
            return number;
        }
        return 0;
    }

    private string GetFileNameFromBlobName(string blobName, string prefix)
    {
        if (!blobName.StartsWith(prefix))
            return string.Empty;
        
        var fileName = blobName.Substring(prefix.Length);
        var lastDot = fileName.LastIndexOf(NumberSuffixSeparatorChar);
        if (lastDot > 0)
        {
            fileName = fileName.Substring(0, lastDot);
        }
        
        return fileName;
    }

    private void InvalidateCache(string key)
    {
        if (!_configuration.UseCache)
            return;

        lock (_cacheLock)
        {
            _containerExistsCache.Remove(key);
            _blobExistsCache.Remove(key);
            _blobSizeCache.Remove(key);
        }
    }

    public override bool DeleteFile(BlobStorePath file)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        try
        {
            var blobs = GetBlobs(file);
            var containerClient = _blobServiceClient.GetBlobContainerClient(file.Container);

            foreach (var blob in blobs)
            {
                var blobClient = containerClient.GetBlobClient(blob.Name);
                blobClient.DeleteIfExists();
            }

            InvalidateCache(file.ToString());
            return true;
        }
        catch (RequestFailedException)
        {
            return false;
        }
    }

    public override byte[] ReadData(BlobStorePath file, long offset, long length)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        try
        {
            var blobs = GetBlobs(file);
            if (!blobs.Any())
            {
                return Array.Empty<byte>();
            }

            var totalSize = blobs.Sum(blob => blob.Properties.ContentLength ?? 0);
            if (offset >= totalSize)
            {
                return Array.Empty<byte>();
            }

            var actualLength = length == -1 ? totalSize - offset : Math.Min(length, totalSize - offset);
            var result = new byte[actualLength];
            var resultOffset = 0;
            var currentOffset = 0L;

            var containerClient = _blobServiceClient.GetBlobContainerClient(file.Container);

            foreach (var blob in blobs)
            {
                var blobSize = blob.Properties.ContentLength ?? 0;
                var blobEndOffset = currentOffset + blobSize;

                if (offset < blobEndOffset && resultOffset < actualLength)
                {
                    var blobClient = containerClient.GetBlobClient(blob.Name);
                    var blobStartOffset = Math.Max(0, offset - currentOffset);
                    var blobReadLength = Math.Min(blobSize - blobStartOffset, actualLength - resultOffset);

                    if (blobReadLength > 0)
                    {
                        var blobData = new byte[blobReadLength];
                        using var stream = new MemoryStream(blobData);

                        // Download the specific range of the blob
                        var response = blobClient.DownloadStreaming(new BlobDownloadOptions
                        {
                            Range = new global::Azure.HttpRange(blobStartOffset, blobReadLength)
                        });

                        response.Value.Content.CopyTo(stream);
                        Array.Copy(blobData, 0, result, resultOffset, blobReadLength);
                        resultOffset += (int)blobReadLength;
                    }
                }

                currentOffset = blobEndOffset;
                if (resultOffset >= actualLength)
                    break;
            }

            return result;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return Array.Empty<byte>();
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
        file.Validate(_pathValidator);

        try
        {
            // Ensure parent directory exists
            var parentPath = file.ParentPath;
            if (parentPath != null && parentPath is BlobStorePath blobParentPath)
            {
                CreateDirectory(blobParentPath);
            }

            // Delete existing blobs for this file
            DeleteFile(file);

            var containerClient = _blobServiceClient.GetBlobContainerClient(file.Container);
            var totalSize = sourceBuffers.Sum(buffer => buffer.Length);
            var allData = new byte[totalSize];
            var position = 0;

            // Combine all source buffers
            foreach (var buffer in sourceBuffers)
            {
                Array.Copy(buffer, 0, allData, position, buffer.Length);
                position += buffer.Length;
            }

            // Split data into blobs based on max blob size
            var blobNumber = 0L;
            var dataOffset = 0;
            var remainingData = allData.Length;

            while (remainingData > 0)
            {
                var blobSize = (int)Math.Min(remainingData, _configuration.MaxBlobSize);
                var blobData = new byte[blobSize];
                Array.Copy(allData, dataOffset, blobData, 0, blobSize);

                var blobName = ToBlobKey(file, blobNumber);
                var blobClient = containerClient.GetBlobClient(blobName);

                using var stream = new MemoryStream(blobData);
                blobClient.Upload(stream, overwrite: true);

                dataOffset += blobSize;
                remainingData -= blobSize;
                blobNumber++;
            }

            InvalidateCache(file.ToString());
            return totalSize;
        }
        catch (RequestFailedException)
        {
            return 0;
        }
    }

    public override void MoveFile(BlobStorePath sourceFile, BlobStorePath targetFile)
    {
        EnsureNotDisposed();
        sourceFile.Validate(_pathValidator);
        targetFile.Validate(_pathValidator);

        // Azure Blob Storage doesn't have a native move operation
        // We need to copy and then delete
        CopyFile(sourceFile, targetFile, 0, -1);
        DeleteFile(sourceFile);
    }

    public override long CopyFile(BlobStorePath sourceFile, BlobStorePath targetFile, long offset, long length)
    {
        EnsureNotDisposed();
        sourceFile.Validate(_pathValidator);
        targetFile.Validate(_pathValidator);

        try
        {
            var data = ReadData(sourceFile, offset, length);
            return WriteData(targetFile, new[] { data });
        }
        catch (RequestFailedException)
        {
            return 0;
        }
    }

    public override void TruncateFile(BlobStorePath file, long newLength)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        try
        {
            if (newLength == 0)
            {
                DeleteFile(file);
                CreateFile(file);
                return;
            }

            var data = ReadData(file, 0, newLength);
            WriteData(file, new[] { data });
        }
        catch (RequestFailedException)
        {
            // Ignore truncation errors
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_cacheLock)
            {
                _containerExistsCache.Clear();
                _blobExistsCache.Clear();
                _blobSizeCache.Clear();
            }
        }
        base.Dispose(disposing);
    }
}
