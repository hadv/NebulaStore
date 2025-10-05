# Kafka AFS Adapter Feasibility Study

**Date:** 2025-10-05  
**Objective:** Evaluate the feasibility of porting Eclipse Store's Kafka AFS adapter to NebulaStore using Confluent.Kafka .NET library

## Executive Summary

**Verdict: FEASIBLE** ✅

Porting the Eclipse Store Kafka AFS adapter to NebulaStore is technically feasible using the Confluent.Kafka .NET library. The .NET client provides all necessary APIs (Producer, Consumer, AdminClient) that map directly to the Java kafka-clients library used by Eclipse Store. The implementation would follow established patterns in NebulaStore's existing AFS connectors (Redis, AWS S3, Azure, Google Cloud Firestore).

**Estimated Complexity:** Medium-High  
**Estimated Development Time:** 2-3 weeks for MVP  
**Key Risk:** Kafka topic management complexity and index synchronization

---

## 1. Architecture Analysis

### 1.1 Eclipse Store Kafka AFS Design

The Eclipse Store Kafka adapter uses a sophisticated design:

1. **Topic-Based Storage**: Each file is stored as a Kafka topic
   - Topic name derived from file path (sanitized for Kafka naming rules)
   - Files split into 1MB chunks (blobs) and written as Kafka messages
   - Each blob is a separate Kafka record

2. **Blob Metadata Structure**:
   ```java
   public interface Blob {
       String topic();      // Kafka topic name
       int partition();     // Kafka partition number
       long offset();       // Kafka offset within partition
       long start();        // Logical start position in file
       long end();          // Logical end position in file
       long size();         // Blob size (end - start + 1)
   }
   ```

3. **Index Management**:
   - Separate index topic per file: `__<topic>_index`
   - Index stores blob metadata (28 bytes per blob):
     - partition (4 bytes)
     - offset (8 bytes)
     - start position (8 bytes)
     - end position (8 bytes)
   - Index enables efficient file reconstruction and partial reads

4. **File System Index**:
   - Tracks all files in the system
   - Enables directory listing and traversal

### 1.2 Key Operations

**Write Operation:**
1. Split data into 1MB chunks
2. Produce each chunk to Kafka topic
3. Collect RecordMetadata (partition, offset)
4. Write blob metadata to index topic
5. Update file system index

**Read Operation:**
1. Query index topic for blob metadata
2. Seek to specific partition/offset
3. Consume record
4. Extract requested byte range from blob

**Delete Operation:**
1. Delete Kafka topics (data + index)
2. Remove from file system index
3. Clean up consumer/producer instances

---

## 2. Confluent.Kafka .NET Library Capabilities

### 2.1 Available APIs

The Confluent.Kafka library provides complete feature parity with Java kafka-clients:

| Java API | .NET Equivalent | Status |
|----------|----------------|--------|
| `KafkaProducer<K,V>` | `IProducer<TKey,TValue>` | ✅ Available |
| `KafkaConsumer<K,V>` | `IConsumer<TKey,TValue>` | ✅ Available |
| `AdminClient` | `IAdminClient` | ✅ Available |
| `ProducerRecord` | `Message<TKey,TValue>` | ✅ Available |
| `ConsumerRecord` | `ConsumeResult<TKey,TValue>` | ✅ Available |
| `RecordMetadata` | `DeliveryResult<TKey,TValue>` | ✅ Available |
| Topic Management | `CreateTopicsAsync`, `DeleteTopicsAsync` | ✅ Available |
| Offset Management | `Seek`, `Position`, `Committed` | ✅ Available |
| Record Deletion | `DeleteRecordsAsync` | ✅ Available |

### 2.2 Key Features

1. **High-Level Producer API**:
   ```csharp
   var result = await producer.ProduceAsync(topic, new Message<string, byte[]> 
   { 
       Key = key, 
       Value = data 
   });
   // result.Partition, result.Offset available
   ```

2. **High-Level Consumer API**:
   ```csharp
   consumer.Assign(new TopicPartition(topic, partition));
   consumer.Seek(new TopicPartitionOffset(topic, partition, offset));
   var result = consumer.Consume(timeout);
   ```

3. **AdminClient API**:
   ```csharp
   await adminClient.CreateTopicsAsync(new[] { new TopicSpecification { Name = topic } });
   await adminClient.DeleteTopicsAsync(new[] { topic });
   await adminClient.DeleteRecordsAsync(recordsToDelete);
   ```

4. **Performance Features**:
   - Automatic batching
   - Compression support
   - Idempotent producer
   - Transactional API support

### 2.3 Compatibility

- **.NET Framework:** >= 4.6.2
- **.NET Core:** >= 1.0
- **.NET Standard:** >= 1.3
- **NebulaStore Target:** .NET 9.0 ✅ Fully compatible

---

## 3. Implementation Design

### 3.1 Module Structure

```
afs/kafka/
├── NebulaStore.Afs.Kafka.csproj
├── README.md
├── src/
│   ├── KafkaConnector.cs           # Main connector implementation
│   ├── KafkaConfiguration.cs       # Configuration wrapper
│   ├── KafkaBlob.cs                # Blob metadata structure
│   ├── KafkaTopicIndex.cs          # Per-topic index management
│   ├── KafkaFileSystemIndex.cs     # Global file system index
│   ├── KafkaPathValidator.cs       # Path validation for Kafka naming
│   └── Extensions/
│       └── EmbeddedStorageKafkaExtensions.cs
├── tests/
│   └── KafkaConnectorTests.cs
└── examples/
    └── KafkaExample.cs
```

### 3.2 Core Classes

#### KafkaConnector

```csharp
public class KafkaConnector : BlobStoreConnectorBase
{
    private readonly ProducerConfig _producerConfig;
    private readonly ConsumerConfig _consumerConfig;
    private readonly AdminClientConfig _adminConfig;
    
    private readonly KafkaFileSystemIndex _fileSystemIndex;
    private readonly ConcurrentDictionary<string, KafkaTopicIndex> _topicIndices;
    private readonly ConcurrentDictionary<string, IProducer<string, byte[]>> _producers;
    private readonly ConcurrentDictionary<string, IConsumer<string, byte[]>> _consumers;
    
    // IBlobStoreConnector implementation
    public override long WriteData(BlobStorePath file, IEnumerable<byte[]> sourceBuffers);
    public override byte[] ReadData(BlobStorePath file, long offset, long length);
    public override bool DeleteFile(BlobStorePath file);
    // ... other methods
}
```

#### KafkaBlob

```csharp
public record KafkaBlob
{
    public string Topic { get; init; }
    public int Partition { get; init; }
    public long Offset { get; init; }
    public long Start { get; init; }
    public long End { get; init; }
    public long Size => End - Start + 1;
    
    // Serialization to/from 28-byte format
    public byte[] ToBytes();
    public static KafkaBlob FromBytes(byte[] bytes);
}
```

#### KafkaTopicIndex

```csharp
public class KafkaTopicIndex : IDisposable
{
    private readonly string _topic;
    private readonly string _indexTopicName;
    private readonly IProducer<string, byte[]> _producer;
    private readonly List<KafkaBlob> _blobs;
    
    public IEnumerable<KafkaBlob> GetBlobs();
    public void AddBlobs(IEnumerable<KafkaBlob> blobs);
    public void DeleteBlobs(IEnumerable<KafkaBlob> blobs);
}
```

### 3.3 Configuration

```csharp
public class KafkaConfiguration
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string ClientId { get; set; } = "nebulastore-kafka";
    public int MaxMessageBytes { get; set; } = 1_000_000; // 1MB chunks
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(1);
    public bool EnableIdempotence { get; set; } = true;
    public CompressionType Compression { get; set; } = CompressionType.None;
    
    // Additional Kafka-specific settings
    public Dictionary<string, string> AdditionalSettings { get; set; } = new();
}
```

---

## 4. Integration with NebulaStore

### 4.1 Usage Pattern

```csharp
using NebulaStore.Storage.Embedded;
using NebulaStore.Afs.Kafka;

// Configure Kafka connection
var kafkaConfig = new KafkaConfiguration
{
    BootstrapServers = "localhost:9092",
    ClientId = "my-app",
    EnableIdempotence = true
};

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

### 4.2 Extension Methods

```csharp
public static class EmbeddedStorageKafkaExtensions
{
    public static IEmbeddedStorageManager StartWithKafka(
        string bootstrapServers,
        string clientId = "nebulastore")
    {
        var config = new KafkaConfiguration
        {
            BootstrapServers = bootstrapServers,
            ClientId = clientId
        };
        
        var connector = KafkaConnector.New(config);
        var fileSystem = BlobStoreFileSystem.New(connector);
        
        return EmbeddedStorage.Start(
            EmbeddedStorageConfiguration.New()
                .SetStorageFileSystem(fileSystem)
                .Build()
        );
    }
}
```

---

## 5. Technical Challenges & Solutions

### 5.1 Topic Naming Constraints

**Challenge:** Kafka topic names have restrictions (alphanumeric, `.`, `_`, `-` only)

**Solution:** Implement path sanitization (same as Eclipse Store):
```csharp
private static string ToTopicName(BlobStorePath path)
{
    return Regex.Replace(
        path.FullQualifiedName.Replace(BlobStorePath.SeparatorChar, '_'),
        "[^a-zA-Z0-9\\._\\-]",
        "_"
    );
}
```

### 5.2 Index Synchronization

**Challenge:** Index topic must stay synchronized with data topic

**Solution:**
- Use transactions for atomic writes (optional, adds overhead)
- Or accept eventual consistency with retry logic
- Implement index rebuild capability from data topics

### 5.3 Partial Blob Deletion

**Challenge:** Kafka doesn't support deleting individual records, only up to an offset

**Solution:** Same as Eclipse Store:
1. Read remaining blobs
2. Delete records up to offset
3. Rewrite remaining blobs
4. Update index

### 5.4 Consumer/Producer Lifecycle

**Challenge:** Managing multiple consumers/producers efficiently

**Solution:**
- Pool consumers/producers per topic
- Lazy initialization
- Proper disposal in connector cleanup

---

## 6. Minimal Viable Path (MVP)

### Phase 1: Core Implementation (Week 1)
- [ ] Create project structure
- [ ] Implement `KafkaBlob` and serialization
- [ ] Implement `KafkaConfiguration`
- [ ] Implement basic `KafkaConnector` (write/read/delete)
- [ ] Implement `KafkaPathValidator`

### Phase 2: Index Management (Week 2)
- [ ] Implement `KafkaTopicIndex`
- [ ] Implement `KafkaFileSystemIndex`
- [ ] Add index synchronization logic
- [ ] Implement directory operations

### Phase 3: Testing & Polish (Week 3)
- [ ] Unit tests for all components
- [ ] Integration tests with real Kafka
- [ ] Performance benchmarks
- [ ] Documentation and examples
- [ ] Error handling and edge cases

---

## 7. Dependencies

### 7.1 NuGet Packages Required

```xml
<PackageReference Include="Confluent.Kafka" Version="2.6.1" />
```

**License:** Apache License 2.0 ✅ Compatible with EPL 2.0

### 7.2 External Requirements

- **Kafka Cluster:** Users must provide their own Kafka installation
  - Apache Kafka 0.8+
  - Confluent Platform
  - Confluent Cloud
  - Amazon MSK
  - Azure Event Hubs (Kafka-compatible)

---

## 8. Performance Considerations

### 8.1 Expected Performance

- **Write Throughput:** 100K+ messages/sec (hardware dependent)
- **Read Latency:** Low (direct partition/offset seeks)
- **Blob Size:** 1MB chunks (configurable)
- **Compression:** Optional (Snappy, LZ4, Gzip, Zstd)

### 8.2 Optimization Opportunities

1. **Batching:** Confluent.Kafka handles automatic batching
2. **Compression:** Enable for network/storage efficiency
3. **Idempotent Producer:** Prevent duplicates
4. **Index Caching:** Cache blob metadata in memory
5. **Connection Pooling:** Reuse producers/consumers

---

## 9. Risks & Mitigations

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Kafka cluster unavailability | High | Medium | Implement retry logic, circuit breakers |
| Index corruption | High | Low | Implement index rebuild from data topics |
| Topic proliferation | Medium | High | Document topic management, cleanup strategies |
| Performance degradation | Medium | Medium | Benchmark early, optimize blob size |
| Version compatibility | Low | Low | Use stable Confluent.Kafka API |

---

## 10. Recommendations

### 10.1 Proceed with Implementation

**Recommendation: YES** - The implementation is feasible and aligns with NebulaStore's architecture.

### 10.2 Suggested Approach

1. **Start with MVP:** Focus on core read/write/delete operations
2. **Test with Docker:** Use Kafka in Docker for development
3. **Benchmark Early:** Compare with other AFS adapters
4. **Document Thoroughly:** Kafka setup is more complex than other backends
5. **Consider Alternatives:** For simpler use cases, Redis or S3 may be better

### 10.3 When to Use Kafka AFS

**Good Fit:**
- Event-driven architectures already using Kafka
- Need for event streaming + persistence
- Multi-datacenter replication requirements
- Audit trail and time-travel capabilities

**Poor Fit:**
- Simple local storage needs (use NIO)
- Cost-sensitive scenarios (Kafka infrastructure overhead)
- Small-scale applications (topic management overhead)

---

## 11. Next Steps

1. **Get Approval:** Confirm stakeholder interest in Kafka adapter
2. **Setup Development Environment:** Kafka cluster (Docker Compose)
3. **Create Feature Branch:** `feature/kafka-afs-adapter`
4. **Implement MVP:** Follow 3-week plan
5. **Create PR:** For peer review before merging

---

## Appendix A: Eclipse Store Reference Files

- **KafkaConnector.java:** https://github.com/eclipse-store/store/blob/main/afs/kafka/src/main/java/org/eclipse/store/afs/kafka/types/KafkaConnector.java
- **Blob.java:** https://github.com/eclipse-store/store/blob/main/afs/kafka/src/main/java/org/eclipse/store/afs/kafka/types/Blob.java
- **TopicIndex.java:** https://github.com/eclipse-store/store/blob/main/afs/kafka/src/main/java/org/eclipse/store/afs/kafka/types/TopicIndex.java

## Appendix B: Confluent.Kafka Documentation

- **Overview:** https://docs.confluent.io/kafka-clients/dotnet/current/overview.html
- **GitHub:** https://github.com/confluentinc/confluent-kafka-dotnet
- **API Docs:** https://docs.confluent.io/platform/current/clients/confluent-kafka-dotnet/

---

**Document Version:** 1.0  
**Author:** NebulaStore Development Team  
**Status:** Draft for Review

