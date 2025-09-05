# NebulaStore

[![CI](https://github.com/hadv/NebulaStore/actions/workflows/ci.yml/badge.svg)](https://github.com/hadv/NebulaStore/actions/workflows/ci.yml)
[![Tests](https://img.shields.io/badge/tests-158%20passing-brightgreen)](https://github.com/hadv/NebulaStore/actions)
[![Migration](https://img.shields.io/badge/Eclipse%20Store%20Migration-In%20Progress-yellow)](https://github.com/hadv/NebulaStore)

A .NET port of [Eclipse Store](https://github.com/eclipse-store/store) - Ultra-fast pure object graph persistence. **Migration In Progress** 🚧

NebulaStore provides .NET developers with high-performance, pure object graph storage capabilities similar to Eclipse Store for Java developers. This project is actively porting core Eclipse Store modules to .NET.

## Overview

NebulaStore provides .NET developers with the same high-performance, pure object graph storage capabilities that Eclipse Store offers to Java developers. This project ports the core storage modules and architecture of Eclipse Store to work natively with .NET objects and the .NET runtime.

### Eclipse Store Modules Ported

This project ports the following Eclipse Store modules from the [original Java repository](https://github.com/eclipse-store/store):

- **storage/embedded** - Core embedded storage engine
- **storage/embedded-configuration** - Configuration system
- **storage/storage** - Core storage types and interfaces
- **afs/blobstore** - Abstract File System blob storage backend
- **afs/aws/s3** - AWS S3 storage backend
- **afs/azure/storage** - Azure Storage backend
- **gigamap/gigamap** - High-performance in-memory data structure with advanced indexing and performance optimizations

The module structure exactly mirrors the Eclipse Store Java repository for familiarity and consistency.

### Module Mapping

| Eclipse Store Java | NebulaStore .NET | Status |
|-------------------|------------------|---------|
| `storage/embedded` | `storage/embedded/` | ✅ Complete |
| `storage/embedded-configuration` | `storage/embedded-configuration/` | ✅ Complete |
| `storage/storage` | `storage/storage/` | ✅ Complete |
| `afs/blobstore` | `afs/blobstore/` | ✅ Complete |
| `afs/aws/s3` | `afs/aws/s3/` | ✅ Complete |
| `afs/azure/storage` | `afs/azure/storage/` | ✅ Complete |
| `afs/googlecloud/firestore` | `afs/googlecloud/firestore/` | ✅ Complete |
| `gigamap/gigamap` | `gigamap/` | ✅ Complete |

## 🚧 Eclipse Store Migration Progress

**Current Status: Core Modules Complete** - Successfully ported essential Eclipse Store functionality to .NET with working storage engine, configuration system, and high-performance collections.

### 🚀 Core Modules Status

- ✅ **`storage/embedded`** - Complete embedded storage engine
- ✅ **`storage/embedded-configuration`** - Complete configuration system
- ✅ **`storage/storage`** - Complete core storage types and interfaces
- ✅ **`afs/blobstore`** - Complete Abstract File System blob storage backend
- ✅ **`afs/aws/s3`** - Complete AWS S3 storage backend
- ✅ **`afs/azure/storage`** - Complete Azure Storage backend
- ✅ **`afs/googlecloud/firestore`** - Complete Google Cloud Firestore integration
- ✅ **`gigamap/gigamap`** - Complete high-performance indexed collections with:
  - ✅ **Advanced indexing system** (bitmap, hash, unique indices)
  - ✅ **Full LINQ support** for querying (Eclipse Store compatible)
  - ✅ **Performance optimizations** (bulk operations, compression, caching)
  - ✅ **Comprehensive test suite** (158 tests passing - 100% success rate)

### 🎯 Available Features

- ✅ **Core Eclipse Store API compatibility**
- ✅ **Pure object graph persistence** without ORM overhead
- ✅ **Advanced querying** with LINQ (equivalent to Java Stream API)
- ✅ **High-performance indexing** with bitmap and hash indices
- ✅ **Case-insensitive string indexing** with proper equality handling
- ✅ **Comprehensive statistics** and performance monitoring
- ✅ **Memory-efficient** data structures and operations
- ✅ **Thread-safe operations** with proper synchronization
- ✅ **Multiple storage backends** (local, AWS S3, Azure Storage)

## Source Code References

This project ports code from the [Eclipse Store Java repository](https://github.com/eclipse-store/store). Key source modules:

- **Java Source**: [`storage/embedded`](https://github.com/eclipse-store/store/tree/main/storage/embedded) → **C# Port**: `storage/embedded/`
- **Java Source**: [`storage/embedded-configuration`](https://github.com/eclipse-store/store/tree/main/storage/embedded-configuration) → **C# Port**: `storage/embedded-configuration/`
- **Java Source**: [`storage/storage`](https://github.com/eclipse-store/store/tree/main/storage/storage) → **C# Port**: `storage/storage/`
- **Java Source**: [`afs/blobstore`](https://github.com/eclipse-store/store/tree/main/afs/blobstore) → **C# Port**: `afs/blobstore/`
- **Java Source**: [`afs/aws/s3`](https://github.com/eclipse-store/store/tree/main/afs/aws/s3) → **C# Port**: `afs/aws/s3/`
- **Java Source**: [`afs/azure/storage`](https://github.com/eclipse-store/store/tree/main/afs/azure/storage) → **C# Port**: `afs/azure/storage/`
- **Java Source**: [`afs/googlecloud/firestore`](https://github.com/eclipse-store/store/tree/main/afs/googlecloud/firestore) → **C# Port**: `afs/googlecloud/firestore/`
- **Java Source**: [`gigamap/gigamap`](https://github.com/eclipse-store/store/tree/main/gigamap/gigamap) → **C# Port**: `gigamap/`

The .NET implementation maintains the same module structure, interfaces, and design patterns as the original Eclipse Store Java code while adapting to .NET conventions and leveraging C# language features.

## ✨ Features - Core Eclipse Store Implementation

### 🏗️ Core Storage Engine
- ✅ **Pure object graph persistence** - Direct object storage without ORM overhead
- ✅ **Ultra-fast storage and retrieval** - Optimized for high-performance operations
- ✅ **Native .NET integration** - Built specifically for .NET runtime and patterns
- ✅ **Embedded storage manager** with builder pattern configuration
- ✅ **Type handler system** for custom serialization and complex types
- ✅ **Lazy query traversal** for efficient data access

### 🚀 Advanced Querying & Indexing
- ✅ **GigaMap indexed collections** - High-performance data structures
- ✅ **Full LINQ support** - Natural .NET querying (equivalent to Java Stream API)
- ✅ **Bitmap indexing** for efficient range and equality queries
- ✅ **Hash indexing** for fast key-based lookups
- ✅ **Unique constraints** with automatic validation
- ✅ **Case-insensitive string indexing** with proper equality handling
- ✅ **Complex query conditions** with AND/OR/NOT operations

### ⚡ Performance & Optimization
- ✅ **Bulk operations** for high-throughput scenarios
- ✅ **Memory optimization** with efficient data structures
- ✅ **Query result caching** for repeated operations
- ✅ **Compression support** for space-efficient storage
- ✅ **Performance monitoring** with detailed statistics
- ✅ **Thread-safe operations** with proper synchronization

### 📊 Monitoring & Management
- ✅ **Comprehensive monitoring system** with real-time metrics
- ✅ **Storage statistics** and performance tracking
- ✅ **Entity cache monitoring** with memory usage insights
- ✅ **Housekeeping operations** for maintenance and optimization
- ✅ **Backup and restore** capabilities with validation

### 🌐 Storage Backends
- ✅ **Local file system** storage with configurable options
- ✅ **Abstract File System (AFS)** for pluggable storage backends
- ✅ **Blob storage** support for large object handling
- ✅ **AWS S3** storage backend for cloud storage
- ✅ **Azure Storage** backend for Microsoft cloud
- ✅ **Google Cloud Firestore** integration

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
  - **aws/** - AWS storage backends
    - **s3/** - AWS S3 integration
      - **src/** - S3 connector and extensions
      - **test/** - S3 integration tests
      - **NebulaStore.Afs.Aws.S3.csproj** - Project file
  - **azure/** - Azure storage backends
    - **storage/** - Azure Storage integration
      - **src/** - Azure Storage connector and extensions
      - **test/** - Azure Storage integration tests
      - **NebulaStore.Afs.Azure.Storage.csproj** - Project file
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

# Run AWS S3 tests
dotnet test afs/aws/s3/test/

# Run Azure Storage tests
dotnet test afs/azure/storage/test/

# Run GigaMap performance tests and benchmarks
dotnet test gigamap/tests/

# Run specific test project
dotnet test tests/NebulaStore.Core.Tests/
```

### 🚀 Using NebulaStore - Core Eclipse Store API

#### Simple Usage
```csharp
using NebulaStore.Storage.Embedded;

// Start with default configuration
using var storage = EmbeddedStorage.Start();

// Get the root object
var root = storage.Root<MyDataClass>();
root.SomeProperty = "value";

// Persist changes with ACID transaction
storage.StoreRoot();

// Query objects with LINQ (Eclipse Store compatible)
var results = storage.Query<SomeType>().Where(x => x.Condition).ToList();
```

#### Advanced GigaMap Usage
```csharp
using NebulaStore.GigaMap;

// Create high-performance indexed collection with Eclipse Store API
var gigaMap = GigaMap.Builder<Person>()
    .WithBitmapIndex(Indexer.Property<Person, string>("Department", p => p.Department))
    .WithBitmapUniqueIndex(Indexer.Property<Person, string>("Email", p => p.Email))
    .WithBitmapIndex(Indexer.StringIgnoreCase<Person>("Name", p => p.Name))
    .Build();

// Add data with ACID transactions
gigaMap.Add(new Person { Name = "John", Department = "Engineering", Email = "john@company.com" });
gigaMap.Add(new Person { Name = "Jane", Department = "Marketing", Email = "jane@company.com" });

// Advanced querying with LINQ (Eclipse Store pattern)
var engineers = gigaMap.Where(p => p.Department == "Engineering").ToList();
var johnDoe = gigaMap.FirstOrDefault(p => p.Name.Contains("John"));

// Case-insensitive queries work correctly
var caseInsensitive = gigaMap.Where(p => p.Name == "JOHN").ToList(); // Finds "John"

// Index statistics and monitoring
var departmentIndex = gigaMap.Index.Bitmap.Get("Department");
Console.WriteLine($"Unique departments: {departmentIndex.Size}");

var stats = departmentIndex.CreateStatistics();
Console.WriteLine($"Total entities: {stats.TotalEntityCount}");
Console.WriteLine($"Unique keys: {stats.UniqueKeyCount}");
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

#### AWS S3 Usage

```csharp
using NebulaStore.Afs.Aws.S3;

// Start with S3 storage
using var storage = EmbeddedStorage.StartWithS3("my-bucket", "storage-prefix");

// Custom S3 configuration
var s3Config = EmbeddedStorageConfiguration.New()
    .UseS3("my-bucket", "storage-prefix")
    .SetChannelCount(4)
    .Build();

using var s3Storage = EmbeddedStorage.Foundation(s3Config).Start();
```

#### Azure Storage Usage

```csharp
using NebulaStore.Afs.Azure.Storage;

// Start with Azure Storage
using var storage = EmbeddedStorage.StartWithAzure("connection-string", "container-name");

// Custom Azure configuration
var azureConfig = EmbeddedStorageConfiguration.New()
    .UseAzureStorage("connection-string", "container-name")
    .SetChannelCount(4)
    .Build();

using var azureStorage = EmbeddedStorage.Foundation(azureConfig).Start();
```

#### Google Cloud Firestore Usage

```csharp
using NebulaStore.Afs.GoogleCloud.Firestore;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

// Start with Google Cloud Firestore
using var storage = EmbeddedStorage.StartWithFirestore("your-project-id");

// Custom Firestore configuration
var firestoreConfig = EmbeddedStorageConfiguration.New()
    .UseFirestore("your-project-id", "my-storage", useCache: true)
    .SetChannelCount(4)
    .Build();

using var firestoreStorage = EmbeddedStorage.Foundation(firestoreConfig).Start();
```

#### Custom Root Object
```csharp
var myRoot = new MyDataClass { SomeProperty = "initial" };
using var storage = EmbeddedStorage.Start(myRoot, "storage-dir");
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

#### Performance Monitoring & Statistics
```csharp
// Comprehensive performance monitoring
var monitoringManager = storage.GetMonitoringManager();

// Storage statistics with detailed metrics
var stats = monitoringManager.StorageManagerMonitor.StorageStatistics;
Console.WriteLine($"Storage Usage: {stats.UsageRatio:P2}");
Console.WriteLine($"Total Objects: {stats.ObjectCount}");

// GigaMap performance statistics
var departmentIndex = gigaMap.Index.Bitmap.Get("Department");
var indexStats = departmentIndex.CreateStatistics();
Console.WriteLine($"Index Size: {indexStats.UniqueKeyCount} unique keys");
Console.WriteLine($"Total Entities: {indexStats.TotalEntityCount}");
Console.WriteLine($"Memory Usage: {indexStats.TotalDataMemorySize} bytes");

// Trigger maintenance operations
monitoringManager.StorageManagerMonitor.IssueFullGarbageCollection();
```

## 📚 Examples - Complete Feature Demonstrations

The `examples/` directory contains comprehensive examples demonstrating NebulaStore features:

### 🏗️ Core Storage Examples
- **`EmbeddedStorageExample.cs`** - Basic storage operations, configuration, and batch processing
- **`TransactionExample.cs`** - **NEW!** ACID transaction demonstrations with:
  - Atomic operations and rollback scenarios
  - Nested transaction support
  - Crash recovery and data integrity validation
  - Transaction logging and performance monitoring

### ☁️ Cloud Storage Examples
- **`FirestoreExample.cs`** - **NEW!** Google Cloud Firestore integration including:
  - Simple Firestore storage setup and configuration
  - Configuration-based approach with custom settings
  - Direct AFS usage for advanced scenarios
  - Batch operations and large object handling
- **`AzureStorageExample.cs`** - Azure Storage backend demonstrations
- **`S3Example.cs`** - AWS S3 storage backend examples

### 🔍 Advanced Querying Examples
- **`GigaMapAdvancedExample.cs`** - **NEW!** Advanced features including:
  - Eclipse Store compatible `GigaMap.Builder<T>()` API
  - Advanced indexing with bitmap and unique constraints
  - Case-insensitive string indexing demonstrations
  - Complex LINQ queries with index optimization
  - Index statistics and performance monitoring

### 📊 Monitoring & Performance Examples
- **`MonitoringExample.cs`** - Complete monitoring and metrics demonstration including:
  - Basic monitoring access and storage statistics
  - Multi-channel monitoring with per-channel metrics
  - Housekeeping operations and monitoring
  - Monitoring manager usage and monitor discovery
- **`PerformanceDemo.cs`** - Performance optimization demonstrations including:
  - Bulk operations performance comparison
  - Memory optimization techniques
  - Query performance benchmarks
  - Index efficiency analysis

### 🚀 Production Ready Examples
- **`ProductionExample.cs`** - **NEW!** Production deployment patterns including:
  - Complete configuration for production environments
  - Error handling and recovery strategies
  - Performance monitoring and alerting
  - Backup and restore procedures

Run the examples:
```bash
cd examples/ConsoleApp
dotnet run

# Run specific example
dotnet run -- --example GigaMapAdvanced
dotnet run -- --example Transactions
dotnet run -- --example Production
```

## 📖 Documentation

- **[Monitoring Documentation](storage/storage/src/monitoring/README.md)** - Comprehensive guide to the monitoring system
- **[Configuration Guide](storage/embedded-configuration/README.md)** - Storage configuration options
- **[GigaMap Advanced Features](gigamap/README.md)** - Complete guide to advanced features
- **[API Documentation](docs/)** - Generated API documentation

## 🚧 Eclipse Store Migration Status

**Current Progress: Core Functionality Complete** 🚀

The migration has successfully implemented essential Eclipse Store functionality for .NET, providing a solid foundation for object graph persistence.

### ✅ What's Available
- **Core Eclipse Store compatibility** - Essential APIs ported to .NET
- **Stable foundation** - 158 tests passing with good coverage
- **High-performance indexing** - GigaMap collections with advanced features
- **Native .NET integration** - Built specifically for .NET runtime and patterns
- **Multiple storage backends** - Local, AWS S3, Azure Storage support

### 🎯 Current Capabilities
NebulaStore currently provides:
- ✅ **Core object graph persistence** without ORM overhead
- ✅ **High-performance indexing** with GigaMap collections
- ✅ **Comprehensive monitoring** and performance tracking
- ✅ **Multiple storage backends** (local, AFS, AWS S3, Azure Storage)
- ✅ **Solid test coverage** and validation

### 🤝 Contributing
The core migration is complete, but we welcome contributions for:
- Additional storage backends
- Performance optimizations
- Documentation improvements
- Example applications
- Integration with other .NET frameworks

### 📞 Support
- **Issues**: [GitHub Issues](https://github.com/hadv/NebulaStore/issues)
- **Discussions**: [GitHub Discussions](https://github.com/hadv/NebulaStore/discussions)
- **Documentation**: [Wiki](https://github.com/hadv/NebulaStore/wiki)

---

**🎯 Eclipse Store for .NET - Core Implementation Complete!** NebulaStore provides ultra-fast, pure object graph persistence similar to Eclipse Store for Java developers, now available for the .NET ecosystem with core functionality and native C# integration.