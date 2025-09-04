# AFS adapter for AWS S3

This module provides an Abstract File System (AFS) adapter for Amazon S3, allowing NebulaStore to use S3 as a storage backend.

## Features

- **Object-based Storage**: Files are stored as objects in S3 buckets
- **Large File Support**: Files larger than S3 limits are automatically split across multiple objects
- **Blob Management**: Efficient handling of binary data using S3's object storage
- **Caching**: Optional caching layer for improved performance
- **Thread-Safe**: All operations are designed to be thread-safe
- **S3 Compliance**: Follows AWS S3 naming conventions and limits
- **Multi-Region Support**: Works with any AWS region or S3-compatible service

## Prerequisites

This module requires the AWS SDK for .NET:
- `AWSSDK.S3` (version 3.7.400.44 or later)

You must also have:
1. An AWS account with S3 access
2. Proper authentication configured (access keys, IAM roles, etc.)
3. An S3 bucket created for storage

## Usage

### Basic Usage

```csharp
using Amazon;
using Amazon.S3;
using NebulaStore.Afs.Aws.S3;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Storage.Embedded;

// Create S3 client
var s3Client = new AmazonS3Client("access-key", "secret-key", RegionEndpoint.USEast1);

// Create AFS configuration with S3
var config = EmbeddedStorageConfiguration.New()
    .SetStorageDirectory("s3-storage")
    .SetUseAfs(true)
    .SetAfsStorageType("s3")
    .SetAfsConnectionString("bucket-name")
    .Build();

using var storage = EmbeddedStorage.StartWithAfs(config);

// Use storage normally
var root = storage.Root<MyDataClass>();
root.SomeProperty = "value";
storage.StoreRoot();
```

### Direct AFS Usage

```csharp
using Amazon;
using Amazon.S3;
using NebulaStore.Afs.Aws.S3;
using NebulaStore.Afs.Blobstore;

// Create S3 client
var s3Client = new AmazonS3Client("access-key", "secret-key", RegionEndpoint.USEast1);

// Create S3 configuration
var s3Config = AwsS3Configuration.New()
    .SetCredentials("access-key", "secret-key")
    .SetRegion(RegionEndpoint.USEast1)
    .SetUseCache(true);

// Create connector
using var connector = AwsS3Connector.New(s3Client, s3Config);

// Create file system
using var fileSystem = BlobStoreFileSystem.New(connector);

// Perform operations
var path = BlobStorePath.New("my-bucket", "folder", "file.txt");
var data = System.Text.Encoding.UTF8.GetBytes("Hello, S3!");

fileSystem.IoHandler.WriteData(path, new[] { data });
var readData = fileSystem.IoHandler.ReadData(path, 0, -1);
```

### Advanced Configuration

```csharp
using Amazon;
using Amazon.S3;
using NebulaStore.Afs.Aws.S3;

// Create advanced S3 configuration
var s3Config = AwsS3Configuration.New()
    .SetCredentials("access-key", "secret-key")
    .SetRegion(RegionEndpoint.USWest2)
    .SetUseCache(true)
    .SetTimeout(60000) // 60 seconds
    .SetMaxRetryAttempts(5)
    .SetForcePathStyle(false)
    .SetUseHttps(true);

// For S3-compatible services (like MinIO)
var minioConfig = AwsS3Configuration.New()
    .SetCredentials("minio-access-key", "minio-secret-key")
    .SetServiceUrl("http://localhost:9000")
    .SetForcePathStyle(true)
    .SetUseHttps(false);

var s3Client = new AmazonS3Client(new AmazonS3Config
{
    ServiceURL = minioConfig.ServiceUrl,
    ForcePathStyle = minioConfig.ForcePathStyle,
    UseHttp = !minioConfig.UseHttps
});

using var connector = AwsS3Connector.New(s3Client, minioConfig);
```

## Configuration Options

### S3-Specific Settings

- **AccessKeyId**: AWS access key ID
- **SecretAccessKey**: AWS secret access key
- **SessionToken**: AWS session token (for temporary credentials)
- **Region**: AWS region endpoint
- **ServiceUrl**: Custom service URL for S3-compatible services
- **ForcePathStyle**: Force path-style addressing (required for some S3-compatible services)
- **UseHttps**: Use HTTPS for connections (default: true)
- **UseCache**: Enable caching for improved performance (default: true)
- **TimeoutMilliseconds**: Timeout for S3 operations (default: 30000)
- **MaxRetryAttempts**: Maximum retry attempts for failed operations (default: 3)

### Bucket Naming Requirements

S3 bucket names must follow AWS naming conventions:
- Between 3 and 63 characters long
- Contain only lowercase letters, numbers, periods (.) and dashes (-)
- Begin with a lowercase letter or number
- Not end with a dash (-)
- Not contain consecutive periods (..)
- Not have dashes adjacent to periods (.- or -.)
- Not be formatted as an IP address
- Not start with 'xn--'

## Performance Considerations

### Caching

The S3 adapter includes an optional caching layer that can significantly improve performance:

```csharp
// Enable caching (recommended for production)
.SetUseCache(true)

// Disable caching (useful for testing or low-memory environments)
.SetUseCache(false)
```

### Large File Handling

Files are automatically split into multiple S3 objects when they exceed practical limits. The adapter handles this transparently, providing seamless read/write operations regardless of file size.

### Network Optimization

- Use appropriate timeout values for your network conditions
- Configure retry attempts based on your reliability requirements
- Consider using S3 Transfer Acceleration for global applications
- Use appropriate S3 storage classes for your access patterns

## Error Handling

S3 operations include comprehensive error handling:

```csharp
try
{
    using var storage = EmbeddedStorage.StartWithAfs(config);
    // ... operations
}
catch (AmazonS3Exception ex)
{
    // Handle S3-specific errors
    Console.WriteLine($"S3 Error: {ex.ErrorCode} - {ex.Message}");
}
catch (Exception ex)
{
    // Handle general errors
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Security Considerations

- Use IAM roles instead of access keys when possible
- Implement least-privilege access policies
- Enable S3 bucket encryption
- Use VPC endpoints for private network access
- Monitor access with CloudTrail
- Regularly rotate access credentials

## Limitations

- Maximum object size: 5TB (S3 limit)
- Maximum number of objects per bucket: Unlimited
- Bucket names must be globally unique
- Some operations may have eventual consistency
- Cross-region data transfer costs may apply

## Troubleshooting

### Common Issues

1. **Access Denied**: Check IAM permissions and bucket policies
2. **Bucket Not Found**: Verify bucket name and region
3. **Network Timeouts**: Increase timeout values or check network connectivity
4. **Invalid Bucket Name**: Ensure bucket name follows AWS naming conventions

### Debug Logging

Enable AWS SDK logging for detailed troubleshooting:

```csharp
AWSConfigs.LoggingConfig.LogTo = LoggingOptions.Console;
AWSConfigs.LoggingConfig.LogResponses = ResponseLoggingOption.Always;
AWSConfigs.LoggingConfig.LogMetrics = true;
```
