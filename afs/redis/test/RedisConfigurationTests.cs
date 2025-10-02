using Xunit;
using NebulaStore.Afs.Redis;

namespace NebulaStore.Afs.Redis.Tests;

/// <summary>
/// Tests for RedisConfiguration.
/// </summary>
public class RedisConfigurationTests
{
    [Fact]
    public void New_CreatesDefaultConfiguration()
    {
        var config = RedisConfiguration.New();

        Assert.NotNull(config);
        Assert.Equal("localhost:6379", config.ConnectionString);
        Assert.Equal(0, config.DatabaseNumber);
        Assert.True(config.UseCache);
        Assert.Equal(TimeSpan.FromMinutes(1), config.CommandTimeout);
    }

    [Fact]
    public void SetConnectionString_SetsValue()
    {
        var config = RedisConfiguration.New()
            .SetConnectionString("redis.example.com:6379");

        Assert.Equal("redis.example.com:6379", config.ConnectionString);
    }

    [Fact]
    public void SetConnectionString_WithNullOrEmpty_ThrowsException()
    {
        var config = RedisConfiguration.New();

        Assert.Throws<ArgumentException>(() => config.SetConnectionString(""));
        Assert.Throws<ArgumentException>(() => config.SetConnectionString(null!));
    }

    [Fact]
    public void SetDatabaseNumber_SetsValue()
    {
        var config = RedisConfiguration.New()
            .SetDatabaseNumber(5);

        Assert.Equal(5, config.DatabaseNumber);
    }

    [Fact]
    public void SetDatabaseNumber_WithNegative_ThrowsException()
    {
        var config = RedisConfiguration.New();

        Assert.Throws<ArgumentOutOfRangeException>(() => config.SetDatabaseNumber(-1));
    }

    [Fact]
    public void SetUseCache_SetsValue()
    {
        var config = RedisConfiguration.New()
            .SetUseCache(false);

        Assert.False(config.UseCache);
    }

    [Fact]
    public void SetCommandTimeout_SetsValue()
    {
        var timeout = TimeSpan.FromSeconds(30);
        var config = RedisConfiguration.New()
            .SetCommandTimeout(timeout);

        Assert.Equal(timeout, config.CommandTimeout);
    }

    [Fact]
    public void SetCommandTimeout_WithZeroOrNegative_ThrowsException()
    {
        var config = RedisConfiguration.New();

        Assert.Throws<ArgumentOutOfRangeException>(() => config.SetCommandTimeout(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => config.SetCommandTimeout(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void SetConnectTimeout_SetsValue()
    {
        var timeout = TimeSpan.FromSeconds(10);
        var config = RedisConfiguration.New()
            .SetConnectTimeout(timeout);

        Assert.Equal(timeout, config.ConnectTimeout);
    }

    [Fact]
    public void SetSyncTimeout_SetsValue()
    {
        var timeout = TimeSpan.FromSeconds(10);
        var config = RedisConfiguration.New()
            .SetSyncTimeout(timeout);

        Assert.Equal(timeout, config.SyncTimeout);
    }

    [Fact]
    public void SetAllowAdmin_SetsValue()
    {
        var config = RedisConfiguration.New()
            .SetAllowAdmin(false);

        Assert.False(config.AllowAdmin);
    }

    [Fact]
    public void SetAbortOnConnectFail_SetsValue()
    {
        var config = RedisConfiguration.New()
            .SetAbortOnConnectFail(true);

        Assert.True(config.AbortOnConnectFail);
    }

    [Fact]
    public void SetPassword_SetsValue()
    {
        var config = RedisConfiguration.New()
            .SetPassword("secret123");

        Assert.Equal("secret123", config.Password);
    }

    [Fact]
    public void SetUseSsl_SetsValue()
    {
        var config = RedisConfiguration.New()
            .SetUseSsl(true);

        Assert.True(config.UseSsl);
    }

    [Fact]
    public void Validate_WithValidConfiguration_DoesNotThrow()
    {
        var config = RedisConfiguration.New()
            .SetConnectionString("localhost:6379")
            .SetDatabaseNumber(0)
            .SetCommandTimeout(TimeSpan.FromMinutes(1));

        var exception = Record.Exception(() => config.Validate());

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WithEmptyConnectionString_ThrowsException()
    {
        var config = RedisConfiguration.New();
        // Use reflection to set invalid state
        var field = typeof(RedisConfiguration).GetField("<ConnectionString>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(config, "");

        Assert.Throws<InvalidOperationException>(() => config.Validate());
    }

    [Fact]
    public void ToConfigurationOptions_CreatesValidOptions()
    {
        var config = RedisConfiguration.New()
            .SetConnectionString("localhost:6379")
            .SetDatabaseNumber(1)
            .SetAllowAdmin(true)
            .SetUseSsl(false);

        var options = config.ToConfigurationOptions();

        Assert.NotNull(options);
        Assert.Equal(1, options.DefaultDatabase);
        Assert.True(options.AllowAdmin);
        Assert.False(options.Ssl);
    }

    [Fact]
    public void ToConfigurationOptions_WithPassword_SetsPassword()
    {
        var config = RedisConfiguration.New()
            .SetConnectionString("localhost:6379")
            .SetPassword("secret123");

        var options = config.ToConfigurationOptions();

        Assert.Equal("secret123", options.Password);
    }

    [Fact]
    public void FluentInterface_AllowsChaining()
    {
        var config = RedisConfiguration.New()
            .SetConnectionString("redis.example.com:6379")
            .SetDatabaseNumber(2)
            .SetUseCache(false)
            .SetCommandTimeout(TimeSpan.FromSeconds(30))
            .SetConnectTimeout(TimeSpan.FromSeconds(10))
            .SetSyncTimeout(TimeSpan.FromSeconds(10))
            .SetAllowAdmin(false)
            .SetAbortOnConnectFail(true)
            .SetPassword("password")
            .SetUseSsl(true);

        Assert.Equal("redis.example.com:6379", config.ConnectionString);
        Assert.Equal(2, config.DatabaseNumber);
        Assert.False(config.UseCache);
        Assert.Equal(TimeSpan.FromSeconds(30), config.CommandTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), config.ConnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(10), config.SyncTimeout);
        Assert.False(config.AllowAdmin);
        Assert.True(config.AbortOnConnectFail);
        Assert.Equal("password", config.Password);
        Assert.True(config.UseSsl);
    }
}

