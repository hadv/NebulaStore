using MessagePack;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.Monitoring;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Examples;

/// <summary>
/// Demonstrates the monitoring capabilities of NebulaStore.
/// Shows how to access and use various monitoring features.
/// </summary>
public class MonitoringExample
{
    public static void Main(string[] args)
    {
        Console.WriteLine("NebulaStore Monitoring Example");
        Console.WriteLine("==============================");
        Console.WriteLine();

        // Run different monitoring examples
        BasicMonitoringExample();
        Console.WriteLine();
        
        MultiChannelMonitoringExample();
        Console.WriteLine();
        
        HousekeepingMonitoringExample();
        Console.WriteLine();
        
        MonitoringManagerExample();
        Console.WriteLine();

        Console.WriteLine("Monitoring examples completed!");
    }

    private static void BasicMonitoringExample()
    {
        Console.WriteLine("1. Basic Monitoring Example");
        Console.WriteLine("---------------------------");

        // Create storage with default configuration
        using var storage = EmbeddedStorage.Start("monitoring-example-storage");

        // Add some data to generate metrics
        var library = storage.Root<Library>();
        library.Name = "Monitoring Library";
        library.Books.AddRange(new[]
        {
            new Book { Title = "Monitoring Systems", Author = "Tech Author", Year = 2023 },
            new Book { Title = "Performance Metrics", Author = "Data Expert", Year = 2022 },
            new Book { Title = "Storage Analytics", Author = "System Designer", Year = 2024 }
        });

        storage.StoreRoot();

        // Get monitoring manager
        var monitoringManager = storage.GetMonitoringManager();

        // Display storage manager metrics
        var storageMonitor = monitoringManager.StorageManagerMonitor;
        var stats = storageMonitor.StorageStatistics;

        Console.WriteLine($"Monitor Name: {storageMonitor.Name}");
        Console.WriteLine($"Channel Count: {stats.ChannelCount}");
        Console.WriteLine($"File Count: {stats.FileCount}");
        Console.WriteLine($"Total Data Length: {stats.TotalDataLength:N0} bytes");
        Console.WriteLine($"Live Data Length: {stats.LiveDataLength:N0} bytes");
        Console.WriteLine($"Usage Ratio: {stats.UsageRatio:P2}");

        // Display entity cache metrics
        var cacheMonitor = monitoringManager.EntityCacheSummaryMonitor;
        Console.WriteLine($"Total Cached Entities: {cacheMonitor.EntityCount}");
        Console.WriteLine($"Total Cache Size: {cacheMonitor.UsedCacheSize:N0} bytes");

        // Display object registry metrics
        var registryMonitor = monitoringManager.ObjectRegistryMonitor;
        Console.WriteLine($"Registry Capacity: {registryMonitor.Capacity:N0}");
        Console.WriteLine($"Registry Size: {registryMonitor.Size:N0}");
    }

    private static void MultiChannelMonitoringExample()
    {
        Console.WriteLine("2. Multi-Channel Monitoring Example");
        Console.WriteLine("-----------------------------------");

        // Create storage with multiple channels
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory("monitoring-multichannel-storage")
            .SetChannelCount(3)
            .SetEntityCacheThreshold(500000)
            .Build();

        using var storage = EmbeddedStorage.Start(config);

        // Add data
        var data = storage.Root<DataContainer>();
        data.Items.AddRange(GenerateTestData(100));
        storage.StoreRoot();

        var monitoringManager = storage.GetMonitoringManager();

        // Display per-channel entity cache metrics
        Console.WriteLine("Entity Cache Metrics by Channel:");
        foreach (var cacheMonitor in monitoringManager.EntityCacheMonitors)
        {
            Console.WriteLine($"  Channel {cacheMonitor.ChannelIndex}:");
            Console.WriteLine($"    Name: {cacheMonitor.Name}");
            Console.WriteLine($"    Entity Count: {cacheMonitor.EntityCount}");
            Console.WriteLine($"    Cache Size: {cacheMonitor.UsedCacheSize:N0} bytes");
            Console.WriteLine($"    Last Sweep Start: {cacheMonitor.LastSweepStart}");
            Console.WriteLine($"    Last Sweep End: {cacheMonitor.LastSweepEnd}");
        }

        // Display aggregated metrics
        var summaryMonitor = monitoringManager.EntityCacheSummaryMonitor;
        Console.WriteLine($"Aggregated Cache Metrics:");
        Console.WriteLine($"  Total Entities: {summaryMonitor.EntityCount}");
        Console.WriteLine($"  Total Cache Size: {summaryMonitor.UsedCacheSize:N0} bytes");

        // Display channel statistics from storage manager
        var storageStats = monitoringManager.StorageManagerMonitor.StorageStatistics;
        Console.WriteLine("Channel Statistics:");
        for (int i = 0; i < storageStats.ChannelStatistics.Count; i++)
        {
            var channelStats = storageStats.ChannelStatistics[i];
            Console.WriteLine($"  Channel {i}:");
            Console.WriteLine($"    Files: {channelStats.FileCount}");
            Console.WriteLine($"    Total Data: {channelStats.TotalDataLength:N0} bytes");
            Console.WriteLine($"    Live Data: {channelStats.LiveDataLength:N0} bytes");
            
            foreach (var fileStats in channelStats.FileStatistics)
            {
                Console.WriteLine($"      File: {fileStats.FileName}");
                Console.WriteLine($"        Size: {fileStats.TotalDataLength:N0} bytes");
                Console.WriteLine($"        Live: {fileStats.LiveDataLength:N0} bytes");
            }
        }
    }

    private static void HousekeepingMonitoringExample()
    {
        Console.WriteLine("3. Housekeeping Monitoring Example");
        Console.WriteLine("----------------------------------");

        using var storage = EmbeddedStorage.Start("monitoring-housekeeping-storage");

        // Add some data
        var data = storage.Root<DataContainer>();
        data.Items.AddRange(GenerateTestData(50));
        storage.StoreRoot();

        var monitoringManager = storage.GetMonitoringManager();
        var storageMonitor = monitoringManager.StorageManagerMonitor;

        // Trigger housekeeping operations
        Console.WriteLine("Triggering housekeeping operations...");
        
        Console.WriteLine("  Issuing full garbage collection...");
        storageMonitor.IssueFullGarbageCollection();
        
        Console.WriteLine("  Issuing full file check...");
        storageMonitor.IssueFullFileCheck();
        
        Console.WriteLine("  Issuing full cache check...");
        storageMonitor.IssueFullCacheCheck();

        // Display housekeeping metrics
        Console.WriteLine("Housekeeping Metrics by Channel:");
        foreach (var housekeepingMonitor in monitoringManager.HousekeepingMonitors)
        {
            Console.WriteLine($"  {housekeepingMonitor.Name}:");
            
            // Garbage collection metrics
            Console.WriteLine($"    Garbage Collection:");
            Console.WriteLine($"      Result: {housekeepingMonitor.GarbageCollectionResult}");
            Console.WriteLine($"      Start Time: {housekeepingMonitor.GarbageCollectionStartTime}");
            Console.WriteLine($"      Duration: {housekeepingMonitor.GarbageCollectionDuration:N0} ns");
            Console.WriteLine($"      Budget: {housekeepingMonitor.GarbageCollectionBudget:N0} ns");
            
            // Entity cache check metrics
            Console.WriteLine($"    Entity Cache Check:");
            Console.WriteLine($"      Result: {housekeepingMonitor.EntityCacheCheckResult}");
            Console.WriteLine($"      Start Time: {housekeepingMonitor.EntityCacheCheckStartTime}");
            Console.WriteLine($"      Duration: {housekeepingMonitor.EntityCacheCheckDuration:N0} ns");
            Console.WriteLine($"      Budget: {housekeepingMonitor.EntityCacheCheckBudget:N0} ns");
            
            // File cleanup check metrics
            Console.WriteLine($"    File Cleanup Check:");
            Console.WriteLine($"      Result: {housekeepingMonitor.FileCleanupCheckResult}");
            Console.WriteLine($"      Start Time: {housekeepingMonitor.FileCleanupCheckStartTime}");
            Console.WriteLine($"      Duration: {housekeepingMonitor.FileCleanupCheckDuration:N0} ns");
            Console.WriteLine($"      Budget: {housekeepingMonitor.FileCleanupCheckBudget:N0} ns");
        }
    }

    private static void MonitoringManagerExample()
    {
        Console.WriteLine("4. Monitoring Manager Example");
        Console.WriteLine("-----------------------------");

        using var storage = EmbeddedStorage.Start("monitoring-manager-storage");
        var monitoringManager = storage.GetMonitoringManager();

        // Display all monitors
        Console.WriteLine($"Total Monitors: {monitoringManager.AllMonitors.Count}");
        
        Console.WriteLine("All Monitors:");
        foreach (var monitor in monitoringManager.AllMonitors)
        {
            Console.WriteLine($"  - {monitor.Name} ({monitor.GetType().Name})");
        }

        // Find monitors by name
        Console.WriteLine("\nFinding Monitors by Name:");
        var storageMonitor = monitoringManager.GetMonitor("name=EmbeddedStorage");
        var cacheMonitor = monitoringManager.GetMonitor("name=EntityCacheSummary");
        var registryMonitor = monitoringManager.GetMonitor("name=ObjectRegistry");

        Console.WriteLine($"  Storage Monitor: {storageMonitor?.Name ?? "Not found"}");
        Console.WriteLine($"  Cache Monitor: {cacheMonitor?.Name ?? "Not found"}");
        Console.WriteLine($"  Registry Monitor: {registryMonitor?.Name ?? "Not found"}");

        // Get monitors by type
        Console.WriteLine("\nMonitors by Type:");
        var entityCacheMonitors = monitoringManager.GetMonitors<IEntityCacheMonitor>();
        var housekeepingMonitors = monitoringManager.GetMonitors<IStorageChannelHousekeepingMonitor>();

        Console.WriteLine($"  Entity Cache Monitors: {entityCacheMonitors.Count()}");
        Console.WriteLine($"  Housekeeping Monitors: {housekeepingMonitors.Count()}");
    }

    private static List<DataItem> GenerateTestData(int count)
    {
        var items = new List<DataItem>();
        var random = new Random();

        for (int i = 0; i < count; i++)
        {
            items.Add(new DataItem
            {
                Id = i,
                Name = $"Item {i}",
                Value = random.NextDouble() * 1000,
                Timestamp = DateTime.UtcNow.AddMinutes(-random.Next(0, 1440)),
                Tags = new List<string> { $"tag{i % 5}", $"category{i % 3}" }
            });
        }

        return items;
    }
}

// Example domain classes
[MessagePackObject(AllowPrivate = true)]
public class Library
{
    [Key(0)]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    public List<Book> Books { get; set; } = new();
}

[MessagePackObject(AllowPrivate = true)]
public class Book
{
    [Key(0)]
    public string Title { get; set; } = string.Empty;

    [Key(1)]
    public string Author { get; set; } = string.Empty;

    [Key(2)]
    public int Year { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
public class DataContainer
{
    [Key(0)]
    public List<DataItem> Items { get; set; } = new();
}

[MessagePackObject(AllowPrivate = true)]
public class DataItem
{
    [Key(0)]
    public int Id { get; set; }

    [Key(1)]
    public string Name { get; set; } = string.Empty;

    [Key(2)]
    public double Value { get; set; }

    [Key(3)]
    public DateTime Timestamp { get; set; }

    [Key(4)]
    public List<string> Tags { get; set; } = new();
}
