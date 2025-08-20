# NebulaStore GigaMap

A .NET implementation of EclipseStore's GigaMap - an indexed collection designed to handle vast amounts of data with exceptional performance through lazy loading and bitmap indexing.

## Overview

GigaMap is an indexed collection that stores data in nested, lazy-loaded segments backed by bitmap indices. This approach allows for efficient querying of data without loading all entities into memory, enabling the handling of billions of entities with exceptional performance.

## Key Features

- **Lazy Loading**: Only loads segments required for query results on demand
- **Bitmap Indexing**: Off-heap bitmap indices for efficient entity lookups
- **Hierarchical Storage**: Three-level segment structure for optimal memory usage
- **Fluent Query API**: Intuitive query builder with AND/OR conditions
- **Constraint System**: Unique constraints and custom validation rules
- **High Performance**: Designed to handle billions of entities efficiently
- **Type Safety**: Strongly typed indexers and queries

## Architecture

### Core Components

1. **IGigaMap<T>**: Main interface providing CRUD operations and querying
2. **Indexing System**: Bitmap indices for efficient data retrieval
3. **Query Engine**: Fluent API for building and executing queries
4. **Constraint System**: Validation and uniqueness enforcement
5. **Builder Pattern**: Fluent configuration of GigaMap instances

### Indexing

GigaMap uses bitmap indices to enable efficient querying:

- **Bitmap Index**: Maps index keys to entity IDs using bitmaps
- **Multiple Index Types**: Support for strings, numbers, dates, GUIDs, and custom types
- **Lazy Loading**: Indices are loaded on-demand during queries
- **Memory Optimization**: Configurable compression and optimization

### Segment Structure

Data is organized in a three-level hierarchical structure:

- **Low Level**: Small segments for frequently accessed data
- **Mid Level**: Medium segments for balanced access patterns
- **High Level**: Large segments for bulk storage

## Usage Examples

### Basic Setup

```csharp
using NebulaStore.Storage.Embedded.GigaMap;

// Create a simple GigaMap
var gigaMap = GigaMap.New<Person>();

// Or use the builder for advanced configuration
var configuredGigaMap = GigaMap.Builder<Person>()
    .WithBitmapIndex(Indexer.Property<Person, string>("Email", p => p.Email))
    .WithBitmapIndex(Indexer.Property<Person, string>("Department", p => p.Department))
    .WithBitmapUniqueIndex(Indexer.Property<Person, string>("Email", p => p.Email))
    .WithCustomConstraint(Constraint.Custom<Person>(
        "ValidAge",
        person => person.Age >= 0 && person.Age <= 150,
        "Age must be between 0 and 150"))
    .Build();
```

### Adding Data

```csharp
var person = new Person
{
    FirstName = "John",
    LastName = "Doe",
    Email = "john.doe@company.com",
    Age = 30,
    Department = "Engineering"
};

var entityId = gigaMap.Add(person);

// Add multiple entities
var people = new[] { person1, person2, person3 };
var lastId = gigaMap.AddAll(people);
```

### Querying

```csharp
// Simple equality query
var engineeringPeople = gigaMap.Query("Department", "Engineering").Execute();

// Complex queries with conditions
var seniorEngineers = gigaMap.Query("Department", "Engineering")
    .And("Age").Where(age => age >= 30)
    .Execute();

// OR conditions
var techAndMarketing = gigaMap.Query("Department", "Engineering")
    .Or("Department", "Marketing")
    .Execute();

// Count and existence checks
var count = gigaMap.Query("Department", "Engineering").Count();
var hasAny = gigaMap.Query("Age").Where(age => age > 65).Any();

// First result
var firstEngineer = gigaMap.Query("Department", "Engineering").FirstOrDefault();

// Pagination
var pagedResults = gigaMap.Query("Department", "Engineering")
    .Skip(10)
    .Limit(20)
    .Execute();
```

### Indexers

GigaMap provides factory methods for common indexer types:

```csharp
// String indexers
Indexer.Property<Person, string>("Email", p => p.Email)
Indexer.StringIgnoreCase<Person>("LastName", p => p.LastName)

// Numeric indexers
Indexer.Numeric<Person, int>("Age", p => p.Age)
Indexer.Numeric<Person, decimal>("Salary", p => p.Salary)

// Date/Time indexers
Indexer.DateTime<Person>("DateOfBirth", p => p.DateOfBirth)

// GUID indexers
Indexer.Guid<Person>("Id", p => p.Id)

// Identity indexers (reference equality)
Indexer.Identity<Person>("Identity")
```

### Constraints

```csharp
// Unique constraints
.WithBitmapUniqueIndex(Indexer.Property<Person, string>("Email", p => p.Email))

// Custom constraints
.WithCustomConstraint(Constraint.Custom<Person>(
    "ValidEmail",
    person => !string.IsNullOrEmpty(person.Email) && person.Email.Contains("@"),
    "Email must be valid"))

// Multiple constraints
.WithCustomConstraints(
    Constraint.Custom<Person>("ValidAge", p => p.Age >= 0, "Age must be positive"),
    Constraint.Custom<Person>("ValidSalary", p => p.Salary > 0, "Salary must be positive")
)
```

### Configuration Options

```csharp
var gigaMap = GigaMap.Builder<Person>()
    // Equality comparison
    .WithValueEquality()        // Use value-based equality
    .WithIdentityEquality()     // Use reference equality
    .WithEqualityComparer(customComparer)

    // Segment size configuration
    .WithSegmentSize(8)         // Low level: 2^8 = 256
    .WithSegmentSize(8, 12)     // Low: 2^8, Mid: 2^12 = 4096
    .WithSegmentSize(8, 12, 20) // Low: 2^8, Mid: 2^12, High max: 2^20

    .Build();
```

## Performance Characteristics

- **Memory Efficiency**: Lazy loading minimizes memory footprint
- **Query Performance**: Bitmap indices provide O(1) key lookups
- **Scalability**: Designed to handle billions of entities
- **Concurrent Access**: Thread-safe operations with read-write locking
- **Storage Integration**: Seamless integration with NebulaStore persistence

## Implementation Status

This is a foundational implementation of the GigaMap concept adapted for .NET. Key features implemented:

✅ Core interfaces and builder pattern
✅ Basic bitmap indexing system
✅ Fluent query API with conditions
✅ Constraint system with validation
✅ CRUD operations and entity management
✅ Example usage and documentation

### Future Enhancements

- Full hierarchical segment implementation
- Advanced bitmap compression and optimization
- Asynchronous query execution
- Integration with NebulaStore persistence layer
- Performance benchmarking and optimization
- Advanced indexing strategies (range queries, full-text search)

## Integration with NebulaStore

GigaMap is designed to integrate seamlessly with NebulaStore's embedded storage system:

```csharp
// Future integration example
var storageManager = new EmbeddedStorageManager();
var gigaMap = storageManager.CreateGigaMap<Person>()
    .WithBitmapIndex(Indexer.Property<Person, string>("Email", p => p.Email))
    .Build();

// Automatic persistence
await gigaMap.StoreAsync();
```

## See Also

- [EclipseStore GigaMap Documentation](https://docs.eclipse.org/store/)
- [NebulaStore Embedded Storage](../README.md)
- [Example Usage](Examples/PersonExample.cs)