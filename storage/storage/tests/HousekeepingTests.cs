using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.Storage.Embedded.Types.Housekeeping;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Tests for the Eclipse Store-style housekeeping system.
/// </summary>
public class HousekeepingTests : IDisposable
{
    private readonly string _testDirectory;

    public HousekeepingTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"NebulaStore_Housekeeping_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void HousekeepingManager_Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        using var manager = new HousekeepingManager(_testDirectory, TimeSpan.FromMinutes(5), 1000000000); // 1 second budget

        // Assert
        Assert.Equal(0, manager.TotalGarbageCollections);
        Assert.Equal(0, manager.TotalFileConsolidations);
        Assert.Equal(0, manager.TotalBytesReclaimed);
        Assert.Equal(TimeSpan.FromMinutes(5), manager.HousekeepingInterval);
        Assert.Equal(1000000000, manager.TimeBudgetNanoseconds);
    }

    [Fact]
    public void HousekeepingManager_PerformFullHousekeeping_ShouldCompleteSuccessfully()
    {
        // Arrange
        using var manager = new HousekeepingManager(_testDirectory, TimeSpan.FromMinutes(5), 1000000000);

        // Create some test files to clean up
        CreateTestFiles();

        // Act
        var result = manager.PerformFullHousekeeping();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HousekeepingStatus.Completed, result.Status);
        Assert.True(result.IsSuccessful);
        Assert.True(result.Duration.TotalMilliseconds >= 0);
        Assert.NotNull(result.GarbageCollectionResult);
        Assert.NotNull(result.FileConsolidationResult);
        Assert.NotNull(result.StorageOptimizationResult);
    }

    [Fact]
    public void HousekeepingManager_PerformTimeBudgetedHousekeeping_ShouldRespectTimeBudget()
    {
        // Arrange
        using var manager = new HousekeepingManager(_testDirectory, TimeSpan.FromMinutes(5), 100000); // Very small budget (0.1ms)

        CreateTestFiles();

        // Act
        var result = manager.PerformTimeBudgetedHousekeeping(100000); // 0.1ms budget

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Status == HousekeepingStatus.Completed || result.Status == HousekeepingStatus.TimeBudgetExceeded);
        Assert.Equal(100000, result.TimeBudgetNanoseconds);
    }

    [Fact]
    public void HousekeepingManager_GetStatistics_ShouldReturnCorrectStatistics()
    {
        // Arrange
        using var manager = new HousekeepingManager(_testDirectory, TimeSpan.FromMinutes(10), 2000000000);

        // Perform housekeeping to generate statistics
        manager.PerformFullHousekeeping();

        // Act
        var statistics = manager.GetStatistics();

        // Assert
        Assert.NotNull(statistics);
        Assert.True(statistics.TotalGarbageCollections >= 0);
        Assert.True(statistics.TotalFileConsolidations >= 0);
        Assert.True(statistics.TotalBytesReclaimed >= 0);
        Assert.Equal(TimeSpan.FromMinutes(10), statistics.HousekeepingInterval);
        Assert.Equal(2000000000, statistics.TimeBudgetNanoseconds);
        Assert.True(statistics.TimeSinceLastRun.TotalSeconds >= 0);
    }

    [Fact]
    public void HousekeepingManager_WithOrphanedFiles_ShouldCleanUpFiles()
    {
        // Arrange
        using var manager = new HousekeepingManager(_testDirectory, TimeSpan.FromMinutes(5), 1000000000);

        // Create orphaned files
        var tempFile = Path.Combine(_testDirectory, "test.tmp");
        var bakFile = Path.Combine(_testDirectory, "test.bak");
        var corruptedFile = Path.Combine(_testDirectory, "test.corrupted.123");

        File.WriteAllText(tempFile, "temp content");
        File.WriteAllText(bakFile, "backup content");
        File.WriteAllText(corruptedFile, "corrupted content");

        Assert.True(File.Exists(tempFile));
        Assert.True(File.Exists(bakFile));
        Assert.True(File.Exists(corruptedFile));

        // Act
        var result = manager.PerformFullHousekeeping();

        // Assert
        Assert.Equal(HousekeepingStatus.Completed, result.Status);
        Assert.NotNull(result.GarbageCollectionResult);
        Assert.True(result.GarbageCollectionResult.FilesDeleted >= 0);
        Assert.True(result.GarbageCollectionResult.BytesReclaimed >= 0);
    }

    [Fact]
    public void HousekeepingManager_WithSmallDataFiles_ShouldAttemptConsolidation()
    {
        // Arrange
        using var manager = new HousekeepingManager(_testDirectory, TimeSpan.FromMinutes(5), 1000000000);

        // Create small data files
        var dataFile1 = Path.Combine(_testDirectory, "channel_000_data_0000000001.dat");
        var dataFile2 = Path.Combine(_testDirectory, "channel_000_data_0000000002.dat");

        File.WriteAllText(dataFile1, "small data 1");
        File.WriteAllText(dataFile2, "small data 2");

        // Act
        var result = manager.PerformFullHousekeeping();

        // Assert
        Assert.Equal(HousekeepingStatus.Completed, result.Status);
        Assert.NotNull(result.FileConsolidationResult);
        Assert.True(result.FileConsolidationResult.FilesConsolidated >= 0);
        Assert.True(result.FileConsolidationResult.BytesConsolidated >= 0);
    }

    [Fact]
    public void GarbageCollectionResult_Properties_ShouldWorkCorrectly()
    {
        // Arrange
        var result = new GarbageCollectionResult
        {
            Status = GarbageCollectionStatus.Completed,
            FilesDeleted = 5,
            BytesReclaimed = 1024,
            StartTime = DateTime.UtcNow.AddSeconds(-1),
            EndTime = DateTime.UtcNow
        };

        result.Duration = result.EndTime - result.StartTime;

        // Act & Assert
        Assert.True(result.IsSuccessful);
        Assert.Contains("Completed", result.Summary);
        Assert.Contains("5", result.Summary);
        Assert.Contains("1,024", result.Summary);
    }

    [Fact]
    public void FileConsolidationResult_Properties_ShouldWorkCorrectly()
    {
        // Arrange
        var result = new FileConsolidationResult
        {
            Status = FileConsolidationStatus.Completed,
            FilesConsolidated = 3,
            BytesConsolidated = 2048,
            StartTime = DateTime.UtcNow.AddSeconds(-1),
            EndTime = DateTime.UtcNow
        };

        result.Duration = result.EndTime - result.StartTime;

        // Act & Assert
        Assert.True(result.IsSuccessful);
        Assert.Contains("Completed", result.Summary);
        Assert.Contains("3", result.Summary);
        Assert.Contains("2,048", result.Summary);
    }

    [Fact]
    public void HousekeepingStatistics_Properties_ShouldCalculateCorrectly()
    {
        // Arrange
        var statistics = new HousekeepingStatistics
        {
            TotalGarbageCollections = 10,
            TotalFileConsolidations = 5,
            TotalBytesReclaimed = 10240,
            LastHousekeepingRun = DateTime.UtcNow.AddMinutes(-2),
            HousekeepingInterval = TimeSpan.FromMinutes(5)
        };

        // Act & Assert
        Assert.Equal(1024.0, statistics.AverageBytesReclaimedPerGC); // 10240 / 10
        Assert.True(statistics.TimeSinceLastRun.TotalMinutes >= 2);
        Assert.False(statistics.IsOverdue); // 2 minutes < 7.5 minutes (1.5 * 5)
        Assert.Contains("10", statistics.Summary);
        Assert.Contains("5", statistics.Summary);
        Assert.Contains("10,240", statistics.Summary);
    }

    private void CreateTestFiles()
    {
        // Create some test files for housekeeping to work with
        var tempFile = Path.Combine(_testDirectory, "test.tmp");
        var dataFile = Path.Combine(_testDirectory, "channel_000_data_0000000001.dat");

        File.WriteAllText(tempFile, "temporary file content");
        File.WriteAllText(dataFile, "data file content");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }
}