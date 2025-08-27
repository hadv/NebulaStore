using System;
using System.IO;
using System.Threading.Tasks;
using NebulaStore.Benchmarks;
using NebulaStore.Benchmarks.Implementations;
using NebulaStore.Benchmarks.Reporting;

namespace NebulaStore.Benchmarks;

/// <summary>
/// Console application for running NebulaStore vs MySQL performance benchmarks.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 NebulaStore vs MySQL Performance Benchmark");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        try
        {
            // Parse command line arguments
            var config = ParseArguments(args);
            
            // Display configuration
            DisplayConfiguration(config);
            
            // Confirm before running
            if (!ConfirmExecution(config))
            {
                Console.WriteLine("Benchmark cancelled by user.");
                return;
            }

            // Create benchmark runner
            using var runner = new BenchmarkRunner(config);

            // Add NebulaStore benchmark
            runner.AddBenchmark(new NebulaStoreBenchmark());

            // Add PostgreSQL benchmark if connection string is provided
            if (!string.IsNullOrEmpty(config.ConnectionString))
            {
                runner.AddBenchmark(new PostgreSqlBenchmark());
            }
            else
            {
                Console.WriteLine("⚠️  PostgreSQL connection string not provided. Running NebulaStore benchmark only.");
                Console.WriteLine("   Use --postgresql-connection to include PostgreSQL benchmark.");
                Console.WriteLine();
            }

            // Run benchmarks
            var results = await runner.RunAllBenchmarksAsync();

            // Generate reports
            await GenerateReportsAsync(results, config);

            Console.WriteLine("🎉 Benchmark completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            if (args.Contains("--verbose"))
            {
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Parse command line arguments into configuration.
    /// </summary>
    private static BenchmarkConfig ParseArguments(string[] args)
    {
        var config = new BenchmarkConfig();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--records":
                case "-r":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var records))
                    {
                        config.TotalRecords = records;
                        i++;
                    }
                    break;

                case "--batch-size":
                case "-b":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var batchSize))
                    {
                        config.BatchSize = batchSize;
                        i++;
                    }
                    break;

                case "--query-count":
                case "-q":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var queryCount))
                    {
                        config.QueryCount = queryCount;
                        i++;
                    }
                    break;

                case "--postgresql-connection":
                case "--postgres-connection":
                case "-p":
                    if (i + 1 < args.Length)
                    {
                        config.ConnectionString = args[i + 1];
                        i++;
                    }
                    break;

                case "--storage-dir":
                case "-s":
                    if (i + 1 < args.Length)
                    {
                        config.StorageDirectory = args[i + 1];
                        i++;
                    }
                    break;

                case "--warmup":
                case "-w":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var warmup))
                    {
                        config.WarmupIterations = warmup;
                        i++;
                    }
                    break;

                case "--iterations":
                case "-i":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var iterations))
                    {
                        config.BenchmarkIterations = iterations;
                        i++;
                    }
                    break;

                case "--verbose":
                case "-v":
                    config.VerboseLogging = true;
                    break;

                case "--help":
                case "-h":
                    DisplayHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        return config;
    }

    /// <summary>
    /// Display help information.
    /// </summary>
    private static void DisplayHelp()
    {
        Console.WriteLine("NebulaStore vs MySQL Performance Benchmark");
        Console.WriteLine();
        Console.WriteLine("Usage: NebulaStore.Benchmarks [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -r, --records <number>        Number of records to benchmark (default: 3,000,000)");
        Console.WriteLine("  -b, --batch-size <number>     Batch size for insert operations (default: 10,000)");
        Console.WriteLine("  -q, --query-count <number>    Number of query operations (default: 1,000)");
        Console.WriteLine("  -p, --postgresql-connection <str>  PostgreSQL connection string");
        Console.WriteLine("      --postgres-connection <str>    Alias for --postgresql-connection");
        Console.WriteLine("  -s, --storage-dir <path>      Storage directory for NebulaStore (default: benchmark-storage)");
        Console.WriteLine("  -w, --warmup <number>         Number of warmup iterations (default: 3)");
        Console.WriteLine("  -i, --iterations <number>     Number of benchmark iterations (default: 5)");
        Console.WriteLine("  -v, --verbose                 Enable verbose logging");
        Console.WriteLine("  -h, --help                    Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Run NebulaStore benchmark only with 1M records");
        Console.WriteLine("  NebulaStore.Benchmarks --records 1000000");
        Console.WriteLine();
        Console.WriteLine("  # Run both NebulaStore and PostgreSQL benchmarks");
        Console.WriteLine("  NebulaStore.Benchmarks --records 3000000 --postgresql-connection \"Host=localhost;Database=benchmark;Username=postchain;Password=yourpassword\"");
        Console.WriteLine();
        Console.WriteLine("  # Quick test with smaller dataset");
        Console.WriteLine("  NebulaStore.Benchmarks --records 100000 --batch-size 5000 --query-count 500 --verbose");
    }

    /// <summary>
    /// Display current configuration.
    /// </summary>
    private static void DisplayConfiguration(BenchmarkConfig config)
    {
        Console.WriteLine("📋 Benchmark Configuration:");
        Console.WriteLine($"   Total Records: {config.TotalRecords:N0}");
        Console.WriteLine($"   Batch Size: {config.BatchSize:N0}");
        Console.WriteLine($"   Query Count: {config.QueryCount:N0}");
        Console.WriteLine($"   Storage Directory: {config.StorageDirectory}");
        Console.WriteLine($"   Warmup Iterations: {config.WarmupIterations}");
        Console.WriteLine($"   Benchmark Iterations: {config.BenchmarkIterations}");
        Console.WriteLine($"   Verbose Logging: {config.VerboseLogging}");
        
        if (!string.IsNullOrEmpty(config.ConnectionString))
        {
            Console.WriteLine($"   PostgreSQL Connection: {MaskConnectionString(config.ConnectionString)}");
        }
        else
        {
            Console.WriteLine($"   PostgreSQL Connection: Not configured");
        }
        
        Console.WriteLine();
    }

    /// <summary>
    /// Confirm execution with user.
    /// </summary>
    private static bool ConfirmExecution(BenchmarkConfig config)
    {
        var estimatedTime = EstimateExecutionTime(config);
        
        Console.WriteLine($"⏱️  Estimated execution time: {estimatedTime.TotalMinutes:F1} minutes");
        Console.WriteLine($"💾 Estimated storage usage: ~{EstimateStorageUsage(config) / 1024 / 1024:F0} MB");
        Console.WriteLine();
        
        Console.Write("Do you want to proceed? (y/N): ");
        var response = Console.ReadLine()?.Trim().ToLower();
        return response == "y" || response == "yes";
    }

    /// <summary>
    /// Estimate execution time based on configuration.
    /// </summary>
    private static TimeSpan EstimateExecutionTime(BenchmarkConfig config)
    {
        // Rough estimates based on typical performance
        var insertTime = config.TotalRecords / 50000.0; // ~50k inserts per second
        var queryTime = config.QueryCount * config.BenchmarkIterations / 1000.0; // ~1k queries per second
        var setupTime = 2; // Setup and cleanup time in minutes
        
        var totalMinutes = (insertTime + queryTime + setupTime) * (string.IsNullOrEmpty(config.ConnectionString) ? 1 : 2);
        return TimeSpan.FromMinutes(totalMinutes);
    }

    /// <summary>
    /// Estimate storage usage based on configuration.
    /// </summary>
    private static long EstimateStorageUsage(BenchmarkConfig config)
    {
        // Rough estimate: 500 bytes per customer record
        return config.TotalRecords * 500L;
    }

    /// <summary>
    /// Generate various report formats.
    /// </summary>
    private static async Task GenerateReportsAsync(List<BenchmarkSuite> results, BenchmarkConfig config)
    {
        var reporter = new BenchmarkReporter(results);
        
        // Console report
        reporter.GenerateConsoleReport();
        
        // Create reports directory
        var reportsDir = Path.Combine(config.StorageDirectory, "reports");
        Directory.CreateDirectory(reportsDir);
        
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        
        // Export to different formats
        await reporter.ExportToJsonAsync(Path.Combine(reportsDir, $"benchmark-results-{timestamp}.json"));
        await reporter.ExportToCsvAsync(Path.Combine(reportsDir, $"benchmark-results-{timestamp}.csv"));
        await reporter.ExportToHtmlAsync(Path.Combine(reportsDir, $"benchmark-results-{timestamp}.html"));
        
        Console.WriteLine($"📁 Reports saved to: {reportsDir}");
    }

    /// <summary>
    /// Mask sensitive information in connection string.
    /// </summary>
    private static string MaskConnectionString(string connectionString)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            connectionString, 
            @"(password|pwd)=([^;]*)", 
            "$1=***", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
