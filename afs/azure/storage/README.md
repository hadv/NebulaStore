# NebulaStore Azure Blob Storage Connector

Azure Blob Storage implementation for NebulaStore Abstract File System (AFS).

## Overview

This connector enables NebulaStore to use Azure Blob Storage as a backend storage system. It implements the `IBlobStoreConnector` interface and provides seamless integration with Azure's cloud storage services.

## Features

- **Azure Blob Storage Integration**: Store data directly in Azure Blob Storage containers
- **Automatic Blob Management**: Files are automatically split into numbered blobs for optimal performance
- **Container Validation**: Enforces Azure container naming rules and validation
- **Caching Support**: Optional caching for improved performance
- **Connection String Support**: Multiple authentication methods including connection strings and managed identity
- **Large File Support**: Handles files larger than Azure's single blob limits through automatic chunking

## Installation

Add the NuGet package to your project:

```bash
dotnet add package NebulaStore.Afs.Azure.Storage
```

## Quick Start

### Using with EmbeddedStorage

```csharp
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

var config = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("my-container")
    .SetUseAfs(true)
    .SetAfsStorageType("azure.storage")
    .SetAfsConnectionString("DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net")
    .SetAfsUseCache(true)
    .Build();

using var storage = EmbeddedStorage.StartWithAfs(config);

// Use storage normally
var root = storage.Root<MyDataClass>();
root.SomeProperty = "value";
storage.StoreRoot();
```

### Direct AFS Usage

```csharp
using Azure.Storage.Blobs;
using NebulaStore.Afs.Azure.Storage;
using NebulaStore.Afs.Blobstore;

// Create Azure Blob Service client
var connectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net";
var blobServiceClient = new BlobServiceClient(connectionString);

// Create connector
using var connector = AzureStorageConnector.New(blobServiceClient);

// Create file system
using var fileSystem = BlobStoreFileSystem.New(connector);

// Perform operations
var path = BlobStorePath.New("my-container", "folder", "file.txt");
var data = System.Text.Encoding.UTF8.GetBytes("Hello, Azure!");

fileSystem.IoHandler.WriteData(path, new[] { data });
var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
```

## Configuration

### Connection String

```csharp
var config = EmbeddedStorageConfiguration.New()
    .SetAfsConnectionString("DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net")
    .Build();
```

### Managed Identity

```csharp
using Azure.Identity;
using Azure.Storage.Blobs;

var credential = new DefaultAzureCredential();
var blobServiceClient = new BlobServiceClient(new Uri("https://myaccount.blob.core.windows.net"), credential);
var connector = AzureStorageConnector.New(blobServiceClient);
```

### Advanced Configuration

```csharp
var azureConfig = AzureStorageConfiguration.New()
    .SetConnectionString(connectionString)
    .SetUseCache(true)
    .SetMaxBlobSize(100 * 1024 * 1024) // 100MB per blob
    .SetRetryOptions(new BlobRequestOptions { MaximumExecutionTime = TimeSpan.FromMinutes(5) });

var connector = AzureStorageConnector.New(azureConfig);
```

## Container Naming Rules

Azure Blob Storage containers must follow specific naming rules:

- Container names must be between 3 and 63 characters long
- Container names can contain only lowercase letters, numbers, and dashes (-)
- Container names must begin with a lowercase letter or number
- Container names cannot end with a dash (-)
- Container names cannot have consecutive dashes (--)

## Performance Considerations

- **Blob Size**: Files larger than 100MB are automatically split into multiple blobs
- **Caching**: Enable caching for frequently accessed metadata
- **Parallel Operations**: The connector supports concurrent read/write operations
- **Regional Placement**: Choose Azure regions close to your application for optimal performance

## Error Handling

The connector includes comprehensive error handling for common Azure storage scenarios:

```csharp
try
{
    using var storage = EmbeddedStorage.StartWithAfs(config);
    // ... operations
}
catch (Azure.RequestFailedException ex) when (ex.Status == 404)
{
    // Container or blob not found
    Console.WriteLine($"Resource not found: {ex.Message}");
}
catch (Azure.RequestFailedException ex) when (ex.Status == 403)
{
    // Access denied
    Console.WriteLine($"Access denied: {ex.Message}");
}
catch (ArgumentException ex)
{
    // Invalid container name or configuration
    Console.WriteLine($"Configuration error: {ex.Message}");
}
```

## Examples

See the [examples](../../../examples/) directory for complete working examples:

- `AzureStorageExample.cs` - Basic Azure storage usage
- `AzureStorageAdvancedExample.cs` - Advanced configuration and error handling
- `AzureStoragePerformanceExample.cs` - Performance optimization techniques

## Testing

The connector includes comprehensive tests that can run against:

- **Azurite Emulator**: Local development and testing
- **Azure Storage Account**: Integration testing with real Azure services

```bash
# Run tests with Azurite emulator
dotnet test

# Run integration tests (requires Azure storage account)
dotnet test --configuration Release --logger "console;verbosity=detailed"
```

## License

This project is licensed under the MIT License - see the [LICENSE](../../../LICENSE) file for details.
