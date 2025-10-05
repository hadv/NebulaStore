# Kafka AFS Quick Start Guide

**Status:** Planned Feature (Not Yet Implemented)

This guide will help you get started with the Kafka AFS adapter once it's implemented.

---

## Prerequisites

### 1. Kafka Cluster

You need a running Kafka cluster. Choose one:

**Option A: Local Development (Docker)**
```bash
# docker-compose.yml
version: '3'
services:
  kafka:
    image: confluentinc/cp-kafka:7.5.0
    ports:
      - "9092:9092"
    environment:
      KAFKA_BROKER_ID: 1
      KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT
      KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://localhost:9092
      KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1
      KAFKA_TRANSACTION_STATE_LOG_MIN_ISR: 1
      KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR: 1
      KAFKA_PROCESS_ROLES: broker,controller
      KAFKA_NODE_ID: 1
      KAFKA_CONTROLLER_QUORUM_VOTERS: 1@localhost:9093
      KAFKA_LISTENERS: PLAINTEXT://0.0.0.0:9092,CONTROLLER://0.0.0.0:9093
      KAFKA_CONTROLLER_LISTENER_NAMES: CONTROLLER
      KAFKA_LOG_DIRS: /tmp/kraft-combined-logs

# Start Kafka
docker-compose up -d
```

**Option B: Confluent Cloud**
```bash
# Sign up at https://confluent.cloud
# Get bootstrap servers and API credentials
```

**Option C: AWS MSK**
```bash
# Create MSK cluster in AWS Console
# Get bootstrap servers
```

### 2. NuGet Package

```bash
dotnet add package NebulaStore.Afs.Kafka
```

---

## Basic Usage

### 1. Simple Configuration

```csharp
using NebulaStore.Storage.Embedded;
using NebulaStore.Afs.Kafka;

// Start with Kafka backend
using var storage = EmbeddedStorage.StartWithKafka(
    bootstrapServers: "localhost:9092",
    clientId: "my-app"
);

// Use normally
var root = storage.Root<MyData>();
root.Value = "Hello, Kafka!";
storage.StoreRoot();
```

### 2. Advanced Configuration

```csharp
using NebulaStore.Storage.Embedded;
using NebulaStore.Afs.Kafka;
using NebulaStore.Afs.Blobstore;
using Confluent.Kafka;

// Configure Kafka
var kafkaConfig = new KafkaConfiguration
{
    BootstrapServers = "localhost:9092",
    ClientId = "nebulastore-app",
    
    // Performance tuning
    MaxMessageBytes = 1_000_000,  // 1MB chunks
    Compression = CompressionType.Snappy,
    EnableIdempotence = true,
    
    // Timeouts
    RequestTimeout = TimeSpan.FromMinutes(1),
    
    // Additional Kafka settings
    AdditionalSettings = new Dictionary<string, string>
    {
        ["acks"] = "all",
        ["retries"] = "3",
        ["max.in.flight.requests.per.connection"] = "5"
    }
};

// Create connector
using var connector = KafkaConnector.New(kafkaConfig);

// Create file system
using var fileSystem = BlobStoreFileSystem.New(connector);

// Configure storage
var storageConfig = EmbeddedStorageConfiguration.New()
    .SetStorageFileSystem(fileSystem)
    .SetChannelCount(4)
    .Build();

// Start storage
using var storage = EmbeddedStorage.Start(storageConfig);
```

---

## Configuration Options

### KafkaConfiguration Properties

| Property | Default | Description |
|----------|---------|-------------|
| `BootstrapServers` | `localhost:9092` | Kafka broker addresses |
| `ClientId` | `nebulastore-kafka` | Client identifier |
| `MaxMessageBytes` | `1000000` | Blob chunk size (1MB) |
| `RequestTimeout` | `1 minute` | Request timeout |
| `EnableIdempotence` | `true` | Prevent duplicate writes |
| `Compression` | `None` | Compression type |
| `AdditionalSettings` | `{}` | Extra Kafka settings |

### Compression Types

```csharp
CompressionType.None      // No compression (fastest)
CompressionType.Gzip      // Good compression, slower
CompressionType.Snappy    // Balanced (recommended)
CompressionType.Lz4       // Fast compression
CompressionType.Zstd      // Best compression
```

---

## Common Patterns

### 1. Production Configuration

```csharp
var kafkaConfig = new KafkaConfiguration
{
    BootstrapServers = "kafka1:9092,kafka2:9092,kafka3:9092",
    ClientId = "nebulastore-prod",
    
    // Reliability
    EnableIdempotence = true,
    AdditionalSettings = new Dictionary<string, string>
    {
        ["acks"] = "all",                    // Wait for all replicas
        ["retries"] = "10",                  // Retry on failure
        ["retry.backoff.ms"] = "100",        // Backoff between retries
        ["max.in.flight.requests.per.connection"] = "5"
    },
    
    // Performance
    Compression = CompressionType.Snappy,
    MaxMessageBytes = 1_000_000,
    
    // Timeouts
    RequestTimeout = TimeSpan.FromMinutes(2)
};
```

### 2. High-Throughput Configuration

```csharp
var kafkaConfig = new KafkaConfiguration
{
    BootstrapServers = "localhost:9092",
    
    // Maximize throughput
    AdditionalSettings = new Dictionary<string, string>
    {
        ["batch.size"] = "32768",            // 32KB batches
        ["linger.ms"] = "10",                // Wait 10ms for batching
        ["compression.type"] = "lz4",        // Fast compression
        ["buffer.memory"] = "67108864"       // 64MB buffer
    }
};
```

### 3. Low-Latency Configuration

```csharp
var kafkaConfig = new KafkaConfiguration
{
    BootstrapServers = "localhost:9092",
    
    // Minimize latency
    AdditionalSettings = new Dictionary<string, string>
    {
        ["linger.ms"] = "0",                 // Send immediately
        ["batch.size"] = "1",                // No batching
        ["compression.type"] = "none"        // No compression
    }
};
```

### 4. Secure Connection (SSL/SASL)

```csharp
var kafkaConfig = new KafkaConfiguration
{
    BootstrapServers = "kafka.example.com:9093",
    
    // SSL/SASL authentication
    AdditionalSettings = new Dictionary<string, string>
    {
        ["security.protocol"] = "SASL_SSL",
        ["sasl.mechanism"] = "PLAIN",
        ["sasl.username"] = "your-username",
        ["sasl.password"] = "your-password",
        ["ssl.ca.location"] = "/path/to/ca-cert"
    }
};
```

### 5. Confluent Cloud

```csharp
var kafkaConfig = new KafkaConfiguration
{
    BootstrapServers = "pkc-xxxxx.us-east-1.aws.confluent.cloud:9092",
    
    AdditionalSettings = new Dictionary<string, string>
    {
        ["security.protocol"] = "SASL_SSL",
        ["sasl.mechanism"] = "PLAIN",
        ["sasl.username"] = "your-api-key",
        ["sasl.password"] = "your-api-secret"
    }
};
```

---

## Monitoring & Debugging

### 1. Enable Logging

```csharp
var kafkaConfig = new KafkaConfiguration
{
    BootstrapServers = "localhost:9092",
    
    AdditionalSettings = new Dictionary<string, string>
    {
        ["debug"] = "broker,topic,msg"  // Enable debug logging
    }
};
```

### 2. Check Topic Status

```bash
# List topics
kafka-topics --bootstrap-server localhost:9092 --list

# Describe topic
kafka-topics --bootstrap-server localhost:9092 --describe --topic storage_channel_000_file_000001

# Check consumer lag
kafka-consumer-groups --bootstrap-server localhost:9092 --describe --group nebulastore-app
```

### 3. Monitor Metrics

```csharp
// Access connector metrics (future feature)
var metrics = connector.GetMetrics();
Console.WriteLine($"Messages produced: {metrics.MessagesProduced}");
Console.WriteLine($"Messages consumed: {metrics.MessagesConsumed}");
Console.WriteLine($"Topics created: {metrics.TopicsCreated}");
```

---

## Troubleshooting

### Problem: Connection Timeout

```
Error: Failed to connect to Kafka broker
```

**Solution:**
1. Check Kafka is running: `docker ps` or `telnet localhost 9092`
2. Verify bootstrap servers address
3. Check firewall/network settings
4. Increase `RequestTimeout`

### Problem: Topic Creation Failed

```
Error: Failed to create topic
```

**Solution:**
1. Check Kafka broker has `auto.create.topics.enable=true`
2. Verify user has topic creation permissions
3. Check topic naming constraints (alphanumeric, `.`, `_`, `-` only)

### Problem: Slow Performance

```
Warning: Write latency > 1 second
```

**Solution:**
1. Enable compression: `Compression = CompressionType.Snappy`
2. Increase batch size: `batch.size = 32768`
3. Add more Kafka brokers
4. Increase `MaxMessageBytes` for larger chunks
5. Check network latency

### Problem: Index Corruption

```
Error: Blob metadata not found
```

**Solution:**
1. Rebuild index from data topics (future feature)
2. Check index topic exists: `__<topic>_index`
3. Verify index topic has data

---

## Best Practices

### 1. Topic Management

```csharp
// Use topic retention policies
var kafkaConfig = new KafkaConfiguration
{
    AdditionalSettings = new Dictionary<string, string>
    {
        ["retention.ms"] = "604800000",      // 7 days
        ["retention.bytes"] = "1073741824"   // 1GB per partition
    }
};
```

### 2. Error Handling

```csharp
try
{
    storage.StoreRoot();
}
catch (KafkaException ex)
{
    // Handle Kafka-specific errors
    Console.WriteLine($"Kafka error: {ex.Error.Reason}");
    
    // Retry logic
    if (ex.Error.IsRetriable)
    {
        // Retry operation
    }
}
```

### 3. Resource Cleanup

```csharp
// Always dispose properly
using var connector = KafkaConnector.New(config);
using var fileSystem = BlobStoreFileSystem.New(connector);
using var storage = EmbeddedStorage.Start(storageConfig);

// Or manual cleanup
try
{
    // Use storage
}
finally
{
    storage?.Dispose();
    fileSystem?.Dispose();
    connector?.Dispose();
}
```

### 4. Testing

```csharp
// Use Docker for integration tests
[Fact]
public async Task TestKafkaStorage()
{
    // Start Kafka container
    await using var kafka = new KafkaContainer();
    await kafka.StartAsync();
    
    // Configure storage
    var config = new KafkaConfiguration
    {
        BootstrapServers = kafka.BootstrapServers
    };
    
    // Test storage operations
    using var storage = EmbeddedStorage.StartWithKafka(config.BootstrapServers);
    // ... test code
}
```

---

## Migration Guide

### From NIO to Kafka

```csharp
// 1. Export data from NIO
var nioStorage = EmbeddedStorage.Start("./nio-storage");
var data = nioStorage.Root<MyData>();

// 2. Import to Kafka
var kafkaStorage = EmbeddedStorage.StartWithKafka("localhost:9092");
kafkaStorage.SetRoot(data);
kafkaStorage.StoreRoot();
```

### From Redis to Kafka

```csharp
// 1. Read from Redis
var redisStorage = EmbeddedStorage.StartWithRedis("localhost:6379");
var data = redisStorage.Root<MyData>();

// 2. Write to Kafka
var kafkaStorage = EmbeddedStorage.StartWithKafka("localhost:9092");
kafkaStorage.SetRoot(data);
kafkaStorage.StoreRoot();
```

---

## Performance Tuning

### Benchmark Your Configuration

```csharp
using System.Diagnostics;

var sw = Stopwatch.StartNew();

// Write test
for (int i = 0; i < 10000; i++)
{
    storage.Store(new MyData { Id = i });
}
storage.Commit();

sw.Stop();
Console.WriteLine($"Write: {10000.0 / sw.Elapsed.TotalSeconds:F0} ops/sec");

// Read test
sw.Restart();
for (int i = 0; i < 10000; i++)
{
    var data = storage.Root<MyData>();
}
sw.Stop();
Console.WriteLine($"Read: {10000.0 / sw.Elapsed.TotalSeconds:F0} ops/sec");
```

### Expected Performance

| Operation | Throughput | Latency |
|-----------|------------|---------|
| Write | 10K-100K ops/sec | 5-20ms |
| Read | 5K-50K ops/sec | 10-50ms |
| Commit | 1K-10K ops/sec | 50-200ms |

---

## Additional Resources

- **Kafka Documentation:** https://kafka.apache.org/documentation/
- **Confluent.Kafka Docs:** https://docs.confluent.io/kafka-clients/dotnet/
- **NebulaStore AFS Guide:** /docs/KafkaAfsFeasibility.md
- **Comparison Guide:** /docs/KafkaAfsComparison.md

---

## Support

- **GitHub Issues:** https://github.com/hadv/NebulaStore/issues
- **Discussions:** https://github.com/hadv/NebulaStore/discussions
- **Kafka Community:** https://kafka.apache.org/community

---

**Note:** This is a planned feature. Check the project roadmap for implementation status.

