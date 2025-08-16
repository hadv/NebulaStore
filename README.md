# NebulaStore

[![CI](https://github.com/hadv/NebulaStore/actions/workflows/ci.yml/badge.svg)](https://github.com/hadv/NebulaStore/actions/workflows/ci.yml)

A .NET Core port of EclipseStore, bringing ultra-fast Java object persistence to the .NET ecosystem.

## Overview

NebulaStore aims to provide .NET developers with the same high-performance, pure object graph storage capabilities that EclipseStore offers to Java developers. This project ports the core concepts and architecture of EclipseStore to work natively with .NET objects and the .NET runtime.

## Project Status

🚧 **In Development** - Currently porting core EclipseStore functionality to .NET Core

## Features (Planned)

- Pure object graph persistence
- Ultra-fast storage and retrieval
- No object-relational mapping overhead
- Native .NET integration
- Thread-safe operations
- ACID compliance

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later

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

### Using ObjectStore

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