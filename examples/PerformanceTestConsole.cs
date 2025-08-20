using System;
using System.Threading.Tasks;
using NebulaStore.GigaMap.Tests.Performance;

namespace NebulaStore.Examples;

/// <summary>
/// Console application to run GigaMap performance tests and benchmarks.
/// </summary>
public class PerformanceTestConsole
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 GigaMap Performance Test Console");
        Console.WriteLine("===================================");
        Console.WriteLine();

        try
        {
            // Parse command line arguments
            var entityCount = 5000;
            var verbose = true;

            if (args.Length > 0 && int.TryParse(args[0], out var count))
            {
                entityCount = count;
            }

            if (args.Length > 1 && bool.TryParse(args[1], out var verboseFlag))
            {
                verbose = verboseFlag;
            }

            Console.WriteLine($"Configuration:");
            Console.WriteLine($"  Entity Count: {entityCount:N0}");
            Console.WriteLine($"  Verbose Output: {verbose}");
            Console.WriteLine();

            // Run comprehensive performance tests
            Console.WriteLine("Starting comprehensive performance test suite...");
            Console.WriteLine();

            var results = await PerformanceTestRunner.RunComprehensiveTestsAsync(entityCount, verbose);

            // Display detailed results
            DisplayDetailedResults(results);

            // Generate performance report
            GeneratePerformanceReport(results);

            Console.WriteLine("✅ Performance tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error running performance tests: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    private static void DisplayDetailedResults(PerformanceTestResults results)
    {
        Console.WriteLine("📊 Detailed Performance Results");
        Console.WriteLine("===============================");

        if (!results.Success)
        {
            Console.WriteLine($"❌ Tests failed: {results.Error}");
            return;
        }

        // Basic Operations Results
        Console.WriteLine("🔧 Basic Operations:");
        Console.WriteLine($"  Add Operations: {results.BasicOperationsResult.AddOperationsPerSecond:F0} ops/sec");
        Console.WriteLine($"  Get Operations: {results.BasicOperationsResult.GetOperationsPerSecond:F0} ops/sec");
        Console.WriteLine($"  Query Operations: {results.BasicOperationsResult.QueryOperationsPerSecond:F0} ops/sec");
        Console.WriteLine();

        // Compression Results
        Console.WriteLine("🗜️ Compression Performance:");
        Console.WriteLine($"  Analysis Time: {results.CompressionResult.AnalysisTimeMs}ms");
        Console.WriteLine($"  Original Size: {results.CompressionResult.OriginalSizeKB:F1} KB");
        Console.WriteLine($"  Compressed Size: {results.CompressionResult.CompressedSizeKB:F1} KB");
        Console.WriteLine($"  Compression Ratio: {results.CompressionResult.CompressionRatio * 100:F1}%");
        Console.WriteLine($"  Best Compression: {results.CompressionResult.BestCompressionRatio * 100:F1}%");
        Console.WriteLine($"  Recommended Level: {results.CompressionResult.RecommendedLevel}");
        Console.WriteLine($"  Compression Time: {results.CompressionResult.CompressionTimeMs}ms");
        Console.WriteLine($"  Decompression Time: {results.CompressionResult.DecompressionTimeMs}ms");
        Console.WriteLine($"  Data Integrity: {(results.CompressionResult.DataIntegrityValid ? "✅" : "❌")}");
        Console.WriteLine();

        // Query Cache Results
        Console.WriteLine("🚀 Query Cache Performance:");
        Console.WriteLine($"  Cache Write Time: {results.QueryCacheResult.CacheWriteTimeMs}ms");
        Console.WriteLine($"  Average Read Time: {results.QueryCacheResult.AverageCacheReadTimeMs:F1}ms");
        Console.WriteLine($"  Compression Ratio: {results.QueryCacheResult.CompressionRatio * 100:F1}%");
        Console.WriteLine($"  Memory Savings: {results.QueryCacheResult.MemorySavingsKB:F1} KB");
        Console.WriteLine();

        // Memory Optimization Results
        Console.WriteLine("💾 Memory Optimization:");
        Console.WriteLine($"  Before Optimization: {results.MemoryOptimizationResult.MemoryBeforeOptimizationMB:F1} MB");
        Console.WriteLine($"  After Optimization: {results.MemoryOptimizationResult.MemoryAfterOptimizationMB:F1} MB");
        Console.WriteLine($"  Memory Freed: {results.MemoryOptimizationResult.MemoryFreedMB:F1} MB");
        Console.WriteLine($"  Optimization Time: {results.MemoryOptimizationResult.OptimizationTimeMs}ms");
        Console.WriteLine();

        // Bulk Operations Results
        Console.WriteLine("⚡ Bulk Operations:");
        Console.WriteLine($"  Individual Adds: {results.BulkOperationsResult.IndividualAddOperationsPerSecond:F0} ops/sec");
        Console.WriteLine($"  Bulk Adds: {results.BulkOperationsResult.BulkAddOperationsPerSecond:F0} ops/sec");
        Console.WriteLine($"  Improvement Factor: {results.BulkOperationsResult.BulkImprovementFactor:F2}x");
        Console.WriteLine();

        // Overall Summary
        Console.WriteLine("📈 Overall Performance Summary:");
        Console.WriteLine($"  Test Duration: {results.Duration.TotalSeconds:F1} seconds");
        Console.WriteLine($"  Entity Count: {results.EntityCount:N0}");
        Console.WriteLine($"  Success Rate: 100%");
        Console.WriteLine();
    }

    private static void GeneratePerformanceReport(PerformanceTestResults results)
    {
        Console.WriteLine("📋 Performance Report");
        Console.WriteLine("====================");

        if (!results.Success)
        {
            Console.WriteLine("❌ Cannot generate report - tests failed");
            return;
        }

        // Performance grades
        var addGrade = GradePerformance(results.BasicOperationsResult.AddOperationsPerSecond, 1000, 10000);
        var getGrade = GradePerformance(results.BasicOperationsResult.GetOperationsPerSecond, 10000, 100000);
        var queryGrade = GradePerformance(results.BasicOperationsResult.QueryOperationsPerSecond, 100, 1000);
        var compressionGrade = GradeCompressionRatio(results.CompressionResult.CompressionRatio);
        var bulkGrade = GradeBulkImprovement(results.BulkOperationsResult.BulkImprovementFactor);

        Console.WriteLine("🎯 Performance Grades:");
        Console.WriteLine($"  Add Operations: {addGrade} ({results.BasicOperationsResult.AddOperationsPerSecond:F0} ops/sec)");
        Console.WriteLine($"  Get Operations: {getGrade} ({results.BasicOperationsResult.GetOperationsPerSecond:F0} ops/sec)");
        Console.WriteLine($"  Query Operations: {queryGrade} ({results.BasicOperationsResult.QueryOperationsPerSecond:F0} ops/sec)");
        Console.WriteLine($"  Compression: {compressionGrade} ({results.CompressionResult.CompressionRatio * 100:F1}% ratio)");
        Console.WriteLine($"  Bulk Operations: {bulkGrade} ({results.BulkOperationsResult.BulkImprovementFactor:F2}x improvement)");
        Console.WriteLine();

        // Recommendations
        Console.WriteLine("💡 Optimization Recommendations:");
        
        if (results.BasicOperationsResult.AddOperationsPerSecond < 5000)
        {
            Console.WriteLine("  • Consider using bulk operations for better add performance");
        }
        
        if (results.CompressionResult.CompressionRatio > 0.7)
        {
            Console.WriteLine("  • Data may not be suitable for compression - consider other optimizations");
        }
        else
        {
            Console.WriteLine("  • Compression is effective - consider enabling for production");
        }
        
        if (results.QueryCacheResult.AverageCacheReadTimeMs > 50)
        {
            Console.WriteLine("  • Query cache read time is high - check compression settings");
        }
        else
        {
            Console.WriteLine("  • Query cache performance is excellent");
        }
        
        if (results.BulkOperationsResult.BulkImprovementFactor < 2.0)
        {
            Console.WriteLine("  • Bulk operations improvement is modest - investigate bottlenecks");
        }
        else
        {
            Console.WriteLine("  • Bulk operations provide significant performance benefits");
        }

        Console.WriteLine();

        // System recommendations
        Console.WriteLine("🖥️ System Recommendations:");
        Console.WriteLine($"  • Tested with {results.EntityCount:N0} entities");
        Console.WriteLine($"  • Total test time: {results.Duration.TotalSeconds:F1} seconds");
        Console.WriteLine($"  • Memory optimization freed: {results.MemoryOptimizationResult.MemoryFreedMB:F1} MB");
        Console.WriteLine($"  • Recommended for production use: {(IsRecommendedForProduction(results) ? "✅ Yes" : "⚠️ Review needed")}");
        Console.WriteLine();
    }

    private static string GradePerformance(double actual, double good, double excellent)
    {
        if (actual >= excellent) return "A+ (Excellent)";
        if (actual >= good) return "B+ (Good)";
        if (actual >= good * 0.5) return "C (Average)";
        return "D (Needs Improvement)";
    }

    private static string GradeCompressionRatio(double ratio)
    {
        if (ratio <= 0.3) return "A+ (Excellent)";
        if (ratio <= 0.5) return "B+ (Good)";
        if (ratio <= 0.7) return "C (Average)";
        return "D (Poor)";
    }

    private static string GradeBulkImprovement(double factor)
    {
        if (factor >= 5.0) return "A+ (Excellent)";
        if (factor >= 3.0) return "B+ (Good)";
        if (factor >= 2.0) return "C (Average)";
        return "D (Needs Improvement)";
    }

    private static bool IsRecommendedForProduction(PerformanceTestResults results)
    {
        return results.BasicOperationsResult.AddOperationsPerSecond >= 1000 &&
               results.BasicOperationsResult.GetOperationsPerSecond >= 10000 &&
               results.CompressionResult.DataIntegrityValid &&
               results.BulkOperationsResult.BulkImprovementFactor >= 1.5;
    }
}
