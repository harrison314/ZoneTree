﻿namespace ZoneTree.Transactional;

public class CommitResult
{
    public readonly static CommitResult ReadyToCommit = new(CommitState.ReadyToCommit);

    public readonly static CommitResult Committed = new(CommitState.Committed);

    /// <summary>
    /// Represents the commit state.
    /// </summary>
    public CommitState Result { get; }

    /// <summary>
    /// If transaction result is "WaitUncommittedTransactions"
    /// this list is the transactions that are uncommitted.
    /// They should commit before this transaction commits.
    /// If any of the pending transactions abort,
    /// this transaction also aborts.
    /// </summary>
    public IReadOnlyList<long> PendingTransactionList { get; }

    public bool IsReadyToCommit => Result == CommitState.ReadyToCommit;

    public bool IsCommitted => Result == CommitState.Committed;

    public bool IsPendingTransactions => Result == CommitState.PendingTransactions;

    public CommitResult(
        CommitState result,
        IReadOnlyList<long> pendingTransactionsList = null)
    {
        Result = result;
        PendingTransactionList = pendingTransactionsList;
    }
}