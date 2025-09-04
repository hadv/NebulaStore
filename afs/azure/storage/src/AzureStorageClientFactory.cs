using Azure.Identity;
using Azure.Storage.Blobs;

namespace NebulaStore.Afs.Azure.Storage;

/// <summary>
/// Factory for creating Azure Blob Storage clients from configuration.
/// </summary>
public static class AzureStorageClientFactory
{
    /// <summary>
    /// Creates a BlobServiceClient from the specified configuration.
    /// </summary>
    /// <param name="configuration">The Azure storage configuration</param>
    /// <returns>A configured BlobServiceClient</returns>
    /// <exception cref="ArgumentNullException">Thrown when configuration is null</exception>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public static BlobServiceClient CreateBlobServiceClient(AzureStorageConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        configuration.Validate();

        var clientOptions = configuration.ClientOptions ?? new BlobClientOptions();
        
        // Configure retry options
        clientOptions.Retry.MaxRetries = configuration.MaxRetryAttempts;
        clientOptions.Retry.Delay = TimeSpan.FromSeconds(1);
        clientOptions.Retry.MaxDelay = TimeSpan.FromSeconds(30);

        // Configure network timeout (using default transport)
        // Note: Custom HttpClient configuration would require additional Azure SDK packages

        // Set encryption scope if specified
        if (!string.IsNullOrEmpty(configuration.EncryptionScope))
        {
            clientOptions.EncryptionScope = configuration.EncryptionScope;
        }

        // Create client based on available authentication method
        if (!string.IsNullOrEmpty(configuration.ConnectionString))
        {
            return new BlobServiceClient(configuration.ConnectionString, clientOptions);
        }

        if (configuration.Credential != null)
        {
            var serviceUri = configuration.ServiceEndpoint ?? 
                           new Uri($"https://{configuration.AccountName}.blob.core.windows.net");
            return new BlobServiceClient(serviceUri, configuration.Credential, clientOptions);
        }

        if (!string.IsNullOrEmpty(configuration.AccountName) && !string.IsNullOrEmpty(configuration.AccountKey))
        {
            var connectionString = $"DefaultEndpointsProtocol=https;AccountName={configuration.AccountName};AccountKey={configuration.AccountKey};EndpointSuffix=core.windows.net";
            return new BlobServiceClient(connectionString, clientOptions);
        }

        if (!string.IsNullOrEmpty(configuration.SasToken))
        {
            var serviceUri = configuration.ServiceEndpoint ?? 
                           throw new InvalidOperationException("Service endpoint must be specified when using SAS token");
            var uriWithSas = new Uri($"{serviceUri}?{configuration.SasToken}");
            return new BlobServiceClient(uriWithSas, clientOptions);
        }

        throw new InvalidOperationException("No valid authentication method found in configuration");
    }

    /// <summary>
    /// Creates a BlobServiceClient from a connection string.
    /// </summary>
    /// <param name="connectionString">The Azure storage connection string</param>
    /// <param name="useCache">Whether to enable caching</param>
    /// <returns>A configured BlobServiceClient</returns>
    public static BlobServiceClient CreateFromConnectionString(string connectionString, bool useCache = true)
    {
        var configuration = AzureStorageConfiguration.New()
            .SetConnectionString(connectionString)
            .SetUseCache(useCache);

        return CreateBlobServiceClient(configuration);
    }

    /// <summary>
    /// Creates a BlobServiceClient from account credentials.
    /// </summary>
    /// <param name="accountName">The storage account name</param>
    /// <param name="accountKey">The storage account key</param>
    /// <param name="useCache">Whether to enable caching</param>
    /// <returns>A configured BlobServiceClient</returns>
    public static BlobServiceClient CreateFromAccountCredentials(string accountName, string accountKey, bool useCache = true)
    {
        var configuration = AzureStorageConfiguration.New()
            .SetAccountCredentials(accountName, accountKey)
            .SetUseCache(useCache);

        return CreateBlobServiceClient(configuration);
    }

    /// <summary>
    /// Creates a BlobServiceClient using managed identity or default Azure credentials.
    /// </summary>
    /// <param name="accountName">The storage account name</param>
    /// <param name="useCache">Whether to enable caching</param>
    /// <returns>A configured BlobServiceClient</returns>
    public static BlobServiceClient CreateFromManagedIdentity(string accountName, bool useCache = true)
    {
        var credential = new global::Azure.Identity.DefaultAzureCredential();
        var configuration = AzureStorageConfiguration.New()
            .SetCredential(credential)
            .SetServiceEndpoint(new Uri($"https://{accountName}.blob.core.windows.net"))
            .SetUseCache(useCache);

        return CreateBlobServiceClient(configuration);
    }

    /// <summary>
    /// Parses a connection string to extract account name.
    /// </summary>
    /// <param name="connectionString">The connection string to parse</param>
    /// <returns>The account name if found, null otherwise</returns>
    public static string? ExtractAccountNameFromConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return null;

        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2 && keyValue[0].Trim().Equals("AccountName", StringComparison.OrdinalIgnoreCase))
            {
                return keyValue[1].Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Validates that a connection string contains required components.
    /// </summary>
    /// <param name="connectionString">The connection string to validate</param>
    /// <returns>True if the connection string is valid</returns>
    public static bool IsValidConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return false;

        try
        {
            // Try to create a client to validate the connection string
            var client = new BlobServiceClient(connectionString);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
