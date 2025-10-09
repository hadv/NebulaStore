using Oci.Common;
using Oci.Common.Auth;
using Oci.ObjectstorageService;

namespace NebulaStore.Afs.OracleCloud.ObjectStorage;

/// <summary>
/// Authentication type for Oracle Cloud Infrastructure.
/// </summary>
public enum OciAuthType
{
    /// <summary>
    /// Use configuration file authentication (default ~/.oci/config).
    /// </summary>
    ConfigFile,

    /// <summary>
    /// Use instance principal authentication (for OCI compute instances).
    /// </summary>
    InstancePrincipal,

    /// <summary>
    /// Use resource principal authentication (for OCI functions).
    /// </summary>
    ResourcePrincipal,

    /// <summary>
    /// Use simple authentication with user OCID, tenancy OCID, fingerprint, and private key.
    /// </summary>
    Simple
}

/// <summary>
/// Configuration for Oracle Cloud Infrastructure Object Storage connections.
/// Provides settings for authentication, connection, and performance tuning.
/// </summary>
public class OracleCloudObjectStorageConfiguration
{
    /// <summary>
    /// Gets or sets the authentication type.
    /// </summary>
    public OciAuthType AuthType { get; set; } = OciAuthType.ConfigFile;

    /// <summary>
    /// Gets or sets the path to the OCI configuration file.
    /// Default is ~/.oci/config.
    /// </summary>
    public string? ConfigFilePath { get; set; }

    /// <summary>
    /// Gets or sets the profile name in the configuration file.
    /// Default is "DEFAULT".
    /// </summary>
    public string? Profile { get; set; } = "DEFAULT";

    /// <summary>
    /// Gets or sets the OCI region identifier (e.g., "us-ashburn-1").
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Gets or sets the OCI Object Storage namespace.
    /// If not set, it will be auto-detected from the tenancy.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets the custom endpoint URL for Object Storage.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the user OCID for simple authentication.
    /// </summary>
    public string? UserOcid { get; set; }

    /// <summary>
    /// Gets or sets the tenancy OCID for simple authentication.
    /// </summary>
    public string? TenancyOcid { get; set; }

    /// <summary>
    /// Gets or sets the fingerprint for simple authentication.
    /// </summary>
    public string? Fingerprint { get; set; }

    /// <summary>
    /// Gets or sets the private key file path for simple authentication.
    /// </summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>
    /// Gets or sets the private key passphrase for simple authentication.
    /// </summary>
    public string? PrivateKeyPassphrase { get; set; }

    /// <summary>
    /// Gets or sets whether to enable caching for improved performance.
    /// </summary>
    public bool UseCache { get; set; } = true;

    /// <summary>
    /// Gets or sets the connection timeout in milliseconds.
    /// </summary>
    public int ConnectionTimeoutMilliseconds { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the read timeout in milliseconds.
    /// </summary>
    public int ReadTimeoutMilliseconds { get; set; } = 60000;

    /// <summary>
    /// Gets or sets the maximum number of async threads.
    /// </summary>
    public int MaxAsyncThreads { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum blob size in bytes before splitting into multiple blobs.
    /// Default is 50 GiB (Oracle Cloud Object Storage limit per object).
    /// </summary>
    public long MaxBlobSize { get; set; } = 50L * 1024 * 1024 * 1024; // 50 GiB

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Creates a new Oracle Cloud Object Storage configuration with default values.
    /// </summary>
    /// <returns>A new configuration instance</returns>
    public static OracleCloudObjectStorageConfiguration New() => new();

    /// <summary>
    /// Sets the authentication type.
    /// </summary>
    /// <param name="authType">The authentication type</param>
    /// <returns>This configuration instance for method chaining</returns>
    public OracleCloudObjectStorageConfiguration SetAuthenticationType(OciAuthType authType)
    {
        AuthType = authType;
        return this;
    }

    /// <summary>
    /// Sets the configuration file path and profile.
    /// </summary>
    /// <param name="configFilePath">The path to the OCI config file</param>
    /// <param name="profile">The profile name (default is "DEFAULT")</param>
    /// <returns>This configuration instance for method chaining</returns>
    public OracleCloudObjectStorageConfiguration SetConfigFile(string configFilePath, string? profile = null)
    {
        ConfigFilePath = configFilePath;
        if (profile != null)
        {
            Profile = profile;
        }
        AuthType = OciAuthType.ConfigFile;
        return this;
    }

    /// <summary>
    /// Sets the OCI region.
    /// </summary>
    /// <param name="region">The OCI region identifier</param>
    /// <returns>This configuration instance for method chaining</returns>
    public OracleCloudObjectStorageConfiguration SetRegion(string region)
    {
        Region = region;
        return this;
    }

    /// <summary>
    /// Sets the OCI Object Storage namespace.
    /// </summary>
    /// <param name="namespace">The namespace</param>
    /// <returns>This configuration instance for method chaining</returns>
    public OracleCloudObjectStorageConfiguration SetNamespace(string @namespace)
    {
        Namespace = @namespace;
        return this;
    }

    /// <summary>
    /// Sets the custom endpoint URL.
    /// </summary>
    /// <param name="endpoint">The endpoint URL</param>
    /// <returns>This configuration instance for method chaining</returns>
    public OracleCloudObjectStorageConfiguration SetEndpoint(string endpoint)
    {
        Endpoint = endpoint;
        return this;
    }

    /// <summary>
    /// Sets simple authentication credentials.
    /// </summary>
    /// <param name="userOcid">The user OCID</param>
    /// <param name="tenancyOcid">The tenancy OCID</param>
    /// <param name="fingerprint">The fingerprint</param>
    /// <param name="privateKeyPath">The private key file path</param>
    /// <param name="privateKeyPassphrase">The private key passphrase (optional)</param>
    /// <returns>This configuration instance for method chaining</returns>
    public OracleCloudObjectStorageConfiguration SetSimpleAuth(
        string userOcid,
        string tenancyOcid,
        string fingerprint,
        string privateKeyPath,
        string? privateKeyPassphrase = null)
    {
        UserOcid = userOcid;
        TenancyOcid = tenancyOcid;
        Fingerprint = fingerprint;
        PrivateKeyPath = privateKeyPath;
        PrivateKeyPassphrase = privateKeyPassphrase;
        AuthType = OciAuthType.Simple;
        return this;
    }

    /// <summary>
    /// Sets whether to enable caching.
    /// </summary>
    /// <param name="useCache">True to enable caching</param>
    /// <returns>This configuration instance for method chaining</returns>
    public OracleCloudObjectStorageConfiguration SetUseCache(bool useCache)
    {
        UseCache = useCache;
        return this;
    }

    /// <summary>
    /// Sets the connection timeout.
    /// </summary>
    /// <param name="timeoutMilliseconds">The timeout in milliseconds</param>
    /// <returns>This configuration instance for method chaining</returns>
    public OracleCloudObjectStorageConfiguration SetConnectionTimeout(int timeoutMilliseconds)
    {
        ConnectionTimeoutMilliseconds = timeoutMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the read timeout.
    /// </summary>
    /// <param name="timeoutMilliseconds">The timeout in milliseconds</param>
    /// <returns>This configuration instance for method chaining</returns>
    public OracleCloudObjectStorageConfiguration SetReadTimeout(int timeoutMilliseconds)
    {
        ReadTimeoutMilliseconds = timeoutMilliseconds;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of async threads.
    /// </summary>
    /// <param name="maxAsyncThreads">The maximum async threads</param>
    /// <returns>This configuration instance for method chaining</returns>
    public OracleCloudObjectStorageConfiguration SetMaxAsyncThreads(int maxAsyncThreads)
    {
        MaxAsyncThreads = maxAsyncThreads;
        return this;
    }

    /// <summary>
    /// Sets the maximum blob size before splitting.
    /// </summary>
    /// <param name="maxBlobSize">The maximum blob size in bytes</param>
    /// <returns>This configuration instance for method chaining</returns>
    public OracleCloudObjectStorageConfiguration SetMaxBlobSize(long maxBlobSize)
    {
        MaxBlobSize = maxBlobSize;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of retry attempts.
    /// </summary>
    /// <param name="maxRetryAttempts">The maximum retry attempts</param>
    /// <returns>This configuration instance for method chaining</returns>
    public OracleCloudObjectStorageConfiguration SetMaxRetryAttempts(int maxRetryAttempts)
    {
        MaxRetryAttempts = maxRetryAttempts;
        return this;
    }

    /// <summary>
    /// Creates an authentication details provider based on the configuration.
    /// </summary>
    /// <returns>The authentication details provider</returns>
    public IBasicAuthenticationDetailsProvider CreateAuthenticationProvider()
    {
        return AuthType switch
        {
            OciAuthType.ConfigFile => CreateConfigFileProvider(),
            OciAuthType.InstancePrincipal => CreateInstancePrincipalProvider(),
            OciAuthType.ResourcePrincipal => CreateResourcePrincipalProvider(),
            OciAuthType.Simple => CreateSimpleProvider(),
            _ => throw new InvalidOperationException($"Unsupported authentication type: {AuthType}")
        };
    }

    private IBasicAuthenticationDetailsProvider CreateConfigFileProvider()
    {
        if (!string.IsNullOrEmpty(ConfigFilePath))
        {
            return new ConfigFileAuthenticationDetailsProvider(ConfigFilePath, Profile);
        }
        return new ConfigFileAuthenticationDetailsProvider(Profile);
    }

    private IBasicAuthenticationDetailsProvider CreateInstancePrincipalProvider()
    {
        return new InstancePrincipalsAuthenticationDetailsProvider();
    }

    private IBasicAuthenticationDetailsProvider CreateResourcePrincipalProvider()
    {
        // Resource principal authentication for OCI Functions
        throw new NotImplementedException(
            "Resource principal authentication is not yet implemented. " +
            "Please use config file or instance principal authentication instead.");
    }

    private IBasicAuthenticationDetailsProvider CreateSimpleProvider()
    {
        if (string.IsNullOrEmpty(UserOcid) || string.IsNullOrEmpty(TenancyOcid) ||
            string.IsNullOrEmpty(Fingerprint) || string.IsNullOrEmpty(PrivateKeyPath))
        {
            throw new InvalidOperationException(
                "Simple authentication requires UserOcid, TenancyOcid, Fingerprint, and PrivateKeyPath");
        }

        // Use config file approach for simple auth
        // Create a temporary config file or use the existing one
        throw new NotImplementedException(
            "Simple authentication is not yet implemented. " +
            "Please use config file or instance principal authentication instead.");
    }

    /// <summary>
    /// Validates the configuration and throws an exception if invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid</exception>
    public void Validate()
    {
        if (ConnectionTimeoutMilliseconds <= 0)
        {
            throw new InvalidOperationException("Connection timeout must be greater than zero");
        }

        if (ReadTimeoutMilliseconds <= 0)
        {
            throw new InvalidOperationException("Read timeout must be greater than zero");
        }

        if (MaxRetryAttempts < 0)
        {
            throw new InvalidOperationException("Max retry attempts cannot be negative");
        }

        if (MaxBlobSize <= 0)
        {
            throw new InvalidOperationException("Max blob size must be greater than zero");
        }

        if (MaxAsyncThreads <= 0)
        {
            throw new InvalidOperationException("Max async threads must be greater than zero");
        }
    }
}

