using System;
using System.Collections.Generic;
using Confluent.Kafka;

namespace NebulaStore.Afs.Kafka;

/// <summary>
/// Configuration for Kafka AFS connector.
/// </summary>
public class KafkaConfiguration
{
    /// <summary>
    /// Gets or sets the Kafka bootstrap servers (comma-separated list).
    /// </summary>
    /// <example>localhost:9092</example>
    /// <example>kafka1:9092,kafka2:9092,kafka3:9092</example>
    public string BootstrapServers { get; set; } = "localhost:9092";

    /// <summary>
    /// Gets or sets the client ID for this application.
    /// </summary>
    public string ClientId { get; set; } = "nebulastore-kafka";

    /// <summary>
    /// Gets or sets the maximum message size in bytes.
    /// This determines the blob chunk size.
    /// </summary>
    /// <remarks>
    /// Default is 1MB (1,000,000 bytes). Larger values reduce the number of
    /// Kafka messages but may impact performance. Must not exceed Kafka's
    /// max.message.bytes broker setting.
    /// </remarks>
    public int MaxMessageBytes { get; set; } = 1_000_000; // 1MB

    /// <summary>
    /// Gets or sets the request timeout.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets whether to enable idempotent producer.
    /// </summary>
    /// <remarks>
    /// When enabled, the producer ensures exactly-once delivery semantics.
    /// Recommended for production use.
    /// </remarks>
    public bool EnableIdempotence { get; set; } = true;

    /// <summary>
    /// Gets or sets the compression type for messages.
    /// </summary>
    public CompressionType Compression { get; set; } = CompressionType.None;

    /// <summary>
    /// Gets or sets whether to use caching for metadata.
    /// </summary>
    public bool UseCache { get; set; } = true;

    /// <summary>
    /// Gets or sets the consumer group ID.
    /// </summary>
    /// <remarks>
    /// Used for index topic consumption. Each connector instance should have
    /// a unique group ID to ensure all messages are consumed.
    /// </remarks>
    public string ConsumerGroupId { get; set; } = "nebulastore-kafka-consumer";

    /// <summary>
    /// Gets or sets additional Kafka configuration settings.
    /// </summary>
    /// <remarks>
    /// These settings are passed directly to the Kafka producer/consumer.
    /// See Kafka documentation for available settings.
    /// </remarks>
    public Dictionary<string, string> AdditionalSettings { get; set; } = new();

    /// <summary>
    /// Creates a ProducerConfig from this configuration.
    /// </summary>
    /// <returns>A ProducerConfig instance</returns>
    public ProducerConfig ToProducerConfig()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = BootstrapServers,
            ClientId = ClientId,
            EnableIdempotence = EnableIdempotence,
            CompressionType = Compression,
            RequestTimeoutMs = (int)RequestTimeout.TotalMilliseconds,
            MessageMaxBytes = MaxMessageBytes,
            
            // Reliability settings
            Acks = Acks.All, // Wait for all in-sync replicas
            MessageSendMaxRetries = 10,
            RetryBackoffMs = 100,
        };

        // Apply additional settings
        foreach (var kvp in AdditionalSettings)
        {
            config.Set(kvp.Key, kvp.Value);
        }

        return config;
    }

    /// <summary>
    /// Creates a ConsumerConfig from this configuration.
    /// </summary>
    /// <returns>A ConsumerConfig instance</returns>
    public ConsumerConfig ToConsumerConfig()
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = BootstrapServers,
            ClientId = ClientId,
            GroupId = ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false, // Manual commit for better control
            MaxPollIntervalMs = (int)RequestTimeout.TotalMilliseconds,
        };

        // Apply additional settings
        foreach (var kvp in AdditionalSettings)
        {
            config.Set(kvp.Key, kvp.Value);
        }

        return config;
    }

    /// <summary>
    /// Creates an AdminClientConfig from this configuration.
    /// </summary>
    /// <returns>An AdminClientConfig instance</returns>
    public AdminClientConfig ToAdminConfig()
    {
        var config = new AdminClientConfig
        {
            BootstrapServers = BootstrapServers,
            ClientId = ClientId,
        };

        // Apply additional settings
        foreach (var kvp in AdditionalSettings)
        {
            config.Set(kvp.Key, kvp.Value);
        }

        return config;
    }

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if configuration is invalid</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BootstrapServers))
            throw new InvalidOperationException("BootstrapServers cannot be null or empty");

        if (string.IsNullOrWhiteSpace(ClientId))
            throw new InvalidOperationException("ClientId cannot be null or empty");

        if (MaxMessageBytes <= 0)
            throw new InvalidOperationException("MaxMessageBytes must be positive");

        if (MaxMessageBytes > 10_000_000) // 10MB sanity check
            throw new InvalidOperationException("MaxMessageBytes should not exceed 10MB");

        if (RequestTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("RequestTimeout must be positive");
    }

    /// <summary>
    /// Creates a new KafkaConfiguration with default settings.
    /// </summary>
    /// <param name="bootstrapServers">The Kafka bootstrap servers</param>
    /// <returns>A new KafkaConfiguration instance</returns>
    public static KafkaConfiguration New(string bootstrapServers)
    {
        return new KafkaConfiguration
        {
            BootstrapServers = bootstrapServers
        };
    }

    /// <summary>
    /// Creates a new KafkaConfiguration for production use.
    /// </summary>
    /// <param name="bootstrapServers">The Kafka bootstrap servers</param>
    /// <param name="clientId">The client ID</param>
    /// <returns>A new KafkaConfiguration instance with production settings</returns>
    public static KafkaConfiguration Production(string bootstrapServers, string clientId)
    {
        return new KafkaConfiguration
        {
            BootstrapServers = bootstrapServers,
            ClientId = clientId,
            EnableIdempotence = true,
            Compression = CompressionType.Snappy,
            UseCache = true,
            AdditionalSettings = new Dictionary<string, string>
            {
                ["acks"] = "all",
                ["retries"] = "10",
                ["retry.backoff.ms"] = "100",
                ["max.in.flight.requests.per.connection"] = "5"
            }
        };
    }

    /// <summary>
    /// Creates a new KafkaConfiguration for development use.
    /// </summary>
    /// <param name="bootstrapServers">The Kafka bootstrap servers (default: localhost:9092)</param>
    /// <returns>A new KafkaConfiguration instance with development settings</returns>
    public static KafkaConfiguration Development(string bootstrapServers = "localhost:9092")
    {
        return new KafkaConfiguration
        {
            BootstrapServers = bootstrapServers,
            ClientId = "nebulastore-dev",
            EnableIdempotence = false, // Faster for development
            Compression = CompressionType.None,
            UseCache = false, // Easier debugging
            RequestTimeout = TimeSpan.FromSeconds(30)
        };
    }
}

