# NebulaStore Storage Monitoring

This package provides comprehensive monitoring and metrics capabilities for NebulaStore embedded storage, migrated from the Eclipse Store Java monitoring package.

## Overview

The monitoring system provides real-time metrics and statistics for various storage components:

- **Storage Manager Monitoring**: Overall storage statistics and housekeeping operations
- **Entity Cache Monitoring**: Per-channel cache metrics and aggregated summaries
- **Object Registry Monitoring**: Object registry capacity and usage metrics
- **Housekeeping Monitoring**: Per-channel housekeeping operation metrics

## Key Features

- **Real-time Metrics**: Live monitoring of storage operations and performance
- **Memory-Safe**: Uses WeakReference to avoid memory leaks when monitoring storage components
- **Channel-Aware**: Provides per-channel metrics for multi-channel storage configurations
- **Aggregated Views**: Summary monitors that aggregate metrics across all channels
- **Extensible**: Interface-based design allows for custom monitoring implementations

## Architecture

The monitoring system is built around several key interfaces:

### Core Interfaces

- `IMetricMonitor`: Base interface for all monitors
- `IStorageMonitoringManager`: Central manager for accessing all monitoring components

### Monitor Types

- `IStorageManagerMonitor`: Storage manager metrics and operations
- `IEntityCacheMonitor`: Entity cache metrics per channel
- `IEntityCacheSummaryMonitor`: Aggregated entity cache metrics
- `IObjectRegistryMonitor`: Object registry metrics
- `IStorageChannelHousekeepingMonitor`: Housekeeping operation metrics

## Usage Examples

### Basic Monitoring Access

```csharp
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.Monitoring;

// Start storage with monitoring
using var storage = EmbeddedStorage.Start("my-storage");

// Get the monitoring manager
var monitoringManager = storage.GetMonitoringManager();

// Access different monitors
var storageMonitor = monitoringManager.StorageManagerMonitor;
var cacheMonitor = monitoringManager.EntityCacheSummaryMonitor;
var registryMonitor = monitoringManager.ObjectRegistryMonitor;
```

### Storage Statistics

```csharp
// Get comprehensive storage statistics
var storageStats = storageMonitor.StorageStatistics;

Console.WriteLine($"Channels: {storageStats.ChannelCount}");
Console.WriteLine($"Files: {storageStats.FileCount}");
Console.WriteLine($"Total Data: {storageStats.TotalDataLength} bytes");
Console.WriteLine($"Live Data: {storageStats.LiveDataLength} bytes");
Console.WriteLine($"Usage Ratio: {storageStats.UsageRatio:P2}");

// Access per-channel statistics
foreach (var channelStats in storageStats.ChannelStatistics)
{
    Console.WriteLine($"Channel Files: {channelStats.FileCount}");
    Console.WriteLine($"Channel Data: {channelStats.TotalDataLength} bytes");
    
    foreach (var fileStats in channelStats.FileStatistics)
    {
        Console.WriteLine($"  File: {fileStats.FileName}");
        Console.WriteLine($"  Size: {fileStats.TotalDataLength} bytes");
    }
}
```

### Entity Cache Monitoring

```csharp
// Monitor entity cache usage
var cacheMonitor = monitoringManager.EntityCacheSummaryMonitor;

Console.WriteLine($"Total Cached Entities: {cacheMonitor.EntityCount}");
Console.WriteLine($"Total Cache Size: {cacheMonitor.UsedCacheSize} bytes");

// Monitor individual channel caches
foreach (var channelCache in monitoringManager.EntityCacheMonitors)
{
    Console.WriteLine($"Channel {channelCache.ChannelIndex}:");
    Console.WriteLine($"  Entities: {channelCache.EntityCount}");
    Console.WriteLine($"  Cache Size: {channelCache.UsedCacheSize} bytes");
    Console.WriteLine($"  Last Sweep: {channelCache.LastSweepStart} - {channelCache.LastSweepEnd}");
}
```

### Housekeeping Operations

```csharp
// Monitor housekeeping operations
foreach (var housekeeping in monitoringManager.HousekeepingMonitors)
{
    Console.WriteLine($"Channel {housekeeping.Name}:");
    
    // Garbage collection metrics
    Console.WriteLine($"  GC Result: {housekeeping.GarbageCollectionResult}");
    Console.WriteLine($"  GC Duration: {housekeeping.GarbageCollectionDuration} ns");
    Console.WriteLine($"  GC Budget: {housekeeping.GarbageCollectionBudget} ns");
    
    // Cache check metrics
    Console.WriteLine($"  Cache Check: {housekeeping.EntityCacheCheckResult}");
    Console.WriteLine($"  Cache Duration: {housekeeping.EntityCacheCheckDuration} ns");
    
    // File cleanup metrics
    Console.WriteLine($"  File Cleanup: {housekeeping.FileCleanupCheckResult}");
    Console.WriteLine($"  Cleanup Duration: {housekeeping.FileCleanupCheckDuration} ns");
}
```

### Triggering Housekeeping Operations

```csharp
// Trigger storage maintenance operations
var storageMonitor = monitoringManager.StorageManagerMonitor;

// Full garbage collection
storageMonitor.IssueFullGarbageCollection();

// Full file check
storageMonitor.IssueFullFileCheck();

// Full cache check
storageMonitor.IssueFullCacheCheck();
```

### Finding Monitors by Name or Type

```csharp
// Find monitor by name
var monitor = monitoringManager.GetMonitor("name=EmbeddedStorage");

// Get all monitors of a specific type
var cacheMonitors = monitoringManager.GetMonitors<IEntityCacheMonitor>();
var housekeepingMonitors = monitoringManager.GetMonitors<IStorageChannelHousekeepingMonitor>();

// Get all monitors
var allMonitors = monitoringManager.AllMonitors;
foreach (var mon in allMonitors)
{
    Console.WriteLine($"Monitor: {mon.Name}");
}
```

## Monitor Descriptions

### MonitorDescription Attribute

The monitoring system uses the `MonitorDescriptionAttribute` to provide descriptions for monitoring properties and methods, similar to the Java `@MonitorDescription` annotation:

```csharp
[MonitorDescription("The number of entries in the channels entity cache.")]
long EntityCount { get; }
```

## Implementation Notes

### Memory Management

- All monitors use `WeakReference` to reference storage components
- This prevents memory leaks when storage components are disposed
- Monitors return default values (usually 0) when referenced components are no longer available

### Thread Safety

- The monitoring system is designed to be thread-safe
- Summary monitors use locking when aggregating data from multiple sources
- Individual monitors are generally lock-free for better performance

### Placeholder Implementation

The current implementation includes placeholder classes for components not yet fully implemented:

- `PlaceholderObjectRegistry`: Temporary object registry implementation
- `PlaceholderEntityCache`: Temporary entity cache implementation

These will be replaced with actual implementations as the storage system evolves.

## Migration from Java

This monitoring package is a direct migration from the Eclipse Store Java monitoring package, with the following key differences:

- **No JMX Dependency**: Replaced JMX MBean interfaces with regular .NET interfaces
- **Attribute-Based Descriptions**: Uses `MonitorDescriptionAttribute` instead of `@MonitorDescription`
- **WeakReference Pattern**: Maintains the same memory-safe monitoring approach
- **Async Support**: Ready for async operations in .NET environment

## Future Enhancements

- Integration with .NET diagnostic tools (EventCounters, Metrics API)
- Performance counters for Windows environments
- Custom metric exporters for monitoring systems (Prometheus, etc.)
- Real-time monitoring dashboards
