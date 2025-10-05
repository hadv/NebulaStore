using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Kafka;

/// <summary>
/// Kafka implementation of IBlobStoreConnector.
/// Stores files as Kafka topics with blob metadata in index topics.
/// </summary>
/// <remarks>
/// Architecture:
/// - Each file is stored as a Kafka topic
/// - Files are split into chunks (blobs) of configurable size (default 1MB)
/// - Each blob is a Kafka message
/// - Blob metadata (partition, offset, range) is stored in an index topic
/// - A file system index tracks all files
/// 
/// Example:
/// <code>
/// var config = KafkaConfiguration.New("localhost:9092");
/// using var connector = KafkaConnector.New(config);
/// using var fileSystem = BlobStoreFileSystem.New(connector);
/// </code>
/// </remarks>
public class KafkaConnector : BlobStoreConnectorBase
{
    private readonly KafkaConfiguration _configuration;
    private readonly ConcurrentDictionary<string, KafkaTopicIndex> _topicIndices;
    private readonly ConcurrentDictionary<string, IProducer<string, byte[]>> _producers;
    private readonly ConcurrentDictionary<string, IConsumer<string, byte[]>> _consumers;
    private readonly HashSet<string> _knownFiles; // Simple file system index
    private readonly object _fileSystemIndexLock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the KafkaConnector class.
    /// </summary>
    /// <param name="configuration">The Kafka configuration</param>
    private KafkaConnector(KafkaConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _configuration.Validate();

        _topicIndices = new ConcurrentDictionary<string, KafkaTopicIndex>();
        _producers = new ConcurrentDictionary<string, IProducer<string, byte[]>>();
        _consumers = new ConcurrentDictionary<string, IConsumer<string, byte[]>>();
        _knownFiles = new HashSet<string>();
    }

    /// <summary>
    /// Creates a new KafkaConnector instance.
    /// </summary>
    /// <param name="configuration">The Kafka configuration</param>
    /// <returns>A new KafkaConnector instance</returns>
    public static KafkaConnector New(KafkaConfiguration configuration)
    {
        return new KafkaConnector(configuration);
    }

    /// <summary>
    /// Gets the topic index for a file.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <returns>The topic index</returns>
    private KafkaTopicIndex GetTopicIndex(BlobStorePath file)
    {
        var topicName = KafkaPathValidator.ToTopicName(file);
        return _topicIndices.GetOrAdd(topicName, _ => KafkaTopicIndex.New(topicName, _configuration));
    }

    /// <summary>
    /// Gets or creates a producer for a topic.
    /// </summary>
    /// <param name="topicName">The topic name</param>
    /// <returns>A Kafka producer</returns>
    private IProducer<string, byte[]> GetProducer(string topicName)
    {
        return _producers.GetOrAdd(topicName, _ =>
        {
            var config = _configuration.ToProducerConfig();
            return new ProducerBuilder<string, byte[]>(config).Build();
        });
    }

    /// <summary>
    /// Gets or creates a consumer for a topic.
    /// </summary>
    /// <param name="topicName">The topic name</param>
    /// <returns>A Kafka consumer</returns>
    private IConsumer<string, byte[]> GetConsumer(string topicName)
    {
        return _consumers.GetOrAdd(topicName, _ =>
        {
            var config = _configuration.ToConsumerConfig();
            config.GroupId = $"{_configuration.ConsumerGroupId}-{topicName}";
            return new ConsumerBuilder<string, byte[]>(config).Build();
        });
    }

    #region IBlobStoreConnector Implementation

    public override long GetFileSize(BlobStorePath file)
    {
        EnsureNotDisposed();

        var index = GetTopicIndex(file);
        var blobs = index.GetBlobs().ToList();

        if (blobs.Count == 0)
            return 0;

        // File size is the end position of the last blob + 1
        return blobs.Max(b => b.End) + 1;
    }

    public override bool DirectoryExists(BlobStorePath directory)
    {
        EnsureNotDisposed();

        // In Kafka, directories are virtual - they exist if any files exist under them
        lock (_fileSystemIndexLock)
        {
            var prefix = directory.FullQualifiedName;
            return _knownFiles.Any(f => f.StartsWith(prefix));
        }
    }

    public override bool FileExists(BlobStorePath file)
    {
        EnsureNotDisposed();

        lock (_fileSystemIndexLock)
        {
            return _knownFiles.Contains(file.FullQualifiedName);
        }
    }

    public override void VisitChildren(BlobStorePath directory, IBlobStorePathVisitor visitor)
    {
        EnsureNotDisposed();

        if (visitor == null)
            throw new ArgumentNullException(nameof(visitor));

        lock (_fileSystemIndexLock)
        {
            var prefix = directory.FullQualifiedName;
            if (!prefix.EndsWith(BlobStorePath.Separator))
                prefix += BlobStorePath.Separator;

            var children = _knownFiles
                .Where(f => f.StartsWith(prefix))
                .Select(f => f.Substring(prefix.Length))
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();

            // Group by immediate child (file or directory)
            var immediateChildren = new HashSet<string>();
            foreach (var child in children)
            {
                var separatorIndex = child.IndexOf(BlobStorePath.SeparatorChar);
                var immediateChild = separatorIndex >= 0
                    ? child.Substring(0, separatorIndex)
                    : child;

                immediateChildren.Add(immediateChild);
            }

            // Visit each immediate child
            foreach (var child in immediateChildren.OrderBy(c => c))
            {
                var fullPath = prefix + child;
                var isDirectory = _knownFiles.Any(f => f.StartsWith(fullPath + BlobStorePath.Separator));

                if (isDirectory)
                {
                    visitor.VisitDirectory(directory, child);
                }
                else
                {
                    visitor.VisitFile(directory, child);
                }
            }
        }
    }

    public override bool IsEmpty(BlobStorePath directory)
    {
        EnsureNotDisposed();

        lock (_fileSystemIndexLock)
        {
            var prefix = directory.FullQualifiedName;
            if (!prefix.EndsWith(BlobStorePath.Separator))
                prefix += BlobStorePath.Separator;

            return !_knownFiles.Any(f => f.StartsWith(prefix));
        }
    }

    public override bool CreateDirectory(BlobStorePath directory)
    {
        EnsureNotDisposed();

        // Directories are virtual in Kafka - always return true
        return true;
    }

    public override bool CreateFile(BlobStorePath file)
    {
        EnsureNotDisposed();

        lock (_fileSystemIndexLock)
        {
            if (_knownFiles.Contains(file.FullQualifiedName))
                return false;

            _knownFiles.Add(file.FullQualifiedName);
            return true;
        }
    }

    public override bool DeleteFile(BlobStorePath file)
    {
        EnsureNotDisposed();

        var topicName = KafkaPathValidator.ToTopicName(file);

        try
        {
            // Delete the Kafka topics (data + index)
            using var adminClient = new AdminClientBuilder(_configuration.ToAdminConfig()).Build();
            
            var indexTopicName = KafkaPathValidator.GetIndexTopicName(topicName);
            var topicsToDelete = new[] { topicName, indexTopicName };

            adminClient.DeleteTopicsAsync(topicsToDelete).GetAwaiter().GetResult();

            // Remove from file system index
            lock (_fileSystemIndexLock)
            {
                _knownFiles.Remove(file.FullQualifiedName);
            }

            // Clean up cached resources
            if (_topicIndices.TryRemove(topicName, out var index))
            {
                index.Dispose();
            }

            if (_producers.TryRemove(topicName, out var producer))
            {
                producer.Dispose();
            }

            if (_consumers.TryRemove(topicName, out var consumer))
            {
                consumer.Dispose();
            }

            return true;
        }
        catch (Exception ex)
        {
            // Log error but don't throw - file might not exist
            Console.WriteLine($"Error deleting file {file.FullQualifiedName}: {ex.Message}");
            return false;
        }
    }

    public override byte[] ReadData(BlobStorePath file, long offset, long length)
    {
        EnsureNotDisposed();

        if (offset < 0)
            throw new ArgumentException("Offset must be non-negative", nameof(offset));

        var index = GetTopicIndex(file);
        var blobs = index.GetBlobs().ToList();

        if (blobs.Count == 0)
            return Array.Empty<byte>();

        // Determine actual length to read
        var fileSize = GetFileSize(file);
        if (length < 0)
            length = fileSize - offset;

        if (offset >= fileSize)
            return Array.Empty<byte>();

        length = Math.Min(length, fileSize - offset);

        // Find blobs that overlap with the requested range
        var endPosition = offset + length - 1;
        var relevantBlobs = blobs
            .Where(b => b.Overlaps(offset, endPosition))
            .OrderBy(b => b.Start)
            .ToList();

        if (relevantBlobs.Count == 0)
            return Array.Empty<byte>();

        // Read data from each blob
        var result = new byte[length];
        var resultOffset = 0;

        foreach (var blob in relevantBlobs)
        {
            var blobData = ReadBlob(file, blob);

            // Calculate which part of the blob we need
            var blobStartInFile = blob.Start;
            var blobEndInFile = blob.End;

            var readStart = Math.Max(offset, blobStartInFile);
            var readEnd = Math.Min(endPosition, blobEndInFile);

            var offsetInBlob = readStart - blobStartInFile;
            var lengthToRead = readEnd - readStart + 1;

            // Copy the relevant portion
            Array.Copy(blobData, offsetInBlob, result, resultOffset, lengthToRead);
            resultOffset += (int)lengthToRead;
        }

        return result;
    }

    public override long ReadData(BlobStorePath file, byte[] targetBuffer, long offset, long length)
    {
        EnsureNotDisposed();

        if (targetBuffer == null)
            throw new ArgumentNullException(nameof(targetBuffer));

        var data = ReadData(file, offset, length);
        var bytesToCopy = Math.Min(data.Length, targetBuffer.Length);
        Array.Copy(data, 0, targetBuffer, 0, bytesToCopy);
        return bytesToCopy;
    }

    public override long WriteData(BlobStorePath file, IEnumerable<byte[]> sourceBuffers)
    {
        EnsureNotDisposed();

        if (sourceBuffers == null)
            throw new ArgumentNullException(nameof(sourceBuffers));

        var topicName = KafkaPathValidator.ToTopicName(file);
        var producer = GetProducer(topicName);
        var index = GetTopicIndex(file);

        // Concatenate all source buffers
        var allData = sourceBuffers.SelectMany(b => b).ToArray();
        if (allData.Length == 0)
            return 0;

        var blobs = new List<KafkaBlob>();
        var currentOffset = GetFileSize(file); // Append to end of file
        var dataOffset = 0;

        // Split data into chunks
        while (dataOffset < allData.Length)
        {
            var chunkSize = Math.Min(_configuration.MaxMessageBytes, allData.Length - dataOffset);
            var chunk = new byte[chunkSize];
            Array.Copy(allData, dataOffset, chunk, 0, chunkSize);

            // Produce to Kafka
            var message = new Message<string, byte[]>
            {
                Key = file.FullQualifiedName,
                Value = chunk
            };

            var deliveryResult = producer.ProduceAsync(topicName, message).GetAwaiter().GetResult();

            // Create blob metadata
            var blob = KafkaBlob.New(
                topicName,
                deliveryResult.Partition.Value,
                deliveryResult.Offset.Value,
                currentOffset,
                currentOffset + chunkSize - 1
            );

            blobs.Add(blob);

            dataOffset += chunkSize;
            currentOffset += chunkSize;
        }

        // Flush producer
        producer.Flush(TimeSpan.FromSeconds(30));

        // Update index
        index.AddBlobsAsync(blobs).GetAwaiter().GetResult();

        // Update file system index
        lock (_fileSystemIndexLock)
        {
            _knownFiles.Add(file.FullQualifiedName);
        }

        return allData.Length;
    }

    public override void MoveFile(BlobStorePath sourceFile, BlobStorePath targetFile)
    {
        EnsureNotDisposed();

        // Read all data from source
        var data = ReadData(sourceFile, 0, -1);

        // Write to target
        WriteData(targetFile, new[] { data });

        // Delete source
        DeleteFile(sourceFile);
    }

    public override long CopyFile(BlobStorePath sourceFile, BlobStorePath targetFile, long offset, long length)
    {
        EnsureNotDisposed();

        // Read data from source
        var data = ReadData(sourceFile, offset, length);

        // Write to target
        return WriteData(targetFile, new[] { data });
    }

    public override void TruncateFile(BlobStorePath file, long newLength)
    {
        EnsureNotDisposed();

        if (newLength < 0)
            throw new ArgumentException("New length must be non-negative", nameof(newLength));

        var currentSize = GetFileSize(file);
        if (newLength >= currentSize)
            return; // Nothing to truncate

        // Read the data we want to keep
        var data = ReadData(file, 0, newLength);

        // Delete the file
        DeleteFile(file);

        // Write back the truncated data
        if (data.Length > 0)
        {
            WriteData(file, new[] { data });
        }
    }

    /// <summary>
    /// Reads a single blob from Kafka.
    /// </summary>
    /// <param name="file">The file path</param>
    /// <param name="blob">The blob metadata</param>
    /// <returns>The blob data</returns>
    private byte[] ReadBlob(BlobStorePath file, KafkaBlob blob)
    {
        var consumer = GetConsumer(blob.Topic);

        // Assign to the specific partition
        var topicPartition = new TopicPartition(blob.Topic, blob.Partition);
        consumer.Assign(new[] { topicPartition });

        // Seek to the specific offset
        consumer.Seek(new TopicPartitionOffset(topicPartition, blob.Offset));

        // Consume the message
        var timeout = _configuration.RequestTimeout;
        var consumeResult = consumer.Consume(timeout);

        if (consumeResult == null)
            throw new InvalidOperationException($"Failed to read blob at offset {blob.Offset}");

        if (consumeResult.IsPartitionEOF)
            throw new InvalidOperationException($"Reached end of partition at offset {blob.Offset}");

        return consumeResult.Message.Value;
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose all topic indices
            foreach (var index in _topicIndices.Values)
            {
                index.Dispose();
            }
            _topicIndices.Clear();

            // Dispose all producers
            foreach (var producer in _producers.Values)
            {
                producer.Dispose();
            }
            _producers.Clear();

            // Dispose all consumers
            foreach (var consumer in _consumers.Values)
            {
                consumer.Dispose();
            }
            _consumers.Clear();
        }

        _disposed = true;
        base.Dispose(disposing);
    }
}

