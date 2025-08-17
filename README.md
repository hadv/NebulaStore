# NebulaStore

[![CI](https://github.com/hadv/NebulaStore/actions/workflows/ci.yml/badge.svg)](https://github.com/hadv/NebulaStore/actions/workflows/ci.yml)

A .NET Core port of EclipseStore, bringing ultra-fast Java object persistence to the .NET ecosystem.

## Overview

NebulaStore aims to provide .NET developers with the same high-performance, pure object graph storage capabilities that EclipseStore offers to Java developers. This project ports the core concepts and architecture of EclipseStore to work natively with .NET objects and the .NET runtime.

## Project Status

🚧 **In Development** - Currently porting core EclipseStore functionality to .NET Core

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
    - **NebulaStore.Storage.Embedded** - Core embedded storage implementation
    - **tests/** - Comprehensive test suite
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