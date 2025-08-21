using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Interface for transaction management.
/// Provides ACID-compliant transaction support with isolation, consistency, rollback, and recovery.
/// </summary>
public interface ITransactionManager : IDisposable
{
    /// <summary>
    /// Gets the transaction isolation level.
    /// </summary>
    TransactionIsolationLevel IsolationLevel { get; }

    /// <summary>
    /// Gets whether transactions are currently enabled.
    /// </summary>
    bool TransactionsEnabled { get; }

    /// <summary>
    /// Gets the current active transaction for the calling thread.
    /// </summary>
    IStorageTransaction? CurrentTransaction { get; }

    /// <summary>
    /// Gets all active transactions.
    /// </summary>
    IReadOnlyList<IStorageTransaction> ActiveTransactions { get; }

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <param name="isolationLevel">Transaction isolation level</param>
    /// <returns>New transaction instance</returns>
    IStorageTransaction BeginTransaction(TransactionIsolationLevel? isolationLevel = null);

    /// <summary>
    /// Begins a new transaction asynchronously.
    /// </summary>
    /// <param name="isolationLevel">Transaction isolation level</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New transaction instance</returns>
    Task<IStorageTransaction> BeginTransactionAsync(TransactionIsolationLevel? isolationLevel = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits a transaction.
    /// </summary>
    /// <param name="transaction">Transaction to commit</param>
    /// <returns>Commit result</returns>
    ITransactionResult Commit(IStorageTransaction transaction);

    /// <summary>
    /// Commits a transaction asynchronously.
    /// </summary>
    /// <param name="transaction">Transaction to commit</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Commit result</returns>
    Task<ITransactionResult> CommitAsync(IStorageTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a transaction.
    /// </summary>
    /// <param name="transaction">Transaction to rollback</param>
    /// <returns>Rollback result</returns>
    ITransactionResult Rollback(IStorageTransaction transaction);

    /// <summary>
    /// Rolls back a transaction asynchronously.
    /// </summary>
    /// <param name="transaction">Transaction to rollback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rollback result</returns>
    Task<ITransactionResult> RollbackAsync(IStorageTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a savepoint within the current transaction.
    /// </summary>
    /// <param name="name">Savepoint name</param>
    /// <returns>Savepoint instance</returns>
    ITransactionSavepoint CreateSavepoint(string name);

    /// <summary>
    /// Rolls back to a specific savepoint.
    /// </summary>
    /// <param name="savepoint">Savepoint to rollback to</param>
    void RollbackToSavepoint(ITransactionSavepoint savepoint);

    /// <summary>
    /// Releases a savepoint.
    /// </summary>
    /// <param name="savepoint">Savepoint to release</param>
    void ReleaseSavepoint(ITransactionSavepoint savepoint);

    /// <summary>
    /// Gets transaction statistics.
    /// </summary>
    /// <returns>Transaction statistics</returns>
    ITransactionStatistics GetStatistics();

    /// <summary>
    /// Performs transaction recovery after system restart.
    /// </summary>
    /// <returns>Recovery result</returns>
    ITransactionRecoveryResult PerformRecovery();

    /// <summary>
    /// Event raised when a transaction is started.
    /// </summary>
    event EventHandler<TransactionEventArgs>? TransactionStarted;

    /// <summary>
    /// Event raised when a transaction is committed.
    /// </summary>
    event EventHandler<TransactionEventArgs>? TransactionCommitted;

    /// <summary>
    /// Event raised when a transaction is rolled back.
    /// </summary>
    event EventHandler<TransactionEventArgs>? TransactionRolledBack;

    /// <summary>
    /// Event raised when a deadlock is detected.
    /// </summary>
    event EventHandler<DeadlockEventArgs>? DeadlockDetected;
}

/// <summary>
/// Interface for storage transactions.
/// </summary>
public interface IStorageTransaction : IDisposable
{
    /// <summary>
    /// Gets the transaction ID.
    /// </summary>
    string TransactionId { get; }

    /// <summary>
    /// Gets the transaction start time.
    /// </summary>
    DateTime StartTime { get; }

    /// <summary>
    /// Gets the transaction isolation level.
    /// </summary>
    TransactionIsolationLevel IsolationLevel { get; }

    /// <summary>
    /// Gets the transaction state.
    /// </summary>
    TransactionState State { get; }

    /// <summary>
    /// Gets whether the transaction is read-only.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Gets the thread that owns this transaction.
    /// </summary>
    Thread OwnerThread { get; }

    /// <summary>
    /// Gets the transaction timeout.
    /// </summary>
    TimeSpan? Timeout { get; }

    /// <summary>
    /// Gets all operations performed in this transaction.
    /// </summary>
    IReadOnlyList<ITransactionOperation> Operations { get; }

    /// <summary>
    /// Gets all savepoints in this transaction.
    /// </summary>
    IReadOnlyList<ITransactionSavepoint> Savepoints { get; }

    /// <summary>
    /// Creates a storer for this transaction.
    /// </summary>
    /// <returns>Transaction-aware storer</returns>
    ITransactionStorer CreateStorer();

    /// <summary>
    /// Adds an operation to this transaction.
    /// </summary>
    /// <param name="operation">Operation to add</param>
    void AddOperation(ITransactionOperation operation);

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <returns>Commit result</returns>
    ITransactionResult Commit();

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    /// <returns>Rollback result</returns>
    ITransactionResult Rollback();

    /// <summary>
    /// Sets the transaction as read-only.
    /// </summary>
    void SetReadOnly();

    /// <summary>
    /// Sets the transaction timeout.
    /// </summary>
    /// <param name="timeout">Timeout duration</param>
    void SetTimeout(TimeSpan timeout);
}

/// <summary>
/// Interface for transaction-aware storer.
/// </summary>
public interface ITransactionStorer : IStorer
{
    /// <summary>
    /// Gets the transaction this storer belongs to.
    /// </summary>
    IStorageTransaction Transaction { get; }

    /// <summary>
    /// Gets whether this storer has uncommitted changes.
    /// </summary>
    bool HasUncommittedChanges { get; }

    /// <summary>
    /// Flushes changes to the transaction log without committing.
    /// </summary>
    void Flush();

    /// <summary>
    /// Discards all uncommitted changes.
    /// </summary>
    void DiscardChanges();
}

/// <summary>
/// Interface for transaction operations.
/// </summary>
public interface ITransactionOperation
{
    /// <summary>
    /// Gets the operation ID.
    /// </summary>
    string OperationId { get; }

    /// <summary>
    /// Gets the operation type.
    /// </summary>
    TransactionOperationType OperationType { get; }

    /// <summary>
    /// Gets the operation timestamp.
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Gets the entity ID affected by this operation.
    /// </summary>
    long EntityId { get; }

    /// <summary>
    /// Gets the operation data.
    /// </summary>
    byte[] OperationData { get; }

    /// <summary>
    /// Gets the undo data for rollback.
    /// </summary>
    byte[]? UndoData { get; }

    /// <summary>
    /// Executes the operation.
    /// </summary>
    void Execute();

    /// <summary>
    /// Undoes the operation.
    /// </summary>
    void Undo();
}

/// <summary>
/// Interface for transaction savepoints.
/// </summary>
public interface ITransactionSavepoint
{
    /// <summary>
    /// Gets the savepoint name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the savepoint ID.
    /// </summary>
    string SavepointId { get; }

    /// <summary>
    /// Gets the savepoint creation time.
    /// </summary>
    DateTime CreationTime { get; }

    /// <summary>
    /// Gets the transaction this savepoint belongs to.
    /// </summary>
    IStorageTransaction Transaction { get; }

    /// <summary>
    /// Gets the operation index at the time of savepoint creation.
    /// </summary>
    int OperationIndex { get; }
}

/// <summary>
/// Interface for transaction results.
/// </summary>
public interface ITransactionResult
{
    /// <summary>
    /// Gets whether the operation was successful.
    /// </summary>
    bool Success { get; }

    /// <summary>
    /// Gets the transaction ID.
    /// </summary>
    string TransactionId { get; }

    /// <summary>
    /// Gets the operation timestamp.
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Gets the number of operations affected.
    /// </summary>
    int OperationsAffected { get; }

    /// <summary>
    /// Gets any errors that occurred.
    /// </summary>
    IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Gets performance metrics.
    /// </summary>
    ITransactionMetrics Metrics { get; }
}

/// <summary>
/// Interface for transaction statistics.
/// </summary>
public interface ITransactionStatistics
{
    /// <summary>
    /// Gets the total number of transactions started.
    /// </summary>
    long TotalTransactionsStarted { get; }

    /// <summary>
    /// Gets the total number of transactions committed.
    /// </summary>
    long TotalTransactionsCommitted { get; }

    /// <summary>
    /// Gets the total number of transactions rolled back.
    /// </summary>
    long TotalTransactionsRolledBack { get; }

    /// <summary>
    /// Gets the number of currently active transactions.
    /// </summary>
    int ActiveTransactionCount { get; }

    /// <summary>
    /// Gets the average transaction duration.
    /// </summary>
    TimeSpan AverageTransactionDuration { get; }

    /// <summary>
    /// Gets the total number of deadlocks detected.
    /// </summary>
    long TotalDeadlocksDetected { get; }

    /// <summary>
    /// Gets the total number of savepoints created.
    /// </summary>
    long TotalSavepointsCreated { get; }

    /// <summary>
    /// Gets the commit success rate.
    /// </summary>
    double CommitSuccessRate { get; }
}

/// <summary>
/// Interface for transaction recovery results.
/// </summary>
public interface ITransactionRecoveryResult
{
    /// <summary>
    /// Gets whether recovery was successful.
    /// </summary>
    bool Success { get; }

    /// <summary>
    /// Gets the recovery start time.
    /// </summary>
    DateTime StartTime { get; }

    /// <summary>
    /// Gets the recovery end time.
    /// </summary>
    DateTime EndTime { get; }

    /// <summary>
    /// Gets the number of transactions recovered.
    /// </summary>
    int TransactionsRecovered { get; }

    /// <summary>
    /// Gets the number of transactions rolled back during recovery.
    /// </summary>
    int TransactionsRolledBack { get; }

    /// <summary>
    /// Gets any errors that occurred during recovery.
    /// </summary>
    IReadOnlyList<string> Errors { get; }
}

/// <summary>
/// Interface for transaction performance metrics.
/// </summary>
public interface ITransactionMetrics
{
    /// <summary>
    /// Gets the time spent acquiring locks.
    /// </summary>
    TimeSpan LockAcquisitionTime { get; }

    /// <summary>
    /// Gets the time spent writing to the transaction log.
    /// </summary>
    TimeSpan LogWriteTime { get; }

    /// <summary>
    /// Gets the time spent in commit processing.
    /// </summary>
    TimeSpan CommitProcessingTime { get; }

    /// <summary>
    /// Gets the number of lock conflicts encountered.
    /// </summary>
    int LockConflicts { get; }

    /// <summary>
    /// Gets the number of retry attempts.
    /// </summary>
    int RetryAttempts { get; }
}

/// <summary>
/// Enumeration of transaction isolation levels.
/// </summary>
public enum TransactionIsolationLevel
{
    /// <summary>
    /// Read uncommitted - lowest isolation level.
    /// </summary>
    ReadUncommitted,

    /// <summary>
    /// Read committed - prevents dirty reads.
    /// </summary>
    ReadCommitted,

    /// <summary>
    /// Repeatable read - prevents dirty and non-repeatable reads.
    /// </summary>
    RepeatableRead,

    /// <summary>
    /// Serializable - highest isolation level.
    /// </summary>
    Serializable
}

/// <summary>
/// Enumeration of transaction states.
/// </summary>
public enum TransactionState
{
    /// <summary>
    /// Transaction is active.
    /// </summary>
    Active,

    /// <summary>
    /// Transaction is preparing to commit.
    /// </summary>
    Preparing,

    /// <summary>
    /// Transaction is committed.
    /// </summary>
    Committed,

    /// <summary>
    /// Transaction is rolled back.
    /// </summary>
    RolledBack,

    /// <summary>
    /// Transaction is aborted due to error.
    /// </summary>
    Aborted
}

/// <summary>
/// Enumeration of transaction operation types.
/// </summary>
public enum TransactionOperationType
{
    /// <summary>
    /// Insert operation.
    /// </summary>
    Insert,

    /// <summary>
    /// Update operation.
    /// </summary>
    Update,

    /// <summary>
    /// Delete operation.
    /// </summary>
    Delete,

    /// <summary>
    /// Create savepoint operation.
    /// </summary>
    CreateSavepoint,

    /// <summary>
    /// Rollback to savepoint operation.
    /// </summary>
    RollbackToSavepoint
}

/// <summary>
/// Event arguments for transaction events.
/// </summary>
public class TransactionEventArgs : EventArgs
{
    public TransactionEventArgs(IStorageTransaction transaction)
    {
        Transaction = transaction;
    }

    public IStorageTransaction Transaction { get; }
}

/// <summary>
/// Event arguments for deadlock events.
/// </summary>
public class DeadlockEventArgs : EventArgs
{
    public DeadlockEventArgs(IReadOnlyList<IStorageTransaction> involvedTransactions)
    {
        InvolvedTransactions = involvedTransactions;
    }

    public IReadOnlyList<IStorageTransaction> InvolvedTransactions { get; }
}
