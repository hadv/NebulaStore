using System;

namespace NebulaStore.Storage;

/// <summary>
/// Base exception class for storage-related errors.
/// </summary>
public class StorageException : Exception
{
    /// <summary>
    /// Initializes a new instance of the StorageException class.
    /// </summary>
    public StorageException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the StorageException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error</param>
    public StorageException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the StorageException class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception</param>
    /// <param name="innerException">The exception that is the cause of the current exception</param>
    public StorageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a storage consistency error is detected.
/// </summary>
public class StorageConsistencyException : StorageException
{
    /// <summary>
    /// Initializes a new instance of the StorageConsistencyException class.
    /// </summary>
    public StorageConsistencyException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the StorageConsistencyException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error</param>
    public StorageConsistencyException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the StorageConsistencyException class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception</param>
    /// <param name="innerException">The exception that is the cause of the current exception</param>
    public StorageConsistencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when a storage initialization error occurs.
/// </summary>
public class StorageInitializationException : StorageException
{
    /// <summary>
    /// Initializes a new instance of the StorageInitializationException class.
    /// </summary>
    public StorageInitializationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the StorageInitializationException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error</param>
    public StorageInitializationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the StorageInitializationException class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception</param>
    /// <param name="innerException">The exception that is the cause of the current exception</param>
    public StorageInitializationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Exception thrown when an invalid entity length is detected.
/// </summary>
public class StorageInvalidEntityLengthException : StorageException
{
    /// <summary>
    /// Gets the object ID of the invalid entity.
    /// </summary>
    public long ObjectId { get; }

    /// <summary>
    /// Gets the invalid length.
    /// </summary>
    public long InvalidLength { get; }

    /// <summary>
    /// Gets the expected minimum length.
    /// </summary>
    public long? ExpectedMinimumLength { get; }

    /// <summary>
    /// Gets the expected maximum length.
    /// </summary>
    public long? ExpectedMaximumLength { get; }

    /// <summary>
    /// Initializes a new instance of the StorageInvalidEntityLengthException class.
    /// </summary>
    /// <param name="message">The message that describes the error</param>
    /// <param name="objectId">The object ID of the invalid entity</param>
    /// <param name="invalidLength">The invalid length</param>
    /// <param name="expectedMinimumLength">The expected minimum length</param>
    /// <param name="expectedMaximumLength">The expected maximum length</param>
    public StorageInvalidEntityLengthException(
        string message, 
        long objectId, 
        long invalidLength, 
        long? expectedMinimumLength = null, 
        long? expectedMaximumLength = null) 
        : base(message)
    {
        ObjectId = objectId;
        InvalidLength = invalidLength;
        ExpectedMinimumLength = expectedMinimumLength;
        ExpectedMaximumLength = expectedMaximumLength;
    }

    /// <summary>
    /// Initializes a new instance of the StorageInvalidEntityLengthException class with a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception</param>
    /// <param name="objectId">The object ID of the invalid entity</param>
    /// <param name="invalidLength">The invalid length</param>
    /// <param name="innerException">The exception that is the cause of the current exception</param>
    /// <param name="expectedMinimumLength">The expected minimum length</param>
    /// <param name="expectedMaximumLength">The expected maximum length</param>
    public StorageInvalidEntityLengthException(
        string message, 
        long objectId, 
        long invalidLength, 
        Exception innerException,
        long? expectedMinimumLength = null, 
        long? expectedMaximumLength = null) 
        : base(message, innerException)
    {
        ObjectId = objectId;
        InvalidLength = invalidLength;
        ExpectedMinimumLength = expectedMinimumLength;
        ExpectedMaximumLength = expectedMaximumLength;
    }
}

/// <summary>
/// Exception thrown when a type handler consistency error is detected.
/// </summary>
public class StorageTypeHandlerConsistencyException : StorageConsistencyException
{
    /// <summary>
    /// Gets the type ID that caused the consistency error.
    /// </summary>
    public long TypeId { get; }

    /// <summary>
    /// Initializes a new instance of the StorageTypeHandlerConsistencyException class.
    /// </summary>
    /// <param name="message">The message that describes the error</param>
    /// <param name="typeId">The type ID that caused the consistency error</param>
    public StorageTypeHandlerConsistencyException(string message, long typeId) : base(message)
    {
        TypeId = typeId;
    }

    /// <summary>
    /// Initializes a new instance of the StorageTypeHandlerConsistencyException class with a reference to the inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception</param>
    /// <param name="typeId">The type ID that caused the consistency error</param>
    /// <param name="innerException">The exception that is the cause of the current exception</param>
    public StorageTypeHandlerConsistencyException(string message, long typeId, Exception innerException) : base(message, innerException)
    {
        TypeId = typeId;
    }
}
