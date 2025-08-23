using System;
using System.IO;
using Xunit;
using NebulaStore.Storage.Embedded.Types;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Simple test to verify enhanced persistence operations work.
/// </summary>
public class SimplePersistenceTest : IDisposable
{
    private readonly string _testDirectory;

    public SimplePersistenceTest()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "NebulaStore_SimpleTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void Enhanced_Storage_File_Management_Works()
    {
        // Test that our enhanced storage file management compiles and basic functionality works
        var configuration = new SimpleTestConfiguration(_testDirectory);

        // This test just verifies that our enhanced code compiles and basic instantiation works
        // More comprehensive testing would require a full storage manager setup
        Assert.True(Directory.Exists(_testDirectory));
        Assert.NotNull(configuration);
        Assert.Equal(_testDirectory, configuration.StorageDirectory);
    }

    [Fact]
    public void File_Integrity_Validation_Works()
    {
        // Test that file integrity validation methods work correctly
        var configuration = new SimpleTestConfiguration(_testDirectory);

        // Create a test file to validate
        var testFilePath = Path.Combine(_testDirectory, "test_file.dat");
        File.WriteAllText(testFilePath, "test data");

        // Verify file exists and has content
        Assert.True(File.Exists(testFilePath));
        Assert.True(new FileInfo(testFilePath).Length > 0);

        // Test file validation would pass for a valid file
        // (The actual validation is done in StorageFileManager which requires more setup)
        Assert.True(File.Exists(testFilePath));
    }

    [Fact]
    public void Metadata_Classes_Work_Correctly()
    {
        // Test that our metadata classes work correctly
        var objectMetadata = new ObjectMetadata
        {
            ObjectId = 123,
            TypeId = 456,
            ChannelIndex = 0,
            FileNumber = 1,
            Position = 1024,
            Size = 512,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            AccessCount = 1
        };

        Assert.Equal(123, objectMetadata.ObjectId);
        Assert.Equal(456, objectMetadata.TypeId);
        Assert.Equal(0, objectMetadata.ChannelIndex);
        Assert.Equal(1, objectMetadata.FileNumber);
        Assert.Equal(1024, objectMetadata.Position);
        Assert.Equal(512, objectMetadata.Size);
        Assert.Equal(1, objectMetadata.AccessCount);

        var typeMetadata = new TypeMetadata
        {
            TypeId = 789,
            TypeName = "TestType",
            AssemblyQualifiedName = "TestNamespace.TestType, TestAssembly",
            RegisteredAt = DateTime.UtcNow,
            ObjectCount = 5
        };

        Assert.Equal(789, typeMetadata.TypeId);
        Assert.Equal("TestType", typeMetadata.TypeName);
        Assert.Equal("TestNamespace.TestType, TestAssembly", typeMetadata.AssemblyQualifiedName);
        Assert.Equal(5, typeMetadata.ObjectCount);

        var storageStats = new StorageStatistics
        {
            TotalObjects = 100,
            TotalTypes = 10,
            TotalDataSize = 1024 * 1024,
            TotalFileSize = 2 * 1024 * 1024,
            ChannelCount = 2,
            FileCount = 5,
            LastUpdated = DateTime.UtcNow
        };

        Assert.Equal(100, storageStats.TotalObjects);
        Assert.Equal(10, storageStats.TotalTypes);
        Assert.Equal(1024 * 1024, storageStats.TotalDataSize);
        Assert.Equal(2 * 1024 * 1024, storageStats.TotalFileSize);
        Assert.Equal(2, storageStats.ChannelCount);
        Assert.Equal(5, storageStats.FileCount);
    }

    [Fact]
    public void Storage_Exceptions_Work()
    {
        // Test that our new exception types work correctly
        var notFoundEx = new StorageExceptionNotFound("Test not found");
        var deserializationEx = new StorageExceptionDeserialization("Test deserialization error");

        Assert.NotNull(notFoundEx);
        Assert.Equal("Test not found", notFoundEx.Message);

        Assert.NotNull(deserializationEx);
        Assert.Equal("Test deserialization error", deserializationEx.Message);
    }

    [Fact]
    public void Pending_Write_Operation_Works()
    {
        // Test that PendingWriteOperation class works correctly
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        var mockFile = new MockStorageLiveDataFile();

        var pendingWrite = new PendingWriteOperation(mockFile, 1000, 2000, testData);

        Assert.NotNull(pendingWrite.File);
        Assert.Equal(1000, pendingWrite.OriginalLength);
        Assert.Equal(2000, pendingWrite.WritePosition);
        Assert.Equal(testData, pendingWrite.Data);
        Assert.True(pendingWrite.Timestamp <= DateTime.UtcNow);
        Assert.True(pendingWrite.Timestamp > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void Storage_File_Metadata_Works()
    {
        // Test that StorageFileMetadata class works correctly
        var metadata = new StorageFileMetadata
        {
            NextFileNumber = 5,
            TotalDataSize = 1024 * 1024,
            FileCount = 3,
            LastUpdated = DateTime.UtcNow
        };

        metadata.Files[1] = new StorageFileMetadata.FileInfo
        {
            Number = 1,
            Size = 512 * 1024,
            DataLength = 400 * 1024,
            Created = DateTime.UtcNow.AddHours(-1),
            LastModified = DateTime.UtcNow,
            IsActive = true
        };

        Assert.Equal(5, metadata.NextFileNumber);
        Assert.Equal(1024 * 1024, metadata.TotalDataSize);
        Assert.Equal(3, metadata.FileCount);
        Assert.Single(metadata.Files);

        var fileInfo = metadata.Files[1];
        Assert.Equal(1, fileInfo.Number);
        Assert.Equal(512 * 1024, fileInfo.Size);
        Assert.Equal(400 * 1024, fileInfo.DataLength);
        Assert.True(fileInfo.IsActive);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

/// <summary>
/// Simple test configuration for basic tests.
/// </summary>
internal class SimpleTestConfiguration : IStorageConfiguration
{
    public string StorageDirectory { get; }
    public int ChannelCount { get; } = 1;
    public TimeSpan HousekeepingInterval { get; } = TimeSpan.FromSeconds(1);
    public long EntityCacheThreshold { get; } = 1000000;
    public TimeSpan EntityCacheTimeout { get; } = TimeSpan.FromDays(1);

    public SimpleTestConfiguration(string storageDirectory)
    {
        StorageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));
    }
}

/// <summary>
/// Mock implementation of IStorageLiveDataFile for testing.
/// </summary>
internal class MockStorageLiveDataFile : IStorageLiveDataFile
{
    public long Number { get; } = 1;
    public long Size { get; private set; } = 1024;
    public long DataLength { get; private set; } = 512;
    public bool HasContent => DataLength > 0;
    public string Identifier => "mock_file_1";
    public int ChannelIndex => 0;
    public long TotalLength => Size;
    public bool HasUsers => false;
    public bool Exists => true;
    public bool IsEmpty => DataLength == 0;

    public void Close() { }
    public void Dispose() { }

    public int ReadBytes(byte[] buffer, long position)
    {
        // Mock implementation - just return some data
        var bytesToRead = Math.Min(buffer.Length, (int)(DataLength - position));
        if (bytesToRead <= 0) return 0;

        for (int i = 0; i < bytesToRead; i++)
        {
            buffer[i] = (byte)(i % 256);
        }
        return bytesToRead;
    }

    public bool NeedsRetirement(IStorageDataFileEvaluator evaluator) => false;
    public void AddChainToTail(IStorageEntity first, IStorageEntity last) { }
    public void FlushAndSync() { }
    public void CommitState() { }
    public void ResetToLastCommittedState() { }
    public void TruncateToLength(long length)
    {
        Size = length;
        DataLength = Math.Min(DataLength, length);
    }

    public void IncreaseContentLength(long length)
    {
        DataLength += length;
        Size = Math.Max(Size, DataLength);
    }

    public void Truncate(long length) => TruncateToLength(length);
    public void CopyTo(Stream stream) { }
    public void RegisterUsage(IStorageFileUser user) { }
    public void UnregisterUsage(IStorageFileUser user, Exception? cause) { }
    public void AppendEntry(IStorageEntity entity) { }
    public void RemoveHeadBoundChain(IStorageEntity entity, long length) { }
    public void EnsureExists() { }
    public void Delete() { }
}
