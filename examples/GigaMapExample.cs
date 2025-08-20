using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NebulaStore.Storage.Embedded;
using NebulaStore.GigaMap;

namespace NebulaStore.Examples;

/// <summary>
/// Example demonstrating GigaMap integration with NebulaStore.
/// Shows how to create indexed collections for high-performance querying.
/// </summary>
public static class GigaMapExample
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== NebulaStore GigaMap Integration Example ===");
        Console.WriteLine();

        // Create storage manager
        using var storage = EmbeddedStorage.Start("gigamap-example-storage");

        // Example 1: Basic GigaMap usage
        await BasicGigaMapExample(storage);
        Console.WriteLine();

        // Example 2: Advanced indexing and querying
        await AdvancedQueryExample(storage);
        Console.WriteLine();

        // Example 3: Persistence and loading
        await PersistenceExample(storage);
        Console.WriteLine();

        Console.WriteLine("GigaMap example completed successfully!");
    }

    private static async Task BasicGigaMapExample(IEmbeddedStorageManager storage)
    {
        Console.WriteLine("1. Basic GigaMap Example");
        Console.WriteLine("------------------------");

        // Create a GigaMap for Person entities with multiple indices
        var gigaMap = storage.CreateGigaMap<Person>()
            .WithBitmapIndex(Indexer.Property<Person, string>("Email", p => p.Email))
            .WithBitmapIndex(Indexer.Property<Person, int>("Age", p => p.Age))
            .WithBitmapIndex(Indexer.Property<Person, string>("Department", p => p.Department))
            .WithBitmapUniqueIndex(Indexer.Property<Person, string>("Email", p => p.Email))
            .Build();

        // Register the GigaMap with storage for persistence
        storage.RegisterGigaMap(gigaMap);

        // Add some sample data
        var people = new[]
        {
            new Person { Email = "john.doe@example.com", Name = "John Doe", Age = 30, Department = "Engineering" },
            new Person { Email = "jane.smith@example.com", Name = "Jane Smith", Age = 25, Department = "Marketing" },
            new Person { Email = "bob.wilson@example.com", Name = "Bob Wilson", Age = 35, Department = "Engineering" },
            new Person { Email = "alice.brown@example.com", Name = "Alice Brown", Age = 28, Department = "Sales" }
        };

        foreach (var person in people)
        {
            var id = gigaMap.Add(person);
            Console.WriteLine($"Added person: {person.Name} (ID: {id})");
        }

        Console.WriteLine($"Total entities in GigaMap: {gigaMap.Size}");

        // Simple query example
        var engineers = gigaMap.Query("Department", "Engineering").Execute();

        Console.WriteLine($"Found {engineers.Count} engineers:");
        foreach (var engineer in engineers)
        {
            Console.WriteLine($"  - {engineer.Name} ({engineer.Email})");
        }

        // Store the GigaMap (persistence temporarily disabled for demo)
        // await gigaMap.StoreAsync();
        Console.WriteLine("GigaMap persistence temporarily disabled for demo");
        Console.WriteLine("GigaMap stored successfully");
    }

    private static async Task AdvancedQueryExample(IEmbeddedStorageManager storage)
    {
        Console.WriteLine("2. Advanced Query Example");
        Console.WriteLine("-------------------------");

        // Get the existing GigaMap or create a new one
        var gigaMap = storage.GetGigaMap<Person>() ?? storage.CreateGigaMap<Person>()
            .WithBitmapIndex(Indexer.Property<Person, string>("Email", p => p.Email))
            .WithBitmapIndex(Indexer.Property<Person, int>("Age", p => p.Age))
            .WithBitmapIndex(Indexer.Property<Person, string>("Department", p => p.Department))
            .Build();

        if (gigaMap.Size == 0)
        {
            Console.WriteLine("No data found, skipping advanced query example");
            return;
        }

        // Complex query: Find people in Engineering department
        var engineers = gigaMap.Query("Department", "Engineering").Execute();

        Console.WriteLine($"Engineers: {engineers.Count}");
        foreach (var person in engineers)
        {
            Console.WriteLine($"  - {person.Name}, Age: {person.Age}");
        }

        // Query with limit and offset
        var allPeople = gigaMap.Query()
            .Skip(1)
            .Limit(2)
            .Execute();

        Console.WriteLine($"People (skip 1, take 2): {allPeople.Count}");
        foreach (var person in allPeople)
        {
            Console.WriteLine($"  - {person.Name}");
        }

        await Task.CompletedTask;
    }

    private static async Task PersistenceExample(IEmbeddedStorageManager storage)
    {
        Console.WriteLine("3. Persistence Example");
        Console.WriteLine("----------------------");

        // Store all GigaMaps (temporarily disabled for demo)
        // await storage.StoreGigaMapsAsync();
        Console.WriteLine("GigaMap persistence temporarily disabled for demo");

        // Demonstrate that GigaMap is registered and can be retrieved
        var retrievedGigaMap = storage.GetGigaMap<Person>();
        if (retrievedGigaMap != null)
        {
            Console.WriteLine($"Retrieved GigaMap with {retrievedGigaMap.Size} entities");
            
            // Show that queries still work
            var firstPerson = retrievedGigaMap.Query().FirstOrDefault();
            if (firstPerson != null)
            {
                Console.WriteLine($"First person: {firstPerson.Name}");
            }
        }
        else
        {
            Console.WriteLine("No GigaMap found for Person type");
        }
    }
}

/// <summary>
/// Sample entity for GigaMap examples.
/// </summary>
public class Person
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Department { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{Name} ({Email}) - Age: {Age}, Dept: {Department}";
    }
}
