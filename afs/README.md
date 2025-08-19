# NebulaStore Abstract File System (AFS)

The Abstract File System (AFS) provides a pluggable storage abstraction layer for NebulaStore, allowing you to use different storage backends while maintaining a consistent API. This implementation is based on the Eclipse Store AFS architecture.

## Features

- **Pluggable Storage Backends**: Support for multiple storage types (currently: local blob store)
- **Blob Storage**: Files are stored as numbered blobs for efficient handling of large files
- **Caching**: Optional caching layer for improved performance
- **Thread-Safe**: All operations are designed to be thread-safe
- **Path Abstraction**: Clean path handling with validation
- **Integration**: Seamless integration with existing NebulaStore configuration

## Architecture

### Core Components

1. **BlobStorePath**: Path representation with container/blob structure
2. **IBlobStoreConnector**: Abstract interface for storage operations
3. **LocalBlobStoreConnector**: Local file system implementation
4. **BlobStoreFileSystem**: File system abstraction layer
5. **AfsStorageConnection**: Integration with NebulaStore storage system

### Storage Structure

```
storage-directory/
├── container1/
│   ├── file1.0
│   ├── file1.1
│   ├── file2.0
│   └── subfolder/
│       └── file3.0
└── container2/
    └── document.0
```

Files are stored as numbered blobs (e.g., `file1.0`, `file1.1`) to support:
- Large file handling
- Efficient partial reads/writes
- Atomic operations
- Concurrent access

## Usage

### Basic Usage

```csharp
using NebulaStore.Storage.Embedded;

// Start with AFS using default settings
using var storage = EmbeddedStorage.StartWithAfs("my-storage");

var root = storage.Root<MyDataClass>();
root.SomeProperty = "value";
storage.StoreRoot();
```

### Custom Configuration

```csharp
using NebulaStore.Storage.EmbeddedConfiguration;

var config = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("my-afs-storage")
    .SetUseAfs(true)
    .SetAfsStorageType("blobstore")
    .SetAfsUseCache(true)
    .SetChannelCount(4)
    .Build();

using var storage = EmbeddedStorage.StartWithAfs(config);
```

### Direct AFS Usage

```csharp
using NebulaStore.Storage.Afs.Blobstore;

// Create connector
using var connector = new LocalBlobStoreConnector("storage-path", useCache: true);

// Create file system
using var fileSystem = BlobStoreFileSystem.New(connector);

// Perform operations
var path = BlobStorePath.New("container", "folder", "file.txt");
var data = System.Text.Encoding.UTF8.GetBytes("Hello, AFS!");

fileSystem.IoHandler.WriteData(path, data);
var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
```

## Configuration Options

### AFS-Specific Settings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `UseAfs` | `bool` | `false` | Enable AFS storage |
| `AfsStorageType` | `string` | `"blobstore"` | Storage backend type |
| `AfsConnectionString` | `string?` | `null` | Connection string (uses StorageDirectory if null) |
| `AfsUseCache` | `bool` | `true` | Enable caching for performance |

### Configuration Methods

```csharp
var config = EmbeddedStorageConfiguration.New()
    .SetUseAfs(true)                           // Enable AFS
    .SetAfsStorageType("blobstore")            // Set storage type
    .SetAfsConnectionString("/custom/path")    // Custom storage path
    .SetAfsUseCache(false)                     // Disable caching
    .Build();
```

## Performance Considerations

### Caching

AFS includes an optional caching layer that can significantly improve performance:

```csharp
// Enable caching (recommended for production)
.SetAfsUseCache(true)

// Disable caching (useful for testing or low-memory environments)
.SetAfsUseCache(false)
```

### Blob Size Management

The blob storage system automatically manages file splitting:
- Large files are split into multiple numbered blobs
- Each blob is stored as a separate file
- Reads can span multiple blobs transparently
- Writes append new blobs as needed

### Concurrent Access

All AFS operations are thread-safe:
- Multiple readers can access the same file simultaneously
- Writers are properly synchronized
- Directory operations are atomic
- Cache consistency is maintained

## Storage Types

### Blobstore (Local File System)

The default storage type that stores data as blobs in the local file system.

**Advantages:**
- Simple setup and configuration
- Good performance for local access
- Familiar file system semantics
- Easy backup and maintenance

**Use Cases:**
- Single-machine deployments
- Development and testing
- Local data processing
- Embedded applications

### Future Storage Types

The AFS architecture supports additional storage backends:
- **NIO**: Java NIO-based file operations
- **SQL**: Database-backed storage
- **Cloud**: AWS S3, Azure Blob, Google Cloud Storage
- **Redis**: In-memory storage with persistence
- **Custom**: Implement `IBlobStoreConnector` for custom backends

## Error Handling

AFS operations include comprehensive error handling:

```csharp
try
{
    using var storage = EmbeddedStorage.StartWithAfs(config);
    // ... operations
}
catch (NotSupportedException ex)
{
    // Unsupported storage type
    Console.WriteLine($"Storage type not supported: {ex.Message}");
}
catch (DirectoryNotFoundException ex)
{
    // Storage directory issues
    Console.WriteLine($"Storage directory not found: {ex.Message}");
}
catch (UnauthorizedAccessException ex)
{
    // Permission issues
    Console.WriteLine($"Access denied: {ex.Message}");
}
```

## Testing

The AFS implementation includes comprehensive tests:

```bash
# Run AFS-specific tests
dotnet test afs/tests/

# Run integration tests
dotnet test afs/tests/AfsIntegrationTests.cs

# Run performance tests
dotnet test afs/tests/ --filter Category=Performance
```

## Migration from Traditional Storage

To migrate existing NebulaStore applications to AFS:

1. **Update Configuration:**
   ```csharp
   // Before
   using var storage = EmbeddedStorage.Start("storage");
   
   // After
   using var storage = EmbeddedStorage.StartWithAfs("storage");
   ```

2. **Preserve Data:**
   - AFS uses a different storage format
   - Export data before migration if needed
   - Test thoroughly in development environment

3. **Update Dependencies:**
   ```xml
   <ProjectReference Include="NebulaStore.Storage.Afs.csproj" />
   ```

## Troubleshooting

### Common Issues

1. **Storage Type Not Supported**
   - Ensure the storage type is correctly specified
   - Currently only "blobstore" is supported

2. **Permission Denied**
   - Check file system permissions
   - Ensure the storage directory is writable

3. **Performance Issues**
   - Enable caching with `SetAfsUseCache(true)`
   - Consider adjusting channel count
   - Monitor disk I/O and available space

### Debugging

Enable detailed logging:

```csharp
// Add logging configuration
var config = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("debug-storage")
    .SetUseAfs(true)
    .SetValidateOnStartup(true)  // Enable validation
    .Build();
```

## Contributing

To contribute to the AFS implementation:

1. Follow the existing code patterns
2. Add comprehensive tests for new features
3. Update documentation
4. Ensure thread safety
5. Consider performance implications

## License

This AFS implementation is part of NebulaStore and follows the same licensing terms.
