namespace NebulaStore.Afs.Redis;

/// <summary>
/// Configuration for Redis AFS connector.
/// </summary>
public class RedisConfiguration
{
    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    public string ConnectionString { get; private set; } = "localhost:6379";

    /// <summary>
    /// Gets or sets the Redis database number.
    /// </summary>
    public int DatabaseNumber { get; private set; } = 0;

    /// <summary>
    /// Gets or sets whether to use caching.
    /// </summary>
    public bool UseCache { get; private set; } = true;

    /// <summary>
    /// Gets or sets the command timeout.
    /// </summary>
    public TimeSpan CommandTimeout { get; private set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the connect timeout.
    /// </summary>
    public TimeSpan ConnectTimeout { get; private set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the sync timeout.
    /// </summary>
    public TimeSpan SyncTimeout { get; private set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets whether to allow admin operations.
    /// </summary>
    public bool AllowAdmin { get; private set; } = true;

    /// <summary>
    /// Gets or sets whether to abort on connect fail.
    /// </summary>
    public bool AbortOnConnectFail { get; private set; } = false;

    /// <summary>
    /// Gets or sets the password for Redis authentication.
    /// </summary>
    public string? Password { get; private set; }

    /// <summary>
    /// Gets or sets the SSL/TLS settings.
    /// </summary>
    public bool UseSsl { get; private set; } = false;

    /// <summary>
    /// Creates a new RedisConfiguration instance.
    /// </summary>
    public static RedisConfiguration New()
    {
        return new RedisConfiguration();
    }

    /// <summary>
    /// Sets the connection string.
    /// </summary>
    public RedisConfiguration SetConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));
        
        ConnectionString = connectionString;
        return this;
    }

    /// <summary>
    /// Sets the database number.
    /// </summary>
    public RedisConfiguration SetDatabaseNumber(int databaseNumber)
    {
        if (databaseNumber < 0)
            throw new ArgumentOutOfRangeException(nameof(databaseNumber), "Database number must be non-negative");
        
        DatabaseNumber = databaseNumber;
        return this;
    }

    /// <summary>
    /// Sets whether to use caching.
    /// </summary>
    public RedisConfiguration SetUseCache(bool useCache)
    {
        UseCache = useCache;
        return this;
    }

    /// <summary>
    /// Sets the command timeout.
    /// </summary>
    public RedisConfiguration SetCommandTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive");
        
        CommandTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the connect timeout.
    /// </summary>
    public RedisConfiguration SetConnectTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive");
        
        ConnectTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the sync timeout.
    /// </summary>
    public RedisConfiguration SetSyncTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive");
        
        SyncTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets whether to allow admin operations.
    /// </summary>
    public RedisConfiguration SetAllowAdmin(bool allowAdmin)
    {
        AllowAdmin = allowAdmin;
        return this;
    }

    /// <summary>
    /// Sets whether to abort on connect fail.
    /// </summary>
    public RedisConfiguration SetAbortOnConnectFail(bool abortOnConnectFail)
    {
        AbortOnConnectFail = abortOnConnectFail;
        return this;
    }

    /// <summary>
    /// Sets the password for Redis authentication.
    /// </summary>
    public RedisConfiguration SetPassword(string? password)
    {
        Password = password;
        return this;
    }

    /// <summary>
    /// Sets whether to use SSL/TLS.
    /// </summary>
    public RedisConfiguration SetUseSsl(bool useSsl)
    {
        UseSsl = useSsl;
        return this;
    }

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("Connection string must be set");

        if (DatabaseNumber < 0)
            throw new InvalidOperationException("Database number must be non-negative");

        if (CommandTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Command timeout must be positive");

        if (ConnectTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Connect timeout must be positive");

        if (SyncTimeout <= TimeSpan.Zero)
            throw new InvalidOperationException("Sync timeout must be positive");
    }

    /// <summary>
    /// Builds a StackExchange.Redis ConfigurationOptions from this configuration.
    /// </summary>
    public StackExchange.Redis.ConfigurationOptions ToConfigurationOptions()
    {
        Validate();

        var options = StackExchange.Redis.ConfigurationOptions.Parse(ConnectionString);
        options.DefaultDatabase = DatabaseNumber;
        options.ConnectTimeout = (int)ConnectTimeout.TotalMilliseconds;
        options.SyncTimeout = (int)SyncTimeout.TotalMilliseconds;
        options.AllowAdmin = AllowAdmin;
        options.AbortOnConnectFail = AbortOnConnectFail;
        options.Ssl = UseSsl;

        if (!string.IsNullOrWhiteSpace(Password))
        {
            options.Password = Password;
        }

        return options;
    }
}

