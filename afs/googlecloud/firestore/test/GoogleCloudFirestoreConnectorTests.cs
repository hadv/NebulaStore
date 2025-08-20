using System;
using System.IO;
using System.Text;
using FluentAssertions;
using Google.Cloud.Firestore;
using NebulaStore.Afs.Blobstore;
using Xunit;

namespace NebulaStore.Afs.GoogleCloud.Firestore.Tests;

/// <summary>
/// Integration tests for GoogleCloudFirestoreConnector.
/// These tests require a Firestore emulator or actual Firestore instance.
/// </summary>
public class GoogleCloudFirestoreConnectorTests : IDisposable
{
    private readonly FirestoreDb _firestore;
    private readonly GoogleCloudFirestoreConnector _connector;
    private readonly string _testCollection;

    public GoogleCloudFirestoreConnectorTests()
    {
        // Use Firestore emulator for testing
        // Start emulator with: gcloud beta emulators firestore start --host-port=localhost:8080
        Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", "localhost:8080");
        
        _testCollection = $"test-{Guid.NewGuid():N}";
        _firestore = FirestoreDb.Create("test-project");
        _connector = GoogleCloudFirestoreConnector.New(_firestore);
    }

    [Fact]
    public void Constructor_WithValidFirestore_ShouldCreateConnector()
    {
        // Arrange & Act
        using var connector = GoogleCloudFirestoreConnector.New(_firestore);

        // Assert
        connector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCaching_ShouldCreateConnector()
    {
        // Arrange & Act
        using var connector = GoogleCloudFirestoreConnector.Caching(_firestore);

        // Assert
        connector.Should().NotBeNull();
    }

    [Fact]
    public void FileExists_WithNonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        var path = BlobStorePath.New(_testCollection, "nonexistent.txt");

        // Act
        var exists = _connector.FileExists(path);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void WriteData_WithSmallFile_ShouldCreateFile()
    {
        // Arrange
        var path = BlobStorePath.New(_testCollection, "test.txt");
        var data = Encoding.UTF8.GetBytes("Hello, Firestore!");

        // Act
        var bytesWritten = _connector.WriteData(path, new[] { data });

        // Assert
        bytesWritten.Should().Be(data.Length);
        _connector.FileExists(path).Should().BeTrue();
        _connector.GetFileSize(path).Should().Be(data.Length);
    }

    [Fact]
    public void ReadData_WithExistingFile_ShouldReturnCorrectData()
    {
        // Arrange
        var path = BlobStorePath.New(_testCollection, "test.txt");
        var originalData = Encoding.UTF8.GetBytes("Hello, Firestore!");
        _connector.WriteData(path, new[] { originalData });

        // Act
        var readData = _connector.ReadData(path, 0, -1);

        // Assert
        readData.Should().BeEquivalentTo(originalData);
    }

    [Fact]
    public void ReadData_WithOffset_ShouldReturnPartialData()
    {
        // Arrange
        var path = BlobStorePath.New(_testCollection, "test.txt");
        var originalData = Encoding.UTF8.GetBytes("Hello, Firestore!");
        _connector.WriteData(path, new[] { originalData });

        // Act
        var readData = _connector.ReadData(path, 7, 9); // "Firestore"

        // Assert
        var expectedData = Encoding.UTF8.GetBytes("Firestore");
        readData.Should().BeEquivalentTo(expectedData);
    }

    [Fact]
    public void WriteData_WithLargeFile_ShouldSplitIntoMultipleBlobs()
    {
        // Arrange
        var path = BlobStorePath.New(_testCollection, "large.txt");
        var largeData = new byte[2_000_000]; // 2MB, should split into 2 blobs
        new Random().NextBytes(largeData);

        // Act
        var bytesWritten = _connector.WriteData(path, new[] { largeData });

        // Assert
        bytesWritten.Should().Be(largeData.Length);
        _connector.FileExists(path).Should().BeTrue();
        _connector.GetFileSize(path).Should().Be(largeData.Length);

        var readData = _connector.ReadData(path, 0, -1);
        readData.Should().BeEquivalentTo(largeData);
    }

    [Fact]
    public void DeleteFile_WithExistingFile_ShouldRemoveFile()
    {
        // Arrange
        var path = BlobStorePath.New(_testCollection, "test.txt");
        var data = Encoding.UTF8.GetBytes("Hello, Firestore!");
        _connector.WriteData(path, new[] { data });

        // Act
        var deleted = _connector.DeleteFile(path);

        // Assert
        deleted.Should().BeTrue();
        _connector.FileExists(path).Should().BeFalse();
    }

    [Fact]
    public void DeleteFile_WithNonExistentFile_ShouldReturnFalse()
    {
        // Arrange
        var path = BlobStorePath.New(_testCollection, "nonexistent.txt");

        // Act
        var deleted = _connector.DeleteFile(path);

        // Assert
        deleted.Should().BeFalse();
    }

    [Fact]
    public void DirectoryExists_WithEmptyDirectory_ShouldReturnFalse()
    {
        // Arrange
        var path = BlobStorePath.New(_testCollection, "empty");

        // Act
        var exists = _connector.DirectoryExists(path);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void DirectoryExists_WithFilesInDirectory_ShouldReturnTrue()
    {
        // Arrange
        var dirPath = BlobStorePath.New(_testCollection, "folder");
        var filePath = BlobStorePath.New(_testCollection, "folder", "file.txt");
        var data = Encoding.UTF8.GetBytes("test");
        _connector.WriteData(filePath, new[] { data });

        // Act
        var exists = _connector.DirectoryExists(dirPath);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_WithEmptyDirectory_ShouldReturnTrue()
    {
        // Arrange
        var path = BlobStorePath.New(_testCollection, "empty");

        // Act
        var isEmpty = _connector.IsEmpty(path);

        // Assert
        isEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_WithFilesInDirectory_ShouldReturnFalse()
    {
        // Arrange
        var dirPath = BlobStorePath.New(_testCollection, "folder");
        var filePath = BlobStorePath.New(_testCollection, "folder", "file.txt");
        var data = Encoding.UTF8.GetBytes("test");
        _connector.WriteData(filePath, new[] { data });

        // Act
        var isEmpty = _connector.IsEmpty(dirPath);

        // Assert
        isEmpty.Should().BeFalse();
    }

    [Fact]
    public void MoveFile_WithExistingFile_ShouldMoveFile()
    {
        // Arrange
        var sourcePath = BlobStorePath.New(_testCollection, "source.txt");
        var targetPath = BlobStorePath.New(_testCollection, "target.txt");
        var data = Encoding.UTF8.GetBytes("Hello, Firestore!");
        _connector.WriteData(sourcePath, new[] { data });

        // Act
        _connector.MoveFile(sourcePath, targetPath);

        // Assert
        _connector.FileExists(sourcePath).Should().BeFalse();
        _connector.FileExists(targetPath).Should().BeTrue();
        var readData = _connector.ReadData(targetPath, 0, -1);
        readData.Should().BeEquivalentTo(data);
    }

    [Fact]
    public void CopyFile_WithExistingFile_ShouldCopyFile()
    {
        // Arrange
        var sourcePath = BlobStorePath.New(_testCollection, "source.txt");
        var targetPath = BlobStorePath.New(_testCollection, "target.txt");
        var data = Encoding.UTF8.GetBytes("Hello, Firestore!");
        _connector.WriteData(sourcePath, new[] { data });

        // Act
        var bytesCopied = _connector.CopyFile(sourcePath, targetPath, 0, -1);

        // Assert
        bytesCopied.Should().Be(data.Length);
        _connector.FileExists(sourcePath).Should().BeTrue();
        _connector.FileExists(targetPath).Should().BeTrue();
        var readData = _connector.ReadData(targetPath, 0, -1);
        readData.Should().BeEquivalentTo(data);
    }

    [Fact]
    public void TruncateFile_WithExistingFile_ShouldTruncateFile()
    {
        // Arrange
        var path = BlobStorePath.New(_testCollection, "test.txt");
        var data = Encoding.UTF8.GetBytes("Hello, Firestore!");
        _connector.WriteData(path, new[] { data });

        // Act
        _connector.TruncateFile(path, 5); // Keep only "Hello"

        // Assert
        _connector.GetFileSize(path).Should().Be(5);
        var readData = _connector.ReadData(path, 0, -1);
        var expectedData = Encoding.UTF8.GetBytes("Hello");
        readData.Should().BeEquivalentTo(expectedData);
    }

    [Theory]
    [InlineData("/")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("__test__")]
    public void Constructor_WithInvalidCollectionName_ShouldThrowException(string invalidName)
    {
        // Arrange
        var path = BlobStorePath.New(invalidName, "file.txt");
        var data = Encoding.UTF8.GetBytes("test");

        // Act & Assert
        var action = () => _connector.WriteData(path, new[] { data });
        action.Should().Throw<ArgumentException>();
    }

    public void Dispose()
    {
        try
        {
            // Clean up test data
            var collection = _firestore.Collection(_testCollection);
            var documents = collection.GetSnapshotAsync().Result;
            
            var batch = _firestore.StartBatch();
            foreach (var doc in documents.Documents)
            {
                batch.Delete(doc.Reference);
            }
            
            if (documents.Documents.Count > 0)
            {
                batch.CommitAsync().Wait();
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
        finally
        {
            _connector?.Dispose();
        }
    }
}
