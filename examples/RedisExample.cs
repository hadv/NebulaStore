using System;
using System.Collections.Generic;
using StackExchange.Redis;
using NebulaStore.Afs.Redis;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Examples;

/// <summary>
/// Examples demonstrating Redis AFS connector usage with NebulaStore.
/// </summary>
public static class RedisExample
{
    // Example data models
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class Inventory
    {
        public List<Product> Products { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Basic example using Redis with EmbeddedStorage.
    /// </summary>
    public static void BasicRedisStorageExample()
    {
        Console.WriteLine("=== Basic Redis Storage Example ===\n");

        // Configure storage to use Redis
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory("redis-storage")
            .SetAfsStorageType("redis")
            .SetAfsConnectionString("localhost:6379")
            .SetAfsUseCache(true);

        // Start storage with Redis backend
        using var storage = EmbeddedStorage.StartWithAfs(config);

        // Initialize or get root object
        var inventory = storage.Root<Inventory>();
        if (inventory.Products.Count == 0)
        {
            Console.WriteLine("Initializing inventory...");
            inventory.Products.Add(new Product
            {
                Id = 1,
                Name = "Laptop",
                Price = 999.99m,
                Tags = new List<string> { "electronics", "computers" }
            });
            inventory.Products.Add(new Product
            {
                Id = 2,
                Name = "Mouse",
                Price = 29.99m,
                Tags = new List<string> { "electronics", "accessories" }
            });
            inventory.LastUpdated = DateTime.UtcNow;
        }

        // Store the root object
        storage.StoreRoot();
        Console.WriteLine($"Stored {inventory.Products.Count} products");
        Console.WriteLine($"Last updated: {inventory.LastUpdated}");

        // Display products
        foreach (var product in inventory.Products)
        {
            Console.WriteLine($"  - {product.Name}: ${product.Price} (Tags: {string.Join(", ", product.Tags)})");
        }
    }

    /// <summary>
    /// Example using direct AFS API with Redis.
    /// </summary>
    public static void DirectRedisAfsExample()
    {
        Console.WriteLine("\n=== Direct Redis AFS Example ===\n");

        // Create Redis connection
        var redis = ConnectionMultiplexer.Connect("localhost:6379");
        Console.WriteLine("Connected to Redis");

        // Create connector with caching
        using var connector = RedisConnector.Caching(redis);
        using var fileSystem = BlobStoreFileSystem.New(connector);

        // Create a path
        var path = BlobStorePath.New("products", "data", "product-1.dat");

        // Write data
        var productData = System.Text.Encoding.UTF8.GetBytes("Product: Laptop, Price: $999.99");
        var bytesWritten = fileSystem.IoHandler.WriteData(path, productData);
        Console.WriteLine($"Written {bytesWritten} bytes to {path}");

        // Read data back
        var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
        var content = System.Text.Encoding.UTF8.GetString(readData);
        Console.WriteLine($"Read back: {content}");

        // Check file size
        var fileSize = fileSystem.IoHandler.GetFileSize(path);
        Console.WriteLine($"File size: {fileSize} bytes");

        // Delete file
        var deleted = fileSystem.IoHandler.DeleteFile(path);
        Console.WriteLine($"File deleted: {deleted}");
    }

    /// <summary>
    /// Example using RedisConfiguration for advanced setup.
    /// </summary>
    public static void AdvancedRedisConfigurationExample()
    {
        Console.WriteLine("\n=== Advanced Redis Configuration Example ===\n");

        // Create detailed configuration
        var redisConfig = RedisConfiguration.New()
            .SetConnectionString("localhost:6379")
            .SetDatabaseNumber(1)  // Use database 1 instead of default 0
            .SetUseCache(true)
            .SetCommandTimeout(TimeSpan.FromSeconds(30))
            .SetConnectTimeout(TimeSpan.FromSeconds(10))
            .SetAllowAdmin(true);

        Console.WriteLine("Redis Configuration:");
        Console.WriteLine($"  Connection: {redisConfig.ConnectionString}");
        Console.WriteLine($"  Database: {redisConfig.DatabaseNumber}");
        Console.WriteLine($"  Cache: {redisConfig.UseCache}");
        Console.WriteLine($"  Command Timeout: {redisConfig.CommandTimeout}");

        // Build StackExchange.Redis options
        var options = redisConfig.ToConfigurationOptions();
        var redis = ConnectionMultiplexer.Connect(options);

        // Create connector
        using var connector = RedisConnector.New(redis, redisConfig.DatabaseNumber);
        using var fileSystem = BlobStoreFileSystem.New(connector);

        // Test operations
        var testPath = BlobStorePath.New("test-container", "advanced", "test.dat");
        var testData = new byte[1024]; // 1KB of test data
        new Random().NextBytes(testData);

        var bytesWritten = fileSystem.IoHandler.WriteData(testPath, testData);
        Console.WriteLine($"\nWritten {bytesWritten} bytes to Redis database {redisConfig.DatabaseNumber}");

        var readData = fileSystem.IoHandler.ReadData(testPath, 0, -1);
        Console.WriteLine($"Read {readData.Length} bytes back");
        Console.WriteLine($"Data integrity: {testData.SequenceEqual(readData)}");

        // Cleanup
        fileSystem.IoHandler.DeleteFile(testPath);
        Console.WriteLine("Test file deleted");
    }

    /// <summary>
    /// Example demonstrating Redis with multiple storage operations.
    /// </summary>
    public static void MultipleOperationsExample()
    {
        Console.WriteLine("\n=== Multiple Operations Example ===\n");

        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory("redis-multi-storage")
            .SetAfsStorageType("redis")
            .SetAfsConnectionString("localhost:6379")
            .SetAfsUseCache(true);

        using var storage = EmbeddedStorage.StartWithAfs(config);

        var inventory = storage.Root<Inventory>();
        
        // Add multiple products
        Console.WriteLine("Adding products...");
        for (int i = 1; i <= 5; i++)
        {
            inventory.Products.Add(new Product
            {
                Id = i,
                Name = $"Product {i}",
                Price = 10.00m * i,
                Tags = new List<string> { "category-" + (i % 3), "tag-" + i }
            });
        }
        inventory.LastUpdated = DateTime.UtcNow;

        // Store all
        storage.StoreRoot();
        Console.WriteLine($"Stored {inventory.Products.Count} products");

        // Update a product
        Console.WriteLine("\nUpdating product...");
        var productToUpdate = inventory.Products[0];
        productToUpdate.Price = 99.99m;
        productToUpdate.Tags.Add("updated");
        storage.Store(productToUpdate);
        Console.WriteLine($"Updated {productToUpdate.Name} to ${productToUpdate.Price}");

        // Remove a product
        Console.WriteLine("\nRemoving product...");
        var productToRemove = inventory.Products[^1];
        inventory.Products.Remove(productToRemove);
        storage.StoreRoot();
        Console.WriteLine($"Removed {productToRemove.Name}, now have {inventory.Products.Count} products");

        // Display final state
        Console.WriteLine("\nFinal inventory:");
        foreach (var product in inventory.Products)
        {
            Console.WriteLine($"  - {product.Name}: ${product.Price}");
        }
    }

    /// <summary>
    /// Example showing Redis connection with authentication.
    /// </summary>
    public static void RedisWithAuthenticationExample()
    {
        Console.WriteLine("\n=== Redis with Authentication Example ===\n");

        // Note: This example assumes you have a Redis server with authentication enabled
        var redisConfig = RedisConfiguration.New()
            .SetConnectionString("localhost:6379")
            .SetPassword("your-redis-password")  // Set your Redis password
            .SetUseSsl(false)  // Set to true if using SSL/TLS
            .SetDatabaseNumber(0)
            .SetUseCache(true);

        try
        {
            var options = redisConfig.ToConfigurationOptions();
            Console.WriteLine("Attempting to connect to Redis with authentication...");
            
            // Note: This will fail if Redis is not configured with the password
            // Uncomment to test with actual Redis authentication
            // var redis = ConnectionMultiplexer.Connect(options);
            // using var connector = RedisConnector.New(redis);
            // Console.WriteLine("Successfully connected to Redis with authentication");
            
            Console.WriteLine("(Example code - requires Redis with authentication configured)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Run all Redis examples.
    /// </summary>
    public static void RunAllExamples()
    {
        try
        {
            BasicRedisStorageExample();
            DirectRedisAfsExample();
            AdvancedRedisConfigurationExample();
            MultipleOperationsExample();
            RedisWithAuthenticationExample();

            Console.WriteLine("\n=== All Redis Examples Completed ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError running examples: {ex.Message}");
            Console.WriteLine("Make sure Redis server is running on localhost:6379");
            Console.WriteLine("You can start Redis with: redis-server");
        }
    }
}

