using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using NebulaStore.Afs.Nio;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Nio.Tests;

public class NioConnectorTests : IDisposable
{
    private readonly string _testDirectory;

    public NioConnectorTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"nio-connector-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public void New_CreatesConnectorWithBaseDirectory()
    {
        // Arrange & Act
        using var connector = NioConnector.New(_testDirectory);

        // Assert
        Assert.NotNull(connector);
    }

    [Fact]
    public void New_ThrowsOnNullBaseDirectory()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentException>(() => NioConnector.New(null!));
    }

    [Fact]
    public void CreateDirectory_CreatesDirectory()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var path = BlobStorePath.New("container", "subfolder");

        // Act
        connector.CreateDirectory(path);

        // Assert
        Assert.True(connector.DirectoryExists(path));
    }

    [Fact]
    public void CreateFile_CreatesEmptyFile()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var path = BlobStorePath.New("container", "test.txt");

        // Act
        connector.CreateFile(path);

        // Assert
        Assert.True(connector.FileExists(path));
        Assert.Equal(0, connector.GetFileSize(path));
    }

    [Fact]
    public void WriteData_WritesDataToFile()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var path = BlobStorePath.New("container", "test.txt");
        var testData = Encoding.UTF8.GetBytes("Hello, NIO Connector!");

        // Act
        connector.WriteData(path, testData);

        // Assert
        Assert.True(connector.FileExists(path));
        Assert.Equal(testData.Length, connector.GetFileSize(path));
    }

    [Fact]
    public void ReadData_ReadsDataFromFile()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var path = BlobStorePath.New("container", "test.txt");
        var testData = Encoding.UTF8.GetBytes("Hello, NIO Connector!");
        connector.WriteData(path, testData);

        // Act
        var readData = connector.ReadData(path, 0, -1);

        // Assert
        Assert.Equal(testData, readData);
    }

    [Fact]
    public void ReadData_ReadsPartialData()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var path = BlobStorePath.New("container", "test.txt");
        var testData = Encoding.UTF8.GetBytes("Hello, NIO Connector!");
        connector.WriteData(path, testData);

        // Act
        var readData = connector.ReadData(path, 0, 5);

        // Assert
        Assert.Equal(5, readData.Length);
        Assert.Equal("Hello", Encoding.UTF8.GetString(readData));
    }

    [Fact]
    public void DeleteFile_DeletesExistingFile()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var path = BlobStorePath.New("container", "test.txt");
        connector.CreateFile(path);

        // Act
        var deleted = connector.DeleteFile(path);

        // Assert
        Assert.True(deleted);
        Assert.False(connector.FileExists(path));
    }

    [Fact]
    public void DeleteFile_ReturnsFalseForNonExistentFile()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var path = BlobStorePath.New("container", "nonexistent.txt");

        // Act
        var deleted = connector.DeleteFile(path);

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public void ListFiles_ReturnsFileNames()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var dirPath = BlobStorePath.New("container");
        connector.CreateFile(BlobStorePath.New("container", "file1.txt"));
        connector.CreateFile(BlobStorePath.New("container", "file2.txt"));

        // Act
        var files = connector.ListFiles(dirPath).ToList();

        // Assert
        Assert.Equal(2, files.Count);
        Assert.Contains("file1.txt", files);
        Assert.Contains("file2.txt", files);
    }

    [Fact]
    public void ListDirectories_ReturnsDirectoryNames()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var dirPath = BlobStorePath.New("container");
        connector.CreateDirectory(BlobStorePath.New("container", "dir1"));
        connector.CreateDirectory(BlobStorePath.New("container", "dir2"));

        // Act
        var directories = connector.ListDirectories(dirPath).ToList();

        // Assert
        Assert.Equal(2, directories.Count);
        Assert.Contains("dir1", directories);
        Assert.Contains("dir2", directories);
    }

    [Fact]
    public void MoveFile_MovesFileToNewLocation()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var sourcePath = BlobStorePath.New("container", "source.txt");
        var targetPath = BlobStorePath.New("container", "target.txt");
        var testData = Encoding.UTF8.GetBytes("Test data");
        connector.WriteData(sourcePath, testData);

        // Act
        connector.MoveFile(sourcePath, targetPath);

        // Assert
        Assert.False(connector.FileExists(sourcePath));
        Assert.True(connector.FileExists(targetPath));
        var readData = connector.ReadData(targetPath, 0, -1);
        Assert.Equal(testData, readData);
    }

    [Fact]
    public void CopyFile_CopiesFileToNewLocation()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var sourcePath = BlobStorePath.New("container", "source.txt");
        var targetPath = BlobStorePath.New("container", "target.txt");
        var testData = Encoding.UTF8.GetBytes("Test data");
        connector.WriteData(sourcePath, testData);

        // Act
        connector.CopyFile(sourcePath, targetPath);

        // Assert
        Assert.True(connector.FileExists(sourcePath));
        Assert.True(connector.FileExists(targetPath));
        var sourceData = connector.ReadData(sourcePath, 0, -1);
        var targetData = connector.ReadData(targetPath, 0, -1);
        Assert.Equal(sourceData, targetData);
    }

    [Fact]
    public void WriteData_WithMultipleChunks_WritesAllChunks()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var path = BlobStorePath.New("container", "test.txt");
        var chunk1 = Encoding.UTF8.GetBytes("Hello, ");
        var chunk2 = Encoding.UTF8.GetBytes("NIO ");
        var chunk3 = Encoding.UTF8.GetBytes("Connector!");

        // Act
        connector.WriteData(path, new[] { chunk1, chunk2, chunk3 });

        // Assert
        var readData = connector.ReadData(path, 0, -1);
        var content = Encoding.UTF8.GetString(readData);
        Assert.Equal("Hello, NIO Connector!", content);
    }
}

