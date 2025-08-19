using System;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using NebulaStore.Afs.Blobstore;
using Xunit;

namespace NebulaStore.Afs.Blobstore.Tests;

/// <summary>
/// Tests for LocalBlobStoreConnector functionality.
/// </summary>
public class LocalBlobStoreConnectorTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly LocalBlobStoreConnector _connector;

    public LocalBlobStoreConnectorTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "NebulaStore.Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _connector = new LocalBlobStoreConnector(_testDirectory, useCache: false);
    }

    [Fact]
    public void Constructor_WithValidPath_ShouldCreateConnector()
    {
        // Arrange & Act
        using var connector = new LocalBlobStoreConnector(_testDirectory);

        // Assert
        Directory.Exists(_testDirectory).Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullPath_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new LocalBlobStoreConnector(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DirectoryExists_WithExistingDirectory_ShouldReturnTrue()
    {
        // Arrange
        var path = BlobStorePath.New("container", "folder");
        _connector.CreateDirectory(path);

        // Act
        var exists = _connector.DirectoryExists(path);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void DirectoryExists_WithNonExistingDirectory_ShouldReturnFalse()
    {
        // Arrange
        var path = BlobStorePath.New("container", "nonexistent");

        // Act
        var exists = _connector.DirectoryExists(path);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void CreateDirectory_WithValidPath_ShouldCreateDirectory()
    {
        // Arrange
        var path = BlobStorePath.New("container", "newfolder");

        // Act
        var result = _connector.CreateDirectory(path);

        // Assert
        result.Should().BeTrue();
        _connector.DirectoryExists(path).Should().BeTrue();
    }

    [Fact]
    public void FileExists_WithNonExistingFile_ShouldReturnFalse()
    {
        // Arrange
        var path = BlobStorePath.New("container", "nonexistent.txt");

        // Act
        var exists = _connector.FileExists(path);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void WriteData_WithValidData_ShouldCreateFile()
    {
        // Arrange
        var path = BlobStorePath.New("container", "testfile.txt");
        var data = Encoding.UTF8.GetBytes("Hello, World!");

        // Act
        var bytesWritten = _connector.WriteData(path, new[] { data });

        // Assert
        bytesWritten.Should().Be(data.Length);
        _connector.FileExists(path).Should().BeTrue();
        _connector.GetFileSize(path).Should().Be(data.Length);
    }

    [Fact]
    public void ReadData_WithExistingFile_ShouldReturnData()
    {
        // Arrange
        var path = BlobStorePath.New("container", "testfile.txt");
        var originalData = Encoding.UTF8.GetBytes("Hello, World!");
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
        var path = BlobStorePath.New("container", "testfile.txt");
        var originalData = Encoding.UTF8.GetBytes("Hello, World!");
        _connector.WriteData(path, new[] { originalData });

        // Act
        var readData = _connector.ReadData(path, 7, 5); // "World"

        // Assert
        var expectedData = Encoding.UTF8.GetBytes("World");
        readData.Should().BeEquivalentTo(expectedData);
    }

    [Fact]
    public void DeleteFile_WithExistingFile_ShouldDeleteFile()
    {
        // Arrange
        var path = BlobStorePath.New("container", "testfile.txt");
        var data = Encoding.UTF8.GetBytes("Hello, World!");
        _connector.WriteData(path, new[] { data });

        // Act
        var result = _connector.DeleteFile(path);

        // Assert
        result.Should().BeTrue();
        _connector.FileExists(path).Should().BeFalse();
    }

    [Fact]
    public void DeleteFile_WithNonExistingFile_ShouldReturnFalse()
    {
        // Arrange
        var path = BlobStorePath.New("container", "nonexistent.txt");

        // Act
        var result = _connector.DeleteFile(path);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetFileSize_WithExistingFile_ShouldReturnCorrectSize()
    {
        // Arrange
        var path = BlobStorePath.New("container", "testfile.txt");
        var data = Encoding.UTF8.GetBytes("Hello, World!");
        _connector.WriteData(path, new[] { data });

        // Act
        var size = _connector.GetFileSize(path);

        // Assert
        size.Should().Be(data.Length);
    }

    [Fact]
    public void GetFileSize_WithNonExistingFile_ShouldReturnZero()
    {
        // Arrange
        var path = BlobStorePath.New("container", "nonexistent.txt");

        // Act
        var size = _connector.GetFileSize(path);

        // Assert
        size.Should().Be(0);
    }

    [Fact]
    public void MoveFile_WithExistingFile_ShouldMoveFile()
    {
        // Arrange
        var sourcePath = BlobStorePath.New("container", "source.txt");
        var targetPath = BlobStorePath.New("container", "target.txt");
        var data = Encoding.UTF8.GetBytes("Hello, World!");
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
        var sourcePath = BlobStorePath.New("container", "source.txt");
        var targetPath = BlobStorePath.New("container", "target.txt");
        var data = Encoding.UTF8.GetBytes("Hello, World!");
        _connector.WriteData(sourcePath, new[] { data });

        // Act
        var bytesCopied = _connector.CopyFile(sourcePath, targetPath, 0, -1);

        // Assert
        bytesCopied.Should().Be(data.Length);
        _connector.FileExists(sourcePath).Should().BeTrue();
        _connector.FileExists(targetPath).Should().BeTrue();
        
        var sourceData = _connector.ReadData(sourcePath, 0, -1);
        var targetData = _connector.ReadData(targetPath, 0, -1);
        sourceData.Should().BeEquivalentTo(data);
        targetData.Should().BeEquivalentTo(data);
    }

    [Fact]
    public void TruncateFile_WithExistingFile_ShouldTruncateFile()
    {
        // Arrange
        var path = BlobStorePath.New("container", "testfile.txt");
        var data = Encoding.UTF8.GetBytes("Hello, World!");
        _connector.WriteData(path, new[] { data });

        // Act
        _connector.TruncateFile(path, 5); // Keep only "Hello"

        // Assert
        _connector.GetFileSize(path).Should().Be(5);
        var readData = _connector.ReadData(path, 0, -1);
        var expectedData = Encoding.UTF8.GetBytes("Hello");
        readData.Should().BeEquivalentTo(expectedData);
    }

    [Fact]
    public void TruncateFile_ToZero_ShouldDeleteFile()
    {
        // Arrange
        var path = BlobStorePath.New("container", "testfile.txt");
        var data = Encoding.UTF8.GetBytes("Hello, World!");
        _connector.WriteData(path, new[] { data });

        // Act
        _connector.TruncateFile(path, 0);

        // Assert
        _connector.FileExists(path).Should().BeFalse();
    }

    [Fact]
    public void IsEmpty_WithEmptyDirectory_ShouldReturnTrue()
    {
        // Arrange
        var path = BlobStorePath.New("container", "emptyfolder");
        _connector.CreateDirectory(path);

        // Act
        var isEmpty = _connector.IsEmpty(path);

        // Assert
        isEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_WithNonEmptyDirectory_ShouldReturnFalse()
    {
        // Arrange
        var dirPath = BlobStorePath.New("container", "folder");
        var filePath = BlobStorePath.New("container", "folder", "file.txt");
        _connector.CreateDirectory(dirPath);
        _connector.WriteData(filePath, new[] { Encoding.UTF8.GetBytes("test") });

        // Act
        var isEmpty = _connector.IsEmpty(dirPath);

        // Assert
        isEmpty.Should().BeFalse();
    }

    [Fact]
    public void VisitChildren_WithFilesAndDirectories_ShouldVisitAll()
    {
        // Arrange
        var rootPath = BlobStorePath.New("container");
        var subDirPath = BlobStorePath.New("container", "subfolder");
        var filePath = BlobStorePath.New("container", "file.txt");
        
        _connector.CreateDirectory(subDirPath);
        _connector.WriteData(filePath, new[] { Encoding.UTF8.GetBytes("test") });

        var visitedDirectories = new List<string>();
        var visitedFiles = new List<string>();
        
        var visitor = new TestPathVisitor(visitedDirectories, visitedFiles);

        // Act
        _connector.VisitChildren(rootPath, visitor);

        // Assert
        visitedDirectories.Should().Contain("subfolder");
        visitedFiles.Should().Contain("file.txt");
    }

    public void Dispose()
    {
        _connector?.Dispose();
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private class TestPathVisitor : IBlobStorePathVisitor
    {
        private readonly List<string> _directories;
        private readonly List<string> _files;

        public TestPathVisitor(List<string> directories, List<string> files)
        {
            _directories = directories;
            _files = files;
        }

        public void VisitDirectory(BlobStorePath parent, string directoryName)
        {
            _directories.Add(directoryName);
        }

        public void VisitFile(BlobStorePath parent, string fileName)
        {
            _files.Add(fileName);
        }
    }
}
