using Azure.Core;
using Azure.Storage.Blobs;

namespace NebulaStore.Afs.Azure.Storage;

/// <summary>
/// Configuration for Azure Blob Storage connections.
/// Provides settings for authentication, connection, and performance tuning.
/// </summary>
public class AzureStorageConfiguration
{
    /// <summary>
    /// Gets or sets the Azure storage connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the Azure storage account name.
    /// </summary>
    public string? AccountName { get; set; }

    /// <summary>
    /// Gets or sets the Azure storage account key.
    /// </summary>
    public string? AccountKey { get; set; }

    /// <summary>
    /// Gets or sets the Azure storage SAS token.
    /// </summary>
    public string? SasToken { get; set; }

    /// <summary>
    /// Gets or sets the Azure storage service endpoint.
    /// </summary>
    public Uri? ServiceEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the Azure credential for authentication.
    /// </summary>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Gets or sets whether to enable caching for improved performance.
    /// </summary>
    public bool UseCache { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for Azure operations in milliseconds.
    /// </summary>
    public int TimeoutMilliseconds { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the maximum blob size in bytes before splitting into multiple blobs.
    /// Default is 100MB to stay well under Azure's 5GB block blob limit.
    /// </summary>
    public long MaxBlobSize { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Gets or sets the encryption scope for blob operations.
    /// </summary>
    public string? EncryptionScope { get; set; }

    /// <summary>
    /// Gets or sets whether to use HTTPS for connections.
    /// </summary>
    public bool UseHttps { get; set; } = true;

    /// <summary>
    /// Gets or sets custom blob client options.
    /// </summary>
    public BlobClientOptions? ClientOptions { get; set; }

    /// <summary>
    /// Creates a new Azure Storage configuration with default values.
    /// </summary>
    /// <returns>A new configuration instance</returns>
    public static AzureStorageConfiguration New() => new();

    /// <summary>
    /// Sets the Azure storage connection string.
    /// </summary>
    /// <param name="connectionString">The connection string</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AzureStorageConfiguration SetConnectionString(string connectionString)
    {
        ConnectionString = connectionString;
        return this;
    }

    /// <summary>
    /// Sets the Azure storage account credentials.
    /// </summary>
    /// <param name="accountName">The storage account name</param>
    /// <param name="accountKey">The storage account key</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AzureStorageConfiguration SetAccountCredentials(string accountName, string accountKey)
    {
        AccountName = accountName;
        AccountKey = accountKey;
        return this;
    }

    /// <summary>
    /// Sets the Azure storage SAS token.
    /// </summary>
    /// <param name="sasToken">The SAS token</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AzureStorageConfiguration SetSasToken(string sasToken)
    {
        SasToken = sasToken;
        return this;
    }

    /// <summary>
    /// Sets the Azure storage service endpoint.
    /// </summary>
    /// <param name="serviceEndpoint">The service endpoint URI</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AzureStorageConfiguration SetServiceEndpoint(Uri serviceEndpoint)
    {
        ServiceEndpoint = serviceEndpoint;
        return this;
    }

    /// <summary>
    /// Sets the Azure credential for authentication.
    /// </summary>
    /// <param name="credential">The token credential</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AzureStorageConfiguration SetCredential(TokenCredential credential)
    {
        Credential = credential;
        return this;
    }

    /// <summary>
    /// Sets whether to enable caching.
    /// </summary>
    /// <param name="useCache">True to enable caching</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AzureStorageConfiguration SetUseCache(bool useCache)
    {
        UseCache = useCache;
        return this;
    }

    /// <summary>
    /// Sets the timeout for Azure operations.
    /// </summary>
    /// <param name="timeoutMilliseconds">The timeout in milliseconds</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AzureStorageConfiguration SetTimeout(int timeoutMilliseconds)
    {
        TimeoutMilliseconds = timeoutMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of retry attempts.
    /// </summary>
    /// <param name="maxRetryAttempts">The maximum retry attempts</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AzureStorageConfiguration SetMaxRetryAttempts(int maxRetryAttempts)
    {
        MaxRetryAttempts = maxRetryAttempts;
        return this;
    }

    /// <summary>
    /// Sets the maximum blob size before splitting.
    /// </summary>
    /// <param name="maxBlobSize">The maximum blob size in bytes</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AzureStorageConfiguration SetMaxBlobSize(long maxBlobSize)
    {
        MaxBlobSize = maxBlobSize;
        return this;
    }

    /// <summary>
    /// Sets the encryption scope for blob operations.
    /// </summary>
    /// <param name="encryptionScope">The encryption scope</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AzureStorageConfiguration SetEncryptionScope(string encryptionScope)
    {
        EncryptionScope = encryptionScope;
        return this;
    }

    /// <summary>
    /// Sets whether to use HTTPS for connections.
    /// </summary>
    /// <param name="useHttps">True to use HTTPS</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AzureStorageConfiguration SetUseHttps(bool useHttps)
    {
        UseHttps = useHttps;
        return this;
    }

    /// <summary>
    /// Sets custom blob client options.
    /// </summary>
    /// <param name="clientOptions">The blob client options</param>
    /// <returns>This configuration instance for method chaining</returns>
    public AzureStorageConfiguration SetClientOptions(BlobClientOptions clientOptions)
    {
        ClientOptions = clientOptions;
        return this;
    }

    /// <summary>
    /// Validates the configuration and throws an exception if invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public void Validate()
    {
        var hasConnectionString = !string.IsNullOrEmpty(ConnectionString);
        var hasAccountCredentials = !string.IsNullOrEmpty(AccountName) && !string.IsNullOrEmpty(AccountKey);
        var hasSasToken = !string.IsNullOrEmpty(SasToken);
        var hasCredential = Credential != null;
        var hasServiceEndpoint = ServiceEndpoint != null;

        if (!hasConnectionString && !hasAccountCredentials && !hasSasToken && !hasCredential)
        {
            throw new InvalidOperationException(
                "Azure Storage configuration must specify either a connection string, account credentials, SAS token, or credential");
        }

        if (TimeoutMilliseconds <= 0)
        {
            throw new InvalidOperationException("Timeout must be greater than zero");
        }

        if (MaxRetryAttempts < 0)
        {
            throw new InvalidOperationException("Max retry attempts cannot be negative");
        }

        if (MaxBlobSize <= 0)
        {
            throw new InvalidOperationException("Max blob size must be greater than zero");
        }
    }
}
