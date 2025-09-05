using System;
using Google.Cloud.Firestore;
using NebulaStore.Afs.GoogleCloud.Firestore;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Afs.GoogleCloud.Firestore.Examples;

/// <summary>
/// Example demonstrating how to use NebulaStore with Google Cloud Firestore.
/// </summary>
public class FirestoreExample
{
    /// <summary>
    /// Example data class for storage.
    /// </summary>
    public class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>
    /// Example using the configuration-based approach.
    /// </summary>
    public static void ConfigurationBasedExample()
    {
        Console.WriteLine("=== Configuration-Based Firestore Example ===");

        // Create configuration for Firestore storage
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory("firestore-storage")
            .UseFirestore("your-project-id", useCache: true)
            .SetChannelCount(4)
            .Build();

        try
        {
            // Start storage with Firestore backend
            using var storage = EmbeddedStorage.Foundation(config).Start();

            // Create and store data
            var root = storage.Root<Person>();
            if (root == null)
            {
                root = new Person
                {
                    Name = "John Doe",
                    Age = 30,
                    Email = "john.doe@example.com"
                };
                storage.SetRoot(root);
            }

            // Modify and store
            root.Age = 31;
            storage.StoreRoot();

            Console.WriteLine($"Stored person: {root.Name}, Age: {root.Age}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Make sure you have:");
            Console.WriteLine("1. A Google Cloud Project with Firestore enabled");
            Console.WriteLine("2. Proper authentication configured");
            Console.WriteLine("3. The Google.Cloud.Firestore package installed");
        }
    }

    /// <summary>
    /// Example using the convenience extension methods.
    /// </summary>
    public static void ConvenienceMethodExample()
    {
        Console.WriteLine("\n=== Convenience Method Firestore Example ===");

        try
        {
            // Start with Firestore using convenience method
            using var storage = EmbeddedStorageFirestoreExtensions.StartWithFirestore("your-project-id");

            var root = storage.Root<Person>();
            if (root == null)
            {
                root = new Person
                {
                    Name = "Jane Smith",
                    Age = 25,
                    Email = "jane.smith@example.com"
                };
                storage.SetRoot(root);
            }

            Console.WriteLine($"Retrieved person: {root.Name}, Age: {root.Age}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Example using direct AFS file system operations.
    /// </summary>
    public static void DirectAfsExample()
    {
        Console.WriteLine("\n=== Direct AFS Firestore Example ===");

        try
        {
            // Create Firestore connection
            var firestore = FirestoreDb.Create("your-project-id");

            // Create file system with Firestore backend
            using var fileSystem = EmbeddedStorageFirestoreExtensions.CreateFirestoreFileSystem("your-project-id");

            // Perform direct file operations
            var path = NebulaStore.Afs.Blobstore.BlobStorePath.New("my-collection", "folder", "file.txt");
            var data = System.Text.Encoding.UTF8.GetBytes("Hello, Firestore!");

            fileSystem.IoHandler.WriteData(path, data);
            var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
            var content = System.Text.Encoding.UTF8.GetString(readData);

            Console.WriteLine($"Stored and retrieved: {content}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Example with authentication setup.
    /// </summary>
    public static void AuthenticationExample()
    {
        Console.WriteLine("\n=== Authentication Setup Example ===");

        try
        {
            // Option 1: Using service account key file
            var firestoreWithKey = new FirestoreDbBuilder
            {
                ProjectId = "your-project-id",
                CredentialsPath = "path/to/service-account-key.json"
            }.Build();

            using var connector1 = GoogleCloudFirestoreConnector.New(firestoreWithKey);
            Console.WriteLine("Created connector with service account key");

            // Option 2: Using environment variable for credentials
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "path/to/service-account-key.json");
            var firestoreWithEnv = FirestoreDb.Create("your-project-id");

            using var connector2 = GoogleCloudFirestoreConnector.Caching(firestoreWithEnv);
            Console.WriteLine("Created connector with environment credentials");

            // Option 3: Using emulator for testing
            Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", "localhost:8080");
            var firestoreEmulator = FirestoreDb.Create("test-project");

            using var connector3 = GoogleCloudFirestoreConnector.New(firestoreEmulator);
            Console.WriteLine("Created connector for emulator");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Main entry point for the examples.
    /// </summary>
    public static void Main(string[] args)
    {
        Console.WriteLine("NebulaStore Google Cloud Firestore Integration Examples");
        Console.WriteLine("=====================================================");

        ConfigurationBasedExample();
        ConvenienceMethodExample();
        DirectAfsExample();
        AuthenticationExample();

        Console.WriteLine("\nFor more information, see the README.md file.");
    }
}
