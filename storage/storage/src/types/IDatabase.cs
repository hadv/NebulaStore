using System;
using System.IO;

namespace NebulaStore.Storage;

/// <summary>
/// Interface representing a database instance.
/// A database is a logical container for storage that can contain multiple storage managers.
/// </summary>
public interface IDatabase : IDatabasePart
{
    /// <summary>
    /// Gets the database name.
    /// </summary>
    new string DatabaseName { get; }

    /// <summary>
    /// Gets a value indicating whether the database is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets a value indicating whether the database is accepting tasks.
    /// </summary>
    bool IsAcceptingTasks { get; }

    /// <summary>
    /// Gets a value indicating whether the database is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Starts the database.
    /// </summary>
    /// <returns>This database instance for method chaining</returns>
    IDatabase Start();

    /// <summary>
    /// Shuts down the database.
    /// </summary>
    /// <returns>True if shutdown was successful</returns>
    bool Shutdown();

    /// <summary>
    /// Gets the storage configuration used by this database.
    /// </summary>
    IStorageConfiguration Configuration { get; }
}

/// <summary>
/// Interface for database parts.
/// Represents components that belong to a database.
/// </summary>
public interface IDatabasePart
{
    /// <summary>
    /// Gets the database name that this part belongs to.
    /// </summary>
    string DatabaseName { get; }
}

/// <summary>
/// Static utility class for database operations.
/// </summary>
public static class Databases
{
    /// <summary>
    /// Creates a new database with the specified name and configuration.
    /// </summary>
    /// <param name="databaseName">The name of the database</param>
    /// <param name="configuration">The storage configuration to use</param>
    /// <returns>A new database instance</returns>
    public static IDatabase New(string databaseName, IStorageConfiguration configuration)
    {
        return new Database(databaseName, configuration);
    }

    /// <summary>
    /// Creates a new database with the specified name and default configuration.
    /// </summary>
    /// <param name="databaseName">The name of the database</param>
    /// <returns>A new database instance</returns>
    public static IDatabase New(string databaseName)
    {
        return New(databaseName, Storage.Configuration());
    }

    /// <summary>
    /// Creates a new database with a default name and configuration.
    /// </summary>
    /// <returns>A new database instance</returns>
    public static IDatabase New()
    {
        return New("NebulaStore", Storage.Configuration());
    }
}

/// <summary>
/// Default implementation of the database interface.
/// </summary>
internal class Database : IDatabase
{
    private readonly object _lock = new();
    private bool _isRunning;
    private bool _isAcceptingTasks;
    private bool _isActive;

    public string DatabaseName { get; }
    public IStorageConfiguration Configuration { get; }

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _isRunning;
            }
        }
    }

    public bool IsAcceptingTasks
    {
        get
        {
            lock (_lock)
            {
                return _isAcceptingTasks;
            }
        }
    }

    public bool IsActive
    {
        get
        {
            lock (_lock)
            {
                return _isActive;
            }
        }
    }

    public Database(string databaseName, IStorageConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Database name cannot be null or empty", nameof(databaseName));

        DatabaseName = databaseName;
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public IDatabase Start()
    {
        lock (_lock)
        {
            if (_isRunning)
                return this;

            try
            {
                // Initialize database components
                InitializeDatabase();

                _isRunning = true;
                _isAcceptingTasks = true;
                _isActive = true;

                return this;
            }
            catch
            {
                // Ensure clean state on failure
                _isRunning = false;
                _isAcceptingTasks = false;
                _isActive = false;
                throw;
            }
        }
    }

    public bool Shutdown()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return true;

            try
            {
                // Stop accepting new tasks first
                _isAcceptingTasks = false;

                // Shutdown database components
                ShutdownDatabase();

                _isRunning = false;
                _isActive = false;

                return true;
            }
            catch
            {
                // Even if shutdown fails, mark as not running
                _isRunning = false;
                _isAcceptingTasks = false;
                _isActive = false;
                return false;
            }
        }
    }

    private void InitializeDatabase()
    {
        // Create storage directories if they don't exist
        var fileProvider = Configuration.FileProvider;
        
        if (!Directory.Exists(fileProvider.StorageDirectory))
            Directory.CreateDirectory(fileProvider.StorageDirectory);
        
        if (!Directory.Exists(fileProvider.DataDirectory))
            Directory.CreateDirectory(fileProvider.DataDirectory);
        
        if (!Directory.Exists(fileProvider.TransactionDirectory))
            Directory.CreateDirectory(fileProvider.TransactionDirectory);
        
        if (!Directory.Exists(fileProvider.TypeDictionaryDirectory))
            Directory.CreateDirectory(fileProvider.TypeDictionaryDirectory);

        // Initialize backup directory if backup is configured
        if (Configuration.BackupSetup?.IsEnabled == true)
        {
            var backupDir = Configuration.BackupSetup.BackupFileProvider.BackupDirectory;
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);
        }
    }

    private void ShutdownDatabase()
    {
        // Perform any necessary cleanup operations
        // This will be expanded as we add more components
    }

    public override string ToString()
    {
        return $"Database[{DatabaseName}] - Running: {IsRunning}, Active: {IsActive}";
    }
}
