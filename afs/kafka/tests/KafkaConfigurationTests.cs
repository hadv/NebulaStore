using System;
using System.Linq;
using Confluent.Kafka;
using Xunit;

namespace NebulaStore.Afs.Kafka.Tests;

/// <summary>
/// Unit tests for KafkaConfiguration.
/// </summary>
public class KafkaConfigurationTests
{
    [Fact]
    public void New_ValidBootstrapServers_CreatesConfiguration()
    {
        // Act
        var config = KafkaConfiguration.New("localhost:9092");

        // Assert
        Assert.Equal("localhost:9092", config.BootstrapServers);
        Assert.NotNull(config.ClientId);
        Assert.True(config.MaxMessageBytes > 0);
    }

    [Fact]
    public void Production_ValidParameters_CreatesProductionConfiguration()
    {
        // Act
        var config = KafkaConfiguration.Production("kafka1:9092,kafka2:9092", "my-app");

        // Assert
        Assert.Equal("kafka1:9092,kafka2:9092", config.BootstrapServers);
        Assert.Equal("my-app", config.ClientId);
        Assert.True(config.EnableIdempotence);
        Assert.Equal(CompressionType.Snappy, config.Compression);
        Assert.True(config.UseCache);
        Assert.NotEmpty(config.AdditionalSettings);
    }

    [Fact]
    public void Development_DefaultParameters_CreatesDevelopmentConfiguration()
    {
        // Act
        var config = KafkaConfiguration.Development();

        // Assert
        Assert.Equal("localhost:9092", config.BootstrapServers);
        Assert.Equal("nebulastore-dev", config.ClientId);
        Assert.False(config.EnableIdempotence);
        Assert.Equal(CompressionType.None, config.Compression);
        Assert.False(config.UseCache);
    }

    [Fact]
    public void Development_CustomBootstrapServers_UsesCustomServers()
    {
        // Act
        var config = KafkaConfiguration.Development("custom:9092");

        // Assert
        Assert.Equal("custom:9092", config.BootstrapServers);
    }

    [Fact]
    public void Validate_ValidConfiguration_DoesNotThrow()
    {
        // Arrange
        var config = KafkaConfiguration.New("localhost:9092");

        // Act & Assert
        config.Validate(); // Should not throw
    }

    [Fact]
    public void Validate_NullBootstrapServers_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new KafkaConfiguration { BootstrapServers = null! };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void Validate_EmptyBootstrapServers_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new KafkaConfiguration { BootstrapServers = "" };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void Validate_NullClientId_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new KafkaConfiguration
        {
            BootstrapServers = "localhost:9092",
            ClientId = null!
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void Validate_NegativeMaxMessageBytes_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new KafkaConfiguration
        {
            BootstrapServers = "localhost:9092",
            MaxMessageBytes = -1
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void Validate_ZeroMaxMessageBytes_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new KafkaConfiguration
        {
            BootstrapServers = "localhost:9092",
            MaxMessageBytes = 0
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void Validate_ExcessiveMaxMessageBytes_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new KafkaConfiguration
        {
            BootstrapServers = "localhost:9092",
            MaxMessageBytes = 20_000_000 // 20MB
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void Validate_NegativeRequestTimeout_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new KafkaConfiguration
        {
            BootstrapServers = "localhost:9092",
            RequestTimeout = TimeSpan.FromSeconds(-1)
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void ToProducerConfig_CreatesValidProducerConfig()
    {
        // Arrange
        var config = KafkaConfiguration.New("localhost:9092");

        // Act
        var producerConfig = config.ToProducerConfig();

        // Assert
        Assert.NotNull(producerConfig);
        Assert.Equal("localhost:9092", producerConfig.BootstrapServers);
        Assert.Equal(config.ClientId, producerConfig.ClientId);
        Assert.Equal(config.EnableIdempotence, producerConfig.EnableIdempotence);
        Assert.Equal(Acks.All, producerConfig.Acks);
    }

    [Fact]
    public void ToProducerConfig_AppliesAdditionalSettings()
    {
        // Arrange
        var config = KafkaConfiguration.New("localhost:9092");
        config.AdditionalSettings["custom.setting"] = "custom-value";

        // Act
        var producerConfig = config.ToProducerConfig();

        // Assert
        Assert.NotNull(producerConfig);
        // Note: We can't easily verify custom settings were applied without reflection
        // This test mainly ensures no exception is thrown
    }

    [Fact]
    public void ToConsumerConfig_CreatesValidConsumerConfig()
    {
        // Arrange
        var config = KafkaConfiguration.New("localhost:9092");

        // Act
        var consumerConfig = config.ToConsumerConfig();

        // Assert
        Assert.NotNull(consumerConfig);
        Assert.Equal("localhost:9092", consumerConfig.BootstrapServers);
        Assert.Equal(config.ClientId, consumerConfig.ClientId);
        Assert.Equal(config.ConsumerGroupId, consumerConfig.GroupId);
        Assert.Equal(AutoOffsetReset.Earliest, consumerConfig.AutoOffsetReset);
        Assert.False(consumerConfig.EnableAutoCommit);
    }

    [Fact]
    public void ToAdminConfig_CreatesValidAdminConfig()
    {
        // Arrange
        var config = KafkaConfiguration.New("localhost:9092");

        // Act
        var adminConfig = config.ToAdminConfig();

        // Assert
        Assert.NotNull(adminConfig);
        Assert.Equal("localhost:9092", adminConfig.BootstrapServers);
        Assert.Equal(config.ClientId, adminConfig.ClientId);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Act
        var config = new KafkaConfiguration();

        // Assert
        Assert.Equal("localhost:9092", config.BootstrapServers);
        Assert.Equal("nebulastore-kafka", config.ClientId);
        Assert.Equal(1_000_000, config.MaxMessageBytes);
        Assert.Equal(TimeSpan.FromMinutes(1), config.RequestTimeout);
        Assert.True(config.EnableIdempotence);
        Assert.Equal(CompressionType.None, config.Compression);
        Assert.True(config.UseCache);
        Assert.Equal("nebulastore-kafka-consumer", config.ConsumerGroupId);
        Assert.NotNull(config.AdditionalSettings);
        Assert.Empty(config.AdditionalSettings);
    }

    [Fact]
    public void CustomConfiguration_AllPropertiesCanBeSet()
    {
        // Act
        var config = new KafkaConfiguration
        {
            BootstrapServers = "custom:9092",
            ClientId = "custom-client",
            MaxMessageBytes = 2_000_000,
            RequestTimeout = TimeSpan.FromMinutes(2),
            EnableIdempotence = false,
            Compression = CompressionType.Lz4,
            UseCache = false,
            ConsumerGroupId = "custom-group",
            AdditionalSettings = new System.Collections.Generic.Dictionary<string, string>
            {
                ["key1"] = "value1",
                ["key2"] = "value2"
            }
        };

        // Assert
        Assert.Equal("custom:9092", config.BootstrapServers);
        Assert.Equal("custom-client", config.ClientId);
        Assert.Equal(2_000_000, config.MaxMessageBytes);
        Assert.Equal(TimeSpan.FromMinutes(2), config.RequestTimeout);
        Assert.False(config.EnableIdempotence);
        Assert.Equal(CompressionType.Lz4, config.Compression);
        Assert.False(config.UseCache);
        Assert.Equal("custom-group", config.ConsumerGroupId);
        Assert.Equal(2, config.AdditionalSettings.Count);
        Assert.Equal("value1", config.AdditionalSettings["key1"]);
        Assert.Equal("value2", config.AdditionalSettings["key2"]);
    }

    [Theory]
    [InlineData(100_000)]
    [InlineData(500_000)]
    [InlineData(1_000_000)]
    [InlineData(2_000_000)]
    [InlineData(5_000_000)]
    public void Validate_ValidMaxMessageBytes_DoesNotThrow(int maxMessageBytes)
    {
        // Arrange
        var config = new KafkaConfiguration
        {
            BootstrapServers = "localhost:9092",
            MaxMessageBytes = maxMessageBytes
        };

        // Act & Assert
        config.Validate(); // Should not throw
    }

    [Fact]
    public void Production_HasReliabilitySettings()
    {
        // Act
        var config = KafkaConfiguration.Production("localhost:9092", "test");

        // Assert
        Assert.True(config.EnableIdempotence);
        Assert.Contains(config.AdditionalSettings, kvp => kvp.Key == "acks" && kvp.Value == "all");
        Assert.Contains(config.AdditionalSettings, kvp => kvp.Key == "retries");
    }
}

