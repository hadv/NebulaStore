using System;
using System.Collections.Generic;
using System.Linq;
using NebulaStore.Afs.GoogleCloud.Firestore;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Examples;

/// <summary>
/// Example demonstrating Google Cloud Firestore integration with NebulaStore AFS.
/// </summary>
public static class FirestoreExample
{
    public static void Run()
    {
        Console.WriteLine("NebulaStore Google Cloud Firestore Example");
        Console.WriteLine("==========================================");
        Console.WriteLine();

        try
        {
            SimpleFirestoreExample();
            Console.WriteLine();

            BasicFirestoreExample();
            Console.WriteLine();

            CustomFirestoreConfigurationExample();
            Console.WriteLine();

            DirectFirestoreUsageExample();
            Console.WriteLine();

            AdvancedFirestoreExample();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Make sure you have:");
            Console.WriteLine("1. A Google Cloud Project with Firestore enabled");
            Console.WriteLine("2. Proper authentication configured (service account key, etc.)");
            Console.WriteLine("3. Or use the Firestore emulator for testing");
        }
    }

    private static void SimpleFirestoreExample()
    {
        Console.WriteLine("0. Simple Firestore Example");
        Console.WriteLine("----------------------------");

        try
        {
            Console.WriteLine("Starting storage with Firestore backend (simple approach)...");

            // Use the new convenience method - fully integrated!
            using var storage = EmbeddedStorage.StartWithFirestore("your-project-id");

            // Create and store data
            var root = storage.Root<Person>();
            if (root == null)
            {
                root = new Person
                {
                    Name = "Simple Example User",
                    Age = 25,
                    Email = "simple@example.com"
                };
                storage.SetRoot(root);
            }
            else
            {
                root.Age += 1; // Another year older
            }

            storage.StoreRoot();
            Console.WriteLine($"Successfully stored: {root.Name}, Age: {root.Age}");
            Console.WriteLine("✓ Firestore integration is working!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("This is expected if Firestore is not properly configured.");
        }
    }

    private static void BasicFirestoreExample()
    {
        Console.WriteLine("1. Basic Firestore Example");
        Console.WriteLine("---------------------------");

        try
        {
            Console.WriteLine("Starting storage with Firestore backend...");

            // Use the Firestore extensions method - fully integrated!
            using var storage = EmbeddedStorageFirestoreExtensions.StartWithFirestore("your-project-id");

        // Create and store data
        var library = storage.Root<Library>();
        library.Name = "Firestore Demo Library";
        library.Books.Add(new Book 
        { 
            Title = "Understanding Firestore", 
            Author = "Cloud Expert", 
            Year = 2025,
            ISBN = "978-0-123456-78-9"
        });
        library.Books.Add(new Book 
        { 
            Title = "NoSQL Database Patterns", 
            Author = "Database Architect", 
            Year = 2024,
            ISBN = "978-0-987654-32-1"
        });

        // Store the root object
        storage.StoreRoot();

        Console.WriteLine($"Library: {library.Name}");
        Console.WriteLine($"Books stored: {library.Books.Count}");
        Console.WriteLine($"Storage type: {storage.Configuration.AfsStorageType}");
        Console.WriteLine($"AFS enabled: {storage.Configuration.UseAfs}");
        Console.WriteLine($"Project ID: {storage.Configuration.AfsConnectionString}");
    }

    private static void CustomFirestoreConfigurationExample()
    {
        Console.WriteLine("2. Custom Firestore Configuration Example");
        Console.WriteLine("------------------------------------------");

        // Create custom Firestore configuration
        var config = EmbeddedStorageConfiguration.New()
            .UseFirestore("your-project-id", "custom-library-storage")
            .SetChannelCount(2)
            .SetEntityCacheThreshold(2000000)
            .Build();

        using var storage = EmbeddedStorage.Foundation(config).Start();

        // Create complex data structure
        var inventory = storage.Root<Inventory>();
        inventory.Name = "Firestore Inventory System";
        inventory.Items.AddRange(new[]
        {
            new Item { Name = "Laptop", Category = "Electronics", Price = 999.99m, Quantity = 50 },
            new Item { Name = "Mouse", Category = "Electronics", Price = 29.99m, Quantity = 200 },
            new Item { Name = "Keyboard", Category = "Electronics", Price = 79.99m, Quantity = 150 },
            new Item { Name = "Monitor", Category = "Electronics", Price = 299.99m, Quantity = 75 }
        });

        storage.StoreRoot();

        Console.WriteLine($"Inventory: {inventory.Name}");
        Console.WriteLine($"Items stored: {inventory.Items.Count}");
        Console.WriteLine($"Total value: ${inventory.Items.Sum(i => i.Price * i.Quantity):F2}");
        Console.WriteLine($"Storage directory: {storage.Configuration.StorageDirectory}");
        Console.WriteLine($"Cache enabled: {storage.Configuration.AfsUseCache}");
    }

    private static void DirectFirestoreUsageExample()
    {
        Console.WriteLine("3. Direct Firestore Usage Example");
        Console.WriteLine("----------------------------------");

        // This example shows direct usage of the Firestore connector
        // with the AFS blob store system

        try
        {
            Console.WriteLine("Creating Firestore file system...");
            using var fileSystem = EmbeddedStorageFirestoreExtensions.CreateFirestoreFileSystem("your-project-id");

            Console.WriteLine("Performing direct file operations...");
            var path = BlobStorePath.New("documents", "folder", "file.txt");
            var data = System.Text.Encoding.UTF8.GetBytes("Hello, Firestore!");

            // Write data
            fileSystem.IoHandler.WriteData(path, data);
            Console.WriteLine($"Written {data.Length} bytes to {path.FullQualifiedName}");

            // Read data back
            var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
            var content = System.Text.Encoding.UTF8.GetString(readData);
            Console.WriteLine($"Read back: {content}");

            // Check file exists
            var exists = fileSystem.IoHandler.FileExists(path);
            Console.WriteLine($"File exists: {exists}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Direct Firestore usage failed: {ex.Message}");
            Console.WriteLine("Make sure you have proper Firestore authentication configured.");
        }
    }

    private static void AdvancedFirestoreExample()
    {
        Console.WriteLine("4. Advanced Firestore Features Example");
        Console.WriteLine("---------------------------------------");

        using var storage = EmbeddedStorageFirestoreExtensions.StartWithFirestore(
            "your-project-id", 
            "advanced-storage", 
            useCache: true);

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
                         "It contains important information stored in Firestore.",
                CreatedDate = DateTime.UtcNow.AddDays(-i),
                Tags = new[] { "important", "document", $"category-{i % 5}" }
            });
        }

        // Store all documents in batch
        var objectIds = storer.StoreAll(documents.ToArray());
        var committedCount = storer.Commit();

        Console.WriteLine($"Batch stored {documents.Count} documents");
        Console.WriteLine($"Committed {committedCount} objects");
        Console.WriteLine($"Object IDs range: {objectIds.Min()} - {objectIds.Max()}");

        // Query documents
        var recentDocuments = storage.Query<Document>()
            .Where(d => d.CreatedDate > DateTime.UtcNow.AddDays(-30))
            .ToList();

        Console.WriteLine($"Recent documents (last 30 days): {recentDocuments.Count}");

        // Demonstrate large object handling
        var largeDocument = new Document
        {
            Id = 999,
            Title = "Large Document",
            Content = new string('X', 2_000_000), // 2MB content
            CreatedDate = DateTime.UtcNow,
            Tags = new[] { "large", "test" }
        };

        storer.Store(largeDocument);
        storer.Commit();

        Console.WriteLine($"Stored large document with {largeDocument.Content.Length:N0} characters");
        Console.WriteLine("Firestore automatically splits large objects across multiple documents");
    }
}

// Example data classes
public class Library
{
    public string Name { get; set; } = "";
    public List<Book> Books { get; set; } = new();
}

public class Book
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public int Year { get; set; }
    public string ISBN { get; set; } = "";
}

public class Inventory
{
    public string Name { get; set; } = "";
    public List<Item> Items { get; set; } = new();
}

public class Item
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class Document
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedDate { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
}
