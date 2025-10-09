# NebulaStore Oracle Cloud Object Storage Adapter

Oracle Cloud Infrastructure (OCI) Object Storage adapter for NebulaStore Abstract File System (AFS). This adapter enables NebulaStore to use OCI Object Storage as a persistent storage backend.

## Features

- **Multiple Authentication Methods**: Config file, instance principal authentication
- **High Performance**: Optimized for large-scale object storage operations
- **Multipart Upload Support**: Handles large files up to 50 GiB per object
- **Namespace Auto-Detection**: Automatically detects OCI namespace from tenancy
- **Configurable Timeouts**: Customizable connection and read timeouts
- **Path Validation**: Validates bucket and object names according to OCI naming rules

## Installation

```bash
dotnet add package NebulaStore.Afs.OracleCloud.ObjectStorage
```

## Prerequisites

- .NET 9.0 or later
- Oracle Cloud Infrastructure account
- OCI Object Storage bucket
- OCI credentials (config file or instance principal)

## Quick Start

### Using Config File Authentication

```csharp
using NebulaStore.Afs.OracleCloud.ObjectStorage;
using NebulaStore.Afs.Blobstore;

// Create configuration
var config = OracleCloudObjectStorageConfiguration.New()
    .SetConfigFile("~/.oci/config", "DEFAULT")
    .SetRegion("us-ashburn-1")
    .SetUseCache(true);

// Create connector
var connector = OracleCloudObjectStorageConnector.New(config);

// Create file system
var fileSystem = BlobStoreFileSystem.New(connector);

// Use the file system
var path = BlobStorePath.New("my-bucket", "data", "file.dat");
fileSystem.WriteFile(path, dataBytes);
```

### Using Instance Principal Authentication

For OCI compute instances:

```csharp
var config = OracleCloudObjectStorageConfiguration.New()
    .SetAuthenticationType(OciAuthType.InstancePrincipal)
    .SetRegion("us-ashburn-1")
    .SetUseCache(true);

var connector = OracleCloudObjectStorageConnector.New(config);
var fileSystem = BlobStoreFileSystem.New(connector);
```

### Using AFS Integration Helper

```csharp
using NebulaStore.Afs.OracleCloud.ObjectStorage;

// Create file system with default config file authentication
var fileSystem = OracleCloudObjectStorageAfsIntegration.CreateFileSystem(
    configFilePath: "~/.oci/config",
    profile: "DEFAULT",
    region: "us-ashburn-1",
    useCache: true
);

// Or with instance principal
var fileSystem = OracleCloudObjectStorageAfsIntegration.CreateFileSystemWithInstancePrincipal(
    region: "us-ashburn-1",
    useCache: true
);
```

## Configuration Options

### Authentication Types

- **ConfigFile**: Use OCI configuration file (default: `~/.oci/config`)
- **InstancePrincipal**: Use instance principal for OCI compute instances
- **ResourcePrincipal**: Use resource principal for OCI Functions (not yet implemented)
- **Simple**: Use simple authentication with credentials (not yet implemented)

### Configuration Properties

```csharp
var config = OracleCloudObjectStorageConfiguration.New()
    .SetConfigFile("~/.oci/config", "DEFAULT")  // Config file and profile
    .SetRegion("us-ashburn-1")                   // OCI region
    .SetNamespace("my-namespace")                // OCI namespace (auto-detected if not set)
    .SetUseCache(true)                           // Enable caching
    .SetConnectionTimeout(30000)                 // Connection timeout in ms
    .SetReadTimeout(60000)                       // Read timeout in ms
    .SetMaxAsyncThreads(50)                      // Max async threads
    .SetMaxBlobSize(50L * 1024 * 1024 * 1024)   // Max blob size (50 GiB)
    .SetMaxRetryAttempts(3);                     // Max retry attempts
```

## OCI Configuration File

Create an OCI configuration file at `~/.oci/config`:

```ini
[DEFAULT]
user=ocid1.user.oc1..aaaaaaaxxxxx
fingerprint=xx:xx:xx:xx:xx:xx:xx:xx:xx:xx:xx:xx:xx:xx:xx:xx
tenancy=ocid1.tenancy.oc1..aaaaaaaxxxxx
region=us-ashburn-1
key_file=~/.oci/oci_api_key.pem
```

## Bucket Naming Rules

OCI Object Storage bucket names must:
- Be between 1 and 256 characters
- Contain only lowercase letters, numbers, hyphens, underscores, and periods
- Start and end with a letter or number
- Not contain consecutive periods

## Object Naming Rules

OCI Object Storage object names:
- Can be up to 1024 characters
- Can contain any UTF-8 characters except null
- Cannot be just "." or ".."
- Should not have leading or trailing whitespace

## Path Validation

```csharp
// Validate bucket name
bool isValid = OracleCloudObjectStorageAfsIntegration.ValidateBucketName("my-bucket");

// Validate object name
bool isValid = OracleCloudObjectStorageAfsIntegration.ValidateObjectName("path/to/object.dat");
```

## Integration with Embedded Storage

```csharp
using NebulaStore.Storage.EmbeddedConfiguration;
using NebulaStore.Afs.OracleCloud.ObjectStorage;

// Create embedded storage configuration
var config = OracleCloudObjectStorageAfsIntegration.CreateConfiguration(
    bucketName: "my-storage-bucket",
    configFilePath: "~/.oci/config",
    profile: "DEFAULT",
    region: "us-ashburn-1",
    useCache: true
);

// Use with embedded storage (when AFS integration is complete)
// var storageManager = EmbeddedStorage.Start(config);
```

## Advanced Configuration

```csharp
var config = OracleCloudObjectStorageConfiguration.New()
    .SetConfigFile("~/.oci/config", "PRODUCTION")
    .SetRegion("us-phoenix-1")
    .SetNamespace("my-custom-namespace")
    .SetConnectionTimeout(60000)
    .SetReadTimeout(120000)
    .SetMaxAsyncThreads(100)
    .SetMaxBlobSize(10L * 1024 * 1024 * 1024)  // 10 GiB per blob
    .SetMaxRetryAttempts(5)
    .SetUseCache(false);  // Disable caching for real-time consistency

config.Validate();  // Validate configuration before use
```

## Error Handling

```csharp
try
{
    var connector = OracleCloudObjectStorageConnector.New(config);
    var fileSystem = BlobStoreFileSystem.New(connector);
    
    // Perform operations
    var path = BlobStorePath.New("my-bucket", "data", "file.dat");
    fileSystem.WriteFile(path, dataBytes);
}
catch (InvalidOperationException ex)
{
    // Configuration error
    Console.WriteLine($"Configuration error: {ex.Message}");
}
catch (ArgumentException ex)
{
    // Path validation error
    Console.WriteLine($"Invalid path: {ex.Message}");
}
catch (Exception ex)
{
    // OCI API error
    Console.WriteLine($"OCI error: {ex.Message}");
}
```

## Performance Considerations

- **Caching**: Enable caching (`SetUseCache(true)`) for better read performance
- **Blob Size**: Adjust `MaxBlobSize` based on your data patterns (default: 50 GiB)
- **Async Threads**: Increase `MaxAsyncThreads` for high-concurrency scenarios
- **Timeouts**: Adjust timeouts based on network conditions and file sizes
- **Retry Attempts**: Configure retry attempts for transient failures

## Limitations

- Resource principal authentication is not yet implemented
- Simple authentication is not yet implemented
- Custom endpoint configuration is not yet supported
- Maximum object size is 50 GiB (OCI limit)

## License

This project is licensed under the Eclipse Public License 2.0 (EPL 2.0).

## Contributing

Contributions are welcome! Please see the main NebulaStore repository for contribution guidelines.

## Support

For issues and questions:
- GitHub Issues: https://github.com/hadv/NebulaStore/issues
- OCI Documentation: https://docs.oracle.com/en-us/iaas/Content/Object/home.htm
- OCI .NET SDK: https://github.com/oracle/oci-dotnet-sdk

## See Also

- [NebulaStore Documentation](../../../README.md)
- [Abstract File System (AFS)](../../blobstore/README.md)
- [AWS S3 Adapter](../../aws/s3/README.md)
- [Azure Storage Adapter](../../azure/storage/README.md)
- [Oracle Cloud Infrastructure Object Storage](https://docs.oracle.com/en-us/iaas/Content/Object/home.htm)

