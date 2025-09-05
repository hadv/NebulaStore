using System;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Afs.GoogleCloud.Firestore;

/// <summary>
/// Extension methods for configuring Google Cloud Firestore storage.
/// </summary>
public static class FirestoreConfigurationExtensions
{
    /// <summary>
    /// Configures the storage to use Google Cloud Firestore.
    /// </summary>
    /// <param name="builder">The configuration builder</param>
    /// <param name="projectId">The Google Cloud Project ID</param>
    /// <param name="useCache">Whether to enable caching (default: true)</param>
    /// <returns>The configuration builder for method chaining</returns>
    public static IEmbeddedStorageConfigurationBuilder UseFirestore(
        this IEmbeddedStorageConfigurationBuilder builder,
        string projectId,
        bool useCache = true)
    {
        if (string.IsNullOrEmpty(projectId))
            throw new ArgumentException("Project ID cannot be null or empty", nameof(projectId));

        return builder
            .SetUseAfs(true)
            .SetAfsStorageType("firestore")
            .SetAfsConnectionString(projectId)
            .SetAfsUseCache(useCache);
    }

    /// <summary>
    /// Configures the storage to use Google Cloud Firestore with custom settings.
    /// </summary>
    /// <param name="builder">The configuration builder</param>
    /// <param name="projectId">The Google Cloud Project ID</param>
    /// <param name="storageDirectory">The storage directory name (used as collection prefix)</param>
    /// <param name="useCache">Whether to enable caching (default: true)</param>
    /// <returns>The configuration builder for method chaining</returns>
    public static IEmbeddedStorageConfigurationBuilder UseFirestore(
        this IEmbeddedStorageConfigurationBuilder builder,
        string projectId,
        string storageDirectory,
        bool useCache = true)
    {
        if (string.IsNullOrEmpty(projectId))
            throw new ArgumentException("Project ID cannot be null or empty", nameof(projectId));
        
        if (string.IsNullOrEmpty(storageDirectory))
            throw new ArgumentException("Storage directory cannot be null or empty", nameof(storageDirectory));

        return builder
            .SetStorageDirectory(storageDirectory)
            .SetUseAfs(true)
            .SetAfsStorageType("firestore")
            .SetAfsConnectionString(projectId)
            .SetAfsUseCache(useCache);
    }
}
