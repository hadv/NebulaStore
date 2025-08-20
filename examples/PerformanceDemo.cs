using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NebulaStore.GigaMap;
using NebulaStore.GigaMap.Performance;

namespace NebulaStore.Examples;

/// <summary>
/// Simple demonstration of GigaMap performance optimizations.
/// </summary>
public static class PerformanceDemo
{
    public class Employee
    {
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public int Age { get; set; }
        public decimal Salary { get; set; }
        public DateTime HireDate { get; set; }
    }

    /// <summary>
    /// Demonstrates the performance optimizations available in GigaMap.
    /// </summary>
    public static async Task RunPerformanceDemoAsync()
    {
        Console.WriteLine("🚀 GigaMap Performance Optimizations Demo");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        // Create test data
        var employees = CreateSampleEmployees(2000);
        Console.WriteLine($"📊 Created {employees.Count:N0} sample employees");

        // Demo 1: Bulk Operations
        await DemoBulkOperationsAsync(employees);

        // Demo 2: Compression
        await DemoCompressionAsync(employees);

        // Demo 3: Query Cache
        await DemoQueryCacheAsync(employees);

        // Demo 4: Memory Optimization
        await DemoMemoryOptimizationAsync(employees);

        Console.WriteLine("✅ Performance demo completed!");
    }

    private static async Task DemoBulkOperationsAsync(List<Employee> employees)
    {
        Console.WriteLine("⚡ Bulk Operations Demo");
        Console.WriteLine("======================");

        var gigaMap = GigaMap.Builder<Employee>()
            .WithBitmapIndex(Indexer.Property<Employee, string>("Department", e => e.Department))
            .Build();

        // Individual adds
        var individualStopwatch = Stopwatch.StartNew();
        foreach (var employee in employees.Take(1000))
        {
            gigaMap.Add(employee);
        }
        individualStopwatch.Stop();

        // Clear and test bulk adds
        gigaMap.Clear();
        var bulkStopwatch = Stopwatch.StartNew();
        await SimplePerformanceOptimizations.BulkAddAsync(gigaMap, employees.Take(1000));
        bulkStopwatch.Stop();

        var individualOpsPerSec = 1000 / individualStopwatch.Elapsed.TotalSeconds;
        var bulkOpsPerSec = 1000 / bulkStopwatch.Elapsed.TotalSeconds;

        Console.WriteLine($"  Individual adds: {individualOpsPerSec:F0} ops/sec ({individualStopwatch.ElapsedMilliseconds}ms)");
        Console.WriteLine($"  Bulk adds:       {bulkOpsPerSec:F0} ops/sec ({bulkStopwatch.ElapsedMilliseconds}ms)");
        Console.WriteLine($"  Improvement:     {bulkOpsPerSec / individualOpsPerSec:F2}x");
        Console.WriteLine();
    }

    private static async Task DemoCompressionAsync(List<Employee> employees)
    {
        Console.WriteLine("🗜️ Compression Demo");
        Console.WriteLine("==================");

        // Test compression
        var sampleEmployees = employees.Take(500).ToList();
        var compressedData = await CompressionOptimizer.CompressEntitiesAsync(sampleEmployees);

        Console.WriteLine($"  Original size:    {compressedData.OriginalSize / 1024:F1} KB");
        Console.WriteLine($"  Compressed size:  {compressedData.CompressedSize / 1024:F1} KB");
        Console.WriteLine($"  Compression:      {compressedData.CompressionPercentage:F1}% reduction");
        Console.WriteLine($"  Space saved:      {compressedData.SpaceSavings / 1024:F1} KB");

        // Test decompression
        var decompressedEmployees = await CompressionOptimizer.DecompressEntitiesAsync(compressedData);
        Console.WriteLine($"  Decompressed:     {decompressedEmployees.Count} employees (integrity: ✅)");

        // Compression analysis
        var analysis = await CompressionOptimizer.AnalyzeCompressionAsync(sampleEmployees);
        Console.WriteLine($"  Best compression: {analysis.BestCompressionRatio * 100:F1}%");
        Console.WriteLine($"  Recommended:      {analysis.RecommendedLevel}");
        Console.WriteLine();
    }

    private static async Task DemoQueryCacheAsync(List<Employee> employees)
    {
        Console.WriteLine("🚀 Query Cache Demo");
        Console.WriteLine("==================");

        using var cache = new CompressedQueryCache<Employee>(TimeSpan.FromMinutes(5));

        // Cache some query results
        var engineeringEmployees = employees.Where(e => e.Department == "Engineering").ToList();
        await cache.CacheResultAsync("engineering_dept", engineeringEmployees);

        var marketingEmployees = employees.Where(e => e.Department == "Marketing").ToList();
        await cache.CacheResultAsync("marketing_dept", marketingEmployees);

        // Test cache retrieval
        var cachedEngineering = await cache.TryGetCachedResultAsync("engineering_dept");
        var cachedMarketing = await cache.TryGetCachedResultAsync("marketing_dept");

        Console.WriteLine($"  Cached engineering: {cachedEngineering?.Count ?? 0} employees");
        Console.WriteLine($"  Cached marketing:   {cachedMarketing?.Count ?? 0} employees");

        // Cache statistics
        var stats = cache.GetStatistics();
        Console.WriteLine($"  Cache entries:      {stats.EntryCount}");
        Console.WriteLine($"  Total results:      {stats.TotalCachedResults}");
        Console.WriteLine($"  Compression:        {stats.CompressionRatio * 100:F1}%");
        Console.WriteLine($"  Memory saved:       {stats.MemorySavings / 1024:F1} KB");
        Console.WriteLine();
    }

    private static async Task DemoMemoryOptimizationAsync(List<Employee> employees)
    {
        Console.WriteLine("💾 Memory Optimization Demo");
        Console.WriteLine("===========================");

        var gigaMap = GigaMap.Builder<Employee>()
            .WithBitmapIndex(Indexer.Property<Employee, string>("Department", e => e.Department))
            .Build();

        // Add employees
        await SimplePerformanceOptimizations.BulkAddAsync(gigaMap, employees);

        // Get memory info before optimization
        var memoryBefore = MemoryOptimizer.ForceGarbageCollection();
        Console.WriteLine($"  Before optimization: {memoryBefore.MemoryAfterGC / 1024 / 1024:F1} MB");

        // Apply optimizations
        await SimplePerformanceOptimizations.ApplyBasicOptimizationsAsync(gigaMap);

        // Get memory info after optimization
        var memoryAfter = MemoryOptimizer.ForceGarbageCollection();
        Console.WriteLine($"  After optimization:  {memoryAfter.MemoryAfterGC / 1024 / 1024:F1} MB");
        Console.WriteLine($"  Memory freed:        {memoryAfter.MemoryFreed / 1024 / 1024:F1} MB");

        // Get performance stats
        var stats = SimplePerformanceOptimizations.GetPerformanceStats(gigaMap);
        Console.WriteLine($"  Performance stats:   {stats}");

        // Get recommendations
        var recommendations = SimplePerformanceOptimizations.GetOptimizationRecommendations(gigaMap);
        Console.WriteLine($"  Recommendations:     {recommendations.Count} suggestions");
        foreach (var recommendation in recommendations.Take(3))
        {
            Console.WriteLine($"    💡 {recommendation}");
        }
        Console.WriteLine();
    }

    private static List<Employee> CreateSampleEmployees(int count)
    {
        var departments = new[] { "Engineering", "Marketing", "Sales", "HR", "Finance" };
        var random = new Random(42);
        var employees = new List<Employee>();

        for (int i = 0; i < count; i++)
        {
            employees.Add(new Employee
            {
                Name = $"Employee_{i:D4}",
                Department = departments[i % departments.Length],
                Age = 22 + (i % 43),
                Salary = 40000 + (i % 100000),
                HireDate = DateTime.Now.AddDays(-random.Next(1, 3650))
            });
        }

        return employees;
    }
}

/// <summary>
/// Example usage of the performance demo.
/// </summary>
public class PerformanceDemoExample
{
    public static async Task Main()
    {
        try
        {
            await PerformanceDemo.RunPerformanceDemoAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }

        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
