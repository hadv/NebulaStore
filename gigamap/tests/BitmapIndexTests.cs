using FluentAssertions;
using NebulaStore.GigaMap.Tests.TestEntities;
using Xunit;

namespace NebulaStore.GigaMap.Tests;

/// <summary>
/// Tests for bitmap index functionality.
/// </summary>
public class BitmapIndexTests
{
    private IGigaMap<TestPerson> CreateIndexedGigaMap()
    {
        return GigaMap.Builder<TestPerson>()
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Department", p => p.Department))
            .WithBitmapIndex(Indexer.Property<TestPerson, int>("Age", p => p.Age))
            .Build();
    }

    [Fact]
    public void BitmapIndex_AfterAddingEntity_ShouldIndexCorrectly()
    {
        // Arrange
        var gigaMap = CreateIndexedGigaMap();
        var person = TestPerson.CreateDefault("test@example.com");
        person.Department = "Engineering";
        person.Age = 30;

        // Act
        gigaMap.Add(person);

        // Assert
        var emailIndex = gigaMap.Index.Bitmap.Get("Email");
        var departmentIndex = gigaMap.Index.Bitmap.Get("Department");
        var ageIndex = gigaMap.Index.Bitmap.Get("Age");

        emailIndex.Should().NotBeNull();
        departmentIndex.Should().NotBeNull();
        ageIndex.Should().NotBeNull();

        emailIndex!.Size.Should().Be(1);
        departmentIndex!.Size.Should().Be(1);
        ageIndex!.Size.Should().Be(1);
    }

    [Fact]
    public void BitmapIndex_WithMultipleEntitiesSameKey_ShouldGroupCorrectly()
    {
        // Arrange
        var gigaMap = CreateIndexedGigaMap();
        var person1 = TestPerson.CreateDefault("person1@test.com");
        person1.Department = "Engineering";
        var person2 = TestPerson.CreateDefault("person2@test.com");
        person2.Department = "Engineering";
        var person3 = TestPerson.CreateDefault("person3@test.com");
        person3.Department = "Marketing";

        // Act
        gigaMap.Add(person1);
        gigaMap.Add(person2);
        gigaMap.Add(person3);

        // Assert
        var departmentIndex = gigaMap.Index.Bitmap.Get("Department");
        departmentIndex.Should().NotBeNull();
        departmentIndex!.Size.Should().Be(3); // Total entities indexed

        // Query to verify grouping
        var engineeringResults = gigaMap.Query("Department", "Engineering").Execute();
        var marketingResults = gigaMap.Query("Department", "Marketing").Execute();

        engineeringResults.Should().HaveCount(2);
        marketingResults.Should().HaveCount(1);
    }

    [Fact]
    public void BitmapIndex_AfterRemovingEntity_ShouldUpdateIndex()
    {
        // Arrange
        var gigaMap = CreateIndexedGigaMap();
        var person = TestPerson.CreateDefault("test@example.com");
        person.Department = "Engineering";
        var entityId = gigaMap.Add(person);

        // Act
        gigaMap.RemoveById(entityId);

        // Assert
        var departmentIndex = gigaMap.Index.Bitmap.Get("Department");
        departmentIndex.Should().NotBeNull();
        departmentIndex!.Size.Should().Be(0);

        var results = gigaMap.Query("Department", "Engineering").Execute();
        results.Should().BeEmpty();
    }

    [Fact]
    public void BitmapIndex_AfterUpdatingEntity_ShouldUpdateIndex()
    {
        // Arrange
        var gigaMap = CreateIndexedGigaMap();
        var person = TestPerson.CreateDefault("test@example.com");
        person.Department = "Engineering";
        gigaMap.Add(person);

        // Act
        gigaMap.Update(person, p => p.Department = "Marketing");

        // Assert
        var engineeringResults = gigaMap.Query("Department", "Engineering").Execute();
        var marketingResults = gigaMap.Query("Department", "Marketing").Execute();

        engineeringResults.Should().BeEmpty();
        marketingResults.Should().HaveCount(1);
        marketingResults.First().Should().BeSameAs(person);
    }

    [Fact]
    public void BitmapIndex_Statistics_ShouldProvideCorrectInformation()
    {
        // Arrange
        var gigaMap = CreateIndexedGigaMap();
        var people = TestPerson.CreateTestCollection(5);
        gigaMap.AddAll(people);

        // Act
        var departmentIndex = gigaMap.Index.Bitmap.Get("Department");
        var statistics = departmentIndex!.CreateStatistics();

        // Assert
        statistics.Should().NotBeNull();
        statistics.Parent.Should().BeSameAs(departmentIndex);
        statistics.TotalEntityCount.Should().Be(5);
        statistics.UniqueKeyCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void BitmapIndex_IterateKeys_ShouldIterateAllKeys()
    {
        // Arrange
        var gigaMap = CreateIndexedGigaMap();
        var people = TestPerson.CreateTestCollection(5);
        gigaMap.AddAll(people);
        var collectedKeys = new List<object>();

        // Act
        var departmentIndex = gigaMap.Index.Bitmap.Get("Department");
        departmentIndex!.IterateKeys(key => collectedKeys.Add(key));

        // Assert
        var expectedDepartments = people.Select(p => p.Department).Distinct().ToList();
        collectedKeys.Should().HaveCount(expectedDepartments.Count);
        collectedKeys.Should().BeEquivalentTo(expectedDepartments);
    }

    [Fact]
    public void BitmapIndex_IterateEntityIds_ShouldIterateAllEntityIds()
    {
        // Arrange
        var gigaMap = CreateIndexedGigaMap();
        var people = TestPerson.CreateTestCollection(3);
        var entityIds = people.Select(p => gigaMap.Add(p)).ToList();
        var collectedEntityIds = new List<long>();

        // Act
        var departmentIndex = gigaMap.Index.Bitmap.Get("Department");
        departmentIndex!.IterateEntityIds(entityId => collectedEntityIds.Add(entityId));

        // Assert
        collectedEntityIds.Should().HaveCount(3);
        collectedEntityIds.Should().BeEquivalentTo(entityIds);
    }

    [Fact]
    public void BitmapIndex_GetEntityIds_ShouldReturnCorrectIds()
    {
        // Arrange
        var gigaMap = CreateIndexedGigaMap();
        var person1 = TestPerson.CreateDefault("person1@test.com");
        person1.Department = "Engineering";
        var person2 = TestPerson.CreateDefault("person2@test.com");
        person2.Department = "Engineering";
        var person3 = TestPerson.CreateDefault("person3@test.com");
        person3.Department = "Marketing";

        var id1 = gigaMap.Add(person1);
        var id2 = gigaMap.Add(person2);
        var id3 = gigaMap.Add(person3);

        // Act
        var departmentIndex = gigaMap.Index.Bitmap.Get("Department");
        var engineeringIds = departmentIndex!.GetEntityIds("Engineering").ToList();
        var marketingIds = departmentIndex.GetEntityIds("Marketing").ToList();

        // Assert
        engineeringIds.Should().HaveCount(2);
        engineeringIds.Should().Contain(new[] { id1, id2 });

        marketingIds.Should().HaveCount(1);
        marketingIds.Should().Contain(id3);
    }

    [Fact]
    public void BitmapIndex_GetEntityIds_WithNonExistentKey_ShouldReturnEmpty()
    {
        // Arrange
        var gigaMap = CreateIndexedGigaMap();
        var person = TestPerson.CreateDefault("test@example.com");
        person.Department = "Engineering";
        gigaMap.Add(person);

        // Act
        var departmentIndex = gigaMap.Index.Bitmap.Get("Department");
        var nonExistentIds = departmentIndex!.GetEntityIds("NonExistentDepartment").ToList();

        // Assert
        nonExistentIds.Should().BeEmpty();
    }

    [Fact]
    public void BitmapIndex_EqualKeys_ShouldUseIndexerEquality()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapIndex(Indexer.StringIgnoreCase<TestPerson>("Department", p => p.Department))
            .Build();

        var person1 = TestPerson.CreateDefault("person1@test.com");
        person1.Department = "Engineering";
        var person2 = TestPerson.CreateDefault("person2@test.com");
        person2.Department = "ENGINEERING"; // Different case

        gigaMap.Add(person1);
        gigaMap.Add(person2);

        // Act
        var results = gigaMap.Query("Department", "engineering").Execute(); // Query with different case

        // Assert
        results.Should().HaveCount(2); // Should find both due to case-insensitive comparison
    }
}