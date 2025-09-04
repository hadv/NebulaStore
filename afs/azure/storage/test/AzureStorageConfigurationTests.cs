using Xunit;

namespace NebulaStore.Afs.Azure.Storage.Tests;

/// <summary>
/// Tests for Azure Storage configuration functionality.
/// </summary>
public class AzureStorageConfigurationTests
{
    [Fact]
    public void New_ShouldCreateBuilderWithDefaults()
    {
        // Act
        var builder = AzureStorageConfiguration.New();

        // Assert
        Assert.NotNull(builder);
    }

    [Fact]
    public void SetConnectionString_ShouldSetConnectionString()
    {
        // Arrange
        const string connectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;EndpointSuffix=core.windows.net";

        // Act
        var config = AzureStorageConfiguration.New()
            .SetConnectionString(connectionString);

        // Assert
        Assert.Equal(connectionString, config.ConnectionString);
    }

    [Fact]
    public void SetUseCache_ShouldSetCacheFlag()
    {
        // Act
        var config = AzureStorageConfiguration.New()
            .SetUseCache(true);

        // Assert
        Assert.True(config.UseCache);
    }

    [Fact]
    public void SetTimeout_ShouldSetTimeout()
    {
        // Arrange
        const int timeoutMs = 30000;

        // Act
        var config = AzureStorageConfiguration.New()
            .SetTimeout(timeoutMs);

        // Assert
        Assert.Equal(timeoutMs, config.TimeoutMilliseconds);
    }

    [Fact]
    public void SetMaxRetryAttempts_ShouldSetRetryParameters()
    {
        // Arrange
        const int maxRetries = 5;

        // Act
        var config = AzureStorageConfiguration.New()
            .SetMaxRetryAttempts(maxRetries);

        // Assert
        Assert.Equal(maxRetries, config.MaxRetryAttempts);
    }

    [Fact]
    public void FluentConfiguration_WithAllSettings_ShouldCreateCompleteConfiguration()
    {
        // Arrange
        const string connectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=key;EndpointSuffix=core.windows.net";
        const bool useCache = true;
        const int timeoutMs = 60000;
        const int maxRetries = 3;

        // Act
        var config = AzureStorageConfiguration.New()
            .SetConnectionString(connectionString)
            .SetUseCache(useCache)
            .SetTimeout(timeoutMs)
            .SetMaxRetryAttempts(maxRetries);

        // Assert
        Assert.Equal(connectionString, config.ConnectionString);
        Assert.Equal(useCache, config.UseCache);
        Assert.Equal(timeoutMs, config.TimeoutMilliseconds);
        Assert.Equal(maxRetries, config.MaxRetryAttempts);
    }

    [Fact]
    public void New_WithDefaults_ShouldCreateConfigurationWithDefaultValues()
    {
        // Act
        var config = AzureStorageConfiguration.New();

        // Assert
        Assert.Null(config.ConnectionString);
        Assert.True(config.UseCache); // Default is true
        Assert.Equal(30000, config.TimeoutMilliseconds); // Default timeout
        Assert.Equal(3, config.MaxRetryAttempts); // Default retries
    }
}
