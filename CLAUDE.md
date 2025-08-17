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

- **NebulaStore.Core**: Core class library using .NET 9.0
- **ObjectStore**: Main persistence engine that provides EclipseStore-like functionality
  - File-based object graph storage using MessagePack serialization
  - Lazy query traversal with `Query<T>()` method
  - Root object management with `Root<T>()` method
  - Transactional commits with `Commit()` method
- **Tests**: xUnit test project with comprehensive ObjectStore tests
- Dependencies: MessagePack for binary serialization

## Key Components

- `ObjectStore`: Main persistence class (src/NebulaStore.Core/ObjectStore.cs:7)
- `RootWrapper`: Internal wrapper for type-safe persistence (src/NebulaStore.Core/ObjectStore.cs:123)