using Azure.Storage.Blobs;
using Azure.Identity;
using NebulaStore.Afs.Azure.Storage;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Examples;

/// <summary>
/// Examples demonstrating Azure Blob Storage integration with NebulaStore.
/// </summary>
public static class AzureStorageExample
{
    public static async Task RunAllExamples()
    {
        Console.WriteLine("NebulaStore Azure Blob Storage Examples");
        Console.WriteLine("======================================");

        await BasicAzureStorageExample();
        await DirectAfsUsageExample();
        await ManagedIdentityExample();
        await AdvancedConfigurationExample();
        await PerformanceExample();
    }

    /// <summary>
    /// Basic example using Azure Blob Storage with NebulaStore's embedded storage.
    /// </summary>
    private static async Task BasicAzureStorageExample()
    {
        Console.WriteLine("\n1. Basic Azure Storage Example");
        Console.WriteLine("------------------------------");

        try
        {
            // Replace with your Azure storage connection string
            var connectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net";
            var containerName = "nebulastore-basic";

            // Create configuration for Azure storage
            var config = EmbeddedStorageConfiguration.New()
                .SetStorageDirectory(containerName)
                .SetUseAfs(true)
                .SetAfsStorageType("azure.storage")
                .SetAfsConnectionString(connectionString)
                .SetAfsUseCache(true)
                .Build();

            // Note: This would require implementing the Azure storage integration in AfsStorageConnection
            // For now, we'll show the direct approach
            Console.WriteLine("Configuration created for Azure Blob Storage");
            Console.WriteLine($"Container: {containerName}");
            Console.WriteLine($"Cache enabled: {config.AfsUseCache}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in basic example: {ex.Message}");
        }
    }

    /// <summary>
    /// Example using Azure Blob Storage directly with AFS.
    /// </summary>
    private static async Task DirectAfsUsageExample()
    {
        Console.WriteLine("\n2. Direct AFS Usage Example");
        Console.WriteLine("---------------------------");

        try
        {
            // Replace with your Azure storage connection string
            var connectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net";

            // Create Azure Storage connector
            using var connector = AzureStorageConnector.FromConnectionString(connectionString, useCache: true);

            // Create file system
            using var fileSystem = BlobStoreFileSystem.New(connector);

            // Perform basic file operations
            var path = BlobStorePath.New("nebulastore-direct", "examples", "basic-file.txt");
            var data = System.Text.Encoding.UTF8.GetBytes("Hello from NebulaStore Azure Storage!");

            // Write data
            var bytesWritten = fileSystem.IoHandler.WriteData(path, new[] { data });
            Console.WriteLine($"Written {bytesWritten} bytes to Azure Blob Storage");

            // Read data back
            var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
            var content = System.Text.Encoding.UTF8.GetString(readData);
            Console.WriteLine($"Read from Azure: {content}");

            // Check file size
            var fileSize = connector.GetFileSize(path);
            Console.WriteLine($"File size: {fileSize} bytes");

            // List directory contents
            Console.WriteLine("Directory contents:");
            var visitor = new ConsolePathVisitor();
            connector.VisitChildren(BlobStorePath.New("nebulastore-direct", "examples"), visitor);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in direct AFS example: {ex.Message}");
        }
    }

    /// <summary>
    /// Example using Azure Managed Identity for authentication.
    /// </summary>
    private static async Task ManagedIdentityExample()
    {
        Console.WriteLine("\n3. Managed Identity Example");
        Console.WriteLine("---------------------------");

        try
        {
            var accountName = "myazurestorageaccount";
            var containerName = "nebulastore-managed";

            // Create Azure Storage client with managed identity
            var credential = new DefaultAzureCredential();
            var serviceUri = new Uri($"https://{accountName}.blob.core.windows.net");
            var blobServiceClient = new BlobServiceClient(serviceUri, credential);

            // Create Azure Storage configuration
            var azureConfig = AzureStorageConfiguration.New()
                .SetCredential(credential)
                .SetServiceEndpoint(serviceUri)
                .SetUseCache(true)
                .SetMaxBlobSize(50 * 1024 * 1024); // 50MB per blob

            // Create connector
            using var connector = AzureStorageConnector.New(blobServiceClient, azureConfig);
            using var fileSystem = BlobStoreFileSystem.New(connector);

            // Test operations
            var path = BlobStorePath.New(containerName, "managed-identity", "test.dat");
            var testData = new byte[1024]; // 1KB of test data
            new Random().NextBytes(testData);

            var bytesWritten = fileSystem.IoHandler.WriteData(path, new[] { testData });
            Console.WriteLine($"Written {bytesWritten} bytes using managed identity");

            var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
            Console.WriteLine($"Read {readData.Length} bytes back");
            Console.WriteLine($"Data integrity: {testData.SequenceEqual(readData)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in managed identity example: {ex.Message}");
            Console.WriteLine("Note: This requires proper Azure managed identity setup");
        }
    }

    /// <summary>
    /// Example with advanced Azure Storage configuration.
    /// </summary>
    private static async Task AdvancedConfigurationExample()
    {
        Console.WriteLine("\n4. Advanced Configuration Example");
        Console.WriteLine("---------------------------------");

        try
        {
            var connectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net";

            // Create advanced Azure Storage configuration
            var azureConfig = AzureStorageConfiguration.New()
                .SetConnectionString(connectionString)
                .SetUseCache(true)
                .SetTimeout(120000) // 2 minutes
                .SetMaxRetryAttempts(5)
                .SetMaxBlobSize(200 * 1024 * 1024) // 200MB per blob
                .SetUseHttps(true);

            // Create blob service client with custom options
            var blobServiceClient = AzureStorageClientFactory.CreateBlobServiceClient(azureConfig);

            // Create connector with advanced configuration
            using var connector = AzureStorageConnector.New(blobServiceClient, azureConfig);
            using var fileSystem = BlobStoreFileSystem.New(connector);

            // Test with larger data
            var path = BlobStorePath.New("nebulastore-advanced", "large-files", "big-data.bin");
            var largeData = new byte[5 * 1024 * 1024]; // 5MB
            new Random().NextBytes(largeData);

            Console.WriteLine("Writing large file (5MB)...");
            var start = DateTime.UtcNow;
            var bytesWritten = fileSystem.IoHandler.WriteData(path, new[] { largeData });
            var writeTime = DateTime.UtcNow - start;

            Console.WriteLine($"Written {bytesWritten:N0} bytes in {writeTime.TotalMilliseconds:F2}ms");
            Console.WriteLine($"Write speed: {(bytesWritten / 1024.0 / 1024.0) / writeTime.TotalSeconds:F2} MB/s");

            // Test partial read
            start = DateTime.UtcNow;
            var partialData = fileSystem.IoHandler.ReadData(path, 1024, 2048); // Read 2KB from offset 1KB
            var readTime = DateTime.UtcNow - start;

            Console.WriteLine($"Partial read: {partialData.Length} bytes in {readTime.TotalMilliseconds:F2}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in advanced configuration example: {ex.Message}");
        }
    }

    /// <summary>
    /// Performance testing example.
    /// </summary>
    private static async Task PerformanceExample()
    {
        Console.WriteLine("\n5. Performance Example");
        Console.WriteLine("----------------------");

        try
        {
            var connectionString = "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=mykey;EndpointSuffix=core.windows.net";

            using var connector = AzureStorageConnector.FromConnectionString(connectionString, useCache: true);
            using var fileSystem = BlobStoreFileSystem.New(connector);

            // Performance test with multiple small files
            var containerPath = BlobStorePath.New("nebulastore-perf", "small-files");
            var fileCount = 100;
            var fileSize = 1024; // 1KB per file

            Console.WriteLine($"Performance test: {fileCount} files of {fileSize} bytes each");

            var start = DateTime.UtcNow;
            var tasks = new List<Task>();

            for (int i = 0; i < fileCount; i++)
            {
                var fileIndex = i;
                tasks.Add(Task.Run(() =>
                {
                    var filePath = BlobStorePath.New("nebulastore-perf", "small-files", $"file-{fileIndex:D3}.dat");
                    var data = new byte[fileSize];
                    new Random(fileIndex).NextBytes(data);
                    fileSystem.IoHandler.WriteData(filePath, new[] { data });
                }));
            }

            await Task.WhenAll(tasks);
            var totalTime = DateTime.UtcNow - start;

            Console.WriteLine($"Wrote {fileCount} files in {totalTime.TotalMilliseconds:F2}ms");
            Console.WriteLine($"Average: {totalTime.TotalMilliseconds / fileCount:F2}ms per file");
            Console.WriteLine($"Throughput: {(fileCount * fileSize / 1024.0) / totalTime.TotalSeconds:F2} KB/s");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in performance example: {ex.Message}");
        }
    }

    /// <summary>
    /// Simple visitor for displaying directory contents.
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
}
