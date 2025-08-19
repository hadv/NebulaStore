using System;
using System.Collections.Generic;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Examples;

/// <summary>
/// Example demonstrating Abstract File System (AFS) usage with NebulaStore.
/// </summary>
public static class AfsExample
{
    public static void RunExample()
    {
        Console.WriteLine("NebulaStore AFS (Abstract File System) Example");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        // Example 1: Basic AFS usage
        BasicAfsExample();
        Console.WriteLine();

        // Example 2: Custom AFS configuration
        CustomAfsConfigurationExample();
        Console.WriteLine();

        // Example 3: Performance comparison
        PerformanceComparisonExample();
        Console.WriteLine();

        // Example 4: Advanced AFS features
        AdvancedAfsExample();
    }

    private static void BasicAfsExample()
    {
        Console.WriteLine("1. Basic AFS Example");
        Console.WriteLine("--------------------");

        // Start storage with AFS blob store
        using var storage = EmbeddedStorage.StartWithAfs("afs-basic-storage");

        // Create and store data
        var library = storage.Root<Library>();
        library.Name = "AFS Demo Library";
        library.Books.Add(new Book 
        { 
            Title = "Understanding AFS", 
            Author = "Storage Expert", 
            Year = 2025,
            ISBN = "978-0-123456-78-9"
        });
        library.Books.Add(new Book 
        { 
            Title = "Blob Storage Patterns", 
            Author = "Cloud Architect", 
            Year = 2024,
            ISBN = "978-0-987654-32-1"
        });

        // Store the root object
        storage.StoreRoot();

        Console.WriteLine($"Library: {library.Name}");
        Console.WriteLine($"Books stored: {library.Books.Count}");
        Console.WriteLine($"Storage type: {storage.Configuration.AfsStorageType}");
        Console.WriteLine($"AFS enabled: {storage.Configuration.UseAfs}");
    }

    private static void CustomAfsConfigurationExample()
    {
        Console.WriteLine("2. Custom AFS Configuration Example");
        Console.WriteLine("-----------------------------------");

        // Create custom AFS configuration
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory("afs-custom-storage")
            .SetUseAfs(true)
            .SetAfsStorageType("blobstore")
            .SetAfsUseCache(true)
            .SetChannelCount(2)
            .SetEntityCacheThreshold(2000000)
            .Build();

        using var storage = EmbeddedStorage.StartWithAfs(config);

        // Create complex data structure
        var inventory = storage.Root<Inventory>();
        inventory.Name = "AFS Inventory System";
        inventory.Items.AddRange(new[]
        {
            new Item { Name = "Laptop", Category = "Electronics", Price = 999.99m, Quantity = 50 },
            new Item { Name = "Mouse", Category = "Electronics", Price = 29.99m, Quantity = 200 },
            new Item { Name = "Keyboard", Category = "Electronics", Price = 79.99m, Quantity = 150 },
            new Item { Name = "Monitor", Category = "Electronics", Price = 299.99m, Quantity = 75 }
        });

        storage.StoreRoot();

        // Query data
        var expensiveItems = inventory.Items.Where(i => i.Price > 100).ToList();
        var totalValue = inventory.Items.Sum(i => i.Price * i.Quantity);

        Console.WriteLine($"Inventory: {inventory.Name}");
        Console.WriteLine($"Total items: {inventory.Items.Count}");
        Console.WriteLine($"Expensive items (>$100): {expensiveItems.Count}");
        Console.WriteLine($"Total inventory value: ${totalValue:F2}");
        Console.WriteLine($"AFS cache enabled: {storage.Configuration.AfsUseCache}");
    }

    private static void PerformanceComparisonExample()
    {
        Console.WriteLine("3. Performance Comparison Example");
        Console.WriteLine("---------------------------------");

        var testData = GenerateTestData(1000);

        // Test with traditional storage
        var traditionalTime = MeasureStoragePerformance("traditional-storage", testData, useAfs: false);

        // Test with AFS storage
        var afsTime = MeasureStoragePerformance("afs-storage", testData, useAfs: true);

        Console.WriteLine($"Traditional storage time: {traditionalTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"AFS storage time: {afsTime.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Performance difference: {((afsTime.TotalMilliseconds - traditionalTime.TotalMilliseconds) / traditionalTime.TotalMilliseconds * 100):F1}%");
    }

    private static void AdvancedAfsExample()
    {
        Console.WriteLine("4. Advanced AFS Features Example");
        Console.WriteLine("--------------------------------");

        using var storage = EmbeddedStorage.StartWithAfs("afs-advanced-storage");

        // Demonstrate batch operations with storer
        using var storer = storage.CreateStorer();

        var documents = new List<Document>();
        for (int i = 1; i <= 100; i++)
        {
            documents.Add(new Document
            {
                Id = i,
                Title = $"Document {i}",
                Content = $"This is the content of document {i}. " + 
                         "It contains important information that needs to be stored efficiently.",
                CreatedDate = DateTime.UtcNow.AddDays(-i),
                Tags = new[] { "important", "document", $"category-{i % 5}" }
            });
        }

        // Store all documents in batch
        var objectIds = storer.StoreAll(documents.ToArray());
        var committedCount = storer.Commit();

        Console.WriteLine($"Batch stored {committedCount} documents");
        Console.WriteLine($"Object IDs generated: {objectIds.Length}");
        Console.WriteLine($"Pending operations: {storer.HasPendingOperations}");

        // Get storage statistics
        var stats = storage.GetStatistics();
        Console.WriteLine($"Storage statistics:");
        Console.WriteLine($"  - Creation time: {stats.CreationTime}");
        Console.WriteLine($"  - Total storage size: {stats.TotalStorageSize:N0} bytes");
        Console.WriteLine($"  - Used storage size: {stats.UsedStorageSize:N0} bytes");
        Console.WriteLine($"  - Available storage: {stats.AvailableStorageSize:N0} bytes");
        Console.WriteLine($"  - Object count: {stats.ObjectCount}");
        Console.WriteLine($"  - Type count: {stats.TypeCount}");

        // Demonstrate garbage collection
        storage.IssueFullGarbageCollection();
        Console.WriteLine("Full garbage collection completed");

        // Demonstrate time-budgeted garbage collection
        var gcCompleted = storage.IssueGarbageCollection(10_000_000); // 10ms budget
        Console.WriteLine($"Time-budgeted GC completed: {gcCompleted}");
    }

    private static TimeSpan MeasureStoragePerformance(string directory, List<TestDataItem> data, bool useAfs)
    {
        var startTime = DateTime.UtcNow;

        if (useAfs)
        {
            using var storage = EmbeddedStorage.StartWithAfs(directory);
            var root = storage.Root<DataContainer>();
            root.Items = data;
            storage.StoreRoot();
        }
        else
        {
            using var storage = EmbeddedStorage.Start(directory);
            var root = storage.Root<DataContainer>();
            root.Items = data;
            storage.StoreRoot();
        }

        return DateTime.UtcNow - startTime;
    }

    private static List<TestDataItem> GenerateTestData(int count)
    {
        var random = new Random(42); // Fixed seed for consistent results
        var data = new List<TestDataItem>();

        for (int i = 0; i < count; i++)
        {
            data.Add(new TestDataItem
            {
                Id = i,
                Name = $"Item_{i}",
                Value = random.NextDouble() * 1000,
                Data = new byte[random.Next(100, 1000)],
                Timestamp = DateTime.UtcNow.AddSeconds(-random.Next(0, 86400))
            });
        }

        return data;
    }
}

// Data classes for examples
public class Library
{
    public string Name { get; set; } = string.Empty;
    public List<Book> Books { get; set; } = new();
}

public class Book
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int Year { get; set; }
    public string ISBN { get; set; } = string.Empty;
}

public class Inventory
{
    public string Name { get; set; } = string.Empty;
    public List<Item> Items { get; set; } = new();
}

public class Item
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
}

public class DataContainer
{
    public List<TestDataItem> Items { get; set; } = new();
}

public class TestDataItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; }
}
