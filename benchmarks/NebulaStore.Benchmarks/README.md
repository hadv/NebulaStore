# NebulaStore vs PostgreSQL Performance Benchmark

This benchmark suite compares the performance of NebulaStore (embedded object storage) against PostgreSQL (traditional relational database) for large-scale data operations.

## Overview

The benchmark tests the following scenarios:
- **Insert Performance**: Batch insertion of 3 million records
- **Query Performance**: Various query patterns including ID lookups, filtered queries, and complex joins
- **Memory Usage**: Peak memory consumption during operations
- **Storage Efficiency**: Final storage size on disk

## Test Data

The benchmark uses realistic e-commerce data models:
- **Customers**: Configurable count (default 100K) with personal information, addresses, and purchase history
- **Products**: ~1K product catalog with categories, pricing, and inventory
- **Orders**: ~83K orders with status, dates, and shipping information
- **Order Items**: ~250K line items linking orders to products

**Note**: The actual benchmark results above used 100,000 customer records for faster testing. Scale up to 3M records for comprehensive performance analysis.

## Quick Start

### Prerequisites

1. **.NET 9.0 SDK** - Required for running the benchmark
2. **PostgreSQL Server** (optional) - For PostgreSQL comparison benchmarks
3. **Sufficient disk space** - At least 5GB free space recommended

### Running NebulaStore Only

```bash
# Basic benchmark with default settings (3M records)
dotnet run

# Custom record count
dotnet run -- --records 1000000

# Verbose output
dotnet run -- --records 500000 --verbose
```

### Running NebulaStore vs PostgreSQL

```bash
# Full comparison benchmark
dotnet run -- --records 3000000 --postgresql-connection "Host=localhost;Database=benchmark;Username=postchain;Password=yourpassword"

# Quick test with smaller dataset
dotnet run -- --records 100000 --postgresql-connection "Host=localhost;Database=benchmark;Username=postchain;Password=yourpassword" --verbose
```

## Command Line Options

| Option | Short | Description | Default |
|--------|-------|-------------|---------|
| `--records` | `-r` | Number of records to benchmark | 3,000,000 |
| `--batch-size` | `-b` | Batch size for insert operations | 10,000 |
| `--query-count` | `-q` | Number of query operations | 1,000 |
| `--postgresql-connection` | `-p` | PostgreSQL connection string | None |
| `--storage-dir` | `-s` | Storage directory for NebulaStore | benchmark-storage |
| `--warmup` | `-w` | Number of warmup iterations | 3 |
| `--iterations` | `-i` | Number of benchmark iterations | 5 |
| `--verbose` | `-v` | Enable verbose logging | false |
| `--help` | `-h` | Show help message | - |

## PostgreSQL Setup

### Using Docker

```bash
# Start PostgreSQL container
docker run --name postgresql-benchmark \
  -e POSTGRES_PASSWORD=password \
  -e POSTGRES_USER=postchain \
  -e POSTGRES_DB=benchmark \
  -p 5432:5432 \
  -d postgres:16

# Connection string for Docker PostgreSQL
--postgresql-connection "Host=localhost;Port=5432;Database=benchmark;Username=postchain;Password=password"
```

### Using Local PostgreSQL

1. Create a database named `benchmark`
2. Ensure the user has full permissions on the database
3. Use the appropriate connection string for your setup

```bash
# Connect to your existing PostgreSQL
psql -U postchain
CREATE DATABASE benchmark;
```

## Actual Benchmark Results

**Test Configuration**: 100,000 records, 5,000 batch size, 500 queries
**Generated on**: 2025-08-28 00:06:31

### 🏆 Performance Comparison Summary

| **Benchmark** | **Insert (ops/s)** | **Query ID (ops/s)** | **Filter (ops/s)** | **Complex (ops/s)** | **Memory (MB)** | **Storage (MB)** |
|---------------|--------------------|--------------------|-------------------|-------------------|----------------|-----------------|
| **NebulaStore** | 4,985 | 2,716,781 | 12,425,785 | 226,139 | 710.9 | 11,887.8 |
| **PostgreSQL** | 6,912 | 131,607 | 258,871 | 81,346 | 364.2 | 87.7 |

### 📊 Performance Analysis

#### **Winners by Category**
- 🥇 **Insert Speed**: PostgreSQL (1.39x faster)
- 🥇 **Query by ID**: NebulaStore (20.6x faster)
- 🥇 **Filter Queries**: NebulaStore (48x faster)
- 🥇 **Complex Queries**: NebulaStore (2.78x faster)
- 🥇 **Memory Efficiency**: PostgreSQL (1.95x less memory)
- 🥇 **Storage Efficiency**: PostgreSQL (135x less storage)

#### **Key Insights**

**NebulaStore Excels At:**
- ✅ **Read-Heavy Workloads** - Dominates query performance (20-48x faster)
- ✅ **Real-time Analytics** - Exceptional for dashboards and reporting
- ✅ **High-Frequency Lookups** - Perfect for caching scenarios
- ✅ **Complex Data Processing** - Superior analytical query performance

**PostgreSQL Excels At:**
- ✅ **Write-Heavy Workloads** - 39% faster inserts
- ✅ **Storage Efficiency** - Uses 135x less disk space
- ✅ **Memory Efficiency** - Uses half the memory
- ✅ **Production Reliability** - Mature, battle-tested database

#### **Use Case Recommendations**

**Choose NebulaStore When:**
- Read-heavy applications (analytics, reporting, dashboards)
- Real-time data processing requiring ultra-fast queries
- High-frequency lookups (user sessions, caching)
- Complex filtering on large datasets
- Memory is abundant and storage cost is secondary

**Choose PostgreSQL When:**
- Write-heavy applications with frequent inserts/updates
- Storage efficiency is critical (limited disk space)
- Memory constraints exist
- ACID compliance and mature ecosystem needed
- Mixed workloads with balanced read/write patterns

## Benchmark Details

### Insert Operations
- Records are inserted in configurable batches (default: 10,000)
- Measures throughput (operations per second)
- Tracks memory usage during insertion
- Monitors storage growth

### Query Operations

1. **ID Queries**: Direct lookup by primary key
2. **Filter Queries**: Conditional queries (e.g., active customers with high spending)
3. **Complex Queries**: Multi-table joins and aggregations

### Metrics Collected

- **Throughput**: Operations per second for each operation type
- **Latency**: Average response time for queries
- **Memory Usage**: Peak memory consumption
- **Storage Size**: Final storage footprint
- **Resource Efficiency**: Memory and storage per record

## Output Files

The benchmark generates several report formats:

- **Console Output**: Real-time progress and summary
- **JSON Report**: Machine-readable detailed results
- **CSV Export**: Spreadsheet-compatible data
- **HTML Report**: Web-viewable formatted results

All reports are saved to `{storage-dir}/reports/` with timestamps.

## Performance Tips

### For NebulaStore
- Use appropriate batch sizes (10K-50K records)
- Ensure sufficient RAM for large datasets
- Use SSD storage for better I/O performance

### For PostgreSQL
- Configure appropriate shared_buffers size
- Tune work_mem and maintenance_work_mem
- Consider disabling WAL logging for benchmarks
- Optimize checkpoint settings

### Sample PostgreSQL Configuration
```ini
# postgresql.conf
shared_buffers = 2GB
work_mem = 256MB
maintenance_work_mem = 1GB
checkpoint_completion_target = 0.9
wal_buffers = 64MB
```

## Interpreting Results

### Insert Performance
- Higher ops/sec indicates better write performance
- Consider memory usage vs. throughput trade-offs
- Batch size affects performance characteristics

### Query Performance
- ID queries test index efficiency
- Filter queries test scan performance
- Complex queries test join optimization

### Resource Usage
- Memory usage affects scalability
- Storage efficiency impacts long-term costs
- Consider both peak and sustained usage

## Troubleshooting

### Common Issues

1. **Out of Memory**: Reduce record count or batch size
2. **PostgreSQL Connection Failed**: Check connection string and server status
3. **Slow Performance**: Ensure adequate hardware resources
4. **Disk Space**: Monitor available disk space during execution

### Performance Optimization

1. **Use SSD storage** for both NebulaStore and PostgreSQL
2. **Allocate sufficient RAM** (8GB+ recommended for 3M records)
3. **Close other applications** to reduce resource contention
4. **Use dedicated test environment** for consistent results

## Contributing

To add new benchmark scenarios or database implementations:

1. Implement the `IBenchmark` interface
2. Add the benchmark to the runner in `Program.cs`
3. Update documentation and help text
4. Test with various data sizes and configurations

## License

This benchmark suite is part of the NebulaStore project and follows the same licensing terms.
