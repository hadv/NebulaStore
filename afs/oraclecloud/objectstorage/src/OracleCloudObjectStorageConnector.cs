using System.Text;
using System.Text.RegularExpressions;
using Oci.Common.Auth;
using Oci.ObjectstorageService;
using Oci.ObjectstorageService.Models;
using Oci.ObjectstorageService.Requests;
using Oci.ObjectstorageService.Responses;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Afs.Blobstore.Types;

namespace NebulaStore.Afs.OracleCloud.ObjectStorage;

/// <summary>
/// Oracle Cloud Infrastructure Object Storage implementation of IBlobStoreConnector.
/// Stores blobs as objects in OCI Object Storage buckets.
/// </summary>
/// <remarks>
/// This connector stores files as numbered blob objects in OCI buckets.
/// Each blob can be up to 50 GiB (OCI object size limit) and larger files
/// are split across multiple objects.
/// 
/// First create an OCI Object Storage client and configuration:
/// <code>
/// var config = OracleCloudObjectStorageConfiguration.New()
///     .SetConfigFile("~/.oci/config", "DEFAULT")
///     .SetRegion("us-ashburn-1")
///     .SetUseCache(true);
/// 
/// var connector = OracleCloudObjectStorageConnector.New(config);
/// var fileSystem = BlobStoreFileSystem.New(connector);
/// </code>
/// </remarks>
public class OracleCloudObjectStorageConnector : BlobStoreConnectorBase
{
    private readonly ObjectStorageClient _client;
    private readonly OracleCloudObjectStorageConfiguration _configuration;
    private readonly IOracleCloudObjectStoragePathValidator _pathValidator;
    private string? _namespace;

    /// <summary>
    /// Initializes a new instance of the OracleCloudObjectStorageConnector class.
    /// </summary>
    /// <param name="client">The OCI Object Storage client</param>
    /// <param name="configuration">The OCI configuration</param>
    /// <param name="pathValidator">The path validator</param>
    private OracleCloudObjectStorageConnector(
        ObjectStorageClient client,
        OracleCloudObjectStorageConfiguration configuration,
        IOracleCloudObjectStoragePathValidator pathValidator)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _pathValidator = pathValidator ?? throw new ArgumentNullException(nameof(pathValidator));
    }

    /// <summary>
    /// Creates a new Oracle Cloud Object Storage connector.
    /// </summary>
    /// <param name="configuration">The OCI configuration</param>
    /// <returns>A new OCI connector instance</returns>
    public static OracleCloudObjectStorageConnector New(OracleCloudObjectStorageConfiguration configuration)
    {
        configuration?.Validate();
        var authProvider = configuration!.CreateAuthenticationProvider();
        var client = new ObjectStorageClient(authProvider);
        
        if (!string.IsNullOrEmpty(configuration.Region))
        {
            client.SetRegion(configuration.Region);
        }

        // Note: Custom endpoint configuration would need to be set via ClientConfiguration
        // if needed in the future

        var pathValidator = IOracleCloudObjectStoragePathValidator.New();
        return new OracleCloudObjectStorageConnector(client, configuration, pathValidator);
    }

    /// <summary>
    /// Gets the OCI Object Storage namespace.
    /// </summary>
    /// <returns>The namespace</returns>
    private string GetNamespace()
    {
        if (_namespace != null)
        {
            return _namespace;
        }

        if (!string.IsNullOrEmpty(_configuration.Namespace))
        {
            _namespace = _configuration.Namespace;
            return _namespace;
        }

        // Auto-detect namespace
        var request = new GetNamespaceRequest();
        var response = _client.GetNamespace(request).Result;
        _namespace = response.Value;
        return _namespace;
    }

    public override long GetFileSize(BlobStorePath file)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        try
        {
            var blobs = GetBlobs(file);
            return blobs.Sum(blob => blob.Size ?? 0);
        }
        catch (Exception ex) when (IsNotFoundException(ex))
        {
            return 0;
        }
    }

    public override bool DirectoryExists(BlobStorePath directory)
    {
        EnsureNotDisposed();
        directory.Validate(_pathValidator);

        try
        {
            var containerKey = GetContainerKey(directory);
            if (string.IsNullOrEmpty(containerKey) || containerKey == BlobStorePath.Separator)
            {
                return true;
            }

            // Check if any objects exist with this prefix
            var request = new ListObjectsRequest
            {
                NamespaceName = GetNamespace(),
                BucketName = directory.Container,
                Prefix = containerKey,
                Limit = 1
            };

            var response = _client.ListObjects(request).Result;
            return response.ListObjects.Objects.Any();
        }
        catch (Exception ex) when (IsNotFoundException(ex))
        {
            return false;
        }
    }

    public override bool FileExists(BlobStorePath file)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        try
        {
            var blobs = GetBlobs(file);
            return blobs.Any();
        }
        catch (Exception ex) when (IsNotFoundException(ex))
        {
            return false;
        }
    }

    public override void VisitChildren(BlobStorePath directory, IBlobStorePathVisitor visitor)
    {
        EnsureNotDisposed();
        directory.Validate(_pathValidator);

        if (visitor == null)
        {
            throw new ArgumentNullException(nameof(visitor));
        }

        var childKeys = GetChildKeys(directory);
        var prefix = GetChildKeysPrefix(directory);

        foreach (var key in childKeys)
        {
            if (IsDirectoryKey(key))
            {
                var dirName = key.Substring(prefix.Length).TrimEnd('/');
                if (!string.IsNullOrEmpty(dirName))
                {
                    visitor.VisitDirectory(directory, dirName);
                }
            }
            else
            {
                var fileName = RemoveNumberSuffix(key.Substring(prefix.Length));
                if (!string.IsNullOrEmpty(fileName))
                {
                    visitor.VisitFile(directory, fileName);
                }
            }
        }
    }

    public override bool IsEmpty(BlobStorePath directory)
    {
        EnsureNotDisposed();
        directory.Validate(_pathValidator);

        try
        {
            var request = new ListObjectsRequest
            {
                NamespaceName = GetNamespace(),
                BucketName = directory.Container,
                Prefix = GetContainerKey(directory),
                Limit = 1
            };

            var response = _client.ListObjects(request).Result;
            return !response.ListObjects.Objects.Any();
        }
        catch (Exception ex) when (IsNotFoundException(ex))
        {
            return true;
        }
    }

    public override bool CreateDirectory(BlobStorePath directory)
    {
        EnsureNotDisposed();
        directory.Validate(_pathValidator);

        // OCI Object Storage doesn't require explicit directory creation
        // Directories are implicit based on object key prefixes
        return true;
    }

    public override bool CreateFile(BlobStorePath file)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        // OCI Object Storage doesn't require explicit file creation
        return true;
    }

    public override bool DeleteFile(BlobStorePath file)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        var blobs = GetBlobs(file).ToList();
        if (!blobs.Any())
        {
            return false;
        }

        return DeleteBlobs(file, blobs);
    }

    private List<ObjectSummary> GetBlobs(BlobStorePath file)
    {
        var prefix = GetBlobKeyPrefix(file);
        var pattern = new Regex(GetBlobKeyRegex(prefix));
        var blobs = new List<ObjectSummary>();
        string? nextStartWith = null;

        do
        {
            var request = new ListObjectsRequest
            {
                NamespaceName = GetNamespace(),
                BucketName = file.Container,
                Prefix = prefix,
                Start = nextStartWith,
                Fields = "name,size"
            };

            var response = _client.ListObjects(request).Result;
            blobs.AddRange(response.ListObjects.Objects);
            nextStartWith = response.ListObjects.NextStartWith;
        }
        while (nextStartWith != null);

        return blobs
            .Where(obj => pattern.IsMatch(obj.Name))
            .OrderBy(obj => GetBlobNumber(obj.Name))
            .ToList();
    }

    private IEnumerable<string> GetChildKeys(BlobStorePath directory)
    {
        var childKeys = new HashSet<string>();
        var prefix = GetChildKeysPrefix(directory);
        string? nextStartWith = null;

        do
        {
            var request = new ListObjectsRequest
            {
                NamespaceName = GetNamespace(),
                BucketName = directory.Container,
                Prefix = prefix,
                Delimiter = BlobStorePath.Separator,
                Start = nextStartWith,
                Fields = "name"
            };

            var response = _client.ListObjects(request).Result;
            
            // Add directories (prefixes)
            if (response.ListObjects.Prefixes != null)
            {
                childKeys.UnionWith(response.ListObjects.Prefixes);
            }
            
            // Add files
            childKeys.UnionWith(response.ListObjects.Objects.Select(obj => obj.Name));
            
            nextStartWith = response.ListObjects.NextStartWith;
        }
        while (nextStartWith != null);

        return childKeys.Where(path => !path.Equals(prefix));
    }

    private bool DeleteBlobs(BlobStorePath file, List<ObjectSummary> blobs)
    {
        var success = true;

        foreach (var blob in blobs)
        {
            try
            {
                var request = new DeleteObjectRequest
                {
                    NamespaceName = GetNamespace(),
                    BucketName = file.Container,
                    ObjectName = blob.Name
                };

                _client.DeleteObject(request).Wait();
            }
            catch
            {
                success = false;
            }
        }

        return success;
    }

    private string GetContainerKey(BlobStorePath path)
    {
        return ToContainerKey(path);
    }

    private string GetChildKeysPrefix(BlobStorePath directory)
    {
        return ToContainerKey(directory);
    }

    private string GetBlobKeyPrefix(BlobStorePath file)
    {
        return ToBlobKeyPrefix(file);
    }

    private string GetBlobKeyRegex(string prefix)
    {
        return $"^{Regex.Escape(prefix)}\\d+$";
    }

    private long GetBlobNumber(string key)
    {
        var lastDot = key.LastIndexOf(NumberSuffixSeparatorChar);
        if (lastDot > 0 && long.TryParse(key.Substring(lastDot + 1), out var number))
        {
            return number;
        }
        return 0;
    }

    private bool IsNotFoundException(Exception ex)
    {
        return ex.Message.Contains("NotFound") || ex.Message.Contains("404");
    }

    public override byte[] ReadData(BlobStorePath file, long offset, long length)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        }

        if (length < 0)
        {
            length = GetFileSize(file) - offset;
        }

        if (length == 0)
        {
            return Array.Empty<byte>();
        }

        var buffer = new byte[length];
        var bytesRead = ReadData(file, buffer, offset, length);

        if (bytesRead < length)
        {
            Array.Resize(ref buffer, (int)bytesRead);
        }

        return buffer;
    }

    public override long ReadData(BlobStorePath file, byte[] targetBuffer, long offset, long length)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        if (targetBuffer == null)
        {
            throw new ArgumentNullException(nameof(targetBuffer));
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        }

        if (length < 0)
        {
            length = GetFileSize(file) - offset;
        }

        if (length == 0)
        {
            return 0;
        }

        var blobs = GetBlobs(file);
        long currentOffset = 0;
        long totalBytesRead = 0;
        int bufferPosition = 0;

        foreach (var blob in blobs)
        {
            var blobSize = blob.Size ?? 0;
            var blobEndOffset = currentOffset + blobSize;

            // Skip blobs before the requested offset
            if (blobEndOffset <= offset)
            {
                currentOffset = blobEndOffset;
                continue;
            }

            // Stop if we've read all requested data
            if (totalBytesRead >= length)
            {
                break;
            }

            // Calculate read range within this blob
            var blobReadOffset = Math.Max(0, offset - currentOffset);
            var remainingToRead = length - totalBytesRead;
            var blobReadLength = Math.Min(blobSize - blobReadOffset, remainingToRead);

            // Read from this blob
            var bytesRead = ReadBlobData(file, blob, targetBuffer, bufferPosition, blobReadOffset, blobReadLength);
            totalBytesRead += bytesRead;
            bufferPosition += (int)bytesRead;
            currentOffset = blobEndOffset;
        }

        return totalBytesRead;
    }

    private long ReadBlobData(
        BlobStorePath file,
        ObjectSummary blob,
        byte[] targetBuffer,
        int bufferOffset,
        long blobOffset,
        long length)
    {
        var request = new GetObjectRequest
        {
            NamespaceName = GetNamespace(),
            BucketName = file.Container,
            ObjectName = blob.Name
        };

        // Set range header for partial read
        if (blobOffset > 0 || length < (blob.Size ?? 0))
        {
            var rangeEnd = blobOffset + length - 1;
            request.Range = new Oci.Common.Model.Range
            {
                StartByte = blobOffset,
                EndByte = rangeEnd
            };
        }

        var response = _client.GetObject(request).Result;
        using var stream = response.InputStream;

        long totalRead = 0;
        int bytesRead;
        var tempBuffer = new byte[8192];

        while (totalRead < length && (bytesRead = stream.Read(tempBuffer, 0, (int)Math.Min(tempBuffer.Length, length - totalRead))) > 0)
        {
            Array.Copy(tempBuffer, 0, targetBuffer, bufferOffset + totalRead, bytesRead);
            totalRead += bytesRead;
        }

        return totalRead;
    }

    public override long WriteData(BlobStorePath file, IEnumerable<byte[]> sourceBuffers)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        if (sourceBuffers == null)
        {
            throw new ArgumentNullException(nameof(sourceBuffers));
        }

        // Delete existing blobs for this file
        var existingBlobs = GetBlobs(file).ToList();
        if (existingBlobs.Any())
        {
            DeleteBlobs(file, existingBlobs);
        }

        long totalBytesWritten = 0;
        long blobNumber = 0;
        var maxBlobSize = _configuration.MaxBlobSize;

        using var memoryStream = new MemoryStream();

        foreach (var buffer in sourceBuffers)
        {
            if (buffer == null || buffer.Length == 0)
            {
                continue;
            }

            memoryStream.Write(buffer, 0, buffer.Length);

            // Write blob if we've reached the max size
            while (memoryStream.Length >= maxBlobSize)
            {
                var bytesWritten = WriteBlob(file, blobNumber++, memoryStream, maxBlobSize);
                totalBytesWritten += bytesWritten;
            }
        }

        // Write remaining data
        if (memoryStream.Length > 0)
        {
            memoryStream.Position = 0;
            var bytesWritten = WriteBlob(file, blobNumber, memoryStream, memoryStream.Length);
            totalBytesWritten += bytesWritten;
        }

        return totalBytesWritten;
    }

    private long WriteBlob(BlobStorePath file, long blobNumber, MemoryStream sourceStream, long length)
    {
        var blobKey = ToBlobKey(file, blobNumber);
        var dataToWrite = new byte[length];
        sourceStream.Position = 0;
        sourceStream.Read(dataToWrite, 0, (int)length);

        using var uploadStream = new MemoryStream(dataToWrite);

        var request = new PutObjectRequest
        {
            NamespaceName = GetNamespace(),
            BucketName = file.Container,
            ObjectName = blobKey,
            ContentLength = length,
            PutObjectBody = uploadStream
        };

        _client.PutObject(request).Wait();

        // Remove written data from source stream
        var remaining = sourceStream.Length - length;
        if (remaining > 0)
        {
            var remainingData = new byte[remaining];
            sourceStream.Position = length;
            sourceStream.Read(remainingData, 0, (int)remaining);
            sourceStream.SetLength(0);
            sourceStream.Write(remainingData, 0, (int)remaining);
        }
        else
        {
            sourceStream.SetLength(0);
        }
        sourceStream.Position = 0;

        return length;
    }

    public override void MoveFile(BlobStorePath sourceFile, BlobStorePath targetFile)
    {
        EnsureNotDisposed();
        sourceFile.Validate(_pathValidator);
        targetFile.Validate(_pathValidator);

        // Copy then delete
        var fileSize = GetFileSize(sourceFile);
        CopyFile(sourceFile, targetFile, 0, fileSize);
        DeleteFile(sourceFile);
    }

    public override long CopyFile(BlobStorePath sourceFile, BlobStorePath targetFile, long offset, long length)
    {
        EnsureNotDisposed();
        sourceFile.Validate(_pathValidator);
        targetFile.Validate(_pathValidator);

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset cannot be negative");
        }

        if (length < 0)
        {
            length = GetFileSize(sourceFile) - offset;
        }

        if (length == 0)
        {
            return 0;
        }

        // Read data from source
        var data = ReadData(sourceFile, offset, length);

        // Write to target
        return WriteData(targetFile, new[] { data });
    }

    public override void TruncateFile(BlobStorePath file, long newLength)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        if (newLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newLength), "New length cannot be negative");
        }

        var currentSize = GetFileSize(file);

        if (newLength >= currentSize)
        {
            // No truncation needed
            return;
        }

        if (newLength == 0)
        {
            // Delete all blobs
            DeleteFile(file);
            return;
        }

        // Read the data up to newLength and rewrite
        var data = ReadData(file, 0, newLength);
        WriteData(file, new[] { data });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _client?.Dispose();
        }
        base.Dispose(disposing);
    }
}

