using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace NebulaStore.Afs.Kafka;

/// <summary>
/// Manages the index for a single Kafka topic.
/// The index stores blob metadata in a separate Kafka topic.
/// </summary>
public class KafkaTopicIndex : IDisposable
{
    private readonly string _topic;
    private readonly string _indexTopicName;
    private readonly KafkaConfiguration _configuration;
    private readonly object _lock = new();
    
    private List<KafkaBlob>? _blobs;
    private IProducer<string, byte[]>? _producer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the KafkaTopicIndex class.
    /// </summary>
    /// <param name="topic">The data topic name</param>
    /// <param name="configuration">The Kafka configuration</param>
    private KafkaTopicIndex(string topic, KafkaConfiguration configuration)
    {
        _topic = topic ?? throw new ArgumentNullException(nameof(topic));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _indexTopicName = KafkaPathValidator.GetIndexTopicName(topic);
    }

    /// <summary>
    /// Creates a new KafkaTopicIndex instance.
    /// </summary>
    /// <param name="topic">The data topic name</param>
    /// <param name="configuration">The Kafka configuration</param>
    /// <returns>A new KafkaTopicIndex instance</returns>
    public static KafkaTopicIndex New(string topic, KafkaConfiguration configuration)
    {
        return new KafkaTopicIndex(topic, configuration);
    }

    /// <summary>
    /// Gets all blobs for this topic.
    /// </summary>
    /// <returns>An enumerable of KafkaBlob instances</returns>
    public IEnumerable<KafkaBlob> GetBlobs()
    {
        EnsureNotDisposed();
        
        lock (_lock)
        {
            EnsureBlobs();
            return _blobs!.ToList(); // Return a copy
        }
    }

    /// <summary>
    /// Adds blobs to the index.
    /// </summary>
    /// <param name="blobs">The blobs to add</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task AddBlobsAsync(IEnumerable<KafkaBlob> blobs)
    {
        EnsureNotDisposed();
        
        if (blobs == null)
            throw new ArgumentNullException(nameof(blobs));

        var blobList = blobs.ToList();
        if (blobList.Count == 0)
            return;

        lock (_lock)
        {
            EnsureBlobs();
            EnsureProducer();

            foreach (var blob in blobList)
            {
                // Add to in-memory index
                _blobs!.Add(blob);

                // Write to index topic
                var metadata = blob.ToBytes();
                var message = new Message<string, byte[]>
                {
                    Key = _topic,
                    Value = metadata
                };

                // Fire and forget - we'll await all at the end
                _producer!.Produce(_indexTopicName, message, deliveryReport =>
                {
                    if (deliveryReport.Error.IsError)
                    {
                        throw new KafkaException(deliveryReport.Error);
                    }
                });
            }

            // Flush to ensure all messages are sent
            _producer!.Flush(TimeSpan.FromSeconds(30));
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Deletes blobs from the index.
    /// </summary>
    /// <param name="blobsToDelete">The blobs to delete</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task DeleteBlobsAsync(IEnumerable<KafkaBlob> blobsToDelete)
    {
        EnsureNotDisposed();
        
        if (blobsToDelete == null)
            throw new ArgumentNullException(nameof(blobsToDelete));

        var deleteList = blobsToDelete.ToList();
        if (deleteList.Count == 0)
            return;

        lock (_lock)
        {
            EnsureBlobs();

            // Remove from in-memory index
            foreach (var blob in deleteList)
            {
                _blobs!.Remove(blob);
            }

            // Note: In a full implementation, we would need to:
            // 1. Delete records from the index topic (using AdminClient.DeleteRecordsAsync)
            // 2. Rewrite the remaining blobs to the index topic
            // For now, we just update the in-memory index
            // This will be implemented in Phase 2
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Loads blobs from the index topic.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    private async Task<List<KafkaBlob>> LoadBlobsAsync()
    {
        var blobs = new List<KafkaBlob>();

        // Create a consumer for the index topic
        var consumerConfig = _configuration.ToConsumerConfig();
        consumerConfig.GroupId = $"index-loader-{Guid.NewGuid()}"; // Unique group ID
        consumerConfig.AutoOffsetReset = AutoOffsetReset.Earliest;

        using var consumer = new ConsumerBuilder<string, byte[]>(consumerConfig).Build();

        try
        {
            // Subscribe to the index topic
            consumer.Subscribe(_indexTopicName);

            // Get the topic partition
            var assignment = consumer.Assignment;
            if (assignment.Count == 0)
            {
                // Wait for assignment
                await Task.Delay(100);
                assignment = consumer.Assignment;
            }

            // If still no assignment, the topic might not exist yet
            if (assignment.Count == 0)
            {
                return blobs;
            }

            // Consume all messages
            var timeout = TimeSpan.FromSeconds(5);
            var endReached = false;

            while (!endReached)
            {
                var consumeResult = consumer.Consume(timeout);
                
                if (consumeResult == null)
                {
                    // Timeout - assume we've reached the end
                    endReached = true;
                    continue;
                }

                if (consumeResult.IsPartitionEOF)
                {
                    endReached = true;
                    continue;
                }

                // Deserialize blob metadata
                var blob = KafkaBlob.FromBytes(_topic, consumeResult.Message.Value);
                blobs.Add(blob);
            }
        }
        catch (ConsumeException ex)
        {
            // Topic might not exist yet - that's okay
            if (!ex.Error.Code.ToString().Contains("UNKNOWN_TOPIC"))
            {
                throw;
            }
        }
        finally
        {
            consumer.Close();
        }

        return blobs;
    }

    /// <summary>
    /// Ensures the blobs list is loaded.
    /// </summary>
    private void EnsureBlobs()
    {
        if (_blobs == null)
        {
            // Load blobs synchronously (blocking)
            // In a production implementation, we might want to make this async
            _blobs = LoadBlobsAsync().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Ensures the producer is created.
    /// </summary>
    private void EnsureProducer()
    {
        if (_producer == null)
        {
            var producerConfig = _configuration.ToProducerConfig();
            _producer = new ProducerBuilder<string, byte[]>(producerConfig).Build();
        }
    }

    /// <summary>
    /// Ensures the instance is not disposed.
    /// </summary>
    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KafkaTopicIndex));
    }

    /// <summary>
    /// Disposes the index.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _blobs?.Clear();
            _blobs = null;

            _producer?.Dispose();
            _producer = null;

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}

