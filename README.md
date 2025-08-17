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

### Using ObjectStore (Legacy API)

```csharp
// Create or open an ObjectStore
using var store = new ObjectStore("data.msgpack");

// Get the root object
var root = store.Root<MyDataClass>();

// Modify data
root.SomeProperty = "value";

// Persist changes
store.Commit();

// Query objects
var results = store.Query<SomeType>().Where(x => x.Condition).ToList();
```

### Using Embedded Storage (New API)

```csharp
using NebulaStore.Core.Storage;

// Simple usage with default configuration
using var storage = EmbeddedStorage.Start();
var root = storage.Root<MyDataClass>();
root.SomeProperty = "value";
storage.StoreRoot();

// Advanced configuration
var config = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("my-storage")
    .SetChannelCount(4)
    .SetEntityCacheThreshold(2000000)
    .Build();

using var storage = EmbeddedStorage.Start(config);

// Custom root object
var myRoot = new MyDataClass { SomeProperty = "initial" };
using var storage = EmbeddedStorage.Start(myRoot, "storage-dir");

// Batch operations
using var storer = storage.CreateStorer();
var objectIds = storer.StoreAll(obj1, obj2, obj3);
storer.Commit();

// Query objects
var results = storage.Query<SomeType>().Where(x => x.Condition).ToList();

// Backup
await storage.CreateBackupAsync("backup-directory");
```