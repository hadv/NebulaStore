# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NebulaStore is a .NET 9.0 class library that ports EclipseStore functionality to .NET Core. It provides ultra-fast object graph persistence without object-relational mapping overhead.

## Development Commands

**Note:** .NET SDK is installed at `~/.dotnet` - ensure this is in your PATH by running `export PATH="$PATH:$HOME/.dotnet"` before using dotnet commands.

- `dotnet build` - Build the entire solution
- `dotnet test` - Run all unit tests
- `dotnet restore` - Restore NuGet packages

## Project Structure

```
NebulaStore.sln              # Solution file
src/
  NebulaStore.Core/          # Core class library
    NebulaStore.Core.csproj  # Project file
    ObjectStore.cs           # Core object persistence engine
tests/
  NebulaStore.Core.Tests/    # Unit tests
    NebulaStore.Core.Tests.csproj
    ObjectStoreTests.cs      # Basic ObjectStore functionality tests
    ObjectStoreLazyQueryTests.cs # Lazy query traversal tests
```

## Architecture

- **NebulaStore.Storage**: Main storage module using .NET 9.0
  - **Embedded**: Embedded storage submodule (mirrors Eclipse Store structure)
    - **EmbeddedStorage**: Static factory class for creating storage managers
    - **IEmbeddedStorageManager**: Main interface for storage operations
    - **EmbeddedStorageFoundation**: Builder pattern for configuration
    - **IEmbeddedStorageConfiguration**: Configuration system
    - **Type Handlers**: Pluggable serialization system
    - **Storage Connections**: Connection management and lifecycle
- **Tests**: xUnit test project with comprehensive tests
- Dependencies: MessagePack for binary serialization

## Key Components

### Embedded Storage API (Primary)
- `EmbeddedStorage`: Static factory class (src/NebulaStore.Storage/Embedded/Storage/EmbeddedStorage.cs)
- `IEmbeddedStorageManager`: Main storage interface (src/NebulaStore.Storage/Embedded/Storage/IEmbeddedStorageManager.cs)
- `EmbeddedStorageFoundation`: Configuration builder (src/NebulaStore.Storage/Embedded/Storage/EmbeddedStorageFoundation.cs)
- `IEmbeddedStorageConfiguration`: Configuration interface (src/NebulaStore.Storage/Embedded/Storage/IEmbeddedStorageConfiguration.cs)
- `ITypeHandler`: Type serialization interface (src/NebulaStore.Storage/Embedded/Storage/IEmbeddedStorageFoundation.cs)
- `MessagePackTypeHandler`: Built-in type handlers (src/NebulaStore.Storage/Embedded/Storage/TypeHandlers/MessagePackTypeHandler.cs)