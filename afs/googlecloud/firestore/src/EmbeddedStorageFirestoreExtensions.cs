using Google.Cloud.Firestore;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Afs.GoogleCloud.Firestore;

/// <summary>
/// Extension methods for starting embedded storage with Google Cloud Firestore.
/// </summary>
public static class EmbeddedStorageFirestoreExtensions
{
    /// <summary>
    /// Creates and starts an embedded storage manager with Google Cloud Firestore.
    /// Note: This creates a storage manager that uses Firestore directly, bypassing the standard AFS system.
    /// </summary>
    /// <param name="projectId">The Google Cloud Project ID</param>
    /// <param name="storageDirectory">The storage directory name (optional, defaults to "firestore-storage")</param>
    /// <param name="useCache">Whether to enable caching (default: true)</param>
    /// <returns>A started storage manager instance using Firestore</returns>
    public static IEmbeddedStorageManager StartWithFirestore(
        string projectId,
        string? storageDirectory = null,
        bool useCache = true)
    {
        if (string.IsNullOrEmpty(projectId))
            throw new ArgumentException("Project ID cannot be null or empty", nameof(projectId));

        // Create configuration with Firestore AFS backend
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(storageDirectory ?? "firestore-storage")
            .SetUseAfs(true)
            .SetAfsStorageType("firestore")
            .SetAfsConnectionString(projectId)
            .SetAfsUseCache(useCache)
            .Build();

        // Start with AFS using Firestore connector
        return EmbeddedStorage.Foundation(config).Start();
    }

    /// <summary>
    /// Creates and starts an embedded storage manager with Google Cloud Firestore and root object.
    /// This method configures the storage to use Firestore as the backend through the AFS system.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <param name="projectId">The Google Cloud Project ID</param>
    /// <param name="storageDirectory">The storage directory name (optional, defaults to "firestore-storage")</param>
    /// <param name="useCache">Whether to enable caching (default: true)</param>
    /// <returns>A started storage manager instance using Firestore</returns>
    public static IEmbeddedStorageManager StartWithFirestore(
        object? root,
        string projectId,
        string? storageDirectory = null,
        bool useCache = true)
    {
        if (string.IsNullOrEmpty(projectId))
            throw new ArgumentException("Project ID cannot be null or empty", nameof(projectId));

        // Create configuration with Firestore AFS backend
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(storageDirectory ?? "firestore-storage")
            .SetUseAfs(true)
            .SetAfsStorageType("firestore")
            .SetAfsConnectionString(projectId)
            .SetAfsUseCache(useCache)
            .Build();

        // Start with AFS using Firestore connector
        return EmbeddedStorage.Foundation(config).Start(root);
    }

    /// <summary>
    /// Creates a Firestore connector for direct use with the AFS blob store system.
    /// This allows you to use Firestore as a storage backend with the AFS file system.
    /// </summary>
    /// <param name="projectId">The Google Cloud Project ID</param>
    /// <param name="useCache">Whether to enable caching (default: true)</param>
    /// <returns>A Firestore blob store connector</returns>
    public static GoogleCloudFirestoreConnector CreateFirestoreConnector(
        string projectId,
        bool useCache = true)
    {
        var firestore = FirestoreDb.Create(projectId);
        return useCache
            ? GoogleCloudFirestoreConnector.Caching(firestore)
            : GoogleCloudFirestoreConnector.New(firestore);
    }

    /// <summary>
    /// Creates a blob store file system using Firestore as the backend.
    /// This provides direct access to the AFS file system with Firestore storage.
    /// </summary>
    /// <param name="projectId">The Google Cloud Project ID</param>
    /// <param name="useCache">Whether to enable caching (default: true)</param>
    /// <returns>A blob store file system using Firestore</returns>
    public static IBlobStoreFileSystem CreateFirestoreFileSystem(
        string projectId,
        bool useCache = true)
    {
        var connector = CreateFirestoreConnector(projectId, useCache);
        return BlobStoreFileSystem.New(connector);
    }
}
