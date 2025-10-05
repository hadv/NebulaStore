using System;
using NebulaStore.Afs.Kafka;
using NebulaStore.Afs.Blobstore;
using Confluent.Kafka;

namespace NebulaStore.Afs.Kafka.Examples;

/// <summary>
/// Examples demonstrating Kafka AFS adapter usage.
/// </summary>
public class KafkaExample
{
    /// <summary>
    /// Basic usage example.
    /// </summary>
    public static void BasicUsage()
    {
        Console.WriteLine("=== Basic Kafka AFS Usage ===\n");

        // Configure Kafka
        var config = KafkaConfiguration.New("localhost:9092");

        // Create connector
        using var connector = KafkaConnector.New(config);

        // Create file system
        using var fileSystem = BlobStoreFileSystem.New(connector);

        // Create a path
        var path = BlobStorePath.New("my-container", "data", "example.txt");

        // Write data
        var data = System.Text.Encoding.UTF8.GetBytes("Hello, Kafka AFS!");
        var bytesWritten = fileSystem.IoHandler.WriteData(path, new[] { data });
        Console.WriteLine($"Wrote {bytesWritten} bytes to {path.FullQualifiedName}");

        // Read data
        var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
        var content = System.Text.Encoding.UTF8.GetString(readData);
        Console.WriteLine($"Read: {content}");

        // Get file size
        var size = fileSystem.IoHandler.GetFileSize(path);
        Console.WriteLine($"File size: {size} bytes");

        // Delete file
        fileSystem.IoHandler.DeleteFile(path);
        Console.WriteLine($"Deleted {path.FullQualifiedName}");
    }

    /// <summary>
    /// Production configuration example.
    /// </summary>
    public static void ProductionConfiguration()
    {
        Console.WriteLine("\n=== Production Configuration ===\n");

        var config = KafkaConfiguration.Production(
            bootstrapServers: "kafka1:9092,kafka2:9092,kafka3:9092",
            clientId: "nebulastore-prod"
        );

        // Customize settings
        config.Compression = CompressionType.Snappy;
        config.MaxMessageBytes = 2_000_000; // 2MB chunks
        config.RequestTimeout = TimeSpan.FromMinutes(2);

        Console.WriteLine($"Bootstrap Servers: {config.BootstrapServers}");
        Console.WriteLine($"Client ID: {config.ClientId}");
        Console.WriteLine($"Compression: {config.Compression}");
        Console.WriteLine($"Max Message Bytes: {config.MaxMessageBytes}");
        Console.WriteLine($"Idempotence: {config.EnableIdempotence}");
    }

    /// <summary>
    /// Development configuration example.
    /// </summary>
    public static void DevelopmentConfiguration()
    {
        Console.WriteLine("\n=== Development Configuration ===\n");

        var config = KafkaConfiguration.Development();

        Console.WriteLine($"Bootstrap Servers: {config.BootstrapServers}");
        Console.WriteLine($"Client ID: {config.ClientId}");
        Console.WriteLine($"Compression: {config.Compression}");
        Console.WriteLine($"Cache Enabled: {config.UseCache}");
    }

    /// <summary>
    /// Custom configuration example.
    /// </summary>
    public static void CustomConfiguration()
    {
        Console.WriteLine("\n=== Custom Configuration ===\n");

        var config = new KafkaConfiguration
        {
            BootstrapServers = "localhost:9092",
            ClientId = "my-custom-app",
            EnableIdempotence = true,
            Compression = CompressionType.Lz4,
            MaxMessageBytes = 1_500_000,
            
            AdditionalSettings = new System.Collections.Generic.Dictionary<string, string>
            {
                ["acks"] = "all",
                ["retries"] = "5",
                ["batch.size"] = "32768",
                ["linger.ms"] = "10"
            }
        };

        // Validate configuration
        config.Validate();

        Console.WriteLine("Configuration validated successfully");
        Console.WriteLine($"Additional settings: {config.AdditionalSettings.Count}");
    }

    /// <summary>
    /// Large file handling example.
    /// </summary>
    public static void LargeFileHandling()
    {
        Console.WriteLine("\n=== Large File Handling ===\n");

        var config = KafkaConfiguration.New("localhost:9092");
        config.MaxMessageBytes = 1_000_000; // 1MB chunks

        using var connector = KafkaConnector.New(config);
        using var fileSystem = BlobStoreFileSystem.New(connector);

        var path = BlobStorePath.New("my-container", "large-file.dat");

        // Create 5MB of data
        var largeData = new byte[5_000_000];
        new Random().NextBytes(largeData);

        Console.WriteLine($"Writing {largeData.Length:N0} bytes...");
        var bytesWritten = fileSystem.IoHandler.WriteData(path, new[] { largeData });
        Console.WriteLine($"Wrote {bytesWritten:N0} bytes");

        // Read back
        Console.WriteLine("Reading data back...");
        var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
        Console.WriteLine($"Read {readData.Length:N0} bytes");

        // Verify
        var match = true;
        for (int i = 0; i < largeData.Length; i++)
        {
            if (largeData[i] != readData[i])
            {
                match = false;
                break;
            }
        }

        Console.WriteLine($"Data integrity: {(match ? "PASS" : "FAIL")}");

        // Cleanup
        fileSystem.IoHandler.DeleteFile(path);
    }

    /// <summary>
    /// Partial read example.
    /// </summary>
    public static void PartialRead()
    {
        Console.WriteLine("\n=== Partial Read ===\n");

        var config = KafkaConfiguration.New("localhost:9092");
        using var connector = KafkaConnector.New(config);
        using var fileSystem = BlobStoreFileSystem.New(connector);

        var path = BlobStorePath.New("my-container", "partial-read.txt");

        // Write data
        var data = System.Text.Encoding.UTF8.GetBytes("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        fileSystem.IoHandler.WriteData(path, new[] { data });

        // Read first 10 bytes
        var first10 = fileSystem.IoHandler.ReadData(path, 0, 10);
        Console.WriteLine($"First 10 bytes: {System.Text.Encoding.UTF8.GetString(first10)}");

        // Read middle 10 bytes
        var middle10 = fileSystem.IoHandler.ReadData(path, 10, 10);
        Console.WriteLine($"Middle 10 bytes: {System.Text.Encoding.UTF8.GetString(middle10)}");

        // Read last 10 bytes
        var last10 = fileSystem.IoHandler.ReadData(path, 26, 10);
        Console.WriteLine($"Last 10 bytes: {System.Text.Encoding.UTF8.GetString(last10)}");

        // Cleanup
        fileSystem.IoHandler.DeleteFile(path);
    }

    /// <summary>
    /// Directory operations example.
    /// </summary>
    public static void DirectoryOperations()
    {
        Console.WriteLine("\n=== Directory Operations ===\n");

        var config = KafkaConfiguration.New("localhost:9092");
        using var connector = KafkaConnector.New(config);
        using var fileSystem = BlobStoreFileSystem.New(connector);

        // Create files in a directory structure
        var files = new[]
        {
            BlobStorePath.New("my-container", "dir1", "file1.txt"),
            BlobStorePath.New("my-container", "dir1", "file2.txt"),
            BlobStorePath.New("my-container", "dir1", "subdir", "file3.txt"),
            BlobStorePath.New("my-container", "dir2", "file4.txt")
        };

        foreach (var file in files)
        {
            var data = System.Text.Encoding.UTF8.GetBytes($"Content of {file.FullQualifiedName}");
            fileSystem.IoHandler.WriteData(file, new[] { data });
            Console.WriteLine($"Created: {file.FullQualifiedName}");
        }

        // List directory contents
        var dir1 = BlobStorePath.New("my-container", "dir1");
        Console.WriteLine($"\nContents of {dir1.FullQualifiedName}:");

        var visitor = new ConsolePathVisitor();
        fileSystem.IoHandler.VisitChildren(dir1, visitor);

        // Check if directory is empty
        var emptyDir = BlobStorePath.New("my-container", "empty-dir");
        var isEmpty = fileSystem.IoHandler.IsEmpty(emptyDir);
        Console.WriteLine($"\n{emptyDir.FullQualifiedName} is empty: {isEmpty}");

        // Cleanup
        foreach (var file in files)
        {
            fileSystem.IoHandler.DeleteFile(file);
        }
    }

    /// <summary>
    /// Simple path visitor for console output.
    /// </summary>
    private class ConsolePathVisitor : IBlobStorePathVisitor
    {
        public void VisitDirectory(BlobStorePath parent, string directoryName)
        {
            Console.WriteLine($"  [DIR]  {directoryName}");
        }

        public void VisitFile(BlobStorePath parent, string fileName)
        {
            Console.WriteLine($"  [FILE] {fileName}");
        }
    }

    /// <summary>
    /// Run all examples.
    /// </summary>
    public static void Main(string[] args)
    {
        Console.WriteLine("Kafka AFS Examples");
        Console.WriteLine("==================\n");

        try
        {
            // Note: These examples require a running Kafka instance
            Console.WriteLine("Note: These examples require Kafka running at localhost:9092\n");

            BasicUsage();
            ProductionConfiguration();
            DevelopmentConfiguration();
            CustomConfiguration();
            
            // Uncomment to run examples that require Kafka:
            // LargeFileHandling();
            // PartialRead();
            // DirectoryOperations();

            Console.WriteLine("\n=== All Examples Completed ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
            Console.WriteLine("\nMake sure Kafka is running at localhost:9092");
        }
    }
}

