# NebulaStore AFS Redis Connector

Redis connector for NebulaStore Abstract File System (AFS). This module provides a Redis-based storage backend for NebulaStore, allowing you to store object graphs in Redis using the StackExchange.Redis client.

## Overview

The Redis AFS connector stores files as numbered blob entries in Redis. Each blob is stored as a separate Redis key with binary data as the value. This implementation follows the Eclipse Store AFS pattern and is compatible with the NebulaStore storage system.

## Features

- **Redis Storage**: Store NebulaStore data in Redis key-value store
- **Caching Support**: Optional in-memory caching for improved performance
- **Configurable**: Flexible configuration options for connection, timeouts, and database selection
- **Thread-Safe**: All operations are thread-safe
- **Compatible**: Works seamlessly with NebulaStore's embedded storage system

## Installation

```bash
dotnet add package NebulaStore.Afs.Redis
dotnet add package StackExchange.Redis
```

## Quick Start

### Using with EmbeddedStorage

```csharp
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

// Configure storage to use Redis
var config = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("storage")
    .SetAfsStorageType("redis")
    .SetAfsConnectionString("localhost:6379")
    .SetAfsUseCache(true);

// Start storage with Redis backend
using var storage = EmbeddedStorage.StartWithAfs(config);

// Use storage normally
var root = storage.Root<MyDataClass>();
root.SomeProperty = "value";
storage.StoreRoot();
```

### Direct AFS Usage

```csharp
using StackExchange.Redis;
using NebulaStore.Afs.Redis;
using NebulaStore.Afs.Blobstore;

// Create Redis connection
var redis = ConnectionMultiplexer.Connect("localhost:6379");

// Create connector
using var connector = RedisConnector.New(redis);

// Create file system
using var fileSystem = BlobStoreFileSystem.New(connector);

// Perform operations
var path = BlobStorePath.New("my-container", "folder", "file.dat");
var data = System.Text.Encoding.UTF8.GetBytes("Hello, Redis!");

fileSystem.IoHandler.WriteData(path, data);
var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
```

### With Caching

```csharp
// Create connector with caching enabled
using var connector = RedisConnector.Caching("localhost:6379");
using var fileSystem = BlobStoreFileSystem.New(connector);
```

### Using RedisConfiguration

```csharp
using StackExchange.Redis;
using NebulaStore.Afs.Redis;

// Create configuration
var config = RedisConfiguration.New()
    .SetConnectionString("localhost:6379")
    .SetDatabaseNumber(0)
    .SetUseCache(true)
    .SetCommandTimeout(TimeSpan.FromMinutes(1))
    .SetPassword("your-password")  // Optional
    .SetUseSsl(false);             // Optional

// Build StackExchange.Redis options
var options = config.ToConfigurationOptions();
var redis = ConnectionMultiplexer.Connect(options);

// Create connector
using var connector = RedisConnector.New(redis);
```

## Configuration Options

### RedisConfiguration Properties

- **ConnectionString**: Redis connection string (default: "localhost:6379")
- **DatabaseNumber**: Redis database number (default: 0)
- **UseCache**: Enable in-memory caching (default: true)
- **CommandTimeout**: Command execution timeout (default: 1 minute)
- **ConnectTimeout**: Connection timeout (default: 5 seconds)
- **SyncTimeout**: Synchronous operation timeout (default: 5 seconds)
- **AllowAdmin**: Allow admin operations (default: true)
- **AbortOnConnectFail**: Abort on connection failure (default: false)
- **Password**: Redis authentication password (optional)
- **UseSsl**: Enable SSL/TLS (default: false)

### AFS Configuration

When using with EmbeddedStorage, configure via `IEmbeddedStorageConfiguration`:

```csharp
var config = EmbeddedStorageConfiguration.New()
    .SetAfsStorageType("redis")
    .SetAfsConnectionString("localhost:6379")
    .SetAfsUseCache(true);
```

## Architecture

### Storage Structure

Files are stored in Redis using a hierarchical key structure:

```
container/path/to/file.0
container/path/to/file.1
container/path/to/file.2
```

Each file is split into numbered blobs (suffixed with `.0`, `.1`, `.2`, etc.). This allows for efficient storage and retrieval of large files.

### Key Features

1. **Virtual Directories**: Directories are virtual in Redis - they don't need to be explicitly created
2. **Blob Numbering**: Files are split into numbered blobs for efficient storage
3. **Atomic Operations**: Uses Redis atomic operations for data consistency
4. **Caching**: Optional in-memory caching reduces Redis queries

## Performance Considerations

- **Caching**: Enable caching for read-heavy workloads
- **Connection Pooling**: StackExchange.Redis handles connection pooling automatically
- **Database Selection**: Use different database numbers to isolate data
- **Timeouts**: Adjust timeouts based on your network latency and data size

## Requirements

- .NET 9.0 or later
- StackExchange.Redis 2.8.16 or later
- Redis server 5.0 or later (recommended)

## Thread Safety

All operations in the RedisConnector are thread-safe. The connector uses:
- Lock-based synchronization for cache operations
- StackExchange.Redis's built-in thread-safe operations
- Atomic Redis commands where applicable

## Error Handling

The connector handles common Redis errors:
- Connection failures
- Timeout errors
- Key not found scenarios
- Data serialization issues

## Limitations

- Redis key size limits apply (512 MB per key)
- Memory constraints of Redis server
- Network latency affects performance
- Requires Redis server to be running and accessible

## Examples

### Complete Example with Custom Data

```csharp
using StackExchange.Redis;
using NebulaStore.Afs.Redis;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Storage.Embedded;

// Define your data model
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public List<Order> Orders { get; set; } = new();
}

public class Order
{
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
}

// Configure and use Redis storage
var config = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("redis-storage")
    .SetAfsStorageType("redis")
    .SetAfsConnectionString("localhost:6379")
    .SetAfsUseCache(true);

using var storage = EmbeddedStorage.StartWithAfs(config);

// Create and store data
var root = storage.Root<Customer>();
root.Id = 1;
root.Name = "John Doe";
root.Orders.Add(new Order { OrderId = 100, Amount = 99.99m });

storage.StoreRoot();
```

## Troubleshooting

### Connection Issues

If you encounter connection issues:
1. Verify Redis server is running: `redis-cli ping`
2. Check connection string format
3. Verify network connectivity
4. Check firewall settings

### Performance Issues

For performance optimization:
1. Enable caching for read-heavy workloads
2. Adjust command timeouts
3. Use connection multiplexing
4. Monitor Redis memory usage

## License

This project is licensed under the MIT License.

## Contributing

Contributions are welcome! Please ensure:
- Code follows existing patterns
- Tests are included
- Documentation is updated

## Related

- [NebulaStore](../../README.md)
- [AFS Overview](../README.md)
- [Eclipse Store](https://github.com/eclipse-store/store)
- [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis)

