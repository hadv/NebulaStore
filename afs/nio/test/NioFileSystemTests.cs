using System;
using System.IO;
using System.Text;
using Xunit;
using NebulaStore.Afs.Nio;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Nio.Tests;

public class NioFileSystemTests : IDisposable
{
    private readonly string _testDirectory;

    public NioFileSystemTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"nio-test-{Guid.NewGuid()}");
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
    public void New_CreatesFileSystemWithDefaultProtocol()
    {
        // Arrange & Act
        using var fileSystem = NioFileSystem.New();

        // Assert
        Assert.NotNull(fileSystem);
        Assert.Equal(NioFileSystem.DefaultFileProtocol, fileSystem.DefaultProtocol);
        Assert.NotNull(fileSystem.IoHandler);
    }

    [Fact]
    public void New_CreatesFileSystemWithCustomProtocol()
    {
        // Arrange
        var customProtocol = "custom:///";

        // Act
        using var fileSystem = NioFileSystem.New(customProtocol);

        // Assert
        Assert.Equal(customProtocol, fileSystem.DefaultProtocol);
    }

    [Fact]
    public void ResolvePath_HandlesHomeDirectory()
    {
        // Arrange
        using var fileSystem = NioFileSystem.New();
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Act
        var elements = fileSystem.ResolvePath("~/Documents/test");

        // Assert
        Assert.NotEmpty(elements);
        Assert.Contains("Documents", elements);
        Assert.Contains("test", elements);
    }

    [Fact]
    public void ResolvePath_HandlesRegularPath()
    {
        // Arrange
        using var fileSystem = NioFileSystem.New();

        // Act
        var elements = fileSystem.ResolvePath("/var/data/files");

        // Assert
        Assert.Contains("var", elements);
        Assert.Contains("data", elements);
        Assert.Contains("files", elements);
    }

    [Fact]
    public void WrapForReading_CreatesReadableFile()
    {
        // Arrange
        using var fileSystem = NioFileSystem.New();
        var path = BlobStorePath.New("container", "test.txt");

        // Act
        using var readableFile = fileSystem.WrapForReading(path);

        // Assert
        Assert.NotNull(readableFile);
        Assert.Equal(path, readableFile.BlobPath);
    }

    [Fact]
    public void WrapForWriting_CreatesWritableFile()
    {
        // Arrange
        using var fileSystem = NioFileSystem.New();
        var path = BlobStorePath.New("container", "test.txt");

        // Act
        using var writableFile = fileSystem.WrapForWriting(path);

        // Assert
        Assert.NotNull(writableFile);
        Assert.Equal(path, writableFile.BlobPath);
    }

    [Fact]
    public void ConvertToWriting_ConvertsReadableToWritable()
    {
        // Arrange
        using var fileSystem = NioFileSystem.New();
        var path = BlobStorePath.New("container", "test.txt");
        using var readableFile = fileSystem.WrapForReading(path);

        // Act
        using var writableFile = fileSystem.ConvertToWriting(readableFile);

        // Assert
        Assert.NotNull(writableFile);
        Assert.Equal(path, writableFile.BlobPath);
    }

    [Fact]
    public void ConvertToReading_ConvertsWritableToReadable()
    {
        // Arrange
        using var fileSystem = NioFileSystem.New();
        var path = BlobStorePath.New("container", "test.txt");
        using var writableFile = fileSystem.WrapForWriting(path);

        // Act
        using var readableFile = fileSystem.ConvertToReading(writableFile);

        // Assert
        Assert.NotNull(readableFile);
        Assert.Equal(path, readableFile.BlobPath);
    }

    [Fact]
    public void WriteAndReadBytes_WorksCorrectly()
    {
        // Arrange
        using var connector = NioConnector.New(_testDirectory);
        var path = BlobStorePath.New("container", "test.txt");
        var testData = Encoding.UTF8.GetBytes("Hello, NIO!");

        // Act - Write
        connector.WriteData(path, testData);

        // Act - Read
        var readData = connector.ReadData(path, 0, -1);

        // Assert
        Assert.Equal(testData, readData);
        Assert.True(connector.FileExists(path));
    }
}

