using System.Text.RegularExpressions;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Aws.S3;

/// <summary>
/// Path validator for AWS S3 bucket names and object keys.
/// Implements AWS S3 naming conventions and restrictions.
/// </summary>
public interface IAwsS3PathValidator : BlobStorePath.IValidator
{
    /// <summary>
    /// Creates a new AWS S3 path validator.
    /// </summary>
    /// <returns>A new path validator instance</returns>
    static IAwsS3PathValidator New() => new AwsS3PathValidator();
}

/// <summary>
/// Default implementation of AWS S3 path validator.
/// </summary>
public class AwsS3PathValidator : IAwsS3PathValidator
{
    private static readonly Regex BucketNameRegex = new(@"^[a-z0-9\.\-]*$", RegexOptions.Compiled);
    private static readonly Regex BucketStartRegex = new(@"^[a-z0-9]", RegexOptions.Compiled);
    private static readonly Regex IpAddressRegex = new(@"^((0|1\d?\d?|2[0-4]?\d?|25[0-5]?|[3-9]\d?)\.){3}(0|1\d?\d?|2[0-4]?\d?|25[0-5]?|[3-9]\d?)$", RegexOptions.Compiled);

    /// <summary>
    /// Validates the specified blob store path for AWS S3 compliance.
    /// </summary>
    /// <param name="path">The path to validate</param>
    /// <exception cref="ArgumentException">Thrown when the path is invalid</exception>
    public void Validate(BlobStorePath path)
    {
        ValidateBucketName(path.Container);
        ValidateObjectKey(path);
    }

    /// <summary>
    /// Validates AWS S3 bucket name according to AWS naming rules.
    /// </summary>
    /// <param name="bucketName">The bucket name to validate</param>
    /// <exception cref="ArgumentException">Thrown when the bucket name is invalid</exception>
    private static void ValidateBucketName(string bucketName)
    {
        var length = bucketName.Length;
        if (length < 3 || length > 63)
        {
            throw new ArgumentException("Bucket name must be between 3 and 63 characters long");
        }

        if (!BucketNameRegex.IsMatch(bucketName))
        {
            throw new ArgumentException("Bucket name can contain only lowercase letters, numbers, periods (.) and dashes (-)");
        }

        if (!BucketStartRegex.IsMatch(bucketName.Substring(0, 1)))
        {
            throw new ArgumentException("Bucket name must begin with a lowercase letter or a number");
        }

        if (bucketName.EndsWith("-"))
        {
            throw new ArgumentException("Bucket name must not end with a dash (-)");
        }

        if (bucketName.Contains(".."))
        {
            throw new ArgumentException("Bucket name cannot have consecutive periods (..)");
        }

        if (bucketName.Contains(".-") || bucketName.Contains("-."))
        {
            throw new ArgumentException("Bucket name cannot have dashes adjacent to periods (.- or -)");
        }

        if (IpAddressRegex.IsMatch(bucketName))
        {
            throw new ArgumentException("Bucket name must not be in an IP address style");
        }

        if (bucketName.StartsWith("xn--"))
        {
            throw new ArgumentException("Bucket names must not start with 'xn--'");
        }
    }

    /// <summary>
    /// Validates AWS S3 object key according to AWS naming rules.
    /// </summary>
    /// <param name="path">The path containing the object key to validate</param>
    /// <exception cref="ArgumentException">Thrown when the object key is invalid</exception>
    private static void ValidateObjectKey(BlobStorePath path)
    {
        var objectKey = path.ToString();
        
        // Remove container part to get just the object key
        if (objectKey.StartsWith(path.Container + "/"))
        {
            objectKey = objectKey.Substring(path.Container.Length + 1);
        }

        if (string.IsNullOrEmpty(objectKey))
        {
            return; // Empty object key is valid for root directory
        }

        if (objectKey.Length > 1024)
        {
            throw new ArgumentException("Object key cannot exceed 1024 characters");
        }

        // Check for invalid characters (basic validation)
        if (objectKey.Contains('\0'))
        {
            throw new ArgumentException("Object key cannot contain null characters");
        }
    }
}
