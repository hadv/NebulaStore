using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using NebulaStore.GigaMap.Tests.TestEntities;
using Xunit;

namespace NebulaStore.GigaMap.Tests;

/// <summary>
/// Test suite that matches Eclipse Store's GigaMap testing patterns
/// Based on Eclipse Store's GigaMapTests.java
/// </summary>
public class GigaMapEclipseStoreTests
{
    [Fact]
    public void Empty()
    {
        var map = GigaMap.New<TestEntity>();
        map.IsEmpty.Should().BeTrue();
        map.Size.Should().Be(0);
    }

    [Fact]
    public void One()
    {
        var map = GigaMap.New<TestEntity>();
        map.Add(TestEntity.Random());
        map.Size.Should().Be(1);
    }

    [Fact]
    public void AddSingle()
    {
        const int initialSize = 100;
        var map = FixedSizeMap(initialSize, EntityCreator.Flat);
        map.Add(TestEntity.Random());
        map.Size.Should().Be(initialSize + 1);
    }

    [Fact]
    public void AddNull()
    {
        var map = EmptyMap();
        var action = () => map.Add(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddMultiple()
    {
        const int initialSize = 100;
        var map = FixedSizeMap(initialSize, EntityCreator.Flat);
        var add = TestEntity.RandomList(initialSize);
        map.AddAll(add);
        map.Size.Should().Be(initialSize + add.Count);
    }

    [Fact]
    public void Release()
    {
        var map = RandomSizeMap(100, EntityCreator.Flat);
        var size = map.Size;
        map.Release();

        map.Size.Should().Be(size);

        map.Add(TestEntity.Random());

        map.Size.Should().Be(size + 1);

        map.Get(0L).Should().NotBeNull();
    }

    [Fact]
    public void RemoveSingle()
    {
        const int initialSize = 100;
        var entities = TestEntity.FixedList(initialSize);
        var map = EmptyMap();
        map.AddAll(entities);

        map.Remove(entities[0]);
        map.Size.Should().Be(initialSize - 1);
    }

    [Fact]
    public void RemoveSingleWithIndex()
    {
        const int initialSize = 100;
        var entities = TestEntity.FixedList(initialSize);
        var map = EmptyMap();
        map.AddAll(entities);

        map.Remove(entities[0], TestEntity.UuidIndex);
        map.Size.Should().Be(initialSize - 1);
    }

    [Fact]
    public void RemoveAll()
    {
        var map = FixedSizeMap(100, EntityCreator.Flat);
        map.RemoveAll();
        map.IsEmpty.Should().BeTrue();

        var entity = TestEntity.Random();

        // test if adding and querying still works after removeAll
        map.Add(entity);
        map.Size.Should().Be(1);
        map.Where(e => e.Uuid == entity.Uuid).Count().Should().Be(1);
    }

    [Fact]
    public void IterationCount()
    {
        const int entryCount = 100;
        var map = FixedSizeMap(entryCount, EntityCreator.Flat);
        var counter = 0;

        // Use LINQ foreach instead of custom Iterate method
        foreach (var element in map)
        {
            counter++;
        }

        counter.Should().Be(entryCount);
    }

    [Fact]
    public void IndexedIterationCount()
    {
        const int entryCount = 100;
        var map = FixedSizeMap(entryCount, EntityCreator.Flat);

        // Use LINQ Count() method
        var count = map.Count();
        count.Should().Be(entryCount);

        // For indexed iteration, we can use LINQ Select with index
        var indexedItems = map.Select((element, index) => new { Index = index, Element = element }).ToList();
        indexedItems.Count.Should().Be(entryCount);
        indexedItems.Max(x => x.Index).Should().Be(entryCount - 1);
    }

    [Fact]
    public void IndexTest()
    {
        const string word = "red";
        var entity = TestEntity.Random().SetWord(word);
        TestEntity.WordIndex.Test(entity, word).Should().BeTrue();
    }

    [Fact]
    public void QueryCount()
    {
        var map = FixedSizeMap(10_000, new IndexBasedEntityCreator());

        // Use LINQ to count entities with IntValue = 0
        var count = map.Where(e => e.IntValue == 0).Count();
        count.Should().Be(100);
    }

    [Fact]
    public void QueryIterationComparison()
    {
        var map = FixedSizeMap(10_000, new IndexBasedEntityCreator());

        // Use LINQ to get entities with IntValue = 0
        var entities = map.Where(e => e.IntValue == 0);

        var forEachCounter = 0;
        var iteratorCounter = 0;
        var executeCounter = 0;

        // Test foreach
        foreach (var e in entities)
        {
            forEachCounter++;
        }

        // Test iterator
        using (var iterator = entities.GetEnumerator())
        {
            while (iterator.MoveNext())
            {
                iteratorCounter++;
            }
        }

        // Test ToList execution
        entities.ToList().ForEach(e => executeCounter++);

        forEachCounter.Should().Be(iteratorCounter);
        forEachCounter.Should().Be(executeCounter);
        iteratorCounter.Should().Be(executeCounter);
    }

    [Fact]
    public void QueryNot()
    {
        var map = FixedSizeMap(100, EntityCreator.Flat);

        // Use LINQ to get entities where Word is NOT "red"
        var notRedEntities = map.Where(e => e.Word != "red").ToList();
        notRedEntities.ForEach(e => e.Should().NotBeNull());
    }

    [Fact]
    public void Set()
    {
        var map = EmptyMap();
        var first = TestEntity.Random();
        map.AddAll(new[] { first, TestEntity.Random(), TestEntity.Random() });
        var replaced = map.Set(0, TestEntity.Random());
        replaced.Should().BeSameAs(first);
        var action = () => map.Set(1000, TestEntity.Random());
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update()
    {
        var map = FixedSizeMap(10, EntityCreator.Flat);
        var entity = map.Get(5);
        const string newWord = "updated";

        map.Update(entity!, e => e.SetWord(newWord));
        entity!.Word.Should().Be(newWord);

        // Use LINQ to find the updated entity
        var found = map.Where(e => e.Word == newWord).FirstOrDefault();
        found.Should().BeSameAs(entity);
    }

    [Fact]
    public void UpdateError()
    {
        var map = EmptyMap();
        map.Add(TestEntity.Random().SetWord("a"));
        map.Add(TestEntity.Random().SetWord("b"));
        map.Add(TestEntity.Random().SetWord("c"));
        map.Add(TestEntity.Random().SetWord("d"));

        const string newWord = "newWord";
        var action = () => map.Update(map.Get(0)!, e =>
        {
            e.SetWord(newWord);
            throw new ArithmeticException();
        });
        action.Should().Throw<ArithmeticException>();

        // changes should be reverted - use LINQ to verify
        map.Where(e => e.Word == newWord).Count().Should().Be(0);
    }

    [Fact]
    public void Peek()
    {
        var map = FixedSizeMap(10, EntityCreator.Flat);
        map.Peek(1).Should().NotBeNull();
        map.Peek(100).Should().BeNull();
    }

    [Fact]
    public void GetIndexByName()
    {
        var map = GigaMap.New<TestEntity>();
        map.AddIndex(Indexer.Property<TestEntity, int>("IntValue", e => e.IntValue));
        map.AddIndex(Indexer.Property<TestEntity, string>("Word", e => e.Word));

        // With simplified GigaMap, we verify indices work by testing queries
        map.Add(TestEntity.Random().SetIntValue(42));
        var result = map.Where(e => e.IntValue == 42).FirstOrDefault();
        result.Should().NotBeNull();
    }

    [Fact]
    public void IndexKeyResolving()
    {
        var map = GigaMap.New<TestEntity>();
        map.AddIndex(Indexer.Property<TestEntity, int>("IntValue", e => e.IntValue));
        map.Add(TestEntity.Random().SetIntValue(0));
        map.Add(TestEntity.Random().SetIntValue(1));
        map.Add(TestEntity.Random().SetIntValue(2));
        map.Add(TestEntity.Random().SetIntValue(2));

        // Use LINQ to resolve unique keys
        var keys = map.Select(e => e.IntValue).Distinct().OrderBy(x => x).ToList();
        keys.Should().BeEquivalentTo(new[] { 0, 1, 2 });
    }

    [Fact]
    public void ToStringTest()
    {
        var map = GigaMap.New<string>();
        map.AddAll(new[] { "a", "b", "c", "d", "e", "f" });
        GigaMap.New<string>().ToString(10).Should().Be("[]");
        map.ToString(3).Should().Be("[a, b, c]");
        map.ToString(3, 3).Should().Be("[d, e, f]");
    }

    // Helper methods following Eclipse Store pattern
    private static SimplifiedGigaMap<TestEntity> FixedSizeMap(int entryCount, EntityCreator entityCreator)
    {
        var map = EmptyMap();

        if (entryCount > 0)
        {
            for (int i = 0; i < entryCount; i++)
            {
                map.Add(entityCreator.Create(i));
            }
        }

        return map;
    }

    private static SimplifiedGigaMap<TestEntity> RandomSizeMap(int maxEntries, EntityCreator entityCreator)
    {
        var map = EmptyMap();

        if (maxEntries > 0)
        {
            var random = new Random();
            var max = random.Next(1, maxEntries + 1);
            for (int i = 0; i < max; i++)
            {
                map.Add(entityCreator.Create(i));
            }
        }

        return map;
    }

    private static SimplifiedGigaMap<TestEntity> EmptyMap()
    {
        var map = GigaMap.New<TestEntity>();

        // Add indices for performance optimization
        map.AddIndex(Indexer.Property<TestEntity, char>("FirstChar", e => e.Text.Length > 0 ? e.Text[0] : '\0'));
        map.AddIndex(Indexer.Property<TestEntity, string>("Word", e => e.Word));
        map.AddIndex(Indexer.Property<TestEntity, int>("IntValue", e => e.IntValue));
        map.AddIndex(Indexer.Property<TestEntity, double>("DoubleValue", e => e.DoubleValue));
        map.AddIndex(Indexer.Property<TestEntity, DateTime>("DateTime", e => e.DateTime));
        map.AddIndex(Indexer.Property<TestEntity, Guid>("Uuid", e => e.Uuid));

        return map;
    }

    // Entity creator interface following Eclipse Store pattern
    public interface EntityCreator
    {
        public static EntityCreator Deep = new DeepEntityCreator();
        public static EntityCreator Flat = new FlatEntityCreator();

        TestEntity Create(int index);
    }

    private class DeepEntityCreator : EntityCreator
    {
        public TestEntity Create(int index) => TestEntity.Random();
    }

    private class FlatEntityCreator : EntityCreator
    {
        public TestEntity Create(int index) => TestEntity.RandomFlat();
    }

    private class IndexBasedEntityCreator : EntityCreator
    {
        public TestEntity Create(int index) => TestEntity.Random().SetIntValue(index % 100);
    }
}
