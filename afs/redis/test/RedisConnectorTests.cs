using Xunit;
using StackExchange.Redis;
using NebulaStore.Afs.Redis;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Redis.Tests;

/// <summary>
/// Tests for RedisConnector.
/// Note: These tests require a running Redis server on localhost:6379
/// </summary>
public class RedisConnectorTests : IDisposable
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly RedisConnector? _connector;
    private readonly bool _redisAvailable;

    public RedisConnectorTests()
    {
        try
        {
            // Try to connect to Redis
            _redis = ConnectionMultiplexer.Connect("localhost:6379");
            _connector = RedisConnector.New(_redis, databaseNumber: 15); // Use DB 15 for tests
            _redisAvailable = true;

            // Clean up test database
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var db = _redis.GetDatabase(15);
            server.FlushDatabase(15);
        }
        catch
        {
            _redisAvailable = false;
        }
    }

    [Fact]
    public void Constructor_WithValidConnection_CreatesConnector()
    {
        if (!_redisAvailable)
        {
            // Skip test if Redis is not available
            return;
        }

        Assert.NotNull(_connector);
    }

    [Fact]
    public void CreateDirectory_CreatesVirtualDirectory()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var directory = BlobStorePath.New("test-container", "dir1");
        var result = _connector.CreateDirectory(directory);

        Assert.True(result);
        Assert.True(_connector.DirectoryExists(directory));
    }

    [Fact]
    public void CreateFile_CreatesVirtualFile()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var file = BlobStorePath.New("test-container", "dir1", "file1.dat");
        var result = _connector.CreateFile(file);

        Assert.True(result);
    }

    [Fact]
    public void WriteData_WritesDataToRedis()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var file = BlobStorePath.New("test-container", "data", "test.dat");
        var data = System.Text.Encoding.UTF8.GetBytes("Hello, Redis!");

        var bytesWritten = _connector.WriteData(file, new[] { data });

        Assert.Equal(data.Length, bytesWritten);
        Assert.True(_connector.FileExists(file));
    }

    [Fact]
    public void ReadData_ReadsDataFromRedis()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var file = BlobStorePath.New("test-container", "data", "test.dat");
        var originalData = System.Text.Encoding.UTF8.GetBytes("Hello, Redis!");

        _connector.WriteData(file, new[] { originalData });
        var readData = _connector.ReadData(file, 0, -1);

        Assert.Equal(originalData, readData);
    }

    [Fact]
    public void ReadData_WithOffset_ReadsPartialData()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var file = BlobStorePath.New("test-container", "data", "test.dat");
        var originalData = System.Text.Encoding.UTF8.GetBytes("Hello, Redis!");

        _connector.WriteData(file, new[] { originalData });
        var readData = _connector.ReadData(file, 7, 5); // Read "Redis"

        var expected = System.Text.Encoding.UTF8.GetBytes("Redis");
        Assert.Equal(expected, readData);
    }

    [Fact]
    public void GetFileSize_ReturnsCorrectSize()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var file = BlobStorePath.New("test-container", "data", "test.dat");
        var data = System.Text.Encoding.UTF8.GetBytes("Hello, Redis!");

        _connector.WriteData(file, new[] { data });
        var size = _connector.GetFileSize(file);

        Assert.Equal(data.Length, size);
    }

    [Fact]
    public void FileExists_ReturnsTrueForExistingFile()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var file = BlobStorePath.New("test-container", "data", "test.dat");
        var data = System.Text.Encoding.UTF8.GetBytes("Test");

        _connector.WriteData(file, new[] { data });

        Assert.True(_connector.FileExists(file));
    }

    [Fact]
    public void FileExists_ReturnsFalseForNonExistingFile()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var file = BlobStorePath.New("test-container", "data", "nonexistent.dat");

        Assert.False(_connector.FileExists(file));
    }

    [Fact]
    public void DeleteFile_DeletesFile()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var file = BlobStorePath.New("test-container", "data", "test.dat");
        var data = System.Text.Encoding.UTF8.GetBytes("Test");

        _connector.WriteData(file, new[] { data });
        Assert.True(_connector.FileExists(file));

        var deleted = _connector.DeleteFile(file);

        Assert.True(deleted);
        Assert.False(_connector.FileExists(file));
    }

    [Fact]
    public void DeleteFile_ReturnsFalseForNonExistingFile()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var file = BlobStorePath.New("test-container", "data", "nonexistent.dat");

        var deleted = _connector.DeleteFile(file);

        Assert.False(deleted);
    }

    [Fact]
    public void WriteData_MultipleBuffers_CombinesData()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var file = BlobStorePath.New("test-container", "data", "multi.dat");
        var buffer1 = System.Text.Encoding.UTF8.GetBytes("Hello, ");
        var buffer2 = System.Text.Encoding.UTF8.GetBytes("Redis!");

        var bytesWritten = _connector.WriteData(file, new[] { buffer1, buffer2 });

        Assert.Equal(buffer1.Length + buffer2.Length, bytesWritten);

        var readData = _connector.ReadData(file, 0, -1);
        var expected = System.Text.Encoding.UTF8.GetBytes("Hello, Redis!");
        Assert.Equal(expected, readData);
    }

    [Fact]
    public void CopyFile_CopiesFileData()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var sourceFile = BlobStorePath.New("test-container", "data", "source.dat");
        var targetFile = BlobStorePath.New("test-container", "data", "target.dat");
        var data = System.Text.Encoding.UTF8.GetBytes("Copy me!");

        _connector.WriteData(sourceFile, new[] { data });
        var bytesCopied = _connector.CopyFile(sourceFile, targetFile, 0, -1);

        Assert.Equal(data.Length, bytesCopied);
        Assert.True(_connector.FileExists(targetFile));

        var copiedData = _connector.ReadData(targetFile, 0, -1);
        Assert.Equal(data, copiedData);
    }

    [Fact]
    public void MoveFile_MovesFileData()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var sourceFile = BlobStorePath.New("test-container", "data", "source.dat");
        var targetFile = BlobStorePath.New("test-container", "data", "target.dat");
        var data = System.Text.Encoding.UTF8.GetBytes("Move me!");

        _connector.WriteData(sourceFile, new[] { data });
        _connector.MoveFile(sourceFile, targetFile);

        Assert.False(_connector.FileExists(sourceFile));
        Assert.True(_connector.FileExists(targetFile));

        var movedData = _connector.ReadData(targetFile, 0, -1);
        Assert.Equal(data, movedData);
    }

    [Fact]
    public void TruncateFile_TruncatesFileToZero()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var file = BlobStorePath.New("test-container", "data", "truncate.dat");
        var data = System.Text.Encoding.UTF8.GetBytes("Truncate me!");

        _connector.WriteData(file, new[] { data });
        _connector.TruncateFile(file, 0);

        Assert.False(_connector.FileExists(file));
    }

    [Fact]
    public void IsEmpty_ReturnsTrueForEmptyDirectory()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var directory = BlobStorePath.New("test-container", "empty-dir");

        Assert.True(_connector.IsEmpty(directory));
    }

    [Fact]
    public void IsEmpty_ReturnsFalseForNonEmptyDirectory()
    {
        if (!_redisAvailable || _connector == null)
            return;

        var directory = BlobStorePath.New("test-container", "non-empty-dir");
        var file = BlobStorePath.New("test-container", "non-empty-dir", "file.dat");
        var data = System.Text.Encoding.UTF8.GetBytes("Data");

        _connector.WriteData(file, new[] { data });

        Assert.False(_connector.IsEmpty(directory));
    }

    public void Dispose()
    {
        if (_redisAvailable && _redis != null)
        {
            // Clean up test database
            try
            {
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                server.FlushDatabase(15);
            }
            catch
            {
                // Ignore cleanup errors
            }

            _connector?.Dispose();
            _redis?.Dispose();
        }
    }
}

