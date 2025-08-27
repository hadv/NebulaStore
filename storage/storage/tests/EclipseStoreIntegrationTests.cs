using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.Storage.Embedded.Types.Memory;
using NebulaStore.Storage.Embedded.Types.Housekeeping;
using NebulaStore.Storage.Embedded.Types.Performance;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Integration tests for the complete Eclipse Store-style system.
/// Tests the interaction between memory management, housekeeping, and performance monitoring.
/// </summary>
public class EclipseStoreIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly MemoryManager _memoryManager;
    private readonly HousekeepingManager _housekeepingManager;
    private readonly PerformanceMonitor _performanceMonitor;

    public EclipseStoreIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"NebulaStore_Integration_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        // Initialize all Eclipse Store-style systems
        _memoryManager = new MemoryManager(10 * 1024 * 1024, TimeSpan.FromMinutes(5)); // 10MB cache
        _housekeepingManager = new HousekeepingManager(_testDirectory, TimeSpan.FromMinutes(10), 5_000_000_000); // 5 second budget
        _performanceMonitor = new PerformanceMonitor(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void EclipseStoreIntegration_MemoryAndPerformance_ShouldWorkTogether()
    {
        // Arrange
        const int entityCount = 100;
        const long entitySize = 1024; // 1KB per entity

        // Act - Simulate storage operations with performance monitoring
        for (long i = 1; i <= entityCount; i++)
        {
            var startTime = DateTime.UtcNow;

            // Allocate memory (simulating entity storage)
            var address = _memoryManager.AllocateEntityMemory(i, entitySize);

            var duration = DateTime.UtcNow - startTime;

            // Record performance metrics
            _performanceMonitor.RecordOperation("MemoryAllocation", duration, address != IntPtr.Zero);

            if (address != IntPtr.Zero)
            {
                _performanceMonitor.RecordWriteOperation(i, entitySize, duration, true);
            }
        }

        // Assert - Verify memory management
        Assert.Equal(entityCount, _memoryManager.CacheEntryCount);
        Assert.Equal(entityCount * entitySize, _memoryManager.CurrentCacheSize);
        Assert.Equal(entityCount, _memoryManager.TotalAllocations);

        // Assert - Verify performance monitoring
        Assert.Equal(entityCount * 2, _performanceMonitor.TotalOperations); // 2 operations per entity
        Assert.Equal(0, _performanceMonitor.TotalErrors);
        Assert.Equal(0.0, _performanceMonitor.ErrorRate);

        var memoryMetric = _performanceMonitor.GetMetric("MemoryAllocation");
        var writeMetric = _performanceMonitor.GetMetric("Write");

        Assert.NotNull(memoryMetric);
        Assert.NotNull(writeMetric);
        Assert.Equal(entityCount, memoryMetric.TotalOperations);
        Assert.Equal(entityCount, writeMetric.TotalOperations);
        Assert.Equal(1.0, memoryMetric.SuccessRate);
        Assert.Equal(1.0, writeMetric.SuccessRate);
    }

    [Fact]
    public void EclipseStoreIntegration_MemoryEvictionWithPerformanceTracking_ShouldWork()
    {
        // Arrange - Fill memory beyond threshold to trigger eviction
        const long largeEntitySize = 2 * 1024 * 1024; // 2MB per entity
        const int entityCount = 8; // 16MB total, exceeding 10MB threshold

        // Act - Allocate large entities
        for (long i = 1; i <= entityCount; i++)
        {
            var startTime = DateTime.UtcNow;
            var address = _memoryManager.AllocateEntityMemory(i, largeEntitySize);
            var duration = DateTime.UtcNow - startTime;

            _performanceMonitor.RecordWriteOperation(i, largeEntitySize, duration, address != IntPtr.Zero);
        }

        // Trigger eviction
        var evictionStart = DateTime.UtcNow;
        var bytesFreed = _memoryManager.PerformEviction();
        var evictionDuration = DateTime.UtcNow - evictionStart;

        _performanceMonitor.RecordOperation("MemoryEviction", evictionDuration, bytesFreed > 0);

        // Assert - Verify eviction occurred
        Assert.True(bytesFreed > 0);
        Assert.True(_memoryManager.TotalEvictions > 0);
        Assert.True(_memoryManager.CurrentCacheSize < entityCount * largeEntitySize);

        // Assert - Verify performance tracking
        var evictionMetric = _performanceMonitor.GetMetric("MemoryEviction");
        Assert.NotNull(evictionMetric);
        Assert.Equal(1, evictionMetric.TotalOperations);
        Assert.Equal(1.0, evictionMetric.SuccessRate);
    }

    [Fact]
    public void EclipseStoreIntegration_HousekeepingWithPerformanceMonitoring_ShouldWork()
    {
        // Arrange - Create test files for housekeeping
        CreateTestFilesForHousekeeping();

        // Act - Perform housekeeping with performance monitoring
        var housekeepingStart = DateTime.UtcNow;
        var result = _housekeepingManager.PerformFullHousekeeping();
        var housekeepingDuration = DateTime.UtcNow - housekeepingStart;

        _performanceMonitor.RecordOperation("Housekeeping", housekeepingDuration, result.IsSuccessful);

        if (result.GarbageCollectionResult != null)
        {
            _performanceMonitor.RecordOperation("GarbageCollection",
                result.GarbageCollectionResult.Duration,
                result.GarbageCollectionResult.IsSuccessful);
        }

        // Assert - Verify housekeeping
        Assert.NotNull(result);
        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.GarbageCollectionResult);
        Assert.NotNull(result.FileConsolidationResult);
        Assert.NotNull(result.StorageOptimizationResult);

        // Assert - Verify performance monitoring
        var housekeepingMetric = _performanceMonitor.GetMetric("Housekeeping");
        Assert.NotNull(housekeepingMetric);
        Assert.Equal(1, housekeepingMetric.TotalOperations);
        Assert.Equal(1.0, housekeepingMetric.SuccessRate);
    }

    [Fact]
    public async Task EclipseStoreIntegration_CompleteWorkflow_ShouldDemonstrateAllSystems()
    {
        // Arrange - Simulate a complete Eclipse Store workflow
        const int operationCount = 50;

        // Act - Phase 1: Entity Storage Operations
        for (int i = 1; i <= operationCount; i++)
        {
            // Simulate read operation
            var readStart = DateTime.UtcNow;
            var cachedAddress = _memoryManager.GetEntityMemory(i);
            var readDuration = DateTime.UtcNow - readStart;
            var cacheHit = cachedAddress != IntPtr.Zero;

            _performanceMonitor.RecordReadOperation(i, 1024, readDuration, cacheHit);

            // If not cached, allocate and simulate write
            if (!cacheHit)
            {
                var writeStart = DateTime.UtcNow;
                var address = _memoryManager.AllocateEntityMemory(i, 1024);
                var writeDuration = DateTime.UtcNow - writeStart;

                _performanceMonitor.RecordWriteOperation(i, 1024, writeDuration, address != IntPtr.Zero);
            }
        }

        // Phase 2: Commit Operation
        var commitStart = DateTime.UtcNow;
        // Simulate commit logic here
        await Task.Delay(10); // Simulate commit time
        var commitDuration = DateTime.UtcNow - commitStart;

        _performanceMonitor.RecordCommitOperation(_memoryManager.CacheEntryCount, commitDuration, true);

        // Phase 3: Housekeeping
        CreateTestFilesForHousekeeping();
        var housekeepingResult = _housekeepingManager.PerformTimeBudgetedHousekeeping(1_000_000_000); // 1 second budget
        _performanceMonitor.RecordOperation("TimeBudgetedHousekeeping",
            housekeepingResult.Duration,
            housekeepingResult.IsSuccessful);

        // Assert - Verify complete system state
        var memoryStats = _memoryManager.GetStatistics();
        var housekeepingStats = _housekeepingManager.GetStatistics();
        var performanceStats = _performanceMonitor.GetStatistics();
        var performanceAnalysis = _performanceMonitor.AnalyzePerformance();

        // Memory Management Assertions
        Assert.True(memoryStats.TotalAllocations > 0);
        Assert.True(memoryStats.CacheUtilization >= 0);
        Assert.True(memoryStats.CacheEntryCount > 0);

        // Housekeeping Assertions
        Assert.True(housekeepingStats.TotalGarbageCollections >= 0);
        Assert.True(housekeepingStats.LastHousekeepingRun <= DateTime.UtcNow);

        // Performance Monitoring Assertions
        Assert.True(performanceStats.TotalOperations > operationCount);
        Assert.True(performanceStats.OperationsPerSecond >= 0);
        Assert.Contains("Read", performanceStats.OperationStatistics.Keys);
        Assert.Contains("Write", performanceStats.OperationStatistics.Keys);
        Assert.Contains("Commit", performanceStats.OperationStatistics.Keys);

        // Performance Analysis Assertions
        Assert.NotNull(performanceAnalysis);
        Assert.True(performanceAnalysis.TotalOperations > 0);
        Assert.True(performanceAnalysis.OperationAnalyses.Count > 0);
    }

    [Fact]
    public void EclipseStoreIntegration_SystemUnderLoad_ShouldMaintainPerformance()
    {
        // Arrange - Simulate high load scenario
        const int highLoadOperations = 1000;
        const long entitySize = 512;

        // Act - Perform many operations rapidly
        var overallStart = DateTime.UtcNow;

        Parallel.For(0, highLoadOperations, i =>
        {
            var operationStart = DateTime.UtcNow;

            try
            {
                // Simulate concurrent memory operations
                var entityId = i + 1;
                var address = _memoryManager.AllocateEntityMemory(entityId, entitySize);
                var duration = DateTime.UtcNow - operationStart;

                _performanceMonitor.RecordOperation("ConcurrentAllocation", duration, address != IntPtr.Zero);

                // Occasionally release memory
                if (i % 10 == 0)
                {
                    _memoryManager.ReleaseEntityMemory(entityId);
                }
            }
            catch
            {
                var duration = DateTime.UtcNow - operationStart;
                _performanceMonitor.RecordOperation("ConcurrentAllocation", duration, false);
            }
        });

        var overallDuration = DateTime.UtcNow - overallStart;

        // Assert - Verify system handled load
        var performanceStats = _performanceMonitor.GetStatistics();
        var memoryStats = _memoryManager.GetStatistics();

        Assert.True(performanceStats.TotalOperations >= highLoadOperations);
        Assert.True(performanceStats.OperationsPerSecond > 0);
        Assert.True(memoryStats.TotalAllocations > 0);

        // System should maintain reasonable error rate even under load
        Assert.True(performanceStats.ErrorRate < 50.0); // Less than 50% errors

        Console.WriteLine($"Load Test Results:");
        Console.WriteLine($"  Operations: {performanceStats.TotalOperations:N0}");
        Console.WriteLine($"  Duration: {overallDuration.TotalSeconds:F2}s");
        Console.WriteLine($"  Ops/Sec: {performanceStats.OperationsPerSecond:F1}");
        Console.WriteLine($"  Error Rate: {performanceStats.ErrorRate:F1}%");
        Console.WriteLine($"  Memory Utilization: {memoryStats.CacheUtilization:F1}%");
    }

    [Fact]
    public void EclipseStoreIntegration_ErrorHandlingAndRecovery_ShouldBeResilient()
    {
        // Arrange - Create scenarios that might cause errors
        var invalidEntityId = -1L;
        var zeroSize = 0L;

        // Act & Assert - Test error handling in memory management
        Assert.Throws<ArgumentException>(() => _memoryManager.AllocateEntityMemory(invalidEntityId, zeroSize));

        // Test performance monitoring with errors
        _performanceMonitor.RecordOperation("ErrorTest", TimeSpan.FromMilliseconds(10), false);
        _performanceMonitor.RecordOperation("ErrorTest", TimeSpan.FromMilliseconds(5), false);
        _performanceMonitor.RecordOperation("ErrorTest", TimeSpan.FromMilliseconds(15), true);

        var errorMetric = _performanceMonitor.GetMetric("ErrorTest");
        Assert.NotNull(errorMetric);
        Assert.Equal(3, errorMetric.TotalOperations);
        Assert.Equal(1, errorMetric.SuccessfulOperations);
        Assert.Equal(2, errorMetric.FailedOperations);
        Assert.Equal(1.0/3.0, errorMetric.SuccessRate, 3);

        // Test housekeeping with non-existent directory
        using var invalidHousekeeping = new HousekeepingManager("/invalid/path/that/does/not/exist",
            TimeSpan.FromMinutes(1), 1_000_000_000);

        var result = invalidHousekeeping.PerformFullHousekeeping();
        // Should complete without throwing, even with invalid path
        Assert.NotNull(result);
    }

    [Fact]
    public void EclipseStoreIntegration_SystemStatisticsAndReporting_ShouldProvideInsights()
    {
        // Arrange - Perform various operations to generate statistics
        PerformMixedOperations();

        // Act - Collect comprehensive statistics
        var memoryStats = _memoryManager.GetStatistics();
        var housekeepingStats = _housekeepingManager.GetStatistics();
        var performanceStats = _performanceMonitor.GetStatistics();
        var performanceAnalysis = _performanceMonitor.AnalyzePerformance();

        // Assert - Verify statistics provide meaningful insights
        Assert.NotNull(memoryStats);
        Assert.NotNull(housekeepingStats);
        Assert.NotNull(performanceStats);
        Assert.NotNull(performanceAnalysis);

        // Memory Statistics
        Assert.True(memoryStats.TotalAllocations > 0);
        Assert.Contains("Cache:", memoryStats.Summary);
        Assert.Contains("Entries:", memoryStats.Summary);

        // Housekeeping Statistics
        Assert.Contains("GC Runs:", housekeepingStats.Summary);
        Assert.Contains("Interval:", housekeepingStats.Summary);

        // Performance Statistics
        Assert.Contains("Uptime:", performanceStats.Summary);
        Assert.Contains("Operations:", performanceStats.Summary);
        Assert.True(performanceStats.OperationStatistics.Count > 0);

        // Performance Analysis
        Assert.True(performanceAnalysis.OperationAnalyses.Count > 0);
        Assert.Contains("Operations:", performanceAnalysis.Summary);

        // Verify cross-system consistency
        Assert.True(performanceStats.TotalOperations >= memoryStats.TotalAllocations);
    }

    [Fact]
    public async Task EclipseStoreIntegration_TimeBudgetedOperations_ShouldRespectConstraints()
    {
        // Arrange - Set up time-budgeted operations
        const long shortBudget = 100_000_000; // 100ms in nanoseconds
        const long mediumBudget = 1_000_000_000; // 1 second in nanoseconds

        // Act - Test time-budgeted housekeeping
        var shortBudgetResult = _housekeepingManager.PerformTimeBudgetedHousekeeping(shortBudget);
        var mediumBudgetResult = _housekeepingManager.PerformTimeBudgetedHousekeeping(mediumBudget);

        // Assert - Verify time budget compliance
        Assert.NotNull(shortBudgetResult);
        Assert.NotNull(mediumBudgetResult);

        // Short budget might exceed due to minimum work requirements
        Assert.True(shortBudgetResult.Status == HousekeepingStatus.Completed ||
                   shortBudgetResult.Status == HousekeepingStatus.TimeBudgetExceeded);

        // Medium budget should typically complete
        Assert.True(mediumBudgetResult.Status == HousekeepingStatus.Completed ||
                   mediumBudgetResult.Status == HousekeepingStatus.TimeBudgetExceeded);

        // Record performance metrics for time-budgeted operations
        _performanceMonitor.RecordOperation("ShortBudgetHousekeeping",
            shortBudgetResult.Duration, shortBudgetResult.IsSuccessful);
        _performanceMonitor.RecordOperation("MediumBudgetHousekeeping",
            mediumBudgetResult.Duration, mediumBudgetResult.IsSuccessful);

        var shortMetric = _performanceMonitor.GetMetric("ShortBudgetHousekeeping");
        var mediumMetric = _performanceMonitor.GetMetric("MediumBudgetHousekeeping");

        Assert.NotNull(shortMetric);
        Assert.NotNull(mediumMetric);
    }

    #region Helper Methods

    private void CreateTestFilesForHousekeeping()
    {
        // Create temporary files
        var tempFile = Path.Combine(_testDirectory, "temp_file.tmp");
        var backupFile = Path.Combine(_testDirectory, "backup_file.bak");
        var corruptedFile = Path.Combine(_testDirectory, "corrupted_file.corrupted.123");

        File.WriteAllText(tempFile, "Temporary file content for testing");
        File.WriteAllText(backupFile, "Backup file content for testing");
        File.WriteAllText(corruptedFile, "Corrupted file content for testing");

        // Create small data files for consolidation
        var dataFile1 = Path.Combine(_testDirectory, "channel_000_data_0000000001.dat");
        var dataFile2 = Path.Combine(_testDirectory, "channel_000_data_0000000002.dat");

        File.WriteAllText(dataFile1, "Small data file 1 content");
        File.WriteAllText(dataFile2, "Small data file 2 content");
    }

    private void PerformMixedOperations()
    {
        // Perform a variety of operations to generate comprehensive statistics

        // Memory operations
        for (int i = 1; i <= 20; i++)
        {
            var startTime = DateTime.UtcNow;
            var address = _memoryManager.AllocateEntityMemory(i, 1024);
            var duration = DateTime.UtcNow - startTime;

            _performanceMonitor.RecordWriteOperation(i, 1024, duration, address != IntPtr.Zero);
        }

        // Read operations (some cache hits, some misses)
        for (int i = 1; i <= 30; i++)
        {
            var startTime = DateTime.UtcNow;
            var address = _memoryManager.GetEntityMemory(i);
            var duration = DateTime.UtcNow - startTime;
            var cacheHit = address != IntPtr.Zero;

            _performanceMonitor.RecordReadOperation(i, 1024, duration, cacheHit);
        }

        // Commit operation
        var commitStart = DateTime.UtcNow;
        // Simulate commit processing
        Thread.Sleep(5);
        var commitDuration = DateTime.UtcNow - commitStart;

        _performanceMonitor.RecordCommitOperation(_memoryManager.CacheEntryCount, commitDuration, true);

        // Housekeeping operation
        CreateTestFilesForHousekeeping();
        var housekeepingResult = _housekeepingManager.PerformFullHousekeeping();
        _performanceMonitor.RecordOperation("MixedOperationsHousekeeping",
            housekeepingResult.Duration, housekeepingResult.IsSuccessful);
    }

    #endregion

    public void Dispose()
    {
        _memoryManager?.Dispose();
        _housekeepingManager?.Dispose();
        _performanceMonitor?.Dispose();

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