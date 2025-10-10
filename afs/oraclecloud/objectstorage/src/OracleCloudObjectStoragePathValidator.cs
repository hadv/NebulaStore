using System.Text.RegularExpressions;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Afs.Blobstore.Types;

namespace NebulaStore.Afs.OracleCloud.ObjectStorage;

/// <summary>
/// Path validator for Oracle Cloud Infrastructure Object Storage.
/// Validates bucket names and object names according to OCI naming rules.
/// </summary>
public interface IOracleCloudObjectStoragePathValidator : IAfsPathValidator
{
    /// <summary>
    /// Creates a new Oracle Cloud Object Storage path validator.
    /// </summary>
    /// <returns>A new path validator instance</returns>
    static IOracleCloudObjectStoragePathValidator New() => new OracleCloudObjectStoragePathValidator();

    /// <summary>
    /// Validates a bucket name according to OCI naming rules.
    /// </summary>
    /// <param name="bucketName">The bucket name to validate</param>
    void ValidateBucketName(string bucketName);

    /// <summary>
    /// Validates an object name according to OCI naming rules.
    /// </summary>
    /// <param name="objectName">The object name to validate</param>
    void ValidateObjectName(string objectName);
}

/// <summary>
/// Default implementation of Oracle Cloud Object Storage path validator.
/// </summary>
internal class OracleCloudObjectStoragePathValidator : IOracleCloudObjectStoragePathValidator
{
    // OCI bucket naming rules:
    // - Must be between 1 and 256 characters
    // - Can contain lowercase letters, numbers, hyphens, underscores, and periods
    // - Must start with a letter or number
    // - Cannot contain two consecutive periods
    private static readonly Regex BucketNameRegex = new(
        @"^[a-z0-9][a-z0-9\-_.]{0,254}[a-z0-9]$|^[a-z0-9]$",
        RegexOptions.Compiled);

    private static readonly Regex ConsecutivePeriodsRegex = new(
        @"\.\.",
        RegexOptions.Compiled);

    // OCI object naming rules:
    // - Can be up to 1024 characters
    // - Can contain any UTF-8 characters except null
    // - Forward slashes (/) are used as path separators
    private const int MaxObjectNameLength = 1024;
    private const int MaxBucketNameLength = 256;
    private const int MinBucketNameLength = 1;

    public void Validate(IAfsPath path)
    {
        if (path is BlobStorePath blobPath)
        {
            ValidateContainer(blobPath.Container);

            // Validate the object path if it exists (skip container)
            if (blobPath.PathElements.Length > 1)
            {
                var objectPath = string.Join(BlobStorePath.Separator, blobPath.PathElements.Skip(1));
                if (!string.IsNullOrEmpty(objectPath))
                {
                    ValidateBlob(objectPath);
                }
            }
        }
        else
        {
            throw new ArgumentException("Path must be a BlobStorePath for OCI validation", nameof(path));
        }
    }

    public void ValidateBucketName(string bucketName)
    {
        ValidateContainer(bucketName);
    }

    public void ValidateObjectName(string objectName)
    {
        ValidateBlob(objectName);
    }

    private void ValidateContainer(string container)
    {
        if (string.IsNullOrEmpty(container))
        {
            throw new ArgumentException("Bucket name cannot be null or empty", nameof(container));
        }

        if (container.Length < MinBucketNameLength || container.Length > MaxBucketNameLength)
        {
            throw new ArgumentException(
                $"Bucket name must be between {MinBucketNameLength} and {MaxBucketNameLength} characters",
                nameof(container));
        }

        if (!BucketNameRegex.IsMatch(container))
        {
            throw new ArgumentException(
                "Bucket name must start and end with a letter or number, and can only contain lowercase letters, numbers, hyphens, underscores, and periods",
                nameof(container));
        }

        if (ConsecutivePeriodsRegex.IsMatch(container))
        {
            throw new ArgumentException(
                "Bucket name cannot contain consecutive periods",
                nameof(container));
        }
    }

    private void ValidateBlob(string blob)
    {
        if (string.IsNullOrEmpty(blob))
        {
            throw new ArgumentException("Object name cannot be null or empty", nameof(blob));
        }

        if (blob.Length > MaxObjectNameLength)
        {
            throw new ArgumentException(
                $"Object name cannot exceed {MaxObjectNameLength} characters",
                nameof(blob));
        }

        // Check for null characters
        if (blob.Contains('\0'))
        {
            throw new ArgumentException(
                "Object name cannot contain null characters",
                nameof(blob));
        }

        // OCI doesn't allow object names that are just "." or ".."
        if (blob == "." || blob == "..")
        {
            throw new ArgumentException(
                "Object name cannot be '.' or '..'",
                nameof(blob));
        }

        // Check for leading/trailing whitespace (not recommended)
        if (blob != blob.Trim())
        {
            throw new ArgumentException(
                "Object name should not have leading or trailing whitespace",
                nameof(blob));
        }
    }
}

