using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.Storage.Embedded.Types.Performance;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Tests for the Eclipse Store-style performance monitoring system.
/// </summary>
public class PerformanceTests : IDisposable
{
    private readonly PerformanceMonitor _monitor;

    public PerformanceTests()
    {
        _monitor = new PerformanceMonitor(TimeSpan.FromSeconds(10)); // Short interval for testing
    }

    [Fact]
    public void PerformanceMonitor_Constructor_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        using var monitor = new PerformanceMonitor();

        // Assert
        Assert.Equal(0, monitor.TotalOperations);
        Assert.Equal(0, monitor.TotalErrors);
        Assert.Equal(0.0, monitor.ErrorRate);
        Assert.True(monitor.StartTime <= DateTime.UtcNow);
        Assert.True(monitor.Uptime.TotalSeconds >= 0);
    }

    [Fact]
    public void PerformanceMonitor_RecordOperation_ShouldUpdateMetrics()
    {
        // Arrange
        const string operationType = "TestOperation";
        var duration = TimeSpan.FromMilliseconds(50);

        // Act
        _monitor.RecordOperation(operationType, duration, true);

        // Assert
        Assert.Equal(1, _monitor.TotalOperations);
        Assert.Equal(0, _monitor.TotalErrors);
        Assert.Equal(0.0, _monitor.ErrorRate);

        var metric = _monitor.GetMetric(operationType);
        Assert.NotNull(metric);
        Assert.Equal(operationType, metric.OperationType);
        Assert.Equal(1, metric.TotalOperations);
        Assert.Equal(1, metric.SuccessfulOperations);
        Assert.Equal(0, metric.FailedOperations);
        Assert.Equal(duration, metric.AverageLatency);
        Assert.Equal(1.0, metric.SuccessRate);
    }

    [Fact]
    public void PerformanceMonitor_RecordFailedOperation_ShouldUpdateErrorMetrics()
    {
        // Arrange
        const string operationType = "FailedOperation";
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        _monitor.RecordOperation(operationType, duration, false);

        // Assert
        Assert.Equal(1, _monitor.TotalOperations);
        Assert.Equal(1, _monitor.TotalErrors);
        Assert.Equal(100.0, _monitor.ErrorRate);

        var metric = _monitor.GetMetric(operationType);
        Assert.NotNull(metric);
        Assert.Equal(0, metric.SuccessfulOperations);
        Assert.Equal(1, metric.FailedOperations);
        Assert.Equal(0.0, metric.SuccessRate);
    }

    [Fact]
    public void PerformanceMonitor_RecordMultipleOperations_ShouldCalculateCorrectAverages()
    {
        // Arrange
        const string operationType = "MultipleOps";
        var durations = new[] {
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(30)
        };

        // Act
        foreach (var duration in durations)
        {
            _monitor.RecordOperation(operationType, duration, true);
        }

        // Assert
        var metric = _monitor.GetMetric(operationType);
        Assert.NotNull(metric);
        Assert.Equal(3, metric.TotalOperations);
        Assert.Equal(TimeSpan.FromMilliseconds(20), metric.AverageLatency); // (10+20+30)/3 = 20
        Assert.Equal(TimeSpan.FromMilliseconds(10), metric.MinLatency);
        Assert.Equal(TimeSpan.FromMilliseconds(30), metric.MaxLatency);
    }

    [Fact]
    public void PerformanceMonitor_RecordReadOperation_ShouldCreateCorrectMetadata()
    {
        // Arrange
        const long entityId = 12345;
        const long bytesRead = 1024;
        var duration = TimeSpan.FromMilliseconds(25);

        // Act
        _monitor.RecordReadOperation(entityId, bytesRead, duration, true);

        // Assert
        Assert.Equal(1, _monitor.TotalOperations);
        var metric = _monitor.GetMetric("Read");
        Assert.NotNull(metric);
        Assert.Equal(1, metric.TotalOperations);

        var events = _monitor.GetRecentEvents(1);
        Assert.Single(events);
        Assert.Equal("Read", events[0].OperationType);
        Assert.True(events[0].Success);
        Assert.Contains("EntityId", events[0].Metadata.Keys);
        Assert.Contains("BytesRead", events[0].Metadata.Keys);
        Assert.Contains("CacheHit", events[0].Metadata.Keys);
        Assert.Equal(entityId, events[0].Metadata["EntityId"]);
        Assert.Equal(bytesRead, events[0].Metadata["BytesRead"]);
        Assert.Equal(true, events[0].Metadata["CacheHit"]);
    }

    [Fact]
    public void PerformanceMonitor_RecordWriteOperation_ShouldCreateCorrectMetadata()
    {
        // Arrange
        const long entityId = 67890;
        const long bytesWritten = 2048;
        var duration = TimeSpan.FromMilliseconds(75);

        // Act
        _monitor.RecordWriteOperation(entityId, bytesWritten, duration, true);

        // Assert
        var metric = _monitor.GetMetric("Write");
        Assert.NotNull(metric);
        Assert.Equal(1, metric.TotalOperations);

        var events = _monitor.GetRecentEvents(1);
        Assert.Single(events);
        Assert.Equal("Write", events[0].OperationType);
        Assert.Contains("EntityId", events[0].Metadata.Keys);
        Assert.Contains("BytesWritten", events[0].Metadata.Keys);
        Assert.Equal(entityId, events[0].Metadata["EntityId"]);
        Assert.Equal(bytesWritten, events[0].Metadata["BytesWritten"]);
    }

    [Fact]
    public void PerformanceMonitor_RecordCommitOperation_ShouldCreateCorrectMetadata()
    {
        // Arrange
        const int entitiesCommitted = 5;
        var duration = TimeSpan.FromMilliseconds(150);

        // Act
        _monitor.RecordCommitOperation(entitiesCommitted, duration, true);

        // Assert
        var metric = _monitor.GetMetric("Commit");
        Assert.NotNull(metric);
        Assert.Equal(1, metric.TotalOperations);

        var events = _monitor.GetRecentEvents(1);
        Assert.Single(events);
        Assert.Equal("Commit", events[0].OperationType);
        Assert.Contains("EntitiesCommitted", events[0].Metadata.Keys);
        Assert.Equal(entitiesCommitted, events[0].Metadata["EntitiesCommitted"]);
    }

    [Fact]
    public void PerformanceMonitor_GetAllMetrics_ShouldReturnAllOperationTypes()
    {
        // Arrange
        _monitor.RecordOperation("Read", TimeSpan.FromMilliseconds(10), true);
        _monitor.RecordOperation("Write", TimeSpan.FromMilliseconds(20), true);
        _monitor.RecordOperation("Commit", TimeSpan.FromMilliseconds(30), true);

        // Act
        var allMetrics = _monitor.GetAllMetrics();

        // Assert
        Assert.Equal(3, allMetrics.Count);
        Assert.Contains("Read", allMetrics.Keys);
        Assert.Contains("Write", allMetrics.Keys);
        Assert.Contains("Commit", allMetrics.Keys);
    }

    [Fact]
    public void PerformanceMonitor_GetRecentEvents_ShouldReturnCorrectCount()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _monitor.RecordOperation($"Op{i}", TimeSpan.FromMilliseconds(i * 10), true);
        }

        // Act
        var events = _monitor.GetRecentEvents(5);

        // Assert
        Assert.Equal(5, events.Count);
        // Should return the most recent events
        Assert.Equal("Op9", events[4].OperationType);
        Assert.Equal("Op5", events[0].OperationType);
    }

    [Fact]
    public void PerformanceMonitor_AnalyzePerformance_ShouldProvideAnalysis()
    {
        // Arrange
        // Create some operations with varying performance
        _monitor.RecordOperation("FastOp", TimeSpan.FromMilliseconds(10), true);
        _monitor.RecordOperation("SlowOp", TimeSpan.FromMilliseconds(200), true);
        _monitor.RecordOperation("FailingOp", TimeSpan.FromMilliseconds(50), false);

        // Act
        var analysis = _monitor.AnalyzePerformance();

        // Assert
        Assert.NotNull(analysis);
        Assert.True(analysis.AnalysisTime <= DateTime.UtcNow);
        Assert.Equal(3, analysis.TotalOperations);
        Assert.Equal(1, analysis.TotalErrors);
        Assert.True(analysis.ErrorRate > 0);
        Assert.Equal(3, analysis.OperationAnalyses.Count);

        // Check that recommendations are generated for slow operations
        var slowOpAnalysis = analysis.OperationAnalyses.Find(a => a.OperationType == "SlowOp");
        Assert.NotNull(slowOpAnalysis);
        Assert.True(slowOpAnalysis.Recommendations.Count > 0);
    }

    [Fact]
    public void PerformanceMonitor_GetStatistics_ShouldReturnCorrectStatistics()
    {
        // Arrange
        _monitor.RecordOperation("TestOp", TimeSpan.FromMilliseconds(50), true);
        _monitor.RecordOperation("TestOp", TimeSpan.FromMilliseconds(100), false);

        // Act
        var statistics = _monitor.GetStatistics();

        // Assert
        Assert.NotNull(statistics);
        Assert.Equal(2, statistics.TotalOperations);
        Assert.Equal(1, statistics.TotalErrors);
        Assert.Equal(50.0, statistics.ErrorRate);
        Assert.True(statistics.OperationsPerSecond >= 0);
        Assert.Contains("TestOp", statistics.OperationStatistics.Keys);
    }

    [Fact]
    public void PerformanceMonitor_Reset_ShouldClearAllMetrics()
    {
        // Arrange
        _monitor.RecordOperation("TestOp", TimeSpan.FromMilliseconds(50), true);
        Assert.Equal(1, _monitor.TotalOperations);

        // Act
        _monitor.Reset();

        // Assert
        Assert.Equal(0, _monitor.TotalOperations);
        Assert.Equal(0, _monitor.TotalErrors);
        Assert.Equal(0.0, _monitor.ErrorRate);
        Assert.Empty(_monitor.GetAllMetrics());
        Assert.Empty(_monitor.GetRecentEvents());
    }

    [Fact]
    public void PerformanceMetric_Properties_ShouldCalculateCorrectly()
    {
        // Arrange
        var metric = new PerformanceMetric("TestMetric");

        // Act
        metric.RecordOperation(TimeSpan.FromMilliseconds(10), true);
        metric.RecordOperation(TimeSpan.FromMilliseconds(20), true);
        metric.RecordOperation(TimeSpan.FromMilliseconds(30), false);

        // Assert
        Assert.Equal("TestMetric", metric.OperationType);
        Assert.Equal(3, metric.TotalOperations);
        Assert.Equal(2, metric.SuccessfulOperations);
        Assert.Equal(1, metric.FailedOperations);
        Assert.Equal(TimeSpan.FromMilliseconds(20), metric.AverageLatency);
        Assert.Equal(TimeSpan.FromMilliseconds(10), metric.MinLatency);
        Assert.Equal(TimeSpan.FromMilliseconds(30), metric.MaxLatency);
        Assert.Equal(2.0/3.0, metric.SuccessRate, 3); // 2/3 ≈ 0.667
    }

    public void Dispose()
    {
        _monitor?.Dispose();
    }
}