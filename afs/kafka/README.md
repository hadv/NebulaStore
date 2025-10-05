# NebulaStore AFS Kafka Adapter

Apache Kafka adapter for NebulaStore Abstract File System (AFS). Provides event-driven storage with built-in audit trails, time-travel capabilities, and multi-datacenter replication.

## Overview

The Kafka AFS adapter stores files as Kafka topics, enabling:
- **Event Streaming**: Storage operations become observable Kafka events
- **Audit Trail**: Complete immutable log of all changes
- **Time Travel**: Read data as it existed at any point in time
- **Multi-Datacenter Replication**: Built-in with Kafka MirrorMaker
- **High Throughput**: 100K+ messages/second

## Architecture

### Storage Model

```
File → Kafka Topic (data) + Index Topic (metadata)
  ├── Data Topic: Contains file chunks (blobs) as Kafka messages
  └── Index Topic: Contains blob metadata (partition, offset, range)
```

**Key Concepts:**
- Each file is stored as a Kafka topic
- Files are split into 1MB chunks (configurable)
- Each chunk is a Kafka message
- Blob metadata is stored in a separate index topic
- File system index tracks all files

### Topic Naming

File paths are converted to valid Kafka topic names:
- Path separators (`/`) → underscores (`_`)
- Invalid characters → underscores
- Example: `storage/channel_000/file_001` → `storage_channel_000_file_001`

## Installation

### NuGet Package

```bash
dotnet add package NebulaStore.Afs.Kafka
```

### Prerequisites

You need a running Kafka cluster:
- Apache Kafka 0.8+
- Confluent Platform
- Confluent Cloud
- AWS MSK
- Azure Event Hubs (Kafka-compatible)

## Usage

### Basic Usage

```csharp
using NebulaStore.Storage.Embedded;
using NebulaStore.Afs.Kafka;

// Configure Kafka
var kafkaConfig = KafkaConfiguration.New("localhost:9092");

// Create connector
using var connector = KafkaConnector.New(kafkaConfig);

// Create file system
using var fileSystem = BlobStoreFileSystem.New(connector);

// Use with EmbeddedStorage
var storageConfig = EmbeddedStorageConfiguration.New()
    .SetStorageFileSystem(fileSystem)
    .Build();

using var storage = EmbeddedStorage.Start(storageConfig);
```

### Production Configuration

```csharp
var kafkaConfig = KafkaConfiguration.Production(
    bootstrapServers: "kafka1:9092,kafka2:9092,kafka3:9092",
    clientId: "nebulastore-prod"
);

kafkaConfig.Compression = CompressionType.Snappy;
kafkaConfig.MaxMessageBytes = 1_000_000; // 1MB chunks

using var connector = KafkaConnector.New(kafkaConfig);
```

### Development Configuration

```csharp
var kafkaConfig = KafkaConfiguration.Development("localhost:9092");

using var connector = KafkaConnector.New(kafkaConfig);
```

### Custom Configuration

```csharp
var kafkaConfig = new KafkaConfiguration
{
    BootstrapServers = "localhost:9092",
    ClientId = "my-app",
    EnableIdempotence = true,
    Compression = CompressionType.Snappy,
    MaxMessageBytes = 2_000_000, // 2MB chunks
    RequestTimeout = TimeSpan.FromMinutes(2),
    
    AdditionalSettings = new Dictionary<string, string>
    {
        ["acks"] = "all",
        ["retries"] = "10",
        ["max.in.flight.requests.per.connection"] = "5"
    }
};

using var connector = KafkaConnector.New(kafkaConfig);
```

## Configuration Options

| Property | Default | Description |
|----------|---------|-------------|
| `BootstrapServers` | `localhost:9092` | Kafka broker addresses |
| `ClientId` | `nebulastore-kafka` | Client identifier |
| `MaxMessageBytes` | `1000000` | Blob chunk size (1MB) |
| `RequestTimeout` | `1 minute` | Request timeout |
| `EnableIdempotence` | `true` | Prevent duplicate writes |
| `Compression` | `None` | Compression type |
| `UseCache` | `true` | Enable metadata caching |
| `ConsumerGroupId` | `nebulastore-kafka-consumer` | Consumer group ID |
| `AdditionalSettings` | `{}` | Extra Kafka settings |

## Features

### Event Streaming

Storage operations produce Kafka events that can be consumed by other systems:

```csharp
// Storage writes produce events
storage.Store(customer);  // → Kafka event

// Other systems can consume these events
var consumer = new KafkaConsumer<string, byte[]>(config);
consumer.Subscribe("customer-data-topic");
// Process storage events in real-time
```

### Audit Trail

Every write is immutable and logged:

```csharp
// All changes are tracked in Kafka
storage.Store(data);  // Logged
storage.Store(data);  // Logged again

// Query audit trail via Kafka consumer
```

### Time Travel

Read data as it existed at any point in time (via Kafka offsets):

```csharp
// Read current state
var currentData = storage.Root<Customer>();

// Read historical state (requires custom implementation)
// var historicalData = ReadAtOffset(topic, offsetOneHourAgo);
```

## Performance

### Expected Throughput

- **Write:** 10K-100K ops/sec
- **Read:** 5K-50K ops/sec
- **Latency:** 5-50ms

### Optimization Tips

1. **Enable Compression:**
   ```csharp
   config.Compression = CompressionType.Snappy;
   ```

2. **Tune Chunk Size:**
   ```csharp
   config.MaxMessageBytes = 2_000_000; // 2MB for larger files
   ```

3. **Batch Writes:**
   ```csharp
   // Kafka automatically batches, but you can tune:
   config.AdditionalSettings["batch.size"] = "32768";
   config.AdditionalSettings["linger.ms"] = "10";
   ```

4. **Use Caching:**
   ```csharp
   config.UseCache = true; // Default
   ```

## Limitations

### Current Implementation

- **No Partial Blob Deletion**: Deleting individual blobs requires rewriting remaining data
- **Synchronous Index Loading**: Index is loaded synchronously on first access
- **Simple File System Index**: In-memory only, not persisted to Kafka

### Future Enhancements

- Async index loading
- Persistent file system index in Kafka
- Kafka Streams integration
- Schema Registry support
- Metrics and monitoring

## When to Use

### ✅ Good Fit

- Event-driven architectures
- Audit trail requirements
- Time travel capabilities needed
- Multi-datacenter deployments
- Already using Kafka infrastructure
- Streaming analytics integration

### ❌ Poor Fit

- Simple local storage needs (use NIO)
- Cost-sensitive scenarios
- Small-scale applications
- No Kafka expertise

## Comparison with Other AFS Adapters

| Feature | NIO | Redis | S3 | **Kafka** |
|---------|-----|-------|-----|-----------|
| Setup | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ |
| Performance | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐ |
| Scalability | ⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| Event Streaming | ❌ | ❌ | ❌ | ⭐⭐⭐⭐⭐ |
| Audit Trail | ❌ | ❌ | ⭐⭐ | ⭐⭐⭐⭐⭐ |

## Troubleshooting

### Connection Issues

```
Error: Failed to connect to Kafka broker
```

**Solution:**
1. Check Kafka is running
2. Verify bootstrap servers address
3. Check firewall/network settings
4. Increase `RequestTimeout`

### Topic Creation Failed

```
Error: Failed to create topic
```

**Solution:**
1. Check `auto.create.topics.enable=true` on broker
2. Verify user has topic creation permissions
3. Check topic naming constraints

### Slow Performance

```
Warning: Write latency > 1 second
```

**Solution:**
1. Enable compression
2. Increase batch size
3. Add more Kafka brokers
4. Increase `MaxMessageBytes`

## Examples

See the `examples/` directory for complete examples:
- Basic usage
- Production configuration
- Event streaming integration
- Custom serialization

## License

Eclipse Public License 2.0 (EPL-2.0)

## Dependencies

- **Confluent.Kafka** (2.6.1) - Apache License 2.0
- **MessagePack** (2.5.172) - MIT License

## References

- [Confluent.Kafka Documentation](https://docs.confluent.io/kafka-clients/dotnet/)
- [Apache Kafka Documentation](https://kafka.apache.org/documentation/)
- [NebulaStore AFS Guide](../../docs/KafkaAfsFeasibility.md)

## Support

- GitHub Issues: https://github.com/hadv/NebulaStore/issues
- Discussions: https://github.com/hadv/NebulaStore/discussions

