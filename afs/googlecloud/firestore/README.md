# AFS adapter for Google Cloud Firestore

This module provides an Abstract File System (AFS) adapter for Google Cloud Firestore, allowing NebulaStore to use Firestore as a storage backend.

## Features

- **Document-based Storage**: Files are stored as documents in Firestore collections
- **Large File Support**: Files larger than 1MB are automatically split across multiple documents
- **Blob Management**: Efficient handling of binary data using Firestore's Blob type
- **Caching**: Optional caching layer for improved performance
- **Thread-Safe**: All operations are designed to be thread-safe
- **Firestore Compliance**: Follows Firestore naming conventions and limits

## Prerequisites

This module requires the Google Cloud Firestore client library:
- `Google.Cloud.Firestore` (version 3.8.0 or later)

You must also have:
1. A Google Cloud Project with Firestore enabled
2. Proper authentication configured (service account key, application default credentials, etc.)

## Usage

### Basic Usage

```csharp
using NebulaStore.Storage.Embedded;

// Simple Firestore storage - easiest way to get started
using var storage = EmbeddedStorage.StartWithFirestore("your-project-id");

// Use storage normally
var root = storage.Root<MyDataClass>();
if (root == null)
{
    root = new MyDataClass { SomeProperty = "value" };
    storage.SetRoot(root);
}
else
{
    root.SomeProperty = "updated value";
}
storage.StoreRoot();
```

### Configuration-Based Usage

```csharp
using NebulaStore.Afs.GoogleCloud.Firestore;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

// Using configuration builder with Firestore extension
var config = EmbeddedStorageConfiguration.New()
    .UseFirestore("your-project-id", "my-storage", useCache: true)
    .SetChannelCount(4)
    .Build();

using var storage = EmbeddedStorage.Foundation(config).Start();

// Use storage normally
var root = storage.Root<MyDataClass>();
root.SomeProperty = "value";
storage.StoreRoot();
```

### Alternative Convenience Methods

```csharp
using NebulaStore.Afs.GoogleCloud.Firestore;

// Using the Firestore extensions directly
using var storage1 = EmbeddedStorageFirestoreExtensions.StartWithFirestore("your-project-id");

// With a root object
var myData = new MyDataClass { SomeProperty = "initial value" };
using var storage2 = EmbeddedStorageFirestoreExtensions.StartWithFirestore(myData, "your-project-id");

// With custom settings
using var storage3 = EmbeddedStorageFirestoreExtensions.StartWithFirestore(
    "your-project-id",
    "custom-storage-name",
    useCache: false);
```

### Direct AFS Usage

```csharp
using Google.Cloud.Firestore;
using NebulaStore.Afs.GoogleCloud.Firestore;
using NebulaStore.Afs.Blobstore;

// Create Firestore connection
var firestore = FirestoreDb.Create("your-project-id");

// Create connector
using var connector = GoogleCloudFirestoreConnector.New(firestore);

// Create file system
using var fileSystem = BlobStoreFileSystem.New(connector);

// Perform operations
var path = BlobStorePath.New("my-collection", "folder", "file.txt");
var data = System.Text.Encoding.UTF8.GetBytes("Hello, Firestore!");

fileSystem.IoHandler.WriteData(path, data);
var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
```

### With Caching

```csharp
// Create connector with caching enabled
using var connector = GoogleCloudFirestoreConnector.Caching(firestore);
```

## Authentication

The connector uses the Google Cloud Firestore client library, which supports several authentication methods:

### Service Account Key

```csharp
var firestore = new FirestoreDbBuilder
{
    ProjectId = "your-project-id",
    CredentialsPath = "path/to/service-account-key.json"
}.Build();
```

### Application Default Credentials

```csharp
// Set environment variable
Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "path/to/service-account-key.json");

var firestore = FirestoreDb.Create("your-project-id");
```

### Emulator (for testing)

```csharp
Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", "localhost:8080");
var firestore = FirestoreDb.Create("test-project");
```

## Storage Structure

Files are stored in Firestore with the following structure:

```
Collection: {container-name}
├── Document: folder_file.txt.0
│   ├── key: "folder/file.txt.0"
│   ├── size: 1000000
│   └── data: <blob-data>
├── Document: folder_file.txt.1
│   ├── key: "folder/file.txt.1"
│   ├── size: 500000
│   └── data: <blob-data>
└── ...
```

## Limitations

- **Document Size**: Each blob is limited to 1MB (Firestore document size limit)
- **Collection Names**: Must follow Firestore naming rules (no `/`, not `.` or `..`, not matching `__.*__`)
- **Batch Size**: Write batches are limited to 10MB total size
- **Query Limits**: Large directories may require pagination (handled automatically)

## Error Handling

The connector includes comprehensive error handling:

```csharp
try
{
    using var storage = EmbeddedStorage.StartWithAfs(config);
    // ... operations
}
catch (IOException ex)
{
    // Firestore I/O errors
    Console.WriteLine($"Firestore operation failed: {ex.Message}");
}
catch (ArgumentException ex)
{
    // Invalid collection names or paths
    Console.WriteLine($"Invalid path or configuration: {ex.Message}");
}
```

## Performance Considerations

- **Caching**: Enable caching for better performance with repeated operations
- **Batch Operations**: Large files are automatically batched for optimal performance
- **Network Latency**: Consider Firestore region selection for your application
- **Concurrent Access**: The connector is thread-safe but Firestore has rate limits

## Testing

Use the Firestore emulator for local testing:

```bash
# Install and start the emulator
gcloud components install cloud-firestore-emulator
gcloud beta emulators firestore start --host-port=localhost:8080
```

```csharp
// In your test code
Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", "localhost:8080");
var firestore = FirestoreDb.Create("test-project");
```

## Migration

When migrating from other AFS storage types:

1. **Export Data**: Export existing data before migration
2. **Update Configuration**: Change `AfsStorageType` to `"firestore"`
3. **Set Connection**: Provide Firestore project ID in `AfsConnectionString`
4. **Test Thoroughly**: Verify all operations work correctly
5. **Monitor Performance**: Firestore has different performance characteristics

## Troubleshooting

### Common Issues

1. **Authentication Errors**
   - Verify service account key or application default credentials
   - Check project ID and permissions

2. **Collection Name Errors**
   - Ensure collection names follow Firestore rules
   - Avoid special characters and reserved names

3. **Performance Issues**
   - Enable caching with `GoogleCloudFirestoreConnector.Caching()`
   - Consider Firestore region and network latency
   - Monitor Firestore quotas and limits

4. **Large File Issues**
   - Files are automatically split into 1MB chunks
   - Very large files may take longer to read/write
   - Consider file size limits for your use case
