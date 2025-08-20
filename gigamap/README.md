# NebulaStore.GigaMap

A high-performance indexed collection for .NET, inspired by EclipseStore's GigaMap. Designed to handle vast amounts of data with exceptional performance through lazy loading and bitmap indexing.

## Overview

GigaMap is an indexed collection that stores data in nested, lazy-loaded segments backed by bitmap indices. This approach allows for efficient querying of data without loading all entities into memory, enabling the handling of billions of entities with exceptional performance.

## Features

- 🚀 **High Performance**: Bitmap indexing for O(1) key lookups
- 💾 **Memory Efficient**: Lazy loading minimizes memory footprint
- 🔍 **Advanced Querying**: Fluent API with complex conditions (AND/OR/NOT)
- 🛡️ **Type Safety**: Strongly typed indexers and queries
- ⚡ **Scalable**: Designed to handle billions of entities
- 🔒 **Constraints**: Unique constraints and custom validation
- 🏗️ **Builder Pattern**: Fluent configuration API

## Quick Start

### Installation

```bash
dotnet add package NebulaStore.GigaMap
```

### Basic Usage

```csharp
using NebulaStore.GigaMap;

// Create a simple GigaMap
var gigaMap = GigaMap.New<Person>();

// Add data
var person = new Person { Name = "John Doe", Email = "john@example.com", Age = 30 };
var entityId = gigaMap.Add(person);

// Query data
var results = gigaMap.Query().Execute();
```

### Advanced Configuration

```csharp
var gigaMap = GigaMap.Builder<Person>()
    // Add bitmap indices for efficient querying
    .WithBitmapIndex(Indexer.Property<Person, string>("Email", p => p.Email))
    .WithBitmapIndex(Indexer.Property<Person, int>("Age", p => p.Age))
    .WithBitmapIndex(Indexer.StringIgnoreCase<Person>("Name", p => p.Name))

    // Add unique constraints
    .WithBitmapUniqueIndex(Indexer.Property<Person, string>("Email", p => p.Email))

    // Add custom validation
    .WithCustomConstraint(Constraint.Custom<Person>(
        "ValidAge",
        person => person.Age >= 0 && person.Age <= 150,
        "Age must be between 0 and 150"))

    // Configure equality comparison
    .WithValueEquality()

    .Build();
```

### Querying Examples

```csharp
// Simple equality query
var adults = gigaMap.Query("Age").Where(age => age >= 18).Execute();

// Complex queries with multiple conditions
var seniorEngineers = gigaMap.Query("Department", "Engineering")
    .And("Age").Where(age => age >= 30)
    .Execute();

// OR conditions
var techAndMarketing = gigaMap.Query("Department", "Engineering")
    .Or("Department", "Marketing")
    .Execute();

// Aggregation and pagination
var count = gigaMap.Query("Department", "Engineering").Count();
var firstEngineer = gigaMap.Query("Department", "Engineering").FirstOrDefault();
var pagedResults = gigaMap.Query("Age").Where(age => age >= 25)
    .Skip(10)
    .Limit(20)
    .Execute();
```

## Architecture

### Core Components

- **IGigaMap<T>**: Main interface providing CRUD operations and querying
- **Indexing System**: Bitmap indices for efficient data retrieval
- **Query Engine**: Fluent API for building and executing queries
- **Constraint System**: Validation and uniqueness enforcement
- **Builder Pattern**: Fluent configuration of GigaMap instances

### Indexer Types

GigaMap provides factory methods for common indexer types:

```csharp
// String indexers
Indexer.Property<T, string>("PropertyName", entity => entity.Property)
Indexer.StringIgnoreCase<T>("PropertyName", entity => entity.Property)

// Numeric indexers
Indexer.Numeric<T, int>("Age", entity => entity.Age)
Indexer.Numeric<T, decimal>("Salary", entity => entity.Salary)

// Date/Time indexers
Indexer.DateTime<T>("CreatedAt", entity => entity.CreatedAt)

// GUID indexers
Indexer.Guid<T>("Id", entity => entity.Id)

// Identity indexers (reference equality)
Indexer.Identity<T>("Identity")
```

### Constraint System

```csharp
// Unique constraints
.WithBitmapUniqueIndex(indexer)

// Custom constraints
.WithCustomConstraint(Constraint.Custom<T>(
    "ConstraintName",
    entity => /* validation logic */,
    "Error message"))

// Entity-level validation
.WithCustomConstraint(Constraint.Custom<T>(
    "ComplexValidation",
    (entityId, oldEntity, newEntity) => /* complex validation */,
    "Complex validation failed"))
```

## Performance Characteristics

- **Memory Efficiency**: Lazy loading minimizes memory footprint
- **Query Performance**: Bitmap indices provide O(1) key lookups
- **Scalability**: Designed to handle billions of entities
- **Thread Safety**: Concurrent read/write operations supported

## Examples

See the [Examples](src/Examples/) directory for comprehensive usage examples:

- **PersonExample.cs**: Complete demonstration of CRUD operations, querying, and constraints

## Documentation

For detailed documentation, see:

- [API Documentation](src/README.md)
- [Examples](src/Examples/)
- [EclipseStore GigaMap Reference](https://docs.eclipse.org/store/)

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

## License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.

## Acknowledgments

- Inspired by [EclipseStore GigaMap](https://github.com/eclipse-store/store)
- Part of the [NebulaStore](https://github.com/hadv/NebulaStore) ecosystem