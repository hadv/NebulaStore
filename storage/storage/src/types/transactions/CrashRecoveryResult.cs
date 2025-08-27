using System;
using System.Collections.Generic;

namespace NebulaStore.Storage.Embedded.Types.Transactions;

/// <summary>
/// Represents the status of a crash recovery operation.
/// </summary>
public enum RecoveryStatus
{
    /// <summary>
    /// No recovery was needed - storage is clean.
    /// </summary>
    NoRecoveryNeeded,

    /// <summary>
    /// Storage was in consistent state - no recovery needed.
    /// </summary>
    ConsistentState,

    /// <summary>
    /// Recovery was performed successfully.
    /// </summary>
    RecoveryPerformed,

    /// <summary>
    /// Recovery failed due to an error.
    /// </summary>
    RecoveryFailed
}

/// <summary>
/// Contains the results of a crash recovery operation following Eclipse Store patterns.
/// </summary>
public class CrashRecoveryResult
{
    /// <summary>
    /// Gets or sets the recovery status.
    /// </summary>
    public RecoveryStatus Status { get; set; } = RecoveryStatus.NoRecoveryNeeded;

    /// <summary>
    /// Gets or sets the recovery message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exception if recovery failed.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the number of transaction log files found.
    /// </summary>
    public int LogFilesFound { get; set; }

    /// <summary>
    /// Gets or sets the total number of transactions found in logs.
    /// </summary>
    public int TotalTransactionsFound { get; set; }

    /// <summary>
    /// Gets or sets the number of committed transactions.
    /// </summary>
    public int CommittedTransactions { get; set; }

    /// <summary>
    /// Gets or sets the number of uncommitted transactions.
    /// </summary>
    public int UncommittedTransactions { get; set; }

    /// <summary>
    /// Gets or sets the number of inconsistent files found.
    /// </summary>
    public int InconsistentFiles { get; set; }

    /// <summary>
    /// Gets the list of recovery actions performed.
    /// </summary>
    public List<string> ActionsPerformed { get; } = new List<string>();

    /// <summary>
    /// Gets or sets the time taken for recovery.
    /// </summary>
    public TimeSpan RecoveryTime { get; set; }

    /// <summary>
    /// Gets a value indicating whether recovery was successful.
    /// </summary>
    public bool IsSuccessful => Status == RecoveryStatus.NoRecoveryNeeded ||
                               Status == RecoveryStatus.ConsistentState ||
                               Status == RecoveryStatus.RecoveryPerformed;

    /// <summary>
    /// Gets a summary of the recovery operation.
    /// </summary>
    public string Summary => $"Status: {Status}, " +
                           $"Log Files: {LogFilesFound}, " +
                           $"Transactions: {TotalTransactionsFound} ({CommittedTransactions} committed, {UncommittedTransactions} uncommitted), " +
                           $"Inconsistent Files: {InconsistentFiles}, " +
                           $"Actions: {ActionsPerformed.Count}, " +
                           $"Time: {RecoveryTime.TotalMilliseconds:F0}ms";

    /// <summary>
    /// Returns a string representation of the recovery result.
    /// </summary>
    /// <returns>A string representation of the recovery result.</returns>
    public override string ToString()
    {
        return $"CrashRecoveryResult: {Summary}";
    }
}