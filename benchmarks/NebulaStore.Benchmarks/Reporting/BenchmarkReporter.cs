using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NebulaStore.Benchmarks.Reporting;

/// <summary>
/// Generates comprehensive reports from benchmark results.
/// </summary>
public class BenchmarkReporter
{
    private readonly List<BenchmarkSuite> _results;

    public BenchmarkReporter(List<BenchmarkSuite> results)
    {
        _results = results ?? throw new ArgumentNullException(nameof(results));
    }

    /// <summary>
    /// Generate a comprehensive console report.
    /// </summary>
    public void GenerateConsoleReport()
    {
        Console.WriteLine();
        Console.WriteLine("📊 BENCHMARK RESULTS SUMMARY");
        Console.WriteLine("============================");
        Console.WriteLine();

        if (!_results.Any())
        {
            Console.WriteLine("No benchmark results to display.");
            return;
        }

        // Overall comparison table
        GenerateComparisonTable();
        
        // Detailed results for each benchmark
        foreach (var result in _results)
        {
            GenerateDetailedReport(result);
        }

        // Performance analysis
        GeneratePerformanceAnalysis();
    }

    /// <summary>
    /// Generate a comparison table of all benchmarks.
    /// </summary>
    private void GenerateComparisonTable()
    {
        Console.WriteLine("🏆 PERFORMANCE COMPARISON");
        Console.WriteLine(new string('-', 80));
        
        var headers = new[] { "Benchmark", "Insert (ops/s)", "Query ID (ops/s)", "Filter (ops/s)", "Complex (ops/s)", "Memory (MB)", "Storage (MB)" };
        var columnWidths = new[] { 12, 14, 16, 12, 14, 12, 14 };

        // Print headers
        for (int i = 0; i < headers.Length; i++)
        {
            Console.Write(headers[i].PadRight(columnWidths[i]));
        }
        Console.WriteLine();
        Console.WriteLine(new string('-', columnWidths.Sum()));

        // Print data rows
        foreach (var result in _results)
        {
            var row = new[]
            {
                result.Name,
                $"{result.AverageInsertOpsPerSecond:F0}",
                $"{result.AverageQueryByIdOpsPerSecond:F0}",
                $"{result.AverageQueryFilterOpsPerSecond:F0}",
                $"{result.AverageQueryComplexOpsPerSecond:F0}",
                $"{result.PeakMemoryUsage / 1024.0 / 1024:F1}",
                $"{result.FinalStorageSize / 1024.0 / 1024:F1}"
            };

            for (int i = 0; i < row.Length; i++)
            {
                Console.Write(row[i].PadRight(columnWidths[i]));
            }
            Console.WriteLine();
        }
        Console.WriteLine();
    }

    /// <summary>
    /// Generate detailed report for a single benchmark.
    /// </summary>
    private void GenerateDetailedReport(BenchmarkSuite result)
    {
        Console.WriteLine($"📋 DETAILED RESULTS: {result.Name}");
        Console.WriteLine(new string('-', 50));
        
        Console.WriteLine($"Configuration:");
        Console.WriteLine($"  Total Records: {result.Config.TotalRecords:N0}");
        Console.WriteLine($"  Batch Size: {result.Config.BatchSize:N0}");
        Console.WriteLine($"  Query Count: {result.Config.QueryCount:N0}");
        Console.WriteLine();

        Console.WriteLine($"Overall Performance:");
        Console.WriteLine($"  Total Duration: {result.TotalDuration.TotalMinutes:F2} minutes");
        Console.WriteLine($"  Peak Memory Usage: {result.PeakMemoryUsage / 1024.0 / 1024:F2} MB");
        Console.WriteLine($"  Final Storage Size: {result.FinalStorageSize / 1024.0 / 1024:F2} MB");
        Console.WriteLine();

        // Insert performance
        if (result.InsertResults.Any())
        {
            Console.WriteLine($"Insert Performance:");
            Console.WriteLine($"  Total Batches: {result.InsertResults.Count}");
            Console.WriteLine($"  Total Records: {result.InsertResults.Sum(r => r.RecordCount):N0}");
            Console.WriteLine($"  Average Speed: {result.AverageInsertOpsPerSecond:F0} ops/sec");
            Console.WriteLine($"  Total Time: {TimeSpan.FromMilliseconds(result.InsertResults.Sum(r => r.Duration.TotalMilliseconds)).TotalSeconds:F2} seconds");
            
            var fastestInsert = result.InsertResults.OrderByDescending(r => r.OperationsPerSecond).First();
            var slowestInsert = result.InsertResults.OrderBy(r => r.OperationsPerSecond).First();
            Console.WriteLine($"  Fastest Batch: {fastestInsert.OperationsPerSecond:F0} ops/sec");
            Console.WriteLine($"  Slowest Batch: {slowestInsert.OperationsPerSecond:F0} ops/sec");
            Console.WriteLine();
        }

        // Query performance
        if (result.QueryByIdResults.Any())
        {
            Console.WriteLine($"Query by ID Performance:");
            Console.WriteLine($"  Average Speed: {result.AverageQueryByIdOpsPerSecond:F0} ops/sec");
            Console.WriteLine($"  Total Queries: {result.QueryByIdResults.Count}");
            Console.WriteLine($"  Average Time: {result.QueryByIdResults.Average(r => r.Duration.TotalMilliseconds):F2} ms");
            Console.WriteLine();
        }

        if (result.QueryFilterResults.Any())
        {
            Console.WriteLine($"Filter Query Performance:");
            Console.WriteLine($"  Average Speed: {result.AverageQueryFilterOpsPerSecond:F0} ops/sec");
            Console.WriteLine($"  Total Queries: {result.QueryFilterResults.Count}");
            Console.WriteLine($"  Average Results: {result.QueryFilterResults.Average(r => r.RecordCount):F0} records");
            Console.WriteLine($"  Average Time: {result.QueryFilterResults.Average(r => r.Duration.TotalMilliseconds):F2} ms");
            Console.WriteLine();
        }

        if (result.QueryComplexResults.Any())
        {
            Console.WriteLine($"Complex Query Performance:");
            Console.WriteLine($"  Average Speed: {result.AverageQueryComplexOpsPerSecond:F0} ops/sec");
            Console.WriteLine($"  Total Queries: {result.QueryComplexResults.Count}");
            Console.WriteLine($"  Average Results: {result.QueryComplexResults.Average(r => r.RecordCount):F0} records");
            Console.WriteLine($"  Average Time: {result.QueryComplexResults.Average(r => r.Duration.TotalMilliseconds):F2} ms");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Generate performance analysis and recommendations.
    /// </summary>
    private void GeneratePerformanceAnalysis()
    {
        if (_results.Count < 2)
        {
            Console.WriteLine("⚠️  Need at least 2 benchmark results for comparison analysis.");
            return;
        }

        Console.WriteLine("🔍 PERFORMANCE ANALYSIS");
        Console.WriteLine(new string('-', 50));

        var nebulaStore = _results.FirstOrDefault(r => r.Name.Contains("NebulaStore"));
        var mysql = _results.FirstOrDefault(r => r.Name.Contains("MySQL"));

        if (nebulaStore != null && mysql != null)
        {
            AnalyzeComparison("NebulaStore vs MySQL", nebulaStore, mysql);
        }

        // General analysis
        var fastest = _results.OrderByDescending(r => r.AverageInsertOpsPerSecond).First();
        var mostMemoryEfficient = _results.OrderBy(r => r.PeakMemoryUsage).First();
        var mostStorageEfficient = _results.OrderBy(r => r.FinalStorageSize).First();

        Console.WriteLine($"🏆 Winners:");
        Console.WriteLine($"  Fastest Insert: {fastest.Name} ({fastest.AverageInsertOpsPerSecond:F0} ops/sec)");
        Console.WriteLine($"  Most Memory Efficient: {mostMemoryEfficient.Name} ({mostMemoryEfficient.PeakMemoryUsage / 1024.0 / 1024:F1} MB)");
        Console.WriteLine($"  Most Storage Efficient: {mostStorageEfficient.Name} ({mostStorageEfficient.FinalStorageSize / 1024.0 / 1024:F1} MB)");
        Console.WriteLine();
    }

    /// <summary>
    /// Analyze comparison between two specific benchmarks.
    /// </summary>
    private void AnalyzeComparison(string title, BenchmarkSuite first, BenchmarkSuite second)
    {
        Console.WriteLine($"📈 {title}:");
        
        var insertRatio = first.AverageInsertOpsPerSecond / second.AverageInsertOpsPerSecond;
        var queryIdRatio = first.AverageQueryByIdOpsPerSecond / second.AverageQueryByIdOpsPerSecond;
        var memoryRatio = (double)second.PeakMemoryUsage / first.PeakMemoryUsage;
        var storageRatio = (double)second.FinalStorageSize / first.FinalStorageSize;

        Console.WriteLine($"  Insert Performance: {first.Name} is {insertRatio:F2}x {(insertRatio > 1 ? "faster" : "slower")} than {second.Name}");
        Console.WriteLine($"  Query Performance: {first.Name} is {queryIdRatio:F2}x {(queryIdRatio > 1 ? "faster" : "slower")} than {second.Name}");
        Console.WriteLine($"  Memory Usage: {first.Name} uses {memoryRatio:F2}x {(memoryRatio < 1 ? "less" : "more")} memory than {second.Name}");
        Console.WriteLine($"  Storage Size: {first.Name} uses {storageRatio:F2}x {(storageRatio < 1 ? "less" : "more")} storage than {second.Name}");
        Console.WriteLine();
    }

    /// <summary>
    /// Export results to JSON file.
    /// </summary>
    public async Task ExportToJsonAsync(string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(_results, options);
        await File.WriteAllTextAsync(filePath, json);
        
        Console.WriteLine($"📄 Results exported to: {filePath}");
    }

    /// <summary>
    /// Export results to CSV file.
    /// </summary>
    public async Task ExportToCsvAsync(string filePath)
    {
        var csv = new StringBuilder();
        
        // Headers
        csv.AppendLine("Benchmark,TotalRecords,InsertOpsPerSec,QueryIdOpsPerSec,QueryFilterOpsPerSec,QueryComplexOpsPerSec,PeakMemoryMB,StorageSizeMB,TotalDurationMinutes");
        
        // Data rows
        foreach (var result in _results)
        {
            csv.AppendLine($"{result.Name},{result.Config.TotalRecords},{result.AverageInsertOpsPerSecond:F2},{result.AverageQueryByIdOpsPerSecond:F2},{result.AverageQueryFilterOpsPerSecond:F2},{result.AverageQueryComplexOpsPerSecond:F2},{result.PeakMemoryUsage / 1024.0 / 1024:F2},{result.FinalStorageSize / 1024.0 / 1024:F2},{result.TotalDuration.TotalMinutes:F2}");
        }
        
        await File.WriteAllTextAsync(filePath, csv.ToString());
        Console.WriteLine($"📊 Results exported to: {filePath}");
    }

    /// <summary>
    /// Generate a simple HTML report.
    /// </summary>
    public async Task ExportToHtmlAsync(string filePath)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><title>Benchmark Results</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
        html.AppendLine("table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
        html.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        html.AppendLine("th { background-color: #f2f2f2; }");
        html.AppendLine(".number { text-align: right; }");
        html.AppendLine("</style></head><body>");
        
        html.AppendLine("<h1>Performance Benchmark Results</h1>");
        html.AppendLine($"<p>Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        
        html.AppendLine("<h2>Summary</h2>");
        html.AppendLine("<table>");
        html.AppendLine("<tr><th>Benchmark</th><th>Insert (ops/s)</th><th>Query ID (ops/s)</th><th>Filter (ops/s)</th><th>Complex (ops/s)</th><th>Memory (MB)</th><th>Storage (MB)</th></tr>");
        
        foreach (var result in _results)
        {
            html.AppendLine($"<tr>");
            html.AppendLine($"<td>{result.Name}</td>");
            html.AppendLine($"<td class='number'>{result.AverageInsertOpsPerSecond:F0}</td>");
            html.AppendLine($"<td class='number'>{result.AverageQueryByIdOpsPerSecond:F0}</td>");
            html.AppendLine($"<td class='number'>{result.AverageQueryFilterOpsPerSecond:F0}</td>");
            html.AppendLine($"<td class='number'>{result.AverageQueryComplexOpsPerSecond:F0}</td>");
            html.AppendLine($"<td class='number'>{result.PeakMemoryUsage / 1024.0 / 1024:F1}</td>");
            html.AppendLine($"<td class='number'>{result.FinalStorageSize / 1024.0 / 1024:F1}</td>");
            html.AppendLine($"</tr>");
        }
        
        html.AppendLine("</table>");
        html.AppendLine("</body></html>");
        
        await File.WriteAllTextAsync(filePath, html.ToString());
        Console.WriteLine($"🌐 HTML report exported to: {filePath}");
    }
}
