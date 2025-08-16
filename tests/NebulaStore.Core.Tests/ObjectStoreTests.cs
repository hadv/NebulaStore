using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace NebulaStore.Core.Tests;

public class ObjectStoreTests : IDisposable
{
    private readonly string _testFilePath;

    public ObjectStoreTests()
    {
        _testFilePath = Path.GetTempFileName();
    }

    [Fact]
    public void Root_ShouldCreateNewInstanceWhenEmpty()
    {
        using var store = new ObjectStore(_testFilePath);
        
        var root = store.Root<TestData>();
        
        Assert.NotNull(root);
        Assert.IsType<TestData>(root);
    }

    [Fact]
    public void Commit_ShouldPersistData()
    {
        var testData = new TestData { Name = "Test", Value = 42 };
        
        using (var store = new ObjectStore(_testFilePath))
        {
            var root = store.Root<TestData>();
            root.Name = testData.Name;
            root.Value = testData.Value;
            store.Commit();
        }

        using (var store = new ObjectStore(_testFilePath))
        {
            var root = store.Root<TestData>();
            Assert.Equal(testData.Name, root.Name);
            Assert.Equal(testData.Value, root.Value);
        }
    }

    [Fact]
    public void Query_ShouldFindObjectsOfSpecificType()
    {
        using var store = new ObjectStore(_testFilePath);
        
        var root = store.Root<TestContainer>();
        root.Items.Add(new TestData { Name = "Item1", Value = 1 });
        root.Items.Add(new TestData { Name = "Item2", Value = 2 });
        
        var items = store.Query<TestData>().ToList();
        
        Assert.Equal(2, items.Count);
        Assert.Contains(items, i => i.Name == "Item1");
        Assert.Contains(items, i => i.Name == "Item2");
    }

    public void Dispose()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
    }

    [MessagePack.MessagePackObject]
    internal class TestData
    {
        [MessagePack.Key(0)]
        public string Name { get; set; } = string.Empty;
        
        [MessagePack.Key(1)]
        public int Value { get; set; }
    }

    [MessagePack.MessagePackObject]
    internal class TestContainer
    {
        [MessagePack.Key(0)]
        public List<TestData> Items { get; set; } = new();
    }
}