using NebulaStore.Afs.Blobstore;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Afs.OracleCloud.ObjectStorage;

/// <summary>
/// Integration utilities for using Oracle Cloud Infrastructure Object Storage with NebulaStore AFS.
/// Provides helper methods to create storage configurations and file systems with OCI backend.
/// </summary>
public static class OracleCloudObjectStorageAfsIntegration
{
    /// <summary>
    /// Creates an embedded storage configuration for OCI Object Storage using config file authentication.
    /// </summary>
    /// <param name="bucketName">The bucket name to use as storage directory</param>
    /// <param name="configFilePath">The path to OCI config file (default: ~/.oci/config)</param>
    /// <param name="profile">The profile name in config file (default: DEFAULT)</param>
    /// <param name="region">The OCI region (optional)</param>
    /// <param name="useCache">Whether to enable caching (default: true)</param>
    /// <returns>A configured embedded storage configuration</returns>
    public static IEmbeddedStorageConfiguration CreateConfiguration(
        string bucketName,
        string? configFilePath = null,
        string? profile = null,
        string? region = null,
        bool useCache = true)
    {
        if (string.IsNullOrEmpty(bucketName))
            throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

        var configBuilder = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(bucketName)
            .SetUseAfs(true)
            .SetAfsStorageType("oraclecloud.objectstorage")
            .SetAfsUseCache(useCache);

        // Build connection string with config file path and profile
        var connectionParts = new List<string>();
        if (!string.IsNullOrEmpty(configFilePath))
        {
            connectionParts.Add($"ConfigFile={configFilePath}");
        }
        if (!string.IsNullOrEmpty(profile))
        {
            connectionParts.Add($"Profile={profile}");
        }
        if (!string.IsNullOrEmpty(region))
        {
            connectionParts.Add($"Region={region}");
        }

        if (connectionParts.Any())
        {
            configBuilder.SetAfsConnectionString(string.Join(";", connectionParts));
        }

        return configBuilder.Build();
    }

    /// <summary>
    /// Creates an embedded storage configuration for OCI Object Storage with advanced settings.
    /// </summary>
    /// <param name="bucketName">The bucket name to use as storage directory</param>
    /// <param name="configFilePath">The path to OCI config file</param>
    /// <param name="profile">The profile name in config file</param>
    /// <param name="region">The OCI region</param>
    /// <param name="channelCount">The number of storage channels</param>
    /// <param name="entityCacheThreshold">The entity cache threshold in bytes</param>
    /// <param name="useCache">Whether to enable caching</param>
    /// <returns>A configured embedded storage configuration</returns>
    public static IEmbeddedStorageConfiguration CreateAdvancedConfiguration(
        string bucketName,
        string? configFilePath = null,
        string? profile = null,
        string? region = null,
        int channelCount = 1,
        long entityCacheThreshold = 1000000,
        bool useCache = true)
    {
        if (string.IsNullOrEmpty(bucketName))
            throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

        var configBuilder = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(bucketName)
            .SetUseAfs(true)
            .SetAfsStorageType("oraclecloud.objectstorage")
            .SetAfsUseCache(useCache)
            .SetChannelCount(channelCount)
            .SetEntityCacheThreshold(entityCacheThreshold);

        // Build connection string
        var connectionParts = new List<string>();
        if (!string.IsNullOrEmpty(configFilePath))
        {
            connectionParts.Add($"ConfigFile={configFilePath}");
        }
        if (!string.IsNullOrEmpty(profile))
        {
            connectionParts.Add($"Profile={profile}");
        }
        if (!string.IsNullOrEmpty(region))
        {
            connectionParts.Add($"Region={region}");
        }

        if (connectionParts.Any())
        {
            configBuilder.SetAfsConnectionString(string.Join(";", connectionParts));
        }

        return configBuilder.Build();
    }

    /// <summary>
    /// Creates a BlobStoreFileSystem for OCI Object Storage using config file authentication.
    /// </summary>
    /// <param name="configFilePath">The path to OCI config file (optional, defaults to ~/.oci/config)</param>
    /// <param name="profile">The profile name (optional, defaults to DEFAULT)</param>
    /// <param name="region">The OCI region (optional)</param>
    /// <param name="useCache">Whether to enable caching</param>
    /// <returns>A blob store file system using OCI Object Storage</returns>
    public static IBlobStoreFileSystem CreateFileSystem(
        string? configFilePath = null,
        string? profile = null,
        string? region = null,
        bool useCache = true)
    {
        var config = OracleCloudObjectStorageConfiguration.New()
            .SetAuthenticationType(OciAuthType.ConfigFile)
            .SetUseCache(useCache);

        if (!string.IsNullOrEmpty(configFilePath))
        {
            config.SetConfigFile(configFilePath, profile);
        }
        else if (!string.IsNullOrEmpty(profile))
        {
            config.Profile = profile;
        }

        if (!string.IsNullOrEmpty(region))
        {
            config.SetRegion(region);
        }

        var connector = OracleCloudObjectStorageConnector.New(config);
        return BlobStoreFileSystem.New(connector);
    }

    /// <summary>
    /// Creates a BlobStoreFileSystem for OCI Object Storage using instance principal authentication.
    /// </summary>
    /// <param name="region">The OCI region</param>
    /// <param name="useCache">Whether to enable caching</param>
    /// <returns>A blob store file system using OCI Object Storage</returns>
    public static IBlobStoreFileSystem CreateFileSystemWithInstancePrincipal(
        string region,
        bool useCache = true)
    {
        if (string.IsNullOrEmpty(region))
            throw new ArgumentException("Region cannot be null or empty", nameof(region));

        var config = OracleCloudObjectStorageConfiguration.New()
            .SetAuthenticationType(OciAuthType.InstancePrincipal)
            .SetRegion(region)
            .SetUseCache(useCache);

        var connector = OracleCloudObjectStorageConnector.New(config);
        return BlobStoreFileSystem.New(connector);
    }

    /// <summary>
    /// Creates a BlobStoreFileSystem for OCI Object Storage using resource principal authentication.
    /// </summary>
    /// <param name="region">The OCI region</param>
    /// <param name="useCache">Whether to enable caching</param>
    /// <returns>A blob store file system using OCI Object Storage</returns>
    public static IBlobStoreFileSystem CreateFileSystemWithResourcePrincipal(
        string region,
        bool useCache = true)
    {
        if (string.IsNullOrEmpty(region))
            throw new ArgumentException("Region cannot be null or empty", nameof(region));

        var config = OracleCloudObjectStorageConfiguration.New()
            .SetAuthenticationType(OciAuthType.ResourcePrincipal)
            .SetRegion(region)
            .SetUseCache(useCache);

        var connector = OracleCloudObjectStorageConnector.New(config);
        return BlobStoreFileSystem.New(connector);
    }

    /// <summary>
    /// Creates a BlobStoreFileSystem for OCI Object Storage with a custom configuration.
    /// </summary>
    /// <param name="configuration">The OCI Object Storage configuration</param>
    /// <returns>A blob store file system using OCI Object Storage</returns>
    public static IBlobStoreFileSystem CreateFileSystem(OracleCloudObjectStorageConfiguration configuration)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        var connector = OracleCloudObjectStorageConnector.New(configuration);
        return BlobStoreFileSystem.New(connector);
    }

    /// <summary>
    /// Validates an OCI bucket name according to OCI naming rules.
    /// </summary>
    /// <param name="bucketName">The bucket name to validate</param>
    /// <returns>True if the bucket name is valid</returns>
    public static bool ValidateBucketName(string bucketName)
    {
        try
        {
            var validator = IOracleCloudObjectStoragePathValidator.New();
            validator.ValidateBucketName(bucketName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates an OCI object name according to OCI naming rules.
    /// </summary>
    /// <param name="objectName">The object name to validate</param>
    /// <returns>True if the object name is valid</returns>
    public static bool ValidateObjectName(string objectName)
    {
        try
        {
            var validator = IOracleCloudObjectStoragePathValidator.New();
            validator.ValidateObjectName(objectName);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

