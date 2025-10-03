# NebulaStore Abstract File System - NIO

The NIO (New I/O) module provides a .NET I/O adapter for the NebulaStore Abstract File System. This implementation uses standard .NET file system APIs (`System.IO`) to provide file operations, making it the default and most straightforward storage backend for local file systems.

## Overview

The NIO module is a direct port of the Eclipse Store AFS NIO module from Java to .NET. It provides:

- **Local File System Access**: Direct access to the local file system using .NET's `System.IO`
- **File Stream Management**: Efficient file stream handling with automatic positioning
- **Path Resolution**: Flexible path resolution with home directory expansion support
- **Thread-Safe Operations**: All operations are thread-safe with proper synchronization
- **Integration**: Seamless integration with the NebulaStore AFS infrastructure

## Architecture

### Core Components

1. **NioFileSystem**: Main entry point for NIO file system operations
2. **NioIoHandler**: Handles all I/O operations (read, write, copy, move, etc.)
3. **NioConnector**: Implements `IBlobStoreConnector` for AFS integration
4. **NioReadableFile**: Wrapper for read-only file access
5. **NioWritableFile**: Wrapper for read-write file access
6. **NioPathResolver**: Resolves path elements to file system paths

### Class Hierarchy

```
INioFileSystem
└── NioFileSystem

INioIoHandler
└── NioIoHandler

INioFileWrapper
├── INioReadableFile
│   └── NioReadableFile<TUser>
└── INioWritableFile
    └── NioWritableFile<TUser>

IBlobStoreConnector
└── NioConnector
```

## Usage

### Basic File System Operations

```csharp
using NebulaStore.Afs.Nio;
using NebulaStore.Afs.Blobstore;

// Create a NIO file system
using var fileSystem = NioFileSystem.New();

// Create a path
var path = BlobStorePath.New("container", "folder", "file.txt");

// Wrap for writing
using var writableFile = fileSystem.WrapForWriting(path);

// Write data
var data = System.Text.Encoding.UTF8.GetBytes("Hello, NIO!");
fileSystem.IoHandler.WriteBytes(writableFile, data);

// Wrap for reading
using var readableFile = fileSystem.WrapForReading(path);

// Read data
var readData = fileSystem.IoHandler.ReadBytes(readableFile);
var content = System.Text.Encoding.UTF8.GetString(readData);
Console.WriteLine(content); // Output: Hello, NIO!
```

### Using NioConnector with BlobStoreFileSystem

```csharp
using NebulaStore.Afs.Nio;
using NebulaStore.Afs.Blobstore;

// Create a NIO connector
using var connector = NioConnector.New("./my-storage", useCache: false);

// Create a blob store file system with NIO backend
using var fileSystem = BlobStoreFileSystem.New(connector);

// Use the file system
var path = BlobStorePath.New("data", "users", "user1.dat");
var userData = System.Text.Encoding.UTF8.GetBytes("User data");

fileSystem.IoHandler.WriteData(path, userData);
var retrievedData = fileSystem.IoHandler.ReadData(path, 0, -1);
```

### Integration with EmbeddedStorage

```csharp
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

// Configure storage to use NIO backend
var config = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("./nio-storage")
    .SetUseAfs(true)
    .SetAfsStorageType("nio")  // Use NIO backend
    .Build();

using var storage = EmbeddedStorage.Start(config);

// Use storage normally
var root = storage.Root<MyDataClass>();
root.SomeProperty = "value";
storage.StoreRoot();
```

### Advanced File Operations

```csharp
using NebulaStore.Afs.Nio;
using NebulaStore.Afs.Blobstore;

using var fileSystem = NioFileSystem.New();
var ioHandler = fileSystem.IoHandler;

// Create directory
var dirPath = BlobStorePath.New("container", "subfolder");
ioHandler.CreateDirectory(dirPath);

// List files
var filePath = BlobStorePath.New("container");
var files = ioHandler.ListFiles(filePath);
foreach (var file in files)
{
    Console.WriteLine($"File: {file}");
}

// Copy file
var sourcePath = BlobStorePath.New("container", "source.txt");
var targetPath = BlobStorePath.New("container", "target.txt");

using var sourceFile = fileSystem.WrapForReading(sourcePath);
using var targetFile = fileSystem.WrapForWriting(targetPath);

ioHandler.CopyFile(sourceFile, targetFile);

// Move file
var newPath = BlobStorePath.New("container", "moved.txt");
using var movableFile = fileSystem.WrapForWriting(targetPath);
using var destinationFile = fileSystem.WrapForWriting(newPath);

ioHandler.MoveFile(movableFile, destinationFile);
```

### Path Resolution

```csharp
using NebulaStore.Afs.Nio;

var fileSystem = NioFileSystem.New();

// Home directory expansion
var pathElements = fileSystem.ResolvePath("~/Documents/data");
// Returns: ["Users", "username", "Documents", "data"] (on Unix-like systems)

// Regular path
var elements = fileSystem.ResolvePath("/var/data/files");
// Returns: ["var", "data", "files"]
```

## Features

### File Stream Management

- **Automatic Positioning**: File streams are automatically positioned at the end for append operations
- **Stream Lifecycle**: Proper stream opening, closing, and disposal
- **Reopen Support**: Ability to reopen streams with different options
- **Validation**: Validates file access modes and options

### Thread Safety

All operations are thread-safe:
- File wrapper operations use internal mutex for synchronization
- I/O handler operations are stateless and thread-safe
- File system operations properly handle concurrent access

### Error Handling

Comprehensive error handling:
- `FileNotFoundException` for missing files
- `InvalidOperationException` for retired file wrappers
- `ArgumentException` for invalid parameters
- `IOException` for I/O errors

## Comparison with Other AFS Backends

| Feature | NIO | BlobStore | AWS S3 | Azure | Redis |
|---------|-----|-----------|--------|-------|-------|
| Local Files | ✓ | ✓ | ✗ | ✗ | ✗ |
| Cloud Storage | ✗ | ✗ | ✓ | ✓ | ✗ |
| In-Memory | ✗ | ✗ | ✗ | ✗ | ✓ |
| Caching | ✗ | ✓ | ✓ | ✓ | N/A |
| Performance | High | High | Medium | Medium | Very High |
| Setup Complexity | Low | Low | Medium | Medium | Low |

## Performance Considerations

- **Direct I/O**: Uses .NET's native file I/O for maximum performance
- **No Overhead**: Minimal abstraction overhead compared to other backends
- **Buffering**: Leverages .NET's built-in buffering mechanisms
- **Streaming**: Supports efficient streaming for large files

## Best Practices

1. **Dispose Resources**: Always dispose file wrappers and file systems
2. **Use Using Statements**: Leverage C#'s `using` statement for automatic disposal
3. **Handle Exceptions**: Properly handle file I/O exceptions
4. **Path Validation**: Validate paths before operations
5. **Concurrent Access**: Be aware of file locking when multiple processes access the same files

## Limitations

- **Local Only**: Only supports local file system access
- **No Caching**: Does not include built-in caching (use BlobStore with NioConnector for caching)
- **Platform-Specific**: Path handling may vary across platforms (Windows vs. Unix-like)

## Migration from Java Eclipse Store

This module is a direct port of the Eclipse Store AFS NIO module. Key differences:

- `FileChannel` → `FileStream`
- `Path` (Java NIO) → `string` (file system path)
- `OpenOption` → `FileMode`, `FileAccess`, `FileShare`
- `ByteBuffer` → `byte[]`
- Exception types adapted to .NET conventions

## See Also

- [AFS Overview](../README.md)
- [BlobStore Module](../blobstore/README.md)
- [Eclipse Store NIO Documentation](https://github.com/eclipse-store/store/tree/main/afs/nio)

