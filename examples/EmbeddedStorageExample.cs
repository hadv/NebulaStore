using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using NebulaStore.Storage;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Examples;

/// <summary>
/// Example demonstrating the new Embedded Storage API
/// </summary>
public class EmbeddedStorageExample
{
    public static void RunExample()
    {
        Console.WriteLine("=== NebulaStore Embedded Storage Example ===\n");

        // Example 1: Simple usage with default configuration
        SimpleUsageExample();

        // Example 2: Advanced configuration
        AdvancedConfigurationExample();

        // Example 3: Custom type handlers
        CustomTypeHandlerExample();

        // Example 4: Batch operations
        BatchOperationsExample();

        Console.WriteLine("=== Example completed successfully! ===");
    }

    private static void SimpleUsageExample()
    {
        Console.WriteLine("1. Simple Usage Example");
        Console.WriteLine("------------------------");

        // Create storage with default settings
        using var storage = EmbeddedStorage.Start();

        // Get root object
        var library = storage.Root<Library>();
        library.Name = "My Library";

        // Add some books
        library.Books.Add(new Book { Title = "The Pragmatic Programmer", Author = "Hunt & Thomas", Year = 1999 });
        library.Books.Add(new Book { Title = "Clean Code", Author = "Robert Martin", Year = 2008 });

        // Store the root
        storage.StoreRoot();

        // Query books
        var modernBooks = storage.Query<Book>()
            .Where(b => b.Year >= 2000)
            .ToList();

        Console.WriteLine($"Library: {library.Name}");
        Console.WriteLine($"Total books: {library.Books.Count}");
        Console.WriteLine($"Modern books (>= 2000): {modernBooks.Count}");
        Console.WriteLine();
    }

    private static void AdvancedConfigurationExample()
    {
        Console.WriteLine("2. Advanced Configuration Example");
        Console.WriteLine("----------------------------------");

        // Create custom configuration
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory("advanced-storage")
            .SetChannelCount(2)
            .SetEntityCacheThreshold(1000000)
            .SetHousekeepingOnStartup(true)
            .SetValidateOnStartup(true)
            .Build();

        // Create storage with custom configuration
        using var storage = EmbeddedStorage.Start(config);

        var inventory = storage.Root<Inventory>();
        inventory.Items.Add(new Item { Name = "Laptop", Price = 999.99m, Quantity = 5 });
        inventory.Items.Add(new Item { Name = "Mouse", Price = 29.99m, Quantity = 50 });

        storage.StoreRoot();

        Console.WriteLine($"Configuration - Storage Directory: {config.StorageDirectory}");
        Console.WriteLine($"Configuration - Channel Count: {config.ChannelCount}");
        Console.WriteLine($"Inventory items: {inventory.Items.Count}");
        Console.WriteLine();
    }

    private static void CustomTypeHandlerExample()
    {
        Console.WriteLine("3. Custom Type Handler Example");
        Console.WriteLine("-------------------------------");

        // Create foundation with custom type handler
        var foundation = EmbeddedStorage.Foundation("custom-handler-storage")
            .RegisterTypeHandler(new CustomStringTypeHandler());

        using var storage = foundation.Start();

        var data = storage.Root<CustomData>();
        data.SpecialString = "This uses a custom type handler!";
        data.NormalValue = 42;

        storage.StoreRoot();

        Console.WriteLine($"Custom data stored with special string: {data.SpecialString}");
        Console.WriteLine();
    }

    private static void BatchOperationsExample()
    {
        Console.WriteLine("4. Batch Operations Example");
        Console.WriteLine("----------------------------");

        using var storage = EmbeddedStorage.Start("batch-storage");

        // Create multiple objects
        var books = new[]
        {
            new Book { Title = "Design Patterns", Author = "Gang of Four", Year = 1994 },
            new Book { Title = "Refactoring", Author = "Martin Fowler", Year = 1999 },
            new Book { Title = "Domain-Driven Design", Author = "Eric Evans", Year = 2003 }
        };

        // Use storer for batch operations
        using var storer = storage.CreateStorer();
        var objectIds = storer.StoreAll(books);
        var committedCount = storer.Commit();

        Console.WriteLine($"Batch stored {committedCount} books");
        Console.WriteLine($"Object IDs: [{string.Join(", ", objectIds)}]");
        Console.WriteLine();
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
public class Inventory
{
    [Key(0)]
    public List<Item> Items { get; set; } = new();
}

[MessagePackObject(AllowPrivate = true)]
public class Item
{
    [Key(0)]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    public decimal Price { get; set; }

    [Key(2)]
    public int Quantity { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
public class CustomData
{
    [Key(0)]
    public string SpecialString { get; set; } = string.Empty;

    [Key(1)]
    public int NormalValue { get; set; }
}

// Example custom type handler
public class CustomStringTypeHandler : ITypeHandler
{
    public Type HandledType => typeof(string);
    public long TypeId => 999;

    public byte[] Serialize(object instance)
    {
        if (instance is not string str)
            throw new ArgumentException("Instance must be a string");

        // Custom serialization: prepend "CUSTOM:" to the string
        var customStr = "CUSTOM:" + str;
        return System.Text.Encoding.UTF8.GetBytes(customStr);
    }

    public object Deserialize(byte[] data)
    {
        var str = System.Text.Encoding.UTF8.GetString(data);
        // Remove the "CUSTOM:" prefix
        return str.StartsWith("CUSTOM:") ? str.Substring(7) : str;
    }

    public long GetSerializedLength(object instance)
    {
        if (instance is not string str)
            throw new ArgumentException("Instance must be a string");

        return System.Text.Encoding.UTF8.GetByteCount("CUSTOM:" + str);
    }

    public bool CanHandle(Type type) => type == typeof(string);
}
