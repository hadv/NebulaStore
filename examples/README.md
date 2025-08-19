# NebulaStore Examples

This directory contains comprehensive examples demonstrating the capabilities of NebulaStore, a high-performance embedded storage solution for .NET applications.

## 📁 Directory Structure

```
examples/
├── README.md                           # This documentation
├── ConsoleApp/                         # Runnable console application
│   ├── NebulaStore.Examples.ConsoleApp.csproj
│   └── Program.cs                      # Interactive menu system
├── EmbeddedStorageExample.cs           # Core storage functionality examples
├── MonitoringExample.cs                # Storage monitoring examples
└── SharedDomainClasses.cs              # Common domain classes and type handlers
```

## 🚀 Quick Start

### Prerequisites

- **.NET 9.0 SDK or later** - [Download here](https://dotnet.microsoft.com/download)
- **Operating System**: Windows, Linux, or macOS
- **IDE** (optional but recommended):
  - Visual Studio 2022 (Windows)
  - VS Code with C# extension (Cross-platform)
  - JetBrains Rider (Cross-platform)
  - Any text editor for command-line development

### Verify Installation
```bash
# Check .NET version
dotnet --version

# Should show 9.0.x or later
```

### Running the Examples

1. **Navigate to the NebulaStore root directory:**

   **Option A - If you cloned the repository:**
   ```bash
   cd NebulaStore
   ```

   **Option B - Using full path:**
   ```bash
   # Windows
   cd C:\path\to\NebulaStore

   # Linux/macOS
   cd /path/to/NebulaStore
   ```

   **Option C - From examples directory:**
   ```bash
   # If you're already in the examples folder
   cd ..
   ```

2. **Build the project:**
   ```bash
   dotnet build examples/ConsoleApp/NebulaStore.Examples.ConsoleApp.csproj
   ```

3. **Run the interactive examples:**
   ```bash
   dotnet run --project examples/ConsoleApp/NebulaStore.Examples.ConsoleApp.csproj
   ```

4. **Choose from the menu:**
   - `1` - Run Embedded Storage Example only
   - `2` - Run Monitoring Example only
   - `3` - Run Both Examples sequentially
   - `0` - Exit

## 📚 Example Descriptions

### 1. Embedded Storage Example (`EmbeddedStorageExample.cs`)

Demonstrates the core functionality of NebulaStore's embedded storage system.

#### Features Covered:
- **Simple Usage**: Basic storage operations with default configuration
- **Advanced Configuration**: Custom storage directories, multiple channels, cache settings
- **Custom Type Handlers**: Implementing custom serialization for specific types
- **Batch Operations**: Efficient bulk data operations

#### Key Concepts:
- Creating and configuring embedded storage instances
- Working with root objects and collections
- Storing and retrieving complex object graphs
- Custom serialization strategies

### 2. Monitoring Example (`MonitoringExample.cs`)

Showcases NebulaStore's comprehensive monitoring and observability features.

#### Features Covered:
- **Basic Monitoring**: Storage statistics, cache metrics, registry information
- **Multi-Channel Monitoring**: Per-channel metrics and aggregated statistics
- **Housekeeping Operations**: Garbage collection, file cleanup, cache management
- **Monitor Management**: Querying and accessing different monitor types

#### Key Concepts:
- Accessing monitoring managers and individual monitors
- Understanding storage performance metrics
- Triggering and monitoring housekeeping operations
- Working with multi-channel storage configurations

### 3. Shared Domain Classes (`SharedDomainClasses.cs`)

Contains common domain classes and utilities used across examples.

#### Included Classes:
- **`Library`** & **`Book`**: Basic library management entities
- **`DataContainer`** & **`DataItem`**: Generic data storage entities
- **`Inventory`** & **`Item`**: Inventory management entities
- **`CustomData`**: Example of custom data structures
- **`CustomStringTypeHandler`**: Example custom type handler implementation

## 🔧 Configuration Examples

### Basic Storage Configuration
```csharp
// Simple default configuration
using var storage = EmbeddedStorage.Start();

// Named storage directory
using var storage = EmbeddedStorage.Start("my-storage");
```

### Advanced Configuration
```csharp
var config = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("advanced-storage")
    .SetChannelCount(3)
    .SetEntityCacheThreshold(500000)
    .Build();

using var storage = EmbeddedStorage.Start(config);
```

## 📊 Monitoring Usage

### Accessing Monitors
```csharp
var monitoringManager = storage.GetMonitoringManager();

// Get specific monitors
var storageMonitor = monitoringManager.StorageManagerMonitor;
var cacheMonitor = monitoringManager.EntityCacheSummaryMonitor;

// Query monitors by type
var entityCacheMonitors = monitoringManager.GetMonitors<IEntityCacheMonitor>();
```

### Reading Metrics
```csharp
var stats = storageMonitor.StorageStatistics;
Console.WriteLine($"Channel Count: {stats.ChannelCount}");
Console.WriteLine($"Total Data: {stats.TotalDataLength:N0} bytes");
Console.WriteLine($"Usage Ratio: {stats.UsageRatio:P2}");
```

## 🛠 Custom Type Handlers

Example of implementing a custom type handler:

```csharp
public class CustomStringTypeHandler : ITypeHandler
{
    public Type HandledType => typeof(string);
    public long TypeId => 999;

    public byte[] Serialize(object instance)
    {
        var str = (string)instance;
        var customStr = "CUSTOM:" + str;
        return System.Text.Encoding.UTF8.GetBytes(customStr);
    }

    public object Deserialize(byte[] data)
    {
        var str = System.Text.Encoding.UTF8.GetString(data);
        return str.StartsWith("CUSTOM:") ? str.Substring(7) : str;
    }

    // ... other required methods
}
```

## 📈 Performance Considerations

- **Channel Count**: Use multiple channels for better concurrent access
- **Cache Thresholds**: Adjust entity cache thresholds based on memory constraints
- **Housekeeping**: Monitor and tune housekeeping operations for optimal performance
- **Batch Operations**: Use batch operations for bulk data modifications

## 🔍 Troubleshooting

### Common Issues

1. **Build Errors**: Ensure .NET 9.0 SDK is installed
2. **Storage Directory Conflicts**: Each example uses different storage directories to avoid conflicts
3. **Memory Usage**: Monitor entity cache metrics if experiencing high memory usage

### Debug Information

The examples include comprehensive error handling and logging. Monitor the console output for:
- Storage statistics and metrics
- Housekeeping operation results
- Cache performance indicators
- File system usage patterns

## 📖 Additional Resources

- [NebulaStore Documentation](../README.md)
- [Storage Configuration Guide](../storage/embedded-configuration/)
- [Monitoring API Reference](../storage/storage/src/monitoring/)

## 🤝 Contributing

When adding new examples:
1. Follow the existing code structure and naming conventions
2. Include comprehensive documentation and comments
3. Add appropriate error handling
4. Update this README with new example descriptions
5. Test examples thoroughly before submitting

## 🎯 Learning Path

### For Beginners
1. Start with **Embedded Storage Example** to understand basic concepts
2. Explore the simple usage patterns and default configurations
3. Experiment with different data models using the shared domain classes

### For Advanced Users
1. Study the **Advanced Configuration Example** for performance tuning
2. Implement custom type handlers for specialized serialization needs
3. Use the **Monitoring Example** to understand system behavior and optimization

### For Production Use
1. Review monitoring and housekeeping examples for operational insights
2. Understand multi-channel configurations for scalability
3. Implement proper error handling and resource management patterns

## 🧪 Example Output

### Embedded Storage Example Output
```
=== NebulaStore Embedded Storage Example ===

1. Simple Usage Example
------------------------
Library: My Library
Total books: 2
Modern books (>= 2000): 1

2. Advanced Configuration Example
----------------------------------
Configuration - Storage Directory: advanced-storage
Configuration - Channel Count: 2
Inventory items: 2

3. Custom Type Handler Example
-------------------------------
Custom data stored with special string: This uses a custom type handler!

4. Batch Operations Example
----------------------------
Batch stored 3 books
Object IDs: [1, 2, 3]

=== Example completed successfully! ===
```

### Monitoring Example Output
```
NebulaStore Monitoring Example
==============================

1. Basic Monitoring Example
---------------------------
Monitor Name: name=EmbeddedStorage
Channel Count: 1
File Count: 1
Total Data Length: 0 bytes
Live Data Length: 0 bytes
Usage Ratio: 0.00%
Total Cached Entities: 0
Total Cache Size: 0 bytes
Registry Capacity: 1,000,000
Registry Size: 0

2. Multi-Channel Monitoring Example
-----------------------------------
Entity Cache Metrics by Channel:
  Channel 0:
    Name: channel=channel-0,group=Entity cache
    Entity Count: 0
    Cache Size: 0 bytes
    Last Sweep Start: 1755570625660
    Last Sweep End: 1755570625660
  [... additional channels ...]

3. Housekeeping Monitoring Example
----------------------------------
Triggering housekeeping operations...
  Issuing full garbage collection...
  Issuing full file check...
  Issuing full cache check...

4. Monitoring Manager Example
-----------------------------
Total Monitors: 5
All Monitors:
  - name=EmbeddedStorage (StorageManagerMonitor)
  - name=EntityCacheSummary (EntityCacheSummaryMonitor)
  - name=ObjectRegistry (ObjectRegistryMonitor)
  [... additional monitors ...]

Monitoring examples completed!
```

## 🔧 Development Setup

### IDE Configuration

**From the NebulaStore root directory:**

- **Visual Studio 2022** (Windows):
  ```bash
  # Open solution file
  start NebulaStore.sln
  ```

- **VS Code** (Cross-platform):
  ```bash
  # Open workspace folder
  code .
  ```
  *Requires C# extension for full functionality*

- **JetBrains Rider** (Cross-platform):
  ```bash
  # Open solution file
  rider NebulaStore.sln
  ```

- **Command Line Only**:
  ```bash
  # Use any text editor
  nano examples/README.md  # Linux/macOS
  notepad examples/README.md  # Windows
  ```

### Build Configuration
```bash
# Debug build (default)
dotnet build examples/ConsoleApp/NebulaStore.Examples.ConsoleApp.csproj

# Release build
dotnet build examples/ConsoleApp/NebulaStore.Examples.ConsoleApp.csproj -c Release

# Clean and rebuild
dotnet clean examples/ConsoleApp/NebulaStore.Examples.ConsoleApp.csproj
dotnet build examples/ConsoleApp/NebulaStore.Examples.ConsoleApp.csproj
```

### Testing the Examples
```bash
# Run with specific example choice
echo "1" | dotnet run --project examples/ConsoleApp/NebulaStore.Examples.ConsoleApp.csproj

# Run all examples
echo "3" | dotnet run --project examples/ConsoleApp/NebulaStore.Examples.ConsoleApp.csproj
```

---

*For more information about NebulaStore, visit the main [README](../README.md) file.*
