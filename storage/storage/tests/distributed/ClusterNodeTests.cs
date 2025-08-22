using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.Storage.Embedded.Distributed;

namespace NebulaStore.Storage.Tests.Distributed;

/// <summary>
/// Unit tests for cluster node functionality.
/// </summary>
public class ClusterNodeTests
{
    private readonly MockNodeCommunication _communication;
    private readonly MockClusterMembership _membership;
    private readonly MockHealthMonitor _healthMonitor;
    private readonly MockLeaderElection _leaderElection;

    public ClusterNodeTests()
    {
        _communication = new MockNodeCommunication();
        _membership = new MockClusterMembership();
        _healthMonitor = new MockHealthMonitor();
        _leaderElection = new MockLeaderElection();
    }

    [Fact]
    public async Task StartAsync_ShouldInitializeNodeCorrectly()
    {
        // Arrange
        var nodeId = "test-node-1";
        var endPoint = new IPEndPoint(IPAddress.Loopback, 8080);
        var node = new ClusterNode(nodeId, endPoint, _communication, _membership, _healthMonitor, _leaderElection);

        // Act
        await node.StartAsync();

        // Assert
        Assert.Equal(NodeStatus.Running, node.Status);
        Assert.Equal(NodeRole.Follower, node.Role);
        Assert.True(_communication.IsStarted);
        Assert.True(_membership.HasJoined);
        Assert.True(_healthMonitor.IsMonitoring);
        Assert.True(_leaderElection.IsStarted);
    }

    [Fact]
    public async Task StopAsync_ShouldCleanupNodeCorrectly()
    {
        // Arrange
        var nodeId = "test-node-1";
        var endPoint = new IPEndPoint(IPAddress.Loopback, 8080);
        var node = new ClusterNode(nodeId, endPoint, _communication, _membership, _healthMonitor, _leaderElection);

        await node.StartAsync();

        // Act
        await node.StopAsync();

        // Assert
        Assert.Equal(NodeStatus.Stopped, node.Status);
        Assert.False(_communication.IsStarted);
        Assert.False(_membership.HasJoined);
        Assert.False(_healthMonitor.IsMonitoring);
        Assert.False(_leaderElection.IsStarted);
    }

    [Fact]
    public async Task BecomeLeaderAsync_ShouldUpdateRoleAndTerm()
    {
        // Arrange
        var nodeId = "test-node-1";
        var endPoint = new IPEndPoint(IPAddress.Loopback, 8080);
        var node = new ClusterNode(nodeId, endPoint, _communication, _membership, _healthMonitor, _leaderElection);
        var term = 5L;

        var roleChanged = false;
        node.RoleChanged += (sender, args) => roleChanged = true;

        // Act
        await node.BecomeLeaderAsync(term);

        // Assert
        Assert.Equal(NodeRole.Leader, node.Role);
        Assert.Equal(term, node.Term);
        Assert.Equal(nodeId, node.LeaderId);
        Assert.True(roleChanged);
    }

    [Fact]
    public async Task BecomeFollowerAsync_ShouldUpdateRoleAndLeader()
    {
        // Arrange
        var nodeId = "test-node-1";
        var endPoint = new IPEndPoint(IPAddress.Loopback, 8080);
        var node = new ClusterNode(nodeId, endPoint, _communication, _membership, _healthMonitor, _leaderElection);
        var leaderId = "leader-node";
        var term = 3L;

        // First become leader
        await node.BecomeLeaderAsync(2L);

        var roleChanged = false;
        node.RoleChanged += (sender, args) => roleChanged = true;

        // Act
        await node.BecomeFollowerAsync(leaderId, term);

        // Assert
        Assert.Equal(NodeRole.Follower, node.Role);
        Assert.Equal(leaderId, node.LeaderId);
        Assert.Equal(term, node.Term);
        Assert.True(roleChanged);
    }

    [Fact]
    public async Task SendMessageAsync_ShouldRouteMessageCorrectly()
    {
        // Arrange
        var nodeId = "test-node-1";
        var endPoint = new IPEndPoint(IPAddress.Loopback, 8080);
        var node = new ClusterNode(nodeId, endPoint, _communication, _membership, _healthMonitor, _leaderElection);

        var targetNodeId = "target-node";
        var targetEndPoint = new IPEndPoint(IPAddress.Loopback, 8081);
        _membership.AddNode(new MockClusterNode(targetNodeId, targetEndPoint));

        var request = new TestMessage { Content = "Hello" };

        // Act
        var response = await node.SendMessageAsync<TestMessage, TestResponse>(targetNodeId, request);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("Echo: Hello", response.Content);
    }

    [Fact]
    public async Task BroadcastMessageAsync_ShouldSendToAllNodes()
    {
        // Arrange
        var nodeId = "test-node-1";
        var endPoint = new IPEndPoint(IPAddress.Loopback, 8080);
        var node = new ClusterNode(nodeId, endPoint, _communication, _membership, _healthMonitor, _leaderElection);

        // Add some nodes to the membership
        _membership.AddNode(new MockClusterNode("node-2", new IPEndPoint(IPAddress.Loopback, 8081)));
        _membership.AddNode(new MockClusterNode("node-3", new IPEndPoint(IPAddress.Loopback, 8082)));

        var message = new TestMessage { Content = "Broadcast" };

        // Act
        await node.BroadcastMessageAsync(message);

        // Assert
        Assert.Equal(2, _communication.BroadcastCount); // Should not send to self
    }

    [Fact]
    public async Task GetClusterTopologyAsync_ShouldReturnCurrentTopology()
    {
        // Arrange
        var nodeId = "test-node-1";
        var endPoint = new IPEndPoint(IPAddress.Loopback, 8080);
        var node = new ClusterNode(nodeId, endPoint, _communication, _membership, _healthMonitor, _leaderElection);

        _membership.AddNode(node);
        _membership.AddNode(new MockClusterNode("node-2", new IPEndPoint(IPAddress.Loopback, 8081)));

        await node.BecomeLeaderAsync(1L);

        // Act
        var topology = await node.GetClusterTopologyAsync();

        // Assert
        Assert.NotNull(topology);
        Assert.Equal(2, topology.Nodes.Count);
        Assert.Equal(nodeId, topology.LeaderId);
        Assert.Equal(1L, topology.Term);
    }
}

/// <summary>
/// Unit tests for cluster membership functionality.
/// </summary>
public class ClusterMembershipTests
{
    [Fact]
    public async Task JoinClusterAsync_ShouldAddNodeToMembership()
    {
        // Arrange
        var discovery = new MockNodeDiscovery();
        var membership = new ClusterMembership(discovery);
        var node = new MockClusterNode("test-node", new IPEndPoint(IPAddress.Loopback, 8080));

        var nodeJoined = false;
        membership.NodeJoined += (sender, args) => nodeJoined = true;

        // Act
        await membership.JoinClusterAsync(node);

        // Assert
        Assert.True(membership.IsNodeInCluster("test-node"));
        Assert.Equal(1, membership.GetClusterSize());
        Assert.True(nodeJoined);
    }

    [Fact]
    public async Task LeaveClusterAsync_ShouldRemoveNodeFromMembership()
    {
        // Arrange
        var discovery = new MockNodeDiscovery();
        var membership = new ClusterMembership(discovery);
        var node = new MockClusterNode("test-node", new IPEndPoint(IPAddress.Loopback, 8080));

        await membership.JoinClusterAsync(node);

        var nodeLeft = false;
        membership.NodeLeft += (sender, args) => nodeLeft = true;

        // Act
        await membership.LeaveClusterAsync(node);

        // Assert
        Assert.False(membership.IsNodeInCluster("test-node"));
        Assert.Equal(0, membership.GetClusterSize());
        Assert.True(nodeLeft);
    }

    [Fact]
    public void GetHealthyNodes_ShouldReturnOnlyRunningNodes()
    {
        // Arrange
        var discovery = new MockNodeDiscovery();
        var membership = new ClusterMembership(discovery);

        var healthyNode = new MockClusterNode("healthy-node", new IPEndPoint(IPAddress.Loopback, 8080))
        {
            Status = NodeStatus.Running
        };
        var unhealthyNode = new MockClusterNode("unhealthy-node", new IPEndPoint(IPAddress.Loopback, 8081))
        {
            Status = NodeStatus.Failed
        };

        membership.HandleNodeJoinAsync(new NodeInfo { NodeId = "healthy-node", EndPoint = healthyNode.EndPoint });
        membership.HandleNodeJoinAsync(new NodeInfo { NodeId = "unhealthy-node", EndPoint = unhealthyNode.EndPoint });

        // Act
        var healthyNodes = membership.GetHealthyNodes();

        // Assert
        Assert.Single(healthyNodes);
        Assert.Equal("healthy-node", healthyNodes[0].NodeId);
    }
}

/// <summary>
/// Unit tests for leader election functionality.
/// </summary>
public class LeaderElectionTests
{
    [Fact]
    public async Task StartElectionAsync_WithMajorityVotes_ShouldBecomeLeader()
    {
        // Arrange
        var membership = new MockClusterMembership();
        var communication = new MockNodeCommunication();
        var election = new LeaderElection(membership, communication);

        var localNode = new MockClusterNode("candidate", new IPEndPoint(IPAddress.Loopback, 8080));
        membership.AddNode(localNode);
        membership.AddNode(new MockClusterNode("voter1", new IPEndPoint(IPAddress.Loopback, 8081)));
        membership.AddNode(new MockClusterNode("voter2", new IPEndPoint(IPAddress.Loopback, 8082)));

        // Configure mock to grant votes
        communication.SetVoteResponse(new VoteResponse { VoteGranted = true, Term = 1 });

        await election.StartAsync(localNode);

        var leaderElected = false;
        election.LeaderElected += (sender, args) => leaderElected = true;

        // Act
        var result = await election.StartElectionAsync();

        // Assert
        Assert.True(result);
        Assert.True(leaderElected);
        Assert.Equal("candidate", election.GetCurrentLeader());
    }

    [Fact]
    public async Task HandleVoteRequestAsync_WithValidRequest_ShouldGrantVote()
    {
        // Arrange
        var membership = new MockClusterMembership();
        var communication = new MockNodeCommunication();
        var election = new LeaderElection(membership, communication);

        var request = new VoteRequest
        {
            Term = 2,
            CandidateId = "candidate",
            LastLogIndex = 0,
            LastLogTerm = 0
        };

        // Act
        var response = await election.HandleVoteRequestAsync(request);

        // Assert
        Assert.True(response.VoteGranted);
        Assert.Equal(2, response.Term);
    }

    [Fact]
    public async Task HandleHeartbeatAsync_ShouldUpdateLeaderInfo()
    {
        // Arrange
        var membership = new MockClusterMembership();
        var communication = new MockNodeCommunication();
        var election = new LeaderElection(membership, communication);

        var heartbeat = new HeartbeatMessage
        {
            LeaderId = "leader",
            Term = 3,
            Timestamp = DateTime.UtcNow
        };

        // Act
        await election.HandleHeartbeatAsync(heartbeat);

        // Assert
        Assert.Equal("leader", election.GetCurrentLeader());
        Assert.Equal(3, election.GetCurrentTerm());
    }
}

#region Mock Implementations

public class MockNodeCommunication : INodeCommunication
{
    public bool IsStarted { get; private set; }
    public int BroadcastCount { get; private set; }
    private VoteResponse? _voteResponse;

    public Task StartAsync(IPEndPoint endPoint, CancellationToken cancellationToken = default)
    {
        IsStarted = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsStarted = false;
        return Task.CompletedTask;
    }

    public Task<TResponse> SendMessageAsync<TRequest, TResponse>(IPEndPoint targetEndPoint, TRequest message, CancellationToken cancellationToken = default)
        where TRequest : class where TResponse : class
    {
        if (typeof(TRequest) == typeof(VoteRequest) && _voteResponse != null)
        {
            return Task.FromResult((TResponse)(object)_voteResponse);
        }

        if (typeof(TRequest) == typeof(TestMessage) && typeof(TResponse) == typeof(TestResponse))
        {
            var testMessage = message as TestMessage;
            var response = new TestResponse { Content = $"Echo: {testMessage?.Content}" };
            return Task.FromResult((TResponse)(object)response);
        }

        throw new NotSupportedException($"Mock does not support {typeof(TRequest).Name} -> {typeof(TResponse).Name}");
    }

    public Task SendOneWayMessageAsync<TMessage>(IPEndPoint targetEndPoint, TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        BroadcastCount++;
        return Task.CompletedTask;
    }

    public void SetVoteResponse(VoteResponse response)
    {
        _voteResponse = response;
    }
}

public class MockClusterMembership : IClusterMembership
{
    private readonly List<IClusterNode> _nodes = new();

    public event EventHandler<NodeJoinedEventArgs>? NodeJoined;
    public event EventHandler<NodeLeftEventArgs>? NodeLeft;
    public event EventHandler<TopologyChangedEventArgs>? TopologyChanged;

    public bool HasJoined { get; private set; }

    public Task JoinClusterAsync(IClusterNode node, CancellationToken cancellationToken = default)
    {
        _nodes.Add(node);
        HasJoined = true;
        NodeJoined?.Invoke(this, new NodeJoinedEventArgs(node));
        return Task.CompletedTask;
    }

    public Task LeaveClusterAsync(IClusterNode node, CancellationToken cancellationToken = default)
    {
        _nodes.Remove(node);
        HasJoined = false;
        NodeLeft?.Invoke(this, new NodeLeftEventArgs(node));
        return Task.CompletedTask;
    }

    public IReadOnlyList<IClusterNode> GetKnownNodes() => _nodes;
    public IClusterNode? GetNode(string nodeId) => _nodes.FirstOrDefault(n => n.NodeId == nodeId);
    public IReadOnlyList<IClusterNode> GetHealthyNodes() => _nodes.Where(n => n.Status == NodeStatus.Running).ToList();
    public IClusterNode? GetLeader() => _nodes.FirstOrDefault(n => n.Role == NodeRole.Leader);
    public bool IsNodeInCluster(string nodeId) => _nodes.Any(n => n.NodeId == nodeId);
    public int GetClusterSize() => _nodes.Count;
    public int GetHealthyNodeCount() => _nodes.Count(n => n.Status == NodeStatus.Running);

    public Task PerformMaintenanceAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpdateNodeStatusAsync(string nodeId, NodeStatus status) => Task.CompletedTask;
    public Task HandleNodeJoinAsync(NodeInfo nodeInfo, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task HandleNodeLeaveAsync(string nodeId) => Task.CompletedTask;

    public void AddNode(IClusterNode node)
    {
        _nodes.Add(node);
    }
}

public class MockHealthMonitor : IHealthMonitor
{
    public bool IsMonitoring { get; private set; }

    public event EventHandler<NodeHealthChangedEventArgs>? NodeHealthChanged;

    public Task StartMonitoringAsync(IClusterNode localNode, CancellationToken cancellationToken = default)
    {
        IsMonitoring = true;
        return Task.CompletedTask;
    }

    public Task StopMonitoringAsync(CancellationToken cancellationToken = default)
    {
        IsMonitoring = false;
        return Task.CompletedTask;
    }

    public Task PerformHealthChecksAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<NodeHealthStatus> GetNodeHealthAsync(string nodeId) => Task.FromResult(NodeHealthStatus.Healthy);
    public Task<ClusterHealthStatus> GetClusterHealthAsync() => Task.FromResult(new ClusterHealthStatus { OverallStatus = ClusterHealthStatus.Healthy });
}

public class MockLeaderElection : ILeaderElection
{
    public bool IsStarted { get; private set; }

    public event EventHandler<LeaderElectedEventArgs>? LeaderElected;
    public event EventHandler<TermChangedEventArgs>? TermChanged;

    public Task StartAsync(IClusterNode localNode, CancellationToken cancellationToken = default)
    {
        IsStarted = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsStarted = false;
        return Task.CompletedTask;
    }

    public Task<bool> StartElectionAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<VoteResponse> HandleVoteRequestAsync(VoteRequest request, CancellationToken cancellationToken = default) => 
        Task.FromResult(new VoteResponse { VoteGranted = true, Term = request.Term });
    public Task HandleHeartbeatAsync(HeartbeatMessage heartbeat) => Task.CompletedTask;
    public long GetCurrentTerm() => 1;
    public string? GetCurrentLeader() => null;

    public void Dispose() { }
}

public class MockNodeDiscovery : INodeDiscovery
{
    public event EventHandler<NodeDiscoveredEventArgs>? NodeDiscovered;
    public event EventHandler<NodeLostEventArgs>? NodeLost;

    public Task StartDiscoveryAsync(IClusterNode localNode, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopDiscoveryAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RefreshDiscoveryAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public IReadOnlyList<NodeInfo> GetDiscoveredNodes() => new List<NodeInfo>();
    public NodeInfo? GetDiscoveredNode(string nodeId) => null;

    public void Dispose() { }
}

public class MockClusterNode : IClusterNode
{
    public string NodeId { get; }
    public IPEndPoint EndPoint { get; }
    public NodeRole Role { get; set; } = NodeRole.Follower;
    public NodeStatus Status { get; set; } = NodeStatus.Running;
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public string? LeaderId { get; set; }
    public long Term { get; set; }
    public IReadOnlyList<IClusterNode> KnownNodes => new List<IClusterNode>();

    public event EventHandler<NodeRoleChangedEventArgs>? RoleChanged;
    public event EventHandler<NodeStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<LeaderChangedEventArgs>? LeaderChanged;

    public MockClusterNode(string nodeId, IPEndPoint endPoint)
    {
        NodeId = nodeId;
        EndPoint = endPoint;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<TResponse> SendMessageAsync<TRequest, TResponse>(string targetNodeId, TRequest message, CancellationToken cancellationToken = default)
        where TRequest : class where TResponse : class
    {
        throw new NotImplementedException();
    }

    public Task BroadcastMessageAsync<TMessage>(TMessage message, CancellationToken cancellationToken = default)
        where TMessage : class => Task.CompletedTask;

    public Task BecomeLeaderAsync(long term, CancellationToken cancellationToken = default)
    {
        Role = NodeRole.Leader;
        Term = term;
        LeaderId = NodeId;
        return Task.CompletedTask;
    }

    public Task BecomeFollowerAsync(string? leaderId = null, long? term = null)
    {
        Role = NodeRole.Follower;
        if (leaderId != null) LeaderId = leaderId;
        if (term.HasValue) Term = term.Value;
        return Task.CompletedTask;
    }

    public Task BecomeCandidateAsync(long term)
    {
        Role = NodeRole.Candidate;
        Term = term;
        return Task.CompletedTask;
    }

    public Task<ClusterTopology> GetClusterTopologyAsync() => Task.FromResult(new ClusterTopology());

    public void Dispose() { }
}

public class TestMessage
{
    public string Content { get; set; } = string.Empty;
}

public class TestResponse
{
    public string Content { get; set; } = string.Empty;
}

#endregion
