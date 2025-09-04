using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using NebulaStore.Afs.Aws.S3;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Examples;

/// <summary>
/// Example demonstrating how to use NebulaStore with AWS S3 as the storage backend.
/// </summary>
public class AwsS3Example
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("NebulaStore AWS S3 Example");
        Console.WriteLine("==========================");

        // Example 1: Basic S3 usage with NebulaStore
        await BasicS3Example();

        // Example 2: Direct AFS usage with S3
        await DirectAfsS3Example();

        // Example 3: Advanced S3 configuration
        await AdvancedS3ConfigurationExample();

        // Example 4: S3-compatible service (MinIO) example
        await MinioExample();

        Console.WriteLine("\nAll examples completed successfully!");
    }

    /// <summary>
    /// Basic example using S3 with NebulaStore's embedded storage.
    /// </summary>
    private static async Task BasicS3Example()
    {
        Console.WriteLine("\n1. Basic S3 Example");
        Console.WriteLine("-------------------");

        try
        {
            // Create S3 client (replace with your credentials)
            var s3Client = new AmazonS3Client("your-access-key", "your-secret-key", RegionEndpoint.USEast1);

            // Create S3 configuration
            var s3Config = AwsS3Configuration.New()
                .SetCredentials("your-access-key", "your-secret-key")
                .SetRegion(RegionEndpoint.USEast1)
                .SetUseCache(true);

            // Create connector and file system
            using var connector = AwsS3Connector.New(s3Client, s3Config);
            using var fileSystem = BlobStoreFileSystem.New(connector);

            // Perform basic file operations
            var path = BlobStorePath.New("my-nebulastore-bucket", "examples", "basic-file.txt");
            var data = System.Text.Encoding.UTF8.GetBytes("Hello from NebulaStore S3!");

            // Write data
            var bytesWritten = fileSystem.IoHandler.WriteData(path, new[] { data });
            Console.WriteLine($"Written {bytesWritten} bytes to S3");

            // Read data back
            var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
            var content = System.Text.Encoding.UTF8.GetString(readData);
            Console.WriteLine($"Read from S3: {content}");

            // Check file size
            var fileSize = fileSystem.IoHandler.GetFileSize(path);
            Console.WriteLine($"File size: {fileSize} bytes");

            s3Client.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in basic S3 example: {ex.Message}");
        }
    }

    /// <summary>
    /// Example using S3 directly with AFS without embedded storage.
    /// </summary>
    private static async Task DirectAfsS3Example()
    {
        Console.WriteLine("\n2. Direct AFS S3 Example");
        Console.WriteLine("------------------------");

        try
        {
            // Create S3 client
            var s3Client = new AmazonS3Client("your-access-key", "your-secret-key", RegionEndpoint.USWest2);

            // Create S3 configuration with custom settings
            var s3Config = AwsS3Configuration.New()
                .SetCredentials("your-access-key", "your-secret-key")
                .SetRegion(RegionEndpoint.USWest2)
                .SetUseCache(true)
                .SetTimeout(60000) // 60 seconds
                .SetMaxRetryAttempts(5);

            // Create connector
            using var connector = AwsS3Connector.NewWithCaching(s3Client, s3Config);

            // Test directory operations
            var directory = BlobStorePath.New("my-nebulastore-bucket", "test-directory");
            
            Console.WriteLine($"Creating directory: {directory}");
            connector.CreateDirectory(directory);

            Console.WriteLine($"Directory exists: {connector.DirectoryExists(directory)}");
            Console.WriteLine($"Directory is empty: {connector.IsEmpty(directory)}");

            // Test file operations
            var file = BlobStorePath.New("my-nebulastore-bucket", "test-directory", "test-file.txt");
            var testData = System.Text.Encoding.UTF8.GetBytes("Direct AFS S3 test data");

            Console.WriteLine($"Writing file: {file}");
            connector.WriteData(file, new[] { testData });

            Console.WriteLine($"File exists: {connector.FileExists(file)}");
            Console.WriteLine($"File size: {connector.GetFileSize(file)} bytes");

            // Read file data
            var readData = connector.ReadData(file, 0, -1);
            var content = System.Text.Encoding.UTF8.GetString(readData);
            Console.WriteLine($"File content: {content}");

            // Clean up
            connector.DeleteFile(file);
            Console.WriteLine("File deleted");

            s3Client.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in direct AFS S3 example: {ex.Message}");
        }
    }

    /// <summary>
    /// Example with advanced S3 configuration options.
    /// </summary>
    private static async Task AdvancedS3ConfigurationExample()
    {
        Console.WriteLine("\n3. Advanced S3 Configuration Example");
        Console.WriteLine("------------------------------------");

        try
        {
            // Advanced S3 configuration
            var s3Config = AwsS3Configuration.New()
                .SetCredentials("your-access-key", "your-secret-key")
                .SetRegion(RegionEndpoint.EUWest1)
                .SetUseCache(true)
                .SetTimeout(120000) // 2 minutes
                .SetMaxRetryAttempts(10)
                .SetForcePathStyle(false)
                .SetUseHttps(true);

            // Create S3 client with advanced configuration
            var s3ClientConfig = new AmazonS3Config
            {
                RegionEndpoint = s3Config.Region,
                Timeout = TimeSpan.FromMilliseconds(s3Config.TimeoutMilliseconds),
                MaxErrorRetry = s3Config.MaxRetryAttempts,
                UseHttp = !s3Config.UseHttps,
                ForcePathStyle = s3Config.ForcePathStyle
            };

            var s3Client = new AmazonS3Client("your-access-key", "your-secret-key", s3ClientConfig);

            // Create connector with advanced configuration
            using var connector = AwsS3Connector.New(s3Client, s3Config);

            // Test with larger data
            var largeFile = BlobStorePath.New("my-nebulastore-bucket", "large-files", "large-test.dat");
            var largeData = new byte[1024 * 1024]; // 1MB of data
            new Random().NextBytes(largeData);

            Console.WriteLine($"Writing large file ({largeData.Length} bytes)");
            var bytesWritten = connector.WriteData(largeFile, new[] { largeData });
            Console.WriteLine($"Written {bytesWritten} bytes");

            // Test partial read
            var partialData = connector.ReadData(largeFile, 1000, 2000);
            Console.WriteLine($"Read partial data: {partialData.Length} bytes");

            // Test file truncation
            connector.TruncateFile(largeFile, 500000); // Truncate to 500KB
            var newSize = connector.GetFileSize(largeFile);
            Console.WriteLine($"File size after truncation: {newSize} bytes");

            // Clean up
            connector.DeleteFile(largeFile);
            Console.WriteLine("Large file deleted");

            s3Client.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in advanced S3 configuration example: {ex.Message}");
        }
    }

    /// <summary>
    /// Example using S3-compatible service like MinIO.
    /// </summary>
    private static async Task MinioExample()
    {
        Console.WriteLine("\n4. MinIO (S3-Compatible) Example");
        Console.WriteLine("--------------------------------");

        try
        {
            // MinIO configuration
            var minioConfig = AwsS3Configuration.New()
                .SetCredentials("minio-access-key", "minio-secret-key")
                .SetServiceUrl("http://localhost:9000")
                .SetForcePathStyle(true) // Required for MinIO
                .SetUseHttps(false)
                .SetUseCache(true);

            // Create S3 client for MinIO
            var s3ClientConfig = new AmazonS3Config
            {
                ServiceURL = minioConfig.ServiceUrl,
                ForcePathStyle = minioConfig.ForcePathStyle,
                UseHttp = !minioConfig.UseHttps
            };

            var s3Client = new AmazonS3Client("minio-access-key", "minio-secret-key", s3ClientConfig);

            // Create connector for MinIO
            using var connector = AwsS3Connector.New(s3Client, minioConfig);

            // Test MinIO operations
            var minioFile = BlobStorePath.New("test-bucket", "minio-test.txt");
            var minioData = System.Text.Encoding.UTF8.GetBytes("Hello from MinIO!");

            Console.WriteLine("Writing to MinIO");
            connector.WriteData(minioFile, new[] { minioData });

            Console.WriteLine($"File exists in MinIO: {connector.FileExists(minioFile)}");

            var readData = connector.ReadData(minioFile, 0, -1);
            var content = System.Text.Encoding.UTF8.GetString(readData);
            Console.WriteLine($"Read from MinIO: {content}");

            // Clean up
            connector.DeleteFile(minioFile);
            Console.WriteLine("MinIO file deleted");

            s3Client.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in MinIO example: {ex.Message}");
            Console.WriteLine("Note: Make sure MinIO is running on localhost:9000 for this example to work");
        }
    }
}

/// <summary>
/// Example data class for storage.
/// </summary>
public class ExampleData
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<string> Tags { get; set; } = new();
}
