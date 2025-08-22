using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.Storage.Embedded.Distributed;

namespace NebulaStore.Storage.Tests.Distributed;

/// <summary>
/// Unit tests for Raft consensus algorithm implementation.
/// </summary>
public class RaftConsensusTests
{
    [Fact]
    public async Task StartAsync_ShouldInitializeAsFollower()
    {
        // Arrange
        var raft = CreateRaftConsensus("node1");

        // Act
        await raft.StartAsync();

        // Assert
        Assert.Equal(RaftState.Follower, raft.State);
        Assert.Equal(0, raft.CurrentTerm);
        Assert.Null(raft.CurrentLeader);
    }

    [Fact]
    public async Task HandleVoteRequest_WithValidRequest_ShouldGrantVote()
    {
        // Arrange
        var raft = CreateRaftConsensus("node1");
        await raft.StartAsync();

        var request = new VoteRequest
        {
            Term = 1,
            CandidateId = "node2",
            LastLogIndex = 0,
            LastLogTerm = 0
        };

        // Act
        var response = await raft.HandleVoteRequestAsync(request);

        // Assert
        Assert.True(response.VoteGranted);
        Assert.Equal(1, response.Term);
    }

    [Fact]
    public async Task HandleVoteRequest_WithOldTerm_ShouldRejectVote()
    {
        // Arrange
        var raft = CreateRaftConsensus("node1");
        await raft.StartAsync();

        // Simulate higher term
        await raft.HandleVoteRequestAsync(new VoteRequest { Term = 2, CandidateId = "node2" });

        var request = new VoteRequest
        {
            Term = 1, // Lower term
            CandidateId = "node3",
            LastLogIndex = 0,
            LastLogTerm = 0
        };

        // Act
        var response = await raft.HandleVoteRequestAsync(request);

        // Assert
        Assert.False(response.VoteGranted);
        Assert.Equal(2, response.Term);
    }

    [Fact]
    public async Task HandleAppendEntries_WithValidRequest_ShouldSucceed()
    {
        // Arrange
        var raft = CreateRaftConsensus("node1");
        await raft.StartAsync();

        var request = new AppendEntriesRequest
        {
            Term = 1,
            LeaderId = "node2",
            PrevLogIndex = 0,
            PrevLogTerm = 0,
            Entries = new List<LogEntry>(),
            LeaderCommit = 0
        };

        // Act
        var response = await raft.HandleAppendEntriesAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Equal(1, response.Term);
        Assert.Equal(RaftState.Follower, raft.State);
    }

    [Fact]
    public async Task ProposeAsync_AsFollower_ShouldReturnFalse()
    {
        // Arrange
        var raft = CreateRaftConsensus("node1");
        await raft.StartAsync();

        var data = System.Text.Encoding.UTF8.GetBytes("test data");

        // Act
        var result = await raft.ProposeAsync(data);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task StartElection_WithMajorityVotes_ShouldBecomeLeader()
    {
        // Arrange
        var membership = new MockClusterMembership();
        var communication = new MockNodeCommunication();
        var raft = CreateRaftConsensus("node1", membership, communication);

        // Add nodes to cluster
        membership.AddNode(new MockClusterNode("node1", new IPEndPoint(IPAddress.Loopback, 8080)));
        membership.AddNode(new MockClusterNode("node2", new IPEndPoint(IPAddress.Loopback, 8081)));
        membership.AddNode(new MockClusterNode("node3", new IPEndPoint(IPAddress.Loopback, 8082)));

        // Configure mock to grant votes
        communication.SetVoteResponse(new VoteResponse { VoteGranted = true, Term = 1 });

        await raft.StartAsync();

        var stateChanged = false;
        raft.StateChanged += (sender, args) => 
        {
            if (args.NewState == RaftState.Leader) stateChanged = true;
        };

        // Act
        var result = await raft.StartElectionAsync();

        // Assert
        Assert.True(result);
        Assert.True(stateChanged);
        Assert.Equal(RaftState.Leader, raft.State);
    }

    [Fact]
    public async Task StartElection_WithoutMajorityVotes_ShouldRemainFollower()
    {
        // Arrange
        var membership = new MockClusterMembership();
        var communication = new MockNodeCommunication();
        var raft = CreateRaftConsensus("node1", membership, communication);

        // Add nodes to cluster
        membership.AddNode(new MockClusterNode("node1", new IPEndPoint(IPAddress.Loopback, 8080)));
        membership.AddNode(new MockClusterNode("node2", new IPEndPoint(IPAddress.Loopback, 8081)));
        membership.AddNode(new MockClusterNode("node3", new IPEndPoint(IPAddress.Loopback, 8082)));

        // Configure mock to reject votes
        communication.SetVoteResponse(new VoteResponse { VoteGranted = false, Term = 1 });

        await raft.StartAsync();

        // Act
        var result = await raft.StartElectionAsync();

        // Assert
        Assert.False(result);
        Assert.Equal(RaftState.Follower, raft.State);
    }

    private RaftConsensus CreateRaftConsensus(
        string nodeId, 
        IClusterMembership? membership = null, 
        INodeCommunication? communication = null)
    {
        membership ??= new MockClusterMembership();
        communication ??= new MockNodeCommunication();
        var log = new MockRaftLog();
        var stateMachine = new MockRaftStateMachine();

        return new RaftConsensus(nodeId, membership, communication, log, stateMachine);
    }
}

/// <summary>
/// Unit tests for Raft log implementation.
/// </summary>
public class RaftLogTests
{
    [Fact]
    public async Task AppendAsync_ShouldAddEntryToLog()
    {
        // Arrange
        var log = new MockRaftLog();
        var entry = new LogEntry
        {
            Index = 1,
            Term = 1,
            Data = System.Text.Encoding.UTF8.GetBytes("test data"),
            Timestamp = DateTime.UtcNow
        };

        // Act
        await log.AppendAsync(entry);

        // Assert
        Assert.Equal(1, log.LastIndex);
        Assert.Equal(1, log.LastTerm);
        Assert.Equal(1, log.Count);
    }

    [Fact]
    public async Task GetEntryAsync_ShouldReturnCorrectEntry()
    {
        // Arrange
        var log = new MockRaftLog();
        var entry = new LogEntry
        {
            Index = 1,
            Term = 1,
            Data = System.Text.Encoding.UTF8.GetBytes("test data"),
            Timestamp = DateTime.UtcNow
        };

        await log.AppendAsync(entry);

        // Act
        var retrievedEntry = await log.GetEntryAsync(1);

        // Assert
        Assert.NotNull(retrievedEntry);
        Assert.Equal(entry.Index, retrievedEntry.Index);
        Assert.Equal(entry.Term, retrievedEntry.Term);
        Assert.Equal(entry.Data, retrievedEntry.Data);
    }

    [Fact]
    public async Task TruncateFromAsync_ShouldRemoveEntriesFromIndex()
    {
        // Arrange
        var log = new MockRaftLog();
        
        for (int i = 1; i <= 5; i++)
        {
            await log.AppendAsync(new LogEntry
            {
                Index = i,
                Term = 1,
                Data = System.Text.Encoding.UTF8.GetBytes($"data {i}"),
                Timestamp = DateTime.UtcNow
            });
        }

        // Act
        await log.TruncateFromAsync(3);

        // Assert
        Assert.Equal(2, log.LastIndex);
        Assert.Equal(2, log.Count);
        Assert.Null(await log.GetEntryAsync(3));
    }

    [Fact]
    public async Task CreateSnapshotAsync_ShouldCreateValidSnapshot()
    {
        // Arrange
        var log = new MockRaftLog();
        
        for (int i = 1; i <= 10; i++)
        {
            await log.AppendAsync(new LogEntry
            {
                Index = i,
                Term = 1,
                Data = System.Text.Encoding.UTF8.GetBytes($"data {i}"),
                Timestamp = DateTime.UtcNow
            });
        }

        // Act
        var snapshot = await log.CreateSnapshotAsync(5);

        // Assert
        Assert.Equal(5, snapshot.LastIncludedIndex);
        Assert.Equal(1, snapshot.LastIncludedTerm);
        Assert.NotEmpty(snapshot.Data);
    }
}

/// <summary>
/// Unit tests for replication manager.
/// </summary>
public class ReplicationManagerTests
{
    [Fact]
    public async Task StartAsync_ShouldInitializeReplicas()
    {
        // Arrange
        var raftConsensus = new MockRaftConsensus("node1");
        var membership = new MockClusterMembership();
        var partitioner = new MockDataPartitioner();
        var replicationManager = new ReplicationManager(raftConsensus, membership, partitioner);

        // Add healthy nodes
        membership.AddNode(new MockClusterNode("node1", new IPEndPoint(IPAddress.Loopback, 8080)));
        membership.AddNode(new MockClusterNode("node2", new IPEndPoint(IPAddress.Loopback, 8081)));

        // Act
        await replicationManager.StartAsync();

        // Assert
        var replicas = await replicationManager.GetReplicaStatusAsync();
        Assert.Single(replicas); // node2 only (node1 is self)
        Assert.Equal("node2", replicas[0].NodeId);
    }

    [Fact]
    public async Task ReplicateAsync_AsLeader_ShouldSucceed()
    {
        // Arrange
        var raftConsensus = new MockRaftConsensus("node1") { State = RaftState.Leader };
        var membership = new MockClusterMembership();
        var partitioner = new MockDataPartitioner();
        var replicationManager = new ReplicationManager(raftConsensus, membership, partitioner);

        var request = new ReplicationRequest
        {
            Key = "test-key",
            Data = System.Text.Encoding.UTF8.GetBytes("test data"),
            Mode = ReplicationMode.Async,
            RequiredAcks = 1
        };

        // Act
        var result = await replicationManager.ReplicateAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.RequiredReplicas);
    }

    [Fact]
    public async Task ReplicateAsync_AsFollower_ShouldFail()
    {
        // Arrange
        var raftConsensus = new MockRaftConsensus("node1") { State = RaftState.Follower };
        var membership = new MockClusterMembership();
        var partitioner = new MockDataPartitioner();
        var replicationManager = new ReplicationManager(raftConsensus, membership, partitioner);

        var request = new ReplicationRequest
        {
            Key = "test-key",
            Data = System.Text.Encoding.UTF8.GetBytes("test data")
        };

        // Act
        var result = await replicationManager.ReplicateAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Only the leader", result.ErrorMessage);
    }
}

#region Mock Implementations

public class MockRaftConsensus : IRaftConsensus
{
    public string NodeId { get; }
    public RaftState State { get; set; } = RaftState.Follower;
    public long CurrentTerm { get; set; } = 0;
    public string? CurrentLeader { get; set; }
    public long CommitIndex { get; set; } = 0;
    public long LastApplied { get; set; } = 0;

    public event EventHandler<RaftStateChangedEventArgs>? StateChanged;
    public event EventHandler<LogEntryCommittedEventArgs>? LogEntryCommitted;
    public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

    public MockRaftConsensus(string nodeId)
    {
        NodeId = nodeId;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> ProposeAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(State == RaftState.Leader);
    }

    public Task<VoteResponse> HandleVoteRequestAsync(VoteRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Term > CurrentTerm)
        {
            CurrentTerm = request.Term;
        }

        return Task.FromResult(new VoteResponse
        {
            Term = CurrentTerm,
            VoteGranted = request.Term >= CurrentTerm,
            Reason = "Mock response"
        });
    }

    public Task<AppendEntriesResponse> HandleAppendEntriesAsync(AppendEntriesRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Term > CurrentTerm)
        {
            CurrentTerm = request.Term;
        }

        CurrentLeader = request.LeaderId;
        State = RaftState.Follower;

        return Task.FromResult(new AppendEntriesResponse
        {
            Term = CurrentTerm,
            Success = true,
            MatchIndex = 0,
            Reason = "Mock response"
        });
    }

    public Task<bool> StartElectionAsync(CancellationToken cancellationToken = default)
    {
        CurrentTerm++;
        State = RaftState.Candidate;
        
        // Simulate election result based on mock setup
        var won = true; // Default to winning for simplicity
        
        if (won)
        {
            State = RaftState.Leader;
            CurrentLeader = NodeId;
            StateChanged?.Invoke(this, new RaftStateChangedEventArgs(NodeId, RaftState.Leader, CurrentTerm));
        }
        else
        {
            State = RaftState.Follower;
        }

        return Task.FromResult(won);
    }

    public void Dispose() { }
}

public class MockRaftLog : IRaftLog
{
    private readonly List<LogEntry> _entries = new();

    public long LastIndex => _entries.Count;
    public long LastTerm => _entries.LastOrDefault()?.Term ?? 0;
    public long Count => _entries.Count;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task AppendAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task AppendRangeAsync(IEnumerable<LogEntry> entries, CancellationToken cancellationToken = default)
    {
        _entries.AddRange(entries);
        return Task.CompletedTask;
    }

    public Task<LogEntry?> GetEntryAsync(long index, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(index > 0 && index <= _entries.Count ? _entries[(int)index - 1] : null);
    }

    public Task<IReadOnlyList<LogEntry>> GetEntriesFromAsync(long startIndex, int maxCount, CancellationToken cancellationToken = default)
    {
        var result = _entries.Skip((int)startIndex - 1).Take(maxCount).ToList();
        return Task.FromResult<IReadOnlyList<LogEntry>>(result);
    }

    public Task<IReadOnlyList<LogEntry>> GetEntriesRangeAsync(long startIndex, long endIndex, CancellationToken cancellationToken = default)
    {
        var result = _entries.Skip((int)startIndex - 1).Take((int)(endIndex - startIndex + 1)).ToList();
        return Task.FromResult<IReadOnlyList<LogEntry>>(result);
    }

    public Task TruncateFromAsync(long fromIndex, CancellationToken cancellationToken = default)
    {
        if (fromIndex > 0 && fromIndex <= _entries.Count)
        {
            _entries.RemoveRange((int)fromIndex - 1, _entries.Count - (int)fromIndex + 1);
        }
        return Task.CompletedTask;
    }

    public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<LogSnapshot> CreateSnapshotAsync(long lastIncludedIndex, CancellationToken cancellationToken = default)
    {
        var snapshot = new LogSnapshot
        {
            LastIncludedIndex = lastIncludedIndex,
            LastIncludedTerm = _entries[(int)lastIncludedIndex - 1].Term,
            Data = System.Text.Encoding.UTF8.GetBytes("snapshot data"),
            CreatedAt = DateTime.UtcNow
        };
        return Task.FromResult(snapshot);
    }

    public Task InstallSnapshotAsync(LogSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task CompactAsync(long retainFromIndex, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Dispose() { }
}

public class MockRaftStateMachine : IRaftStateMachine
{
    public long LastAppliedIndex { get; private set; } = 0;
    public int StateSize => 0;

    public event EventHandler<StateMachineAppliedEventArgs>? EntryApplied;
    public event EventHandler<StateMachineSnapshotEventArgs>? SnapshotCreated;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ApplyAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        LastAppliedIndex = entry.Index;
        EntryApplied?.Invoke(this, new StateMachineAppliedEventArgs(entry, null));
        return Task.CompletedTask;
    }

    public Task<StateMachineSnapshot> CreateSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = new StateMachineSnapshot
        {
            LastAppliedIndex = LastAppliedIndex,
            State = new Dictionary<string, object>(),
            CreatedAt = DateTime.UtcNow,
            Checksum = "mock-checksum"
        };
        return Task.FromResult(snapshot);
    }

    public Task InstallSnapshotAsync(StateMachineSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        LastAppliedIndex = snapshot.LastAppliedIndex;
        return Task.CompletedTask;
    }

    public Task<object?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<object?>(null);
    public Task<IReadOnlyList<string>> GetKeysAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>(new List<string>());
    public Task<IReadOnlyDictionary<string, object>> GetStateAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, object>>(new Dictionary<string, object>());

    public void Dispose() { }
}

public class MockDataPartitioner : IDataPartitioner
{
    public int PartitionCount => 0;
    public int ReplicationFactor => 3;

    public event EventHandler<PartitionChangedEventArgs>? PartitionChanged;
    public event EventHandler<DataMigrationEventArgs>? DataMigrationRequired;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public PartitionInfo GetPartition(string key) => new PartitionInfo { Key = key, PrimaryNode = "node1" };
    public Task<IReadOnlyList<PartitionInfo>> GetNodePartitionsAsync(string nodeId) => Task.FromResult<IReadOnlyList<PartitionInfo>>(new List<PartitionInfo>());
    public Task<DataDistributionStats> GetDistributionStatsAsync() => Task.FromResult(new DataDistributionStats());
    public Task<RebalanceResult> RebalanceAsync(CancellationToken cancellationToken = default) => Task.FromResult(new RebalanceResult());
    public Task AddNodeAsync(string nodeId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveNodeAsync(string nodeId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<MigrationPlan> GetMigrationPlanAsync(string fromNodeId, string toNodeId) => Task.FromResult(new MigrationPlan());
    public Task<PartitionValidationResult> ValidatePartitionsAsync() => Task.FromResult(new PartitionValidationResult { IsValid = true });

    public void Dispose() { }
}

#endregion
