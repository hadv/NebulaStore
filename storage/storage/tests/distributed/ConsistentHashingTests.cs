using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using NebulaStore.Storage.Embedded.Distributed;

namespace NebulaStore.Storage.Tests.Distributed;

/// <summary>
/// Unit tests for consistent hash ring functionality.
/// </summary>
public class ConsistentHashRingTests
{
    [Fact]
    public async Task AddNodeAsync_ShouldAddNodeWithVirtualNodes()
    {
        // Arrange
        var ring = new ConsistentHashRing();
        var nodeId = "node1";
        var metadata = CreateTestNodeMetadata(nodeId);

        // Act
        await ring.AddNodeAsync(nodeId, metadata);

        // Assert
        Assert.Equal(1, ring.PhysicalNodeCount);
        Assert.True(ring.VirtualNodeCount > 0);
        Assert.Contains(nodeId, ring.PhysicalNodeIds);
    }

    [Fact]
    public async Task AddNodeAsync_WithDuplicateNode_ShouldThrowException()
    {
        // Arrange
        var ring = new ConsistentHashRing();
        var nodeId = "node1";
        var metadata = CreateTestNodeMetadata(nodeId);

        await ring.AddNodeAsync(nodeId, metadata);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            ring.AddNodeAsync(nodeId, metadata));
    }

    [Fact]
    public async Task RemoveNodeAsync_ShouldRemoveNodeAndVirtualNodes()
    {
        // Arrange
        var ring = new ConsistentHashRing();
        var nodeId = "node1";
        var metadata = CreateTestNodeMetadata(nodeId);

        await ring.AddNodeAsync(nodeId, metadata);

        // Act
        await ring.RemoveNodeAsync(nodeId);

        // Assert
        Assert.Equal(0, ring.PhysicalNodeCount);
        Assert.Equal(0, ring.VirtualNodeCount);
        Assert.DoesNotContain(nodeId, ring.PhysicalNodeIds);
    }

    [Fact]
    public async Task GetPrimaryNode_ShouldReturnConsistentNode()
    {
        // Arrange
        var ring = new ConsistentHashRing();
        await ring.AddNodeAsync("node1", CreateTestNodeMetadata("node1"));
        await ring.AddNodeAsync("node2", CreateTestNodeMetadata("node2"));
        await ring.AddNodeAsync("node3", CreateTestNodeMetadata("node3"));

        var testKey = "test-key-123";

        // Act
        var node1 = ring.GetPrimaryNode(testKey);
        var node2 = ring.GetPrimaryNode(testKey);

        // Assert
        Assert.Equal(node1, node2); // Should be consistent
        Assert.Contains(node1, ring.PhysicalNodeIds);
    }

    [Fact]
    public async Task GetReplicaNodes_ShouldReturnCorrectNumberOfReplicas()
    {
        // Arrange
        var ring = new ConsistentHashRing();
        await ring.AddNodeAsync("node1", CreateTestNodeMetadata("node1"));
        await ring.AddNodeAsync("node2", CreateTestNodeMetadata("node2"));
        await ring.AddNodeAsync("node3", CreateTestNodeMetadata("node3"));

        var testKey = "test-key-123";
        var replicationFactor = 2;

        // Act
        var replicaNodes = ring.GetReplicaNodes(testKey, replicationFactor);

        // Assert
        Assert.Equal(replicationFactor, replicaNodes.Count);
        Assert.Equal(replicaNodes.Count, replicaNodes.Distinct().Count()); // No duplicates
    }

    [Fact]
    public async Task GetNodeKeyRangesAsync_ShouldReturnKeyRangesForNode()
    {
        // Arrange
        var ring = new ConsistentHashRing();
        var nodeId = "node1";
        await ring.AddNodeAsync(nodeId, CreateTestNodeMetadata(nodeId));

        // Act
        var keyRanges = await ring.GetNodeKeyRangesAsync(nodeId);

        // Assert
        Assert.NotEmpty(keyRanges);
        Assert.All(keyRanges, range => Assert.Equal(nodeId, range.NodeId));
    }

    [Fact]
    public async Task GetRingTopology_ShouldReturnCurrentTopology()
    {
        // Arrange
        var ring = new ConsistentHashRing();
        await ring.AddNodeAsync("node1", CreateTestNodeMetadata("node1"));
        await ring.AddNodeAsync("node2", CreateTestNodeMetadata("node2"));

        // Act
        var topology = ring.GetRingTopology();

        // Assert
        Assert.Equal(2, topology.Nodes.Count);
        Assert.True(topology.TotalVirtualNodes > 0);
        Assert.NotNull(topology.LoadDistribution);
    }

    [Fact]
    public async Task RebalanceAsync_WithBalancedRing_ShouldNotRequireRebalancing()
    {
        // Arrange
        var ring = new ConsistentHashRing();
        await ring.AddNodeAsync("node1", CreateTestNodeMetadata("node1"));
        await ring.AddNodeAsync("node2", CreateTestNodeMetadata("node2"));

        // Act
        var result = await ring.RebalanceAsync();

        // Assert
        Assert.False(result.IsRequired);
        Assert.Empty(result.DataMovements);
    }

    [Fact]
    public void HashDistribution_ShouldBeEvenlyDistributed()
    {
        // Arrange
        var ring = new ConsistentHashRing();
        var nodeIds = new[] { "node1", "node2", "node3", "node4" };
        
        // Add nodes
        foreach (var nodeId in nodeIds)
        {
            ring.AddNodeAsync(nodeId, CreateTestNodeMetadata(nodeId)).Wait();
        }

        // Generate test keys
        var testKeys = Enumerable.Range(0, 10000)
            .Select(i => $"key-{i}")
            .ToList();

        // Act - distribute keys
        var distribution = new Dictionary<string, int>();
        foreach (var nodeId in nodeIds)
        {
            distribution[nodeId] = 0;
        }

        foreach (var key in testKeys)
        {
            var node = ring.GetPrimaryNode(key);
            distribution[node]++;
        }

        // Assert - check distribution is reasonably even
        var expectedPerNode = testKeys.Count / nodeIds.Length;
        var tolerance = expectedPerNode * 0.2; // 20% tolerance

        foreach (var nodeId in nodeIds)
        {
            var count = distribution[nodeId];
            Assert.True(Math.Abs(count - expectedPerNode) <= tolerance,
                $"Node {nodeId} has {count} keys, expected ~{expectedPerNode} (±{tolerance})");
        }
    }

    private static NodeMetadata CreateTestNodeMetadata(string nodeId)
    {
        return new NodeMetadata
        {
            NodeId = nodeId,
            EndPoint = new IPEndPoint(IPAddress.Loopback, 8080),
            Role = NodeRole.Follower,
            Status = NodeStatus.Running,
            Capacity = 1.0
        };
    }
}

/// <summary>
/// Unit tests for data partitioner functionality.
/// </summary>
public class DataPartitionerTests
{
    [Fact]
    public async Task InitializeAsync_ShouldSetupPartitionsForHealthyNodes()
    {
        // Arrange
        var hashRing = new ConsistentHashRing();
        var membership = new MockClusterMembership();
        var partitioner = new DataPartitioner(hashRing, membership);

        // Add healthy nodes to membership
        var node1 = new MockClusterNode("node1", new IPEndPoint(IPAddress.Loopback, 8080));
        var node2 = new MockClusterNode("node2", new IPEndPoint(IPAddress.Loopback, 8081));
        membership.AddNode(node1);
        membership.AddNode(node2);

        // Act
        await partitioner.InitializeAsync();

        // Assert
        Assert.Equal(2, hashRing.PhysicalNodeCount);
    }

    [Fact]
    public void GetPartition_ShouldReturnPartitionInfo()
    {
        // Arrange
        var hashRing = new ConsistentHashRing();
        var membership = new MockClusterMembership();
        var partitioner = new DataPartitioner(hashRing, membership);

        // Add a node
        hashRing.AddNodeAsync("node1", CreateTestNodeMetadata("node1")).Wait();

        var testKey = "test-key";

        // Act
        var partition = partitioner.GetPartition(testKey);

        // Assert
        Assert.NotNull(partition);
        Assert.Equal(testKey, partition.Key);
        Assert.NotEmpty(partition.PrimaryNode);
        Assert.NotEmpty(partition.ReplicaNodes);
    }

    [Fact]
    public async Task GetNodePartitionsAsync_ShouldReturnPartitionsForNode()
    {
        // Arrange
        var hashRing = new ConsistentHashRing();
        var membership = new MockClusterMembership();
        var partitioner = new DataPartitioner(hashRing, membership);

        var nodeId = "node1";
        await hashRing.AddNodeAsync(nodeId, CreateTestNodeMetadata(nodeId));

        // Act
        var partitions = await partitioner.GetNodePartitionsAsync(nodeId);

        // Assert
        Assert.NotEmpty(partitions);
        Assert.All(partitions, p => Assert.Equal(nodeId, p.PrimaryNode));
    }

    [Fact]
    public async Task GetDistributionStatsAsync_ShouldReturnStats()
    {
        // Arrange
        var hashRing = new ConsistentHashRing();
        var membership = new MockClusterMembership();
        var partitioner = new DataPartitioner(hashRing, membership);

        await hashRing.AddNodeAsync("node1", CreateTestNodeMetadata("node1"));
        await hashRing.AddNodeAsync("node2", CreateTestNodeMetadata("node2"));

        // Act
        var stats = await partitioner.GetDistributionStatsAsync();

        // Assert
        Assert.Equal(2, stats.NodeCount);
        Assert.Equal(2, stats.NodeStats.Count);
        Assert.True(stats.TotalPartitions > 0);
    }

    [Fact]
    public async Task AddNodeAsync_ShouldTriggerRebalancing()
    {
        // Arrange
        var hashRing = new ConsistentHashRing();
        var membership = new MockClusterMembership();
        var partitioner = new DataPartitioner(hashRing, membership);

        var node1 = new MockClusterNode("node1", new IPEndPoint(IPAddress.Loopback, 8080));
        var node2 = new MockClusterNode("node2", new IPEndPoint(IPAddress.Loopback, 8081));
        membership.AddNode(node1);
        membership.AddNode(node2);

        await partitioner.InitializeAsync();

        var partitionChanged = false;
        partitioner.PartitionChanged += (sender, args) => partitionChanged = true;

        // Act
        await partitioner.AddNodeAsync("node2");

        // Assert
        Assert.Equal(2, hashRing.PhysicalNodeCount);
        Assert.True(partitionChanged);
    }

    [Fact]
    public async Task GetMigrationPlanAsync_ShouldCreateMigrationPlan()
    {
        // Arrange
        var hashRing = new ConsistentHashRing();
        var membership = new MockClusterMembership();
        var partitioner = new DataPartitioner(hashRing, membership);

        await hashRing.AddNodeAsync("node1", CreateTestNodeMetadata("node1"));
        await hashRing.AddNodeAsync("node2", CreateTestNodeMetadata("node2"));

        // Act
        var plan = await partitioner.GetMigrationPlanAsync("node1", "node2");

        // Assert
        Assert.NotNull(plan);
        Assert.Equal("node1", plan.FromNodeId);
        Assert.Equal("node2", plan.ToNodeId);
        Assert.NotEmpty(plan.Tasks);
    }

    [Fact]
    public async Task ValidatePartitionsAsync_ShouldDetectIssues()
    {
        // Arrange
        var hashRing = new ConsistentHashRing();
        var membership = new MockClusterMembership();
        var partitioner = new DataPartitioner(hashRing, membership, new DataPartitionerOptions
        {
            ReplicationFactor = 5 // More than available nodes
        });

        await hashRing.AddNodeAsync("node1", CreateTestNodeMetadata("node1"));

        // Act
        var result = await partitioner.ValidatePartitionsAsync();

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);
        Assert.Contains(result.Issues, i => i.Type == PartitionIssueType.UnderReplicated);
    }
}

/// <summary>
/// Unit tests for hash function implementations.
/// </summary>
public class HashFunctionTests
{
    [Theory]
    [InlineData(typeof(Sha256HashFunction))]
    [InlineData(typeof(Md5HashFunction))]
    [InlineData(typeof(MurmurHash3Function))]
    [InlineData(typeof(Crc32HashFunction))]
    [InlineData(typeof(Fnv1aHashFunction))]
    public void HashFunction_ShouldProduceConsistentResults(Type hashFunctionType)
    {
        // Arrange
        var hashFunction = (IHashFunction)Activator.CreateInstance(hashFunctionType)!;
        var testInput = "test-input-string";

        // Act
        var hash1 = hashFunction.ComputeHash(testInput);
        var hash2 = hashFunction.ComputeHash(testInput);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Theory]
    [InlineData(typeof(Sha256HashFunction))]
    [InlineData(typeof(Md5HashFunction))]
    [InlineData(typeof(MurmurHash3Function))]
    [InlineData(typeof(Crc32HashFunction))]
    [InlineData(typeof(Fnv1aHashFunction))]
    public void HashFunction_ShouldProduceDifferentHashesForDifferentInputs(Type hashFunctionType)
    {
        // Arrange
        var hashFunction = (IHashFunction)Activator.CreateInstance(hashFunctionType)!;
        var input1 = "test-input-1";
        var input2 = "test-input-2";

        // Act
        var hash1 = hashFunction.ComputeHash(input1);
        var hash2 = hashFunction.ComputeHash(input2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashFunctionFactory_ShouldCreateCorrectHashFunction()
    {
        // Act & Assert
        Assert.IsType<Sha256HashFunction>(HashFunctionFactory.Create(HashFunctionType.SHA256));
        Assert.IsType<Md5HashFunction>(HashFunctionFactory.Create(HashFunctionType.MD5));
        Assert.IsType<MurmurHash3Function>(HashFunctionFactory.Create(HashFunctionType.MurmurHash3));
        Assert.IsType<Crc32HashFunction>(HashFunctionFactory.Create(HashFunctionType.CRC32));
        Assert.IsType<Fnv1aHashFunction>(HashFunctionFactory.Create(HashFunctionType.FNV1a));
    }

    [Fact]
    public void HashFunctionCatalog_ShouldProvideCorrectInformation()
    {
        // Act
        var allFunctions = HashFunctionCatalog.GetAllFunctions();
        var cryptographicFunctions = HashFunctionCatalog.GetCryptographicFunctions();
        var nonCryptographicFunctions = HashFunctionCatalog.GetNonCryptographicFunctions();

        // Assert
        Assert.Equal(5, allFunctions.Count);
        Assert.Single(cryptographicFunctions); // Only SHA-256
        Assert.Equal(4, nonCryptographicFunctions.Count);
        
        var sha256Info = HashFunctionCatalog.GetInfo(HashFunctionType.SHA256);
        Assert.True(sha256Info.IsCryptographic);
        Assert.Equal(256, sha256Info.OutputBits);
    }

    [Fact]
    public void HashDistribution_ShouldBeReasonablyUniform()
    {
        // Arrange
        var hashFunction = new MurmurHash3Function();
        var bucketCount = 100;
        var testCount = 10000;
        var buckets = new int[bucketCount];

        // Act
        for (int i = 0; i < testCount; i++)
        {
            var hash = hashFunction.ComputeHash($"key-{i}");
            var bucket = (int)(hash % bucketCount);
            buckets[bucket]++;
        }

        // Assert
        var expectedPerBucket = testCount / bucketCount;
        var tolerance = expectedPerBucket * 0.3; // 30% tolerance

        for (int i = 0; i < bucketCount; i++)
        {
            Assert.True(Math.Abs(buckets[i] - expectedPerBucket) <= tolerance,
                $"Bucket {i} has {buckets[i]} items, expected ~{expectedPerBucket} (±{tolerance})");
        }
    }
}

#region Mock Implementations

public class MockClusterMembership : IClusterMembership
{
    private readonly List<IClusterNode> _nodes = new();

    public event EventHandler<NodeJoinedEventArgs>? NodeJoined;
    public event EventHandler<NodeLeftEventArgs>? NodeLeft;
    public event EventHandler<TopologyChangedEventArgs>? TopologyChanged;

    public Task JoinClusterAsync(IClusterNode node, CancellationToken cancellationToken = default)
    {
        _nodes.Add(node);
        NodeJoined?.Invoke(this, new NodeJoinedEventArgs(node));
        return Task.CompletedTask;
    }

    public Task LeaveClusterAsync(IClusterNode node, CancellationToken cancellationToken = default)
    {
        _nodes.Remove(node);
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

#endregion
