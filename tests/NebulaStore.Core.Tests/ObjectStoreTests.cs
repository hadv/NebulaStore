using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using NebulaStore.Core.Storage;

namespace NebulaStore.Core.Tests;

/// <summary>
/// Tests for the embedded storage system (formerly ObjectStore tests).
/// Now tests the primary embedded storage API directly.
/// </summary>
public class ObjectStoreTests : IDisposable
{
    private readonly string _testDirectory;

    public ObjectStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void Root_ShouldCreateNewInstanceWhenEmpty()
    {
        using var storage = EmbeddedStorage.Start(_testDirectory);

        var root = storage.Root<TestData>();

        Assert.NotNull(root);
        Assert.IsType<TestData>(root);
    }

    [Fact]
    public void StoreRoot_ShouldPersistData()
    {
        var testData = new TestData { Name = "Test", Value = 42 };

        using (var storage = EmbeddedStorage.Start(_testDirectory))
        {
            var root = storage.Root<TestData>();
            root.Name = testData.Name;
            root.Value = testData.Value;
            storage.StoreRoot();
        }

        using (var storage = EmbeddedStorage.Start(_testDirectory))
        {
            var root = storage.Root<TestData>();
            Assert.Equal(testData.Name, root.Name);
            Assert.Equal(testData.Value, root.Value);
        }
    }

    [Fact]
    public void Query_ShouldFindObjectsOfSpecificType()
    {
        using var storage = EmbeddedStorage.Start(_testDirectory);

        var root = storage.Root<TestContainer>();
        root.Items.Add(new TestData { Name = "Item1", Value = 1 });
        root.Items.Add(new TestData { Name = "Item2", Value = 2 });
        storage.StoreRoot();

        var items = storage.Query<TestData>().ToList();

        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Name == "Item1");
        Assert.Contains(items, i => i.Name == "Item2");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [MessagePack.MessagePackObject(AllowPrivate = true)]
    internal class TestData
    {
        [MessagePack.Key(0)]
        public string Name { get; set; } = string.Empty;
        
        [MessagePack.Key(1)]
        public int Value { get; set; }
    }

    [MessagePack.MessagePackObject(AllowPrivate = true)]
    internal class TestContainer
    {
        [MessagePack.Key(0)]
        public List<TestData> Items { get; set; } = new();
    }
}