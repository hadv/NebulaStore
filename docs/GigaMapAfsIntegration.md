# GigaMap + AFS Integration

This document describes the integration between NebulaStore's GigaMap and Abstract File System (AFS), following Eclipse Store patterns for seamless persistence.

## Overview

The GigaMap + AFS integration provides:

- **🔄 Transparent Integration**: GigaMap seamlessly integrates with AFS storage backends
- **⚡ Lazy Loading**: Entities are loaded on-demand from AFS storage
- **💾 Automatic Persistence**: Changes are automatically persisted following Eclipse Store patterns
- **🌐 Multi-Backend Support**: Works with local storage, Google Cloud Firestore, and future backends
- **🎯 Eclipse Store Compatibility**: Maintains all Eclipse Store API patterns and behaviors

## Quick Start

### 1. Basic Setup with AFS

```csharp
using NebulaStore.Storage.Embedded;
using NebulaStore.GigaMap;

// Create storage with AFS enabled
using var storage = EmbeddedStorage.StartWithAfs("my-storage");

// Create GigaMap with automatic AFS integration
var gigaMap = storage.CreateGigaMap<Person>()
    .WithBitmapIndex(Indexer.Property<Person, string>("Email", p => p.Email))
    .WithBitmapIndex(Indexer.Property<Person, int>("Age", p => p.Age))
    .Build();

// Add data - automatically persisted through AFS
gigaMap.Add(new Person { Email = "john@example.com", Name = "John", Age = 30 });

// Query with LINQ - data loaded from AFS as needed
var results = gigaMap.Where(p => p.Age > 25).ToList();
```

### 2. Eclipse Store Compatibility

```csharp
// Following Eclipse Store patterns exactly
using var storage = EmbeddedStorage.StartWithAfs("my-storage");

var gigaMap = storage.CreateGigaMap<Person>().Build();
storage.RegisterGigaMap(gigaMap);

// Add and update data
gigaMap.Add(new Person { Email = "jane@example.com", Name = "Jane", Age = 28 });

// Store following Eclipse Store pattern
gigaMap.Store(); // Individual GigaMap storage
// or
await storage.StoreGigaMapsAsync(); // Store all registered GigaMaps
```

## Architecture

### Core Components

1. **AfsGigaMap<T>**: AFS-aware wrapper that provides lazy loading and automatic persistence
2. **AfsGigaMapBuilder<T>**: Builder that creates AFS-integrated GigaMaps
3. **GigaMapStorageExtensions**: Extension methods for seamless integration
4. **GigaMapPersistenceMetadata**: Metadata for AFS storage format

### Storage Structure

```
storage-directory/
├── gigamap/
│   ├── MyApp.Person/
│   │   ├── metadata.msgpack
│   │   ├── entities.msgpack
│   │   └── indices.msgpack
│   └── MyApp.Product/
│       ├── metadata.msgpack
│       ├── entities.msgpack
│       └── indices.msgpack
└── [other AFS storage files]
```

## Features

### Automatic Persistence

Following Eclipse Store's pattern, changes are automatically persisted:

```csharp
var gigaMap = storage.CreateGigaMap<Person>().Build();

// These operations trigger automatic persistence
gigaMap.Add(person);           // Auto-stored
gigaMap.Update(person, p => p.Age = 31); // Auto-stored
gigaMap.RemoveById(entityId);  // Auto-stored
```

### Lazy Loading

Data is loaded on-demand from AFS storage:

```csharp
var gigaMap = storage.CreateGigaMap<Person>().Build();

// First access triggers loading from AFS
var count = gigaMap.Size; // Loads metadata
var person = gigaMap.Get(1); // Loads entities if needed
```

### Multi-Backend Support

Works with all AFS backends:

```csharp
// Local blob storage
using var localStorage = EmbeddedStorage.StartWithAfs("local-storage");

// Google Cloud Firestore
using var firestoreStorage = EmbeddedStorageFirestoreExtensions
    .StartWithFirestore("your-project-id");

// Both support the same GigaMap API
var gigaMap = storage.CreateGigaMap<Person>().Build();
```

## Configuration

### AFS-Specific Settings

```csharp
var config = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("my-storage")
    .SetUseAfs(true)                    // Enable AFS integration
    .SetAfsStorageType("blobstore")     // Storage backend type
    .SetAfsUseCache(true)               // Enable caching
    .SetChannelCount(4)                 // Parallel processing
    .Build();

using var storage = EmbeddedStorage.Foundation(config).Start();
```

### GigaMap Configuration

```csharp
var gigaMap = storage.CreateGigaMap<Person>()
    // Index configuration
    .WithBitmapIndex(Indexer.Property<Person, string>("Email", p => p.Email))
    .WithBitmapUniqueIndex(Indexer.Property<Person, string>("SSN", p => p.SSN))
    
    // Performance tuning
    .WithSegmentSize(16)                // Segment size exponent
    .WithInitialCapacity(1000)          // Initial capacity hint
    
    // Equality comparison
    .WithValueEquality()                // Use value equality
    .Build();
```

## Performance Considerations

### Caching

AFS includes caching that improves GigaMap performance:

```csharp
// Enable caching for better performance
.SetAfsUseCache(true)
```

### Batch Operations

Use batch operations for better performance:

```csharp
// Batch add
var people = new List<Person> { /* ... */ };
gigaMap.AddAll(people);

// Batch store
await storage.StoreGigaMapsAsync();
```

### Lazy Loading Optimization

Structure your queries to minimize loading:

```csharp
// Efficient: Uses indices, minimal loading
var engineers = gigaMap.Where(p => p.Department == "Engineering").ToList();

// Less efficient: Loads all entities
var allPeople = gigaMap.ToList();
```

## Error Handling

The integration includes comprehensive error handling:

```csharp
try
{
    using var storage = EmbeddedStorage.StartWithAfs(config);
    var gigaMap = storage.CreateGigaMap<Person>().Build();
    
    // Operations...
}
catch (AfsStorageException ex)
{
    // Handle AFS-specific errors
    Console.WriteLine($"AFS error: {ex.Message}");
}
catch (GigaMapException ex)
{
    // Handle GigaMap-specific errors
    Console.WriteLine($"GigaMap error: {ex.Message}");
}
```

## Migration from Traditional Storage

To migrate existing applications to AFS:

1. **Update Configuration:**
   ```csharp
   // Before
   using var storage = EmbeddedStorage.Start("storage");
   
   // After
   using var storage = EmbeddedStorage.StartWithAfs("storage");
   ```

2. **No Code Changes Required:**
   - Existing GigaMap code works unchanged
   - AFS integration is transparent
   - Performance improvements are automatic

## Testing

Run integration tests to verify functionality:

```bash
# Run GigaMap + AFS integration tests
dotnet test storage/embedded/tests/ --filter Category=GigaMapAfs

# Run specific integration test
dotnet test storage/embedded/tests/GigaMapAfsIntegrationTests.cs
```

## Troubleshooting

### Common Issues

1. **Storage Type Not Supported**
   - Ensure AFS storage type is correctly specified
   - Currently supported: "blobstore"

2. **Performance Issues**
   - Enable caching with `SetAfsUseCache(true)`
   - Consider adjusting channel count
   - Use batch operations for large datasets

3. **Loading Errors**
   - Check storage directory permissions
   - Verify AFS configuration
   - Review error logs for specific issues

### Debug Information

Enable detailed logging for troubleshooting:

```csharp
var config = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("my-storage")
    .SetUseAfs(true)
    .SetAfsStorageType("blobstore")
    .SetAfsUseCache(true)
    .Build();
```

## Next Steps

This integration provides the foundation for:

1. **Recovery Mechanisms**: Data integrity and crash recovery
2. **Performance Optimization**: Large-scale performance tuning
3. **Advanced Features**: Enhanced querying and indexing
4. **Additional Backends**: SQL databases, cloud storage, Redis

The GigaMap + AFS integration follows Eclipse Store patterns while leveraging NebulaStore's powerful AFS infrastructure for maximum flexibility and performance.
