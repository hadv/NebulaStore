using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.Storage.Embedded.Types.Transactions;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Tests for the Eclipse Store-style data integrity system.
/// </summary>
public class DataIntegrityTests : IDisposable
{
    private readonly string _testDirectory;

    public DataIntegrityTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"NebulaStore_DataIntegrity_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task DataIntegrityManager_NoFiles_ShouldReturnIntactStatus()
    {
        // Arrange
        var integrityManager = new DataIntegrityManager(_testDirectory);

        // Act
        var result = await integrityManager.PerformIntegrityCheckAsync();

        // Assert
        Assert.Equal(IntegrityStatus.Intact, result.Status);
        Assert.Equal(0, result.TotalFilesChecked);
        Assert.Empty(result.CorruptedFiles);
    }

    [Fact]
    public async Task DataIntegrityManager_ValidDataFile_ShouldPassIntegrityCheck()
    {
        // Arrange
        var integrityManager = new DataIntegrityManager(_testDirectory);

        // Create a test data file
        var dataFilePath = Path.Combine(_testDirectory, "channel_000_data_0000000001.dat");
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(dataFilePath, testData);

        // Act
        var result = await integrityManager.PerformIntegrityCheckAsync();

        // Assert
        Assert.Equal(IntegrityStatus.Intact, result.Status);
        Assert.Equal(1, result.TotalFilesChecked);
        Assert.Empty(result.CorruptedFiles);
        Assert.Single(result.FileResults);
        Assert.True(result.FileResults[0].IsValid);
    }

    [Fact]
    public async Task DataIntegrityManager_GenerateChecksums_ShouldCreateChecksumFiles()
    {
        // Arrange
        var integrityManager = new DataIntegrityManager(_testDirectory);

        // Create test files
        var dataFilePath = Path.Combine(_testDirectory, "channel_000_data_0000000001.dat");
        var logFilePath = Path.Combine(_testDirectory, "channel_000_transactions.log");
        await File.WriteAllBytesAsync(dataFilePath, new byte[] { 1, 2, 3 });
        await File.WriteAllBytesAsync(logFilePath, new byte[] { 4, 5, 6 });

        // Act
        var checksumCount = await integrityManager.GenerateChecksumsAsync();

        // Assert
        Assert.Equal(2, checksumCount);

        // Verify checksum files were created
        var checksumDir = Path.Combine(_testDirectory, ".integrity");
        Assert.True(Directory.Exists(checksumDir));
        Assert.True(File.Exists(Path.Combine(checksumDir, "channel_000_data_0000000001.dat.sha256")));
        Assert.True(File.Exists(Path.Combine(checksumDir, "channel_000_transactions.log.sha256")));
    }

    [Fact]
    public async Task DataIntegrityManager_ValidateFile_ShouldReturnTrueForValidFile()
    {
        // Arrange
        var integrityManager = new DataIntegrityManager(_testDirectory);

        // Create a test file
        var testFilePath = Path.Combine(_testDirectory, "channel_000_data_0000000001.dat");
        var testData = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(testFilePath, testData);

        // Act
        var isValid = await integrityManager.ValidateFileAsync(testFilePath);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task DataIntegrityManager_CorruptedFile_ShouldDetectCorruption()
    {
        // Arrange
        var integrityManager = new DataIntegrityManager(_testDirectory);

        // Create a test file and generate checksum
        var testFilePath = Path.Combine(_testDirectory, "channel_000_data_0000000001.dat");
        var originalData = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(testFilePath, originalData);

        // Generate initial checksum
        await integrityManager.GenerateChecksumsAsync();

        // Corrupt the file
        var corruptedData = new byte[] { 1, 2, 3, 4, 6 }; // Changed last byte
        await File.WriteAllBytesAsync(testFilePath, corruptedData);

        // Act
        var result = await integrityManager.PerformIntegrityCheckAsync();

        // Assert
        Assert.Equal(IntegrityStatus.PartiallyCorrupted, result.Status);
        Assert.Single(result.CorruptedFiles);
        Assert.Contains(testFilePath, result.CorruptedFiles);
        Assert.Single(result.FileResults);
        Assert.False(result.FileResults[0].IsValid);
        Assert.Contains("Checksum mismatch", result.FileResults[0].ErrorMessage);
    }

    [Fact]
    public async Task DataIntegrityManager_RepairCorruptedFiles_ShouldAttemptRepair()
    {
        // Arrange
        var integrityManager = new DataIntegrityManager(_testDirectory);

        // Create a corrupted file
        var corruptedFilePath = Path.Combine(_testDirectory, "channel_000_data_0000000001.dat");
        await File.WriteAllBytesAsync(corruptedFilePath, new byte[] { 1, 2, 3 });

        var corruptedFiles = new[] { corruptedFilePath }.ToList();

        // Act
        var repairResult = await integrityManager.RepairCorruptedFilesAsync(corruptedFiles);

        // Assert
        Assert.NotEqual(RepairStatus.RepairFailed, repairResult.Status);
        Assert.Single(repairResult.RepairActions);
        Assert.Equal(corruptedFilePath, repairResult.RepairActions[0].FilePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}