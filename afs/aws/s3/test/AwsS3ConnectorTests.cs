using System.Text;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Moq;
using NebulaStore.Afs.Aws.S3;
using NebulaStore.Afs.Blobstore;
using Xunit;

namespace NebulaStore.Afs.Aws.S3.Tests;

public class AwsS3ConnectorTests : IDisposable
{
    private readonly Mock<IAmazonS3> _mockS3Client;
    private readonly AwsS3Configuration _configuration;
    private readonly AwsS3Connector _connector;

    public AwsS3ConnectorTests()
    {
        _mockS3Client = new Mock<IAmazonS3>();
        _configuration = AwsS3Configuration.New()
            .SetCredentials("test-access-key", "test-secret-key")
            .SetRegion(RegionEndpoint.USEast1)
            .SetUseCache(false); // Disable caching for tests

        _connector = AwsS3Connector.New(_mockS3Client.Object, _configuration);
    }

    [Fact]
    public void New_WithValidParameters_CreatesConnector()
    {
        // Arrange & Act
        var connector = AwsS3Connector.New(_mockS3Client.Object, _configuration);

        // Assert
        Assert.NotNull(connector);
    }

    [Fact]
    public void New_WithNullS3Client_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => AwsS3Connector.New(null!, _configuration));
    }

    [Fact]
    public void DirectoryExists_WithExistingDirectory_ReturnsTrue()
    {
        // Arrange
        var directory = BlobStorePath.New("test-bucket", "test-folder");
        var response = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>
            {
                new S3Object { Key = "test-folder/file.txt", Size = 100 }
            }
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(response);

        // Act
        var exists = _connector.DirectoryExists(directory);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void DirectoryExists_WithNonExistingDirectory_ReturnsFalse()
    {
        // Arrange
        var directory = BlobStorePath.New("test-bucket", "non-existing-folder");
        var response = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>()
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(response);

        // Act
        var exists = _connector.DirectoryExists(directory);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public void FileExists_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var file = BlobStorePath.New("test-bucket", "test-file.txt");
        var response = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>
            {
                new S3Object { Key = "test-file.txt.0", Size = 100 }
            }
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(response);

        // Act
        var exists = _connector.FileExists(file);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public void FileExists_WithNonExistingFile_ReturnsFalse()
    {
        // Arrange
        var file = BlobStorePath.New("test-bucket", "non-existing-file.txt");
        var response = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>()
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(response);

        // Act
        var exists = _connector.FileExists(file);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public void GetFileSize_WithExistingFile_ReturnsCorrectSize()
    {
        // Arrange
        var file = BlobStorePath.New("test-bucket", "test-file.txt");
        var response = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>
            {
                new S3Object { Key = "test-file.txt.0", Size = 100 },
                new S3Object { Key = "test-file.txt.1", Size = 200 }
            }
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(response);

        // Act
        var size = _connector.GetFileSize(file);

        // Assert
        Assert.Equal(300, size);
    }

    [Fact]
    public void GetFileSize_WithNonExistingFile_ReturnsZero()
    {
        // Arrange
        var file = BlobStorePath.New("test-bucket", "non-existing-file.txt");
        var response = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>()
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(response);

        // Act
        var size = _connector.GetFileSize(file);

        // Assert
        Assert.Equal(0, size);
    }

    [Fact]
    public void CreateDirectory_WithValidPath_ReturnsTrue()
    {
        // Arrange
        var directory = BlobStorePath.New("test-bucket", "new-folder");
        var response = new PutObjectResponse();

        _mockS3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(response);

        // Act
        var created = _connector.CreateDirectory(directory);

        // Assert
        Assert.True(created);
        _mockS3Client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
    }

    [Fact]
    public void CreateFile_WithValidPath_ReturnsTrue()
    {
        // Arrange
        var file = BlobStorePath.New("test-bucket", "new-file.txt");

        // Act
        var created = _connector.CreateFile(file);

        // Assert
        Assert.True(created);
    }

    [Fact]
    public void IsEmpty_WithEmptyDirectory_ReturnsTrue()
    {
        // Arrange
        var directory = BlobStorePath.New("test-bucket", "empty-folder");
        var response = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>()
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(response);

        // Act
        var isEmpty = _connector.IsEmpty(directory);

        // Assert
        Assert.True(isEmpty);
    }

    [Fact]
    public void IsEmpty_WithNonEmptyDirectory_ReturnsFalse()
    {
        // Arrange
        var directory = BlobStorePath.New("test-bucket", "non-empty-folder");
        var response = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>
            {
                new S3Object { Key = "non-empty-folder/file.txt.0", Size = 100 }
            }
        };

        _mockS3Client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(response);

        // Act
        var isEmpty = _connector.IsEmpty(directory);

        // Assert
        Assert.False(isEmpty);
    }

    [Fact]
    public void WriteData_WithValidData_WritesToS3()
    {
        // Arrange
        var file = BlobStorePath.New("test-bucket", "test-file.txt");
        var data = Encoding.UTF8.GetBytes("Hello, S3!");
        var sourceBuffers = new[] { data };
        var response = new PutObjectResponse();

        _mockS3Client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default))
            .ReturnsAsync(response);

        // Mock the ListObjectsV2 call for GetNextBlobNumber
        var listResponse = new ListObjectsV2Response
        {
            S3Objects = new List<S3Object>()
        };
        _mockS3Client.Setup(x => x.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), default))
            .ReturnsAsync(listResponse);

        // Act
        var bytesWritten = _connector.WriteData(file, sourceBuffers);

        // Assert
        Assert.Equal(data.Length, bytesWritten);
        _mockS3Client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), default), Times.Once);
    }

    public void Dispose()
    {
        _connector?.Dispose();
    }
}
