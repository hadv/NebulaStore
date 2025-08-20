# NebulaStore

[![CI](https://github.com/hadv/NebulaStore/actions/workflows/ci.yml/badge.svg)](https://github.com/hadv/NebulaStore/actions/workflows/ci.yml)

A .NET port of [Eclipse Store](https://github.com/eclipse-store/store) - Ultra-fast pure object graph persistence. Ports core Eclipse Store modules from Java to .NET Core with native C# integration.

## Overview

NebulaStore provides .NET developers with the same high-performance, pure object graph storage capabilities that Eclipse Store offers to Java developers. This project ports the core storage modules and architecture of Eclipse Store to work natively with .NET objects and the .NET runtime.

### Eclipse Store Modules Ported

This project ports the following Eclipse Store modules from the [original Java repository](https://github.com/eclipse-store/store):

- **storage/embedded** - Core embedded storage engine
- **storage/embedded-configuration** - Configuration system
- **storage/storage** - Core storage types and interfaces
- **afs/blobstore** - Abstract File System blob storage backend
- **gigamap/gigamap** - High-performance in-memory data structure with advanced indexing and performance optimizations

The module structure exactly mirrors the Eclipse Store Java repository for familiarity and consistency.

### Module Mapping

| Eclipse Store Java | NebulaStore .NET | Status |
|-------------------|------------------|---------|
| `storage/embedded` | `storage/embedded/` | ✅ Complete |
| `storage/embedded-configuration` | `storage/embedded-configuration/` | ✅ Complete |
| `storage/storage` | `storage/storage/` | ✅ Complete |
| `afs/blobstore` | `afs/blobstore/` | ✅ Complete |
| `afs/googlecloud/firestore` | `afs/googlecloud/firestore/` | ✅ Complete |
| `gigamap/gigamap` | `gigamap/` | ✅ Complete |

## Project Status

✅ **Core Modules Ported** - Successfully ported 6 Eclipse Store modules to .NET Core:
- ✅ `storage/embedded` - Complete with embedded storage engine
- ✅ `storage/embedded-configuration` - Complete with configuration system
- ✅ `storage/storage` - Complete with core storage types and interfaces
- ✅ `afs/blobstore` - Complete with Abstract File System blob storage backend
- ✅ `afs/googlecloud/firestore` - Complete Google Cloud Firestore integration
- ✅ `gigamap/gigamap` - Complete high-performance in-memory data structure with:
  - ✅ Advanced indexing system (bitmap, hash, range indices)
  - ✅ Performance optimizations (bulk operations, compression, caching)
  - ✅ Comprehensive test suite (141 tests passing)
  - ✅ Performance benchmarks and monitoring
  - ✅ Production-ready with full documentation

🚧 **In Progress** - Additional Eclipse Store modules and advanced features

## Source Code References

This project ports code from the [Eclipse Store Java repository](https://github.com/eclipse-store/store). Key source modules:

- **Java Source**: [`storage/embedded`](https://github.com/eclipse-store/store/tree/main/storage/embedded) → **C# Port**: `storage/embedded/`
- **Java Source**: [`storage/embedded-configuration`](https://github.com/eclipse-store/store/tree/main/storage/embedded-configuration) → **C# Port**: `storage/embedded-configuration/`
- **Java Source**: [`storage/storage`](https://github.com/eclipse-store/store/tree/main/storage/storage) → **C# Port**: `storage/storage/`
- **Java Source**: [`afs/blobstore`](https://github.com/eclipse-store/store/tree/main/afs/blobstore) → **C# Port**: `afs/blobstore/`
- **Java Source**: [`afs/googlecloud/firestore`](https://github.com/eclipse-store/store/tree/main/afs/googlecloud/firestore) → **C# Port**: `afs/googlecloud/firestore/`
- **Java Source**: [`gigamap/gigamap`](https://github.com/eclipse-store/store/tree/main/gigamap/gigamap) → **C# Port**: `gigamap/`

The .NET implementation maintains the same module structure, interfaces, and design patterns as the original Eclipse Store Java code while adapting to .NET conventions and leveraging C# language features.

## Features

- ✅ Pure object graph persistence
- ✅ Ultra-fast storage and retrieval
- ✅ No object-relational mapping overhead
- ✅ Native .NET integration
- ✅ Embedded storage manager with builder pattern
- ✅ Configurable storage options
- ✅ Type handler system for custom serialization
- ✅ Lazy query traversal
- ✅ Backup and restore capabilities
- ✅ Comprehensive monitoring and metrics system
- ✅ Real-time storage statistics and performance monitoring
- ✅ Memory-safe monitoring with WeakReference pattern
- 🚧 Thread-safe operations (in progress)
- 🚧 ACID compliance (in progress)

## Architecture

NebulaStore follows the Eclipse Store module structure:

- **storage/** - Main storage module (mirrors Eclipse Store)
  - **embedded/** - Embedded storage submodule
    - **src/** - Core embedded storage implementation
    - **tests/** - Comprehensive test suite
    - **NebulaStore.Storage.Embedded.csproj** - Project file
  - **embedded-configuration/** - Configuration module (mirrors Eclipse Store)
    - **src/** - Configuration classes and interfaces
    - **NebulaStore.Storage.EmbeddedConfiguration.csproj** - Project file
  - **storage/** - Core storage types module (mirrors Eclipse Store)
    - **src/types/** - Storage interfaces and implementations
    - **NebulaStore.Storage.csproj** - Project file
- **afs/** - Abstract File System module (mirrors Eclipse Store)
  - **blobstore/** - Blob storage backend implementation
    - **src/** - Core AFS blobstore implementation
    - **test/** - Unit tests for blobstore functionality
    - **NebulaStore.Afs.Blobstore.csproj** - Project file
  - **googlecloud/** - Google Cloud storage backends
    - **firestore/** - Google Cloud Firestore integration
      - **src/** - Firestore connector and extensions
      - **test/** - Firestore integration tests
      - **NebulaStore.Afs.GoogleCloud.Firestore.csproj** - Project file
  - **tests/** - Integration tests for AFS functionality
- **gigamap/** - High-performance in-memory data structure module (mirrors Eclipse Store)
  - **src/** - Core GigaMap implementation with indexing and performance optimizations
  - **tests/** - Comprehensive test suite with performance benchmarks
  - **NebulaStore.GigaMap.csproj** - Project file
- Dependencies: MessagePack for binary serialization

### Key Components
- **EmbeddedStorage**: Static factory class for creating storage managers
- **IEmbeddedStorageManager**: Main interface for storage operations
- **EmbeddedStorageFoundation**: Builder pattern for configuration
- **IEmbeddedStorageConfiguration**: Configuration system
- **Type Handlers**: Pluggable serialization system
- **Storage Connections**: Connection management and lifecycle
- **Abstract File System (AFS)**: Pluggable storage backends with blob support
- **GigaMap**: High-performance in-memory data structure with advanced indexing and optimization features

## Getting Started

### Prerequisites

- .NET 9.0 SDK or later

### Building the Project

```bash
# Clone the repository
git clone https://github.com/hadv/NebulaStore.git
cd NebulaStore

# Restore dependencies
dotnet restore

# Build the solution
dotnet build
```

### Running Tests

```bash
# Run all unit tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run AFS blobstore tests
dotnet test afs/blobstore/test/

# Run AFS integration tests
dotnet test afs/tests/

# Run Firestore tests (requires emulator or actual Firestore)
dotnet test afs/googlecloud/firestore/test/

# Run GigaMap performance tests and benchmarks
dotnet test gigamap/tests/

# Run specific test project
dotnet test tests/NebulaStore.Core.Tests/
```

### Using NebulaStore

#### Simple Usage
```csharp
using NebulaStore.Storage.Embedded;

// Start with default configuration
using var storage = EmbeddedStorage.Start();

// Get the root object
var root = storage.Root<MyDataClass>();
root.SomeProperty = "value";

// Persist changes
storage.StoreRoot();

// Query objects
var results = storage.Query<SomeType>().Where(x => x.Condition).ToList();
```

#### Advanced Configuration
```csharp
var config = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("my-storage")
    .SetChannelCount(4)
    .SetEntityCacheThreshold(2000000)
    .Build();

using var storage = EmbeddedStorage.Start(config);
```

#### AFS (Abstract File System) Usage
```csharp
// Start with AFS blob storage
using var storage = EmbeddedStorage.StartWithAfs("afs-storage");

// Custom AFS configuration
var afsConfig = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("my-afs-storage")
    .SetUseAfs(true)
    .SetAfsStorageType("blobstore")
    .SetAfsUseCache(true)
    .Build();

using var afsStorage = EmbeddedStorage.StartWithAfs(afsConfig);
```

#### Google Cloud Firestore Usage

**Prerequisites:**
- Google Cloud Project with Firestore enabled
- Service Account with appropriate permissions (`Cloud Datastore User` role minimum)
- Authentication configured (see authentication methods below)

```csharp
using NebulaStore.Afs.GoogleCloud.Firestore;
using Google.Cloud.Firestore;

// Method 1: Using Service Account Key
var firestore = new FirestoreDbBuilder
{
    ProjectId = "your-project-id",
    CredentialsPath = "path/to/service-account-key.json"
}.Build();

// Method 2: Using Application Default Credentials (ADC)
Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "path/to/service-account-key.json");
var firestore = FirestoreDb.Create("your-project-id");

// Start with Firestore
using var storage = EmbeddedStorageFirestoreExtensions.StartWithFirestore("your-project-id");

// Custom Firestore configuration
var firestoreConfig = EmbeddedStorageConfiguration.New()
    .UseFirestore("your-project-id", "my-storage-collection")
    .SetChannelCount(4)
    .Build();

using var firestoreStorage = EmbeddedStorage.Foundation(firestoreConfig).Start();
```

#### Custom Root Object
```csharp
var myRoot = new MyDataClass { SomeProperty = "initial" };
using var storage = EmbeddedStorage.Start(myRoot, "storage-dir");
```

#### Batch Operations
```csharp
using var storer = storage.CreateStorer();
var objectIds = storer.StoreAll(obj1, obj2, obj3);
storer.Commit();
```

#### Backup & Restore
```csharp
await storage.CreateBackupAsync("backup-directory");
```

#### Monitoring & Metrics
```csharp
// Access comprehensive monitoring
var monitoringManager = storage.GetMonitoringManager();

// Storage statistics
var stats = monitoringManager.StorageManagerMonitor.StorageStatistics;
Console.WriteLine($"Usage Ratio: {stats.UsageRatio:P2}");

// Entity cache metrics
var cacheMonitor = monitoringManager.EntityCacheSummaryMonitor;
Console.WriteLine($"Cached Entities: {cacheMonitor.EntityCount}");

// Trigger housekeeping operations
monitoringManager.StorageManagerMonitor.IssueFullGarbageCollection();
```

#### GigaMap High-Performance Data Structure
```csharp
using NebulaStore.GigaMap;
using NebulaStore.GigaMap.Performance;

// Create a high-performance GigaMap with indexing
var gigaMap = GigaMap.Builder<Employee>()
    .WithBitmapIndex(Indexer.Property<Employee, string>("Department", e => e.Department))
    .WithHashIndex(Indexer.Property<Employee, int>("EmployeeId", e => e.EmployeeId))
    .Build();

// Add entities with bulk operations for better performance
var employees = CreateEmployees(10000);
await SimplePerformanceOptimizations.BulkAddAsync(gigaMap, employees);

// Fast indexed queries
var engineeringEmployees = gigaMap.Query()
    .Where(e => e.Department == "Engineering")
    .ToList();

// Performance optimizations
await SimplePerformanceOptimizations.ApplyBasicOptimizationsAsync(gigaMap);

// Compression for memory efficiency
var compressedData = await CompressionOptimizer.CompressEntitiesAsync(employees);
Console.WriteLine($"Compression: {compressedData.CompressionPercentage:F1}% space saved");

// Query result caching
using var cache = new CompressedQueryCache<Employee>();
await cache.CacheResultAsync("engineering_dept", engineeringEmployees);
var cachedResults = await cache.TryGetCachedResultAsync("engineering_dept");

// Performance monitoring
var stats = SimplePerformanceOptimizations.GetPerformanceStats(gigaMap);
var recommendations = SimplePerformanceOptimizations.GetOptimizationRecommendations(gigaMap);
```

## Examples

The `examples/` directory contains comprehensive examples demonstrating various NebulaStore features:

- **`EmbeddedStorageExample.cs`** - Basic storage operations, configuration, and batch processing
- **`MonitoringExample.cs`** - Complete monitoring and metrics demonstration including:
  - Basic monitoring access and storage statistics
  - Multi-channel monitoring with per-channel metrics
  - Housekeeping operations and monitoring
  - Monitoring manager usage and monitor discovery
- **`PerformanceDemo.cs`** - GigaMap performance optimization demonstrations including:
  - Bulk operations performance comparison
  - Compression effectiveness testing
  - Query cache performance benchmarks
  - Memory optimization techniques

Run the examples:
```bash
cd examples/ConsoleApp
dotnet run
```

## Documentation

- **[Monitoring Documentation](storage/storage/src/monitoring/README.md)** - Comprehensive guide to the monitoring system
- **[Configuration Guide](storage/embedded-configuration/README.md)** - Storage configuration options
- **[API Documentation](docs/)** - Generated API documentation (coming soon)