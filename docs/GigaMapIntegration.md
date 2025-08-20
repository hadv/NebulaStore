# GigaMap Integration with NebulaStore

## Overview

GigaMap is now fully integrated with NebulaStore's embedded storage system, providing high-performance indexed collections with persistent storage capabilities. This integration allows you to create large, queryable collections that automatically persist to disk and support complex queries with bitmap indexing.

## Key Features

- **🔍 High-Performance Indexing**: Bitmap indices for lightning-fast queries
- **💾 Automatic Persistence**: Seamless integration with NebulaStore's storage system
- **🎯 Complex Queries**: Support for AND/OR conditions, ranges, and custom constraints
- **⚡ Lazy Loading**: Efficient memory usage with on-demand data loading
- **🔧 Easy Configuration**: Simple builder pattern for setup

## Quick Start

### 1. Basic Setup

```csharp
using NebulaStore.Storage.Embedded;
using NebulaStore.GigaMap;

// Create storage with GigaMap enabled
var config = EmbeddedStorageConfiguration.Builder()
    .SetStorageDirectory("my-storage")
    .SetGigaMapEnabled(true)
    .Build();

using var storage = EmbeddedStorage.Start(config);
```

### 2. Create and Configure GigaMap

```csharp
// Create a GigaMap for Person entities
var gigaMap = storage.CreateGigaMap<Person>()
    .WithBitmapIndex(Indexer.Property<Person, string>("Email", p => p.Email))
    .WithBitmapIndex(Indexer.Property<Person, int>("Age", p => p.Age))
    .WithBitmapIndex(Indexer.Property<Person, string>("Department", p => p.Department))
    .WithBitmapUniqueIndex(Indexer.Property<Person, string>("Email", p => p.Email)) // Unique constraint
    .Build();

// Register for automatic persistence
storage.RegisterGigaMap(gigaMap);
```

### 3. Add Data

```csharp
var person1 = new Person 
{ 
    Email = "john.doe@company.com", 
    Name = "John Doe", 
    Age = 30, 
    Department = "Engineering" 
};

var person2 = new Person 
{ 
    Email = "jane.smith@company.com", 
    Name = "Jane Smith", 
    Age = 25, 
    Department = "Marketing" 
};

var id1 = gigaMap.Add(person1);
var id2 = gigaMap.Add(person2);
```

### 4. Query Data

```csharp
// Simple queries
var engineers = gigaMap.Query()
    .And(Indexer.Property<Person, string>("Department", p => p.Department).Is("Engineering"))
    .Execute();

// Complex queries with multiple conditions
var youngEngineers = gigaMap.Query()
    .And(Indexer.Property<Person, string>("Department", p => p.Department).Is("Engineering"))
    .And(Indexer.Property<Person, int>("Age", p => p.Age).IsLessThan(35))
    .Execute();

// Range queries
var middleAged = gigaMap.Query()
    .And(Indexer.Property<Person, int>("Age", p => p.Age).IsGreaterThanOrEqual(25))
    .And(Indexer.Property<Person, int>("Age", p => p.Age).IsLessThanOrEqual(40))
    .Execute();
```

### 5. Persistence

```csharp
// Store individual GigaMap
await gigaMap.StoreAsync();

// Store all registered GigaMaps
await storage.StoreGigaMapsAsync();
```

## Configuration Options

### GigaMap-Specific Configuration

```csharp
var config = EmbeddedStorageConfiguration.Builder()
    .SetStorageDirectory("my-storage")
    .SetGigaMapEnabled(true)                        // Enable/disable GigaMap functionality
    .SetGigaMapDefaultSegmentSize(16)               // Default segment size (2^16 = 65536 entities)
    .SetGigaMapUseOffHeapIndices(false)             // Use off-heap storage for indices
    .SetGigaMapIndexDirectory("gigamap-indices")    // Directory for index files
    .Build();
```

### GigaMap Builder Options

```csharp
var gigaMap = storage.CreateGigaMap<MyEntity>()
    // Index types
    .WithBitmapIndex(indexer)           // Regular index (allows duplicates)
    .WithBitmapUniqueIndex(indexer)     // Unique index (enforces uniqueness)
    .WithBitmapIdentityIndex(indexer)   // Identity index (reference equality)
    
    // Equality comparison
    .WithValueEquality()                // Use value equality (default)
    .WithIdentityEquality()             // Use reference equality
    .WithEqualityComparer(comparer)     // Custom equality comparer
    
    // Performance tuning
    .WithSegmentSize(16)                // Segment size exponent
    .WithInitialCapacity(1000)          // Initial capacity hint
    
    // Constraints
    .WithCustomConstraint(constraint)   // Custom validation rules
    .Build();
```

## Advanced Usage

### Custom Indexers

```csharp
// Create custom indexers for complex properties
var fullNameIndexer = Indexer.Create<Person, string>(
    "FullName", 
    person => $"{person.FirstName} {person.LastName}"
);

var yearOfBirthIndexer = Indexer.Create<Person, int>(
    "YearOfBirth",
    person => person.DateOfBirth.Year
);

var gigaMap = storage.CreateGigaMap<Person>()
    .WithBitmapIndex(fullNameIndexer)
    .WithBitmapIndex(yearOfBirthIndexer)
    .Build();
```

### Complex Queries

```csharp
// OR conditions
var results = gigaMap.Query()
    .Or(
        gigaMap.Query().And(departmentIndexer.Is("Engineering")),
        gigaMap.Query().And(departmentIndexer.Is("Research"))
    )
    .Execute();

// Pagination
var pagedResults = gigaMap.Query()
    .And(ageIndexer.IsGreaterThan(25))
    .Skip(20)
    .Limit(10)
    .Execute();

// Sorting (if supported by your entity)
var sortedResults = gigaMap.Query()
    .OrderBy(person => person.Name)
    .Execute();
```

### Retrieving Existing GigaMaps

```csharp
// Get previously registered GigaMap
var existingGigaMap = storage.GetGigaMap<Person>();
if (existingGigaMap != null)
{
    Console.WriteLine($"Found existing GigaMap with {existingGigaMap.Size} entities");
}
```

## Performance Tips

1. **Choose Appropriate Indices**: Only index properties you'll query frequently
2. **Segment Size**: Larger segments = better query performance, smaller segments = better memory usage
3. **Unique Constraints**: Use unique indices for properties that should be unique (like email addresses)
4. **Batch Operations**: Add multiple entities before calling `StoreAsync()` for better performance
5. **Query Optimization**: Use the most selective conditions first in your queries

## Integration with Existing NebulaStore Features

GigaMap works seamlessly with existing NebulaStore features:

- **Transactions**: GigaMap operations participate in NebulaStore transactions
- **Type Handlers**: Custom serialization works with GigaMap entities
- **Monitoring**: GigaMap operations are included in storage monitoring
- **Backup/Restore**: GigaMap data is included in backup and restore operations

## Example Entity

```csharp
public class Person
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Department { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    
    public override string ToString()
    {
        return $"{Name} ({Email}) - Age: {Age}, Dept: {Department}";
    }
}
```

## Next Steps

- See `examples/GigaMapExample.cs` for a complete working example
- Run the integration tests in `tests/GigaMapIntegrationTest.cs`
- Explore advanced querying capabilities in the GigaMap documentation
