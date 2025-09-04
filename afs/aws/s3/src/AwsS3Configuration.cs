using Amazon;
using Amazon.S3;

namespace NebulaStore.Afs.Aws.S3;

/// <summary>
/// Configuration for AWS S3 connections.
/// </summary>
public class AwsS3Configuration
{
    /// <summary>
    /// Gets or sets the AWS access key ID.
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// Gets or sets the AWS secret access key.
    /// </summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>
    /// Gets or sets the AWS session token (for temporary credentials).
    /// </summary>
    public string? SessionToken { get; set; }

    /// <summary>
    /// Gets or sets the AWS region endpoint.
    /// </summary>
    public RegionEndpoint? Region { get; set; }

    /// <summary>
    /// Gets or sets the service URL for S3-compatible services.
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Gets or sets whether to force path style addressing.
    /// </summary>
    public bool ForcePathStyle { get; set; }

    /// <summary>
    /// Gets or sets whether to use HTTPS.
    /// </summary>
    public bool UseHttps { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable caching.
    /// </summary>
    public bool UseCache { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for S3 operations in milliseconds.
    /// </summary>
    public int TimeoutMilliseconds { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Creates a new AWS S3 configuration with default values.
    /// </summary>
    /// <returns>A new configuration instance</returns>
    public static AwsS3Configuration New() => new();

    /// <summary>
    /// Sets the AWS credentials.
    /// </summary>
    /// <param name="accessKeyId">The access key ID</param>
    /// <param name="secretAccessKey">The secret access key</param>
    /// <param name="sessionToken">The session token (optional)</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AwsS3Configuration SetCredentials(string accessKeyId, string secretAccessKey, string? sessionToken = null)
    {
        AccessKeyId = accessKeyId;
        SecretAccessKey = secretAccessKey;
        SessionToken = sessionToken;
        return this;
    }

    /// <summary>
    /// Sets the AWS region.
    /// </summary>
    /// <param name="region">The AWS region</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AwsS3Configuration SetRegion(RegionEndpoint region)
    {
        Region = region;
        return this;
    }

    /// <summary>
    /// Sets the service URL for S3-compatible services.
    /// </summary>
    /// <param name="serviceUrl">The service URL</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AwsS3Configuration SetServiceUrl(string serviceUrl)
    {
        ServiceUrl = serviceUrl;
        return this;
    }

    /// <summary>
    /// Sets whether to force path style addressing.
    /// </summary>
    /// <param name="forcePathStyle">True to force path style</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AwsS3Configuration SetForcePathStyle(bool forcePathStyle)
    {
        ForcePathStyle = forcePathStyle;
        return this;
    }

    /// <summary>
    /// Sets whether to use HTTPS.
    /// </summary>
    /// <param name="useHttps">True to use HTTPS</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AwsS3Configuration SetUseHttps(bool useHttps)
    {
        UseHttps = useHttps;
        return this;
    }

    /// <summary>
    /// Sets whether to enable caching.
    /// </summary>
    /// <param name="useCache">True to enable caching</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AwsS3Configuration SetUseCache(bool useCache)
    {
        UseCache = useCache;
        return this;
    }

    /// <summary>
    /// Sets the timeout for S3 operations.
    /// </summary>
    /// <param name="timeoutMilliseconds">The timeout in milliseconds</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AwsS3Configuration SetTimeout(int timeoutMilliseconds)
    {
        TimeoutMilliseconds = timeoutMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of retry attempts.
    /// </summary>
    /// <param name="maxRetryAttempts">The maximum retry attempts</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AwsS3Configuration SetMaxRetryAttempts(int maxRetryAttempts)
    {
        MaxRetryAttempts = maxRetryAttempts;
        return this;
    }
}
