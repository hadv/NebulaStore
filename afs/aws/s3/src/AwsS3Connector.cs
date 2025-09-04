using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Afs.Blobstore.Types;

namespace NebulaStore.Afs.Aws.S3;

/// <summary>
/// AWS S3 implementation of IBlobStoreConnector.
/// Stores blobs as objects in AWS S3 buckets.
/// </summary>
/// <remarks>
/// This connector stores files as numbered blob objects in S3 buckets.
/// Each blob can be up to 5TB (S3 object size limit) and larger files
/// are split across multiple objects.
/// 
/// First create an S3 client and configuration:
/// <code>
/// var config = AwsS3Configuration.New()
///     .SetCredentials("access-key", "secret-key")
///     .SetRegion(RegionEndpoint.USEast1);
/// 
/// var s3Client = new AmazonS3Client(config.AccessKeyId, config.SecretAccessKey, config.Region);
/// var connector = AwsS3Connector.New(s3Client, config);
/// var fileSystem = BlobStoreFileSystem.New(connector);
/// </code>
/// </remarks>
public class AwsS3Connector : BlobStoreConnectorBase
{
    private readonly IAmazonS3 _s3Client;
    private readonly AwsS3Configuration _configuration;
    private readonly IAwsS3PathValidator _pathValidator;

    /// <summary>
    /// Initializes a new instance of the AwsS3Connector class.
    /// </summary>
    /// <param name="s3Client">The S3 client</param>
    /// <param name="configuration">The S3 configuration</param>
    /// <param name="pathValidator">The path validator</param>
    private AwsS3Connector(IAmazonS3 s3Client, AwsS3Configuration configuration, IAwsS3PathValidator pathValidator)
    {
        _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _pathValidator = pathValidator ?? throw new ArgumentNullException(nameof(pathValidator));
    }

    /// <summary>
    /// Creates a new AWS S3 connector.
    /// </summary>
    /// <param name="s3Client">The S3 client</param>
    /// <param name="configuration">The S3 configuration</param>
    /// <returns>A new S3 connector instance</returns>
    public static AwsS3Connector New(IAmazonS3 s3Client, AwsS3Configuration? configuration = null)
    {
        configuration ??= AwsS3Configuration.New();
        var pathValidator = IAwsS3PathValidator.New();
        return new AwsS3Connector(s3Client, configuration, pathValidator);
    }

    /// <summary>
    /// Creates a new AWS S3 connector with caching enabled.
    /// </summary>
    /// <param name="s3Client">The S3 client</param>
    /// <param name="configuration">The S3 configuration</param>
    /// <returns>A new S3 connector instance with caching</returns>
    public static AwsS3Connector NewWithCaching(IAmazonS3 s3Client, AwsS3Configuration? configuration = null)
    {
        configuration ??= AwsS3Configuration.New().SetUseCache(true);
        var pathValidator = IAwsS3PathValidator.New();
        return new AwsS3Connector(s3Client, configuration, pathValidator);
    }

    public override long GetFileSize(BlobStorePath file)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        try
        {
            var blobs = GetBlobs(file);
            return blobs.Sum(blob => blob.Size);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
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

            var request = new ListObjectsV2Request
            {
                BucketName = directory.Container,
                Prefix = containerKey,
                Delimiter = BlobStorePath.Separator,
                MaxKeys = 1
            };

            var response = _s3Client.ListObjectsV2Async(request).Result;
            return response.S3Objects.Count > 0 || response.CommonPrefixes.Count > 0;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
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
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public override void VisitChildren(BlobStorePath directory, IBlobStorePathVisitor visitor)
    {
        EnsureNotDisposed();
        directory.Validate(_pathValidator);

        var childKeys = GetChildKeys(directory);
        var prefix = GetChildKeysPrefix(directory);

        foreach (var childKey in childKeys)
        {
            if (childKey.EndsWith(BlobStorePath.Separator))
            {
                // It's a directory
                var directoryName = childKey.Substring(prefix.Length).TrimEnd(BlobStorePath.Separator[0]);
                visitor.VisitDirectory(directory, directoryName);
            }
            else
            {
                // It's a file
                var fileName = childKey.Substring(prefix.Length);
                // Remove blob number suffix if present
                var dotIndex = fileName.LastIndexOf(NumberSuffixSeparator);
                if (dotIndex > 0 && long.TryParse(fileName.Substring(dotIndex + 1), out _))
                {
                    fileName = fileName.Substring(0, dotIndex);
                }
                visitor.VisitFile(directory, fileName);
            }
        }
    }

    public override bool IsEmpty(BlobStorePath directory)
    {
        EnsureNotDisposed();
        directory.Validate(_pathValidator);

        var request = new ListObjectsV2Request
        {
            BucketName = directory.Container,
            Prefix = GetChildKeysPrefix(directory),
            MaxKeys = 1
        };

        var response = _s3Client.ListObjectsV2Async(request).Result;
        return response.S3Objects.Count == 0;
    }

    public override bool CreateDirectory(BlobStorePath directory)
    {
        EnsureNotDisposed();
        directory.Validate(_pathValidator);

        var containerKey = GetContainerKey(directory);
        if (string.IsNullOrEmpty(containerKey) || containerKey == BlobStorePath.Separator)
        {
            return true;
        }

        var request = new PutObjectRequest
        {
            BucketName = directory.Container,
            Key = containerKey,
            ContentBody = string.Empty
        };

        _s3Client.PutObjectAsync(request).Wait();
        return true;
    }

    public override bool CreateFile(BlobStorePath file)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        // S3 doesn't require explicit file creation
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

    private List<S3Object> GetBlobs(BlobStorePath file)
    {
        var prefix = GetBlobKeyPrefix(file);
        var pattern = new Regex(GetBlobKeyRegex(prefix));
        var blobs = new List<S3Object>();
        string? continuationToken = null;

        do
        {
            var request = new ListObjectsV2Request
            {
                BucketName = file.Container,
                Prefix = prefix,
                ContinuationToken = continuationToken
            };

            var response = _s3Client.ListObjectsV2Async(request).Result;
            blobs.AddRange(response.S3Objects);
            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        }
        while (continuationToken != null);

        return blobs
            .Where(obj => pattern.IsMatch(obj.Key))
            .OrderBy(obj => GetBlobNumber(obj.Key))
            .ToList();
    }

    private IEnumerable<string> GetChildKeys(BlobStorePath directory)
    {
        var childKeys = new HashSet<string>();
        var prefix = GetChildKeysPrefix(directory);
        string? continuationToken = null;

        do
        {
            var request = new ListObjectsV2Request
            {
                BucketName = directory.Container,
                Prefix = prefix,
                Delimiter = BlobStorePath.Separator,
                ContinuationToken = continuationToken
            };

            var response = _s3Client.ListObjectsV2Async(request).Result;
            
            // Add directories
            childKeys.UnionWith(response.CommonPrefixes);
            
            // Add files
            childKeys.UnionWith(response.S3Objects.Select(obj => obj.Key));
            
            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        }
        while (continuationToken != null);

        return childKeys.Where(path => !path.Equals(prefix));
    }

    private bool DeleteBlobs(BlobStorePath file, List<S3Object> blobs)
    {
        const int batchSize = 1000; // S3 delete limit
        var success = true;

        for (int i = 0; i < blobs.Count; i += batchSize)
        {
            var batch = blobs.Skip(i).Take(batchSize).ToList();
            if (!DeleteBlobsBatch(file, batch))
            {
                success = false;
            }
        }

        return success;
    }

    private bool DeleteBlobsBatch(BlobStorePath file, List<S3Object> blobs)
    {
        var objects = blobs.Select(obj => new KeyVersion { Key = obj.Key }).ToList();
        var request = new DeleteObjectsRequest
        {
            BucketName = file.Container,
            Objects = objects
        };

        var response = _s3Client.DeleteObjectsAsync(request).Result;
        return response.DeletedObjects.Count == blobs.Count;
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

    public override byte[] ReadData(BlobStorePath file, long offset, long length)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        var blobs = GetBlobs(file);
        if (!blobs.Any())
        {
            return Array.Empty<byte>();
        }

        var result = new List<byte>();
        long currentOffset = 0;
        long remainingLength = length == -1 ? long.MaxValue : length;

        foreach (var blob in blobs)
        {
            if (remainingLength <= 0)
                break;

            var blobSize = blob.Size;

            if (currentOffset + blobSize <= offset)
            {
                currentOffset += blobSize;
                continue;
            }

            var blobOffset = Math.Max(0, offset - currentOffset);
            var blobLength = Math.Min(blobSize - blobOffset, remainingLength);

            var request = new GetObjectRequest
            {
                BucketName = file.Container,
                Key = blob.Key,
                ByteRange = new ByteRange(blobOffset, blobOffset + blobLength - 1)
            };

            using var response = _s3Client.GetObjectAsync(request).Result;
            using var stream = response.ResponseStream;
            var buffer = new byte[blobLength];
            stream.Read(buffer, 0, (int)blobLength);
            result.AddRange(buffer);

            currentOffset += blobSize;
            remainingLength -= blobLength;
            offset = Math.Max(offset, currentOffset);
        }

        return result.ToArray();
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

        // Ensure parent directory exists
        var parentPath = file.ParentPath;
        if (parentPath != null && parentPath is BlobStorePath blobParentPath)
        {
            CreateDirectory(blobParentPath);
        }

        var nextBlobNumber = GetNextBlobNumber(file);
        var totalSize = sourceBuffers.Sum(buffer => buffer.Length);
        var allData = new byte[totalSize];
        var position = 0;

        foreach (var buffer in sourceBuffers)
        {
            Array.Copy(buffer, 0, allData, position, buffer.Length);
            position += buffer.Length;
        }

        var blobKey = GetBlobKey(file, nextBlobNumber);
        var request = new PutObjectRequest
        {
            BucketName = file.Container,
            Key = blobKey,
            InputStream = new MemoryStream(allData),
            ContentLength = totalSize
        };

        _s3Client.PutObjectAsync(request).Wait();
        return totalSize;
    }

    public override void MoveFile(BlobStorePath sourceFile, BlobStorePath targetFile)
    {
        EnsureNotDisposed();
        sourceFile.Validate(_pathValidator);
        targetFile.Validate(_pathValidator);

        CopyFile(sourceFile, targetFile, 0, -1);
        DeleteFile(sourceFile);
    }

    public override long CopyFile(BlobStorePath sourceFile, BlobStorePath targetFile, long offset, long length)
    {
        EnsureNotDisposed();
        sourceFile.Validate(_pathValidator);
        targetFile.Validate(_pathValidator);

        var data = ReadData(sourceFile, offset, length);
        WriteData(targetFile, new[] { data });
        return data.Length;
    }

    public override void TruncateFile(BlobStorePath file, long newLength)
    {
        EnsureNotDisposed();
        file.Validate(_pathValidator);

        if (newLength < 0)
        {
            throw new ArgumentException("New length cannot be negative", nameof(newLength));
        }

        var data = ReadData(file, 0, newLength);
        DeleteFile(file);
        if (data.Length > 0)
        {
            WriteData(file, new[] { data });
        }
    }

    private long GetNextBlobNumber(BlobStorePath file)
    {
        var blobs = GetBlobs(file);
        return blobs.Any() ? blobs.Max(blob => GetBlobNumber(blob.Key)) + 1 : 0;
    }

    private string GetBlobKey(BlobStorePath file, long blobNumber)
    {
        return ToBlobKey(file, blobNumber);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _s3Client?.Dispose();
        }
        base.Dispose(disposing);
    }
}
