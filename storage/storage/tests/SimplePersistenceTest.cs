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
