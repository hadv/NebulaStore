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

The module structure exactly mirrors the Eclipse Store Java repository for familiarity and consistency.

### Module Mapping

| Eclipse Store Java | NebulaStore .NET | Status |
|-------------------|------------------|---------|
| `storage/embedded` | `storage/embedded/` | ✅ Complete |
| `storage/embedded-configuration` | `storage/embedded-configuration/` | ✅ Complete |
| `storage/storage` | `storage/storage/` | ✅ Complete |

## Project Status

✅ **Core Modules Ported** - Successfully ported 3 core Eclipse Store modules to .NET Core:
- ✅ `storage/embedded` - Complete with embedded storage engine
- ✅ `storage/embedded-configuration` - Complete with configuration system
- ✅ `storage/storage` - Complete with core storage types and interfaces

🚧 **In Progress** - Additional Eclipse Store modules and advanced features

## Source Code References

This project ports code from the [Eclipse Store Java repository](https://github.com/eclipse-store/store). Key source modules:

- **Java Source**: [`storage/embedded`](https://github.com/eclipse-store/store/tree/main/storage/embedded) → **C# Port**: `storage/embedded/`
- **Java Source**: [`storage/embedded-configuration`](https://github.com/eclipse-store/store/tree/main/storage/embedded-configuration) → **C# Port**: `storage/embedded-configuration/`
- **Java Source**: [`storage/storage`](https://github.com/eclipse-store/store/tree/main/storage/storage) → **C# Port**: `storage/storage/`

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
- Dependencies: MessagePack for binary serialization

### Key Components
- **EmbeddedStorage**: Static factory class for creating storage managers
- **IEmbeddedStorageManager**: Main interface for storage operations
- **EmbeddedStorageFoundation**: Builder pattern for configuration
- **IEmbeddedStorageConfiguration**: Configuration system
- **Type Handlers**: Pluggable serialization system
- **Storage Connections**: Connection management and lifecycle

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