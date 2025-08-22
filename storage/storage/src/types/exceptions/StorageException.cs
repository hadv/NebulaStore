using System;

namespace NebulaStore.Storage.Embedded.Types.Exceptions;

/// <summary>
/// Base exception type for all storage-related exceptions.
/// Check usages of this type, replace by better typed exceptions.
/// </summary>
public class StorageException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageException"/> class.
    /// </summary>
    public StorageException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageException"/> class with a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageException(Exception innerException) : base(innerException?.Message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a storage configuration is invalid.
/// </summary>
public class StorageConfigurationException : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageConfigurationException"/> class.
    /// </summary>
    public StorageConfigurationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageConfigurationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageConfigurationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageConfigurationException"/> class with a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageConfigurationException(Exception innerException) : base(innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageConfigurationException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a storage operation fails.
/// </summary>
public class StorageOperationException : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageOperationException"/> class.
    /// </summary>
    public StorageOperationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageOperationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageOperationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageOperationException"/> class with a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageOperationException(Exception innerException) : base(innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageOperationException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageOperationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a storage file operation fails.
/// </summary>
public class StorageFileException : StorageException
{
    /// <summary>
    /// Gets the file path that caused the exception.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageFileException"/> class.
    /// </summary>
    public StorageFileException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageFileException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageFileException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageFileException"/> class with a specified error message and file path.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="filePath">The file path that caused the exception.</param>
    public StorageFileException(string message, string filePath) : base(message)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageFileException"/> class with a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageFileException(Exception innerException) : base(innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageFileException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageFileException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageFileException"/> class with a specified error message, file path, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="filePath">The file path that caused the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageFileException(string message, string filePath, Exception innerException) : base(message, innerException)
    {
        FilePath = filePath;
    }
}

/// <summary>
/// Exception thrown when a storage backup operation fails.
/// </summary>
public class StorageBackupException : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageBackupException"/> class.
    /// </summary>
    public StorageBackupException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageBackupException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageBackupException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageBackupException"/> class with a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageBackupException(Exception innerException) : base(innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageBackupException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageBackupException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a storage housekeeping operation fails.
/// </summary>
public class StorageHousekeepingException : StorageException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StorageHousekeepingException"/> class.
    /// </summary>
    public StorageHousekeepingException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageHousekeepingException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public StorageHousekeepingException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageHousekeepingException"/> class with a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageHousekeepingException(Exception innerException) : base(innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StorageHousekeepingException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public StorageHousekeepingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
