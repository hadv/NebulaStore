using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using NebulaStore.GigaMap;
using NebulaStore.GigaMap.Performance;
using NebulaStore.Storage.Embedded;

namespace NebulaStore.Examples;

/// <summary>
/// Example demonstrating compression features for GigaMap performance optimization.
/// </summary>
public class CompressionExample
{
    public class Employee
    {
        public string Name { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Age { get; set; }
        public decimal Salary { get; set; }
        public string Position { get; set; } = string.Empty;
        public DateTime HireDate { get; set; }
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public List<string> Skills { get; set; } = new();
    }

    public static async Task RunCompressionExampleAsync()
    {
        Console.WriteLine("🗜️ GigaMap Compression Performance Example");
        Console.WriteLine("==========================================");

        // Create storage and GigaMap
        using var storage = EmbeddedStorageConfiguration.Builder()
            .SetStorageDirectory("compression_example_data")
            .SetGigaMapEnabled(true)
            .Build()
            .CreateEmbeddedStorage();

        var gigaMap = storage.CreateGigaMap<Employee>()
            .WithIndex(Indexer.Property<Employee, string>("Department", e => e.Department))
            .WithIndex(Indexer.Property<Employee, string>("Position", e => e.Position))
            .Build();

        // Generate sample data
        Console.WriteLine("📊 Generating sample employee data...");
        var employees = GenerateSampleEmployees(5000);
        
        // Add employees to GigaMap
        Console.WriteLine("➕ Adding employees to GigaMap...");
        var entityIds = await SimplePerformanceOptimizations.BulkAddAsync(gigaMap, employees);
        Console.WriteLine($"✅ Added {entityIds.Count} employees");

        // Demonstrate compression analysis
        await DemonstrateCompressionAnalysisAsync(employees);

        // Demonstrate memory savings estimation
        await DemonstrateMemorySavingsAsync(gigaMap);

        // Demonstrate compressed backups
        await DemonstrateCompressedBackupsAsync(gigaMap);

        // Demonstrate compressed query caching
        await DemonstrateCompressedQueryCachingAsync(gigaMap);

        // Apply comprehensive optimizations
        await SimplePerformanceOptimizations.ApplyComprehensiveOptimizationsAsync(gigaMap);

        Console.WriteLine("\n🎉 Compression example completed!");
    }

    private static async Task DemonstrateCompressionAnalysisAsync(List<Employee> employees)
    {
        Console.WriteLine("\n🔍 Analyzing Compression Effectiveness");
        Console.WriteLine("=====================================");

        // Analyze compression for different levels
        var analysis = await CompressionOptimizer.AnalyzeCompressionAsync(employees.Take(1000));
        
        Console.WriteLine($"📈 {analysis}");
        Console.WriteLine($"🏆 Best compression ratio: {analysis.BestCompressionRatio * 100:F1}%");
        Console.WriteLine($"⚡ Fastest compression: {analysis.FastestCompressionTime.TotalMilliseconds:F1}ms");

        // Show results for each compression level
        foreach (var kvp in analysis.LevelResults)
        {
            var level = kvp.Key;
            var result = kvp.Value;
            Console.WriteLine($"  {level}: {result}");
        }
    }

    private static async Task DemonstrateMemorySavingsAsync(IGigaMap<Employee> gigaMap)
    {
        Console.WriteLine("\n💾 Memory Savings Estimation");
        Console.WriteLine("============================");

        var estimate = await CompressionOptimizer.EstimateMemorySavingsAsync(gigaMap, sampleSize: 500);
        
        Console.WriteLine($"📊 {estimate}");
        Console.WriteLine($"💰 Potential savings: {estimate.EstimatedMemorySavings / 1024 / 1024:F1} MB");
        Console.WriteLine($"📉 Compression ratio: {estimate.EstimatedCompressionRatio * 100:F1}%");
        Console.WriteLine($"🎯 Recommended level: {estimate.RecommendedCompressionLevel}");
    }

    private static async Task DemonstrateCompressedBackupsAsync(IGigaMap<Employee> gigaMap)
    {
        Console.WriteLine("\n📦 Compressed Backup Creation");
        Console.WriteLine("=============================");

        // Create compressed backup
        var backup = await SimplePerformanceOptimizations.CreateCompressedBackupAsync(
            gigaMap, CompressionLevel.Optimal);
        
        Console.WriteLine($"💾 {backup}");
        Console.WriteLine($"📁 Original size: {backup.OriginalSize / 1024:F1} KB");
        Console.WriteLine($"🗜️ Compressed size: {backup.CompressedSize / 1024:F1} KB");
        Console.WriteLine($"💡 Space savings: {backup.SpaceSavings / 1024:F1} KB ({backup.CompressionPercentage:F1}%)");

        // Test restoration (create a new GigaMap for testing)
        var testGigaMap = gigaMap.EqualityComparer != null 
            ? new DefaultGigaMap<Employee>(gigaMap.EqualityComparer)
            : new DefaultGigaMap<Employee>();

        var restoredIds = await SimplePerformanceOptimizations.RestoreFromCompressedBackupAsync(
            testGigaMap, backup);
        
        Console.WriteLine($"✅ Restored {restoredIds.Count} employees from backup");
    }

    private static async Task DemonstrateCompressedQueryCachingAsync(IGigaMap<Employee> gigaMap)
    {
        Console.WriteLine("\n🚀 Compressed Query Caching");
        Console.WriteLine("===========================");

        using var cache = new CompressedQueryCache<Employee>(
            defaultExpiry: TimeSpan.FromMinutes(10),
            maxCacheSize: 100);

        // Execute and cache some queries
        var queries = new[]
        {
            ("engineering_dept", "Department", "Engineering"),
            ("marketing_dept", "Department", "Marketing"),
            ("senior_positions", "Position", "Senior Developer")
        };

        foreach (var (signature, indexName, value) in queries)
        {
            Console.WriteLine($"🔍 Executing query: {indexName} = {value}");
            
            // Try to get from cache first
            var cachedResult = await cache.TryGetCachedResultAsync(signature);
            
            if (cachedResult != null)
            {
                Console.WriteLine($"⚡ Retrieved {cachedResult.Count} results from cache");
            }
            else
            {
                // Execute query and cache result
                var results = gigaMap.Query(indexName, value).Execute();
                await cache.CacheResultAsync(signature, results);
                Console.WriteLine($"💾 Executed query and cached {results.Count} results");
            }
        }

        // Show cache statistics
        var stats = cache.GetStatistics();
        Console.WriteLine($"\n📈 Cache Statistics:");
        Console.WriteLine($"  {stats}");
        Console.WriteLine($"  Memory efficiency: {(1 - stats.CompressionRatio) * 100:F1}% space saved");
    }

    private static List<Employee> GenerateSampleEmployees(int count)
    {
        var random = new Random(42); // Fixed seed for reproducible results
        var departments = new[] { "Engineering", "Marketing", "Sales", "HR", "Finance", "Operations" };
        var positions = new[] { "Junior Developer", "Senior Developer", "Team Lead", "Manager", "Director", "VP" };
        var skills = new[] { "C#", "Python", "JavaScript", "SQL", "React", "Azure", "AWS", "Docker", "Kubernetes" };
        
        var employees = new List<Employee>();
        
        for (int i = 0; i < count; i++)
        {
            var employee = new Employee
            {
                Name = $"Employee {i + 1}",
                Department = departments[random.Next(departments.Length)],
                Email = $"employee{i + 1}@company.com",
                Age = random.Next(22, 65),
                Salary = random.Next(40000, 200000),
                Position = positions[random.Next(positions.Length)],
                HireDate = DateTime.Now.AddDays(-random.Next(1, 3650)), // Up to 10 years ago
                Address = $"{random.Next(1, 9999)} Main St, City {random.Next(1, 100)}",
                Phone = $"555-{random.Next(100, 999)}-{random.Next(1000, 9999)}",
                Skills = skills.OrderBy(x => random.Next()).Take(random.Next(2, 6)).ToList()
            };
            
            employees.Add(employee);
        }
        
        return employees;
    }
}

/// <summary>
/// Extension methods for compression examples.
/// </summary>
public static class CompressionExampleExtensions
{
    /// <summary>
    /// Formats bytes in a human-readable format.
    /// </summary>
    public static string FormatBytes(this long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        
        return $"{number:n1} {suffixes[counter]}";
    }

    /// <summary>
    /// Formats a compression ratio as a percentage.
    /// </summary>
    public static string FormatCompressionRatio(this double ratio)
    {
        return $"{ratio * 100:F1}%";
    }
}
