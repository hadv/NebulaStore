using FluentAssertions;
using NebulaStore.GigaMap.Tests.TestEntities;
using Xunit;

namespace NebulaStore.GigaMap.Tests;

/// <summary>
/// Tests for GigaMap query functionality.
/// </summary>
public class GigaQueryTests
{
    private IGigaMap<TestPerson> CreateTestGigaMap()
    {
        return GigaMap.Builder<TestPerson>()
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Department", p => p.Department))
            .WithBitmapIndex(Indexer.Property<TestPerson, int>("Age", p => p.Age))
            .WithBitmapIndex(Indexer.StringIgnoreCase<TestPerson>("LastName", p => p.LastName))
            .Build();
    }

    [Fact]
    public void Query_EmptyQuery_ShouldReturnAllEntities()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(3);
        gigaMap.AddAll(people);

        // Act
        var results = gigaMap.Query().Execute();

        // Assert
        results.Should().HaveCount(3);
        results.Should().BeEquivalentTo(people);
    }

    [Fact]
    public void Query_ByStringIndex_ShouldReturnMatchingEntities()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(5);
        gigaMap.AddAll(people);

        // Act
        var results = gigaMap.Query("Department", "Engineering").Execute();

        // Assert
        var expectedPeople = people.Where(p => p.Department == "Engineering").ToList();
        results.Should().HaveCount(expectedPeople.Count);
        results.Should().BeEquivalentTo(expectedPeople);
    }

    [Fact]
    public void Query_WithAndCondition_ShouldReturnMatchingEntities()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(10);
        gigaMap.AddAll(people);

        // Act
        var results = gigaMap.Query("Department", "Engineering")
            .And("Age").Where(age => age >= 30)
            .Execute();

        // Assert
        var expectedPeople = people.Where(p => p.Department == "Engineering" && p.Age >= 30).ToList();
        results.Should().HaveCount(expectedPeople.Count);
        results.Should().BeEquivalentTo(expectedPeople);
    }

    [Fact]
    public void Query_WithOrCondition_ShouldReturnMatchingEntities()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(10);
        gigaMap.AddAll(people);

        // Act
        var results = gigaMap.Query("Department", "Engineering")
            .Or("Department", "Marketing")
            .Execute();

        // Assert
        var expectedPeople = people.Where(p => p.Department == "Engineering" || p.Department == "Marketing").ToList();
        results.Should().HaveCount(expectedPeople.Count);
        results.Should().BeEquivalentTo(expectedPeople);
    }

    [Fact]
    public void Query_WithLimit_ShouldReturnLimitedResults()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(10);
        gigaMap.AddAll(people);

        // Act
        var results = gigaMap.Query().Limit(3).Execute();

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public void Query_WithSkip_ShouldSkipResults()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(5);
        gigaMap.AddAll(people);

        // Act
        var results = gigaMap.Query().Skip(2).Execute();

        // Assert
        results.Should().HaveCount(3);
    }

    [Fact]
    public void Query_WithSkipAndLimit_ShouldReturnPagedResults()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(10);
        gigaMap.AddAll(people);

        // Act
        var results = gigaMap.Query().Skip(3).Limit(4).Execute();

        // Assert
        results.Should().HaveCount(4);
    }

    [Fact]
    public void Query_Count_ShouldReturnCorrectCount()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(7);
        gigaMap.AddAll(people);

        // Act
        var count = gigaMap.Query("Department", "Engineering").Count();

        // Assert
        var expectedCount = people.Count(p => p.Department == "Engineering");
        count.Should().Be(expectedCount);
    }

    [Fact]
    public void Query_Any_WithMatchingEntities_ShouldReturnTrue()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(5);
        gigaMap.AddAll(people);

        // Act
        var hasAny = gigaMap.Query("Department", "Engineering").Any();

        // Assert
        var expectedHasAny = people.Any(p => p.Department == "Engineering");
        hasAny.Should().Be(expectedHasAny);
    }

    [Fact]
    public void Query_Any_WithNoMatchingEntities_ShouldReturnFalse()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(3);
        gigaMap.AddAll(people);

        // Act
        var hasAny = gigaMap.Query("Department", "NonExistentDepartment").Any();

        // Assert
        hasAny.Should().BeFalse();
    }

    [Fact]
    public void Query_FirstOrDefault_WithMatchingEntities_ShouldReturnFirstEntity()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(5);
        gigaMap.AddAll(people);

        // Act
        var firstEngineer = gigaMap.Query("Department", "Engineering").FirstOrDefault();

        // Assert
        var expectedFirst = people.FirstOrDefault(p => p.Department == "Engineering");
        if (expectedFirst != null)
        {
            firstEngineer.Should().NotBeNull();
            firstEngineer!.Department.Should().Be("Engineering");
        }
        else
        {
            firstEngineer.Should().BeNull();
        }
    }

    [Fact]
    public void Query_FirstOrDefault_WithNoMatchingEntities_ShouldReturnNull()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(3);
        gigaMap.AddAll(people);

        // Act
        var result = gigaMap.Query("Department", "NonExistentDepartment").FirstOrDefault();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Query_ForEach_ShouldExecuteActionOnAllResults()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(5);
        gigaMap.AddAll(people);
        var processedEmails = new List<string>();

        // Act
        gigaMap.Query("Department", "Engineering").ForEach(p => processedEmails.Add(p.Email));

        // Assert
        var expectedEmails = people.Where(p => p.Department == "Engineering").Select(p => p.Email).ToList();
        processedEmails.Should().BeEquivalentTo(expectedEmails);
    }

    [Fact]
    public void Query_ForEach_WithNullAction_ShouldThrowArgumentNullException()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();

        // Act & Assert
        var action = () => gigaMap.Query().ForEach(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Query_ExecuteAsync_ShouldReturnSameAsExecute()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(3);
        gigaMap.AddAll(people);

        // Act
        var syncResults = gigaMap.Query("Department", "Engineering").Execute();
        var asyncResults = await gigaMap.Query("Department", "Engineering").ExecuteAsync();

        // Assert
        asyncResults.Should().BeEquivalentTo(syncResults);
    }

    [Fact]
    public async Task Query_CountAsync_ShouldReturnSameAsCount()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(5);
        gigaMap.AddAll(people);

        // Act
        var syncCount = gigaMap.Query("Department", "Engineering").Count();
        var asyncCount = await gigaMap.Query("Department", "Engineering").CountAsync();

        // Assert
        asyncCount.Should().Be(syncCount);
    }

    [Fact]
    public async Task Query_AnyAsync_ShouldReturnSameAsAny()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(3);
        gigaMap.AddAll(people);

        // Act
        var syncAny = gigaMap.Query("Department", "Engineering").Any();
        var asyncAny = await gigaMap.Query("Department", "Engineering").AnyAsync();

        // Assert
        asyncAny.Should().Be(syncAny);
    }

    [Fact]
    public async Task Query_FirstOrDefaultAsync_ShouldReturnSameAsFirstOrDefault()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(5);
        gigaMap.AddAll(people);

        // Act
        var syncFirst = gigaMap.Query("Department", "Engineering").FirstOrDefault();
        var asyncFirst = await gigaMap.Query("Department", "Engineering").FirstOrDefaultAsync();

        // Assert
        if (syncFirst != null)
        {
            asyncFirst.Should().NotBeNull();
            asyncFirst!.Department.Should().Be(syncFirst.Department);
        }
        else
        {
            asyncFirst.Should().BeNull();
        }
    }

    [Fact]
    public async Task Query_ForEachAsync_ShouldExecuteActionOnAllResults()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(5);
        gigaMap.AddAll(people);
        var processedEmails = new List<string>();

        // Act
        await gigaMap.Query("Department", "Engineering").ForEachAsync(p => processedEmails.Add(p.Email));

        // Assert
        var expectedEmails = people.Where(p => p.Department == "Engineering").Select(p => p.Email).ToList();
        processedEmails.Should().BeEquivalentTo(expectedEmails);
    }

    [Fact]
    public void Query_WithNegativeLimit_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();

        // Act & Assert
        var action = () => gigaMap.Query().Limit(-1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Query_WithNegativeSkip_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();

        // Act & Assert
        var action = () => gigaMap.Query().Skip(-1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Query_Enumeration_ShouldReturnSameAsExecute()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();
        var people = TestPerson.CreateTestCollection(3);
        gigaMap.AddAll(people);

        // Act
        var executeResults = gigaMap.Query("Department", "Engineering").Execute();
        var enumerationResults = gigaMap.Query("Department", "Engineering").ToList();

        // Assert
        enumerationResults.Should().BeEquivalentTo(executeResults);
    }

    [Fact]
    public void Query_WithInvalidIndexName_ShouldThrowArgumentException()
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();

        // Act & Assert
        var action = () => gigaMap.Query("NonExistentIndex", "value");
        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Query_WithInvalidIndexName_ShouldThrowArgumentException(string? invalidIndexName)
    {
        // Arrange
        var gigaMap = CreateTestGigaMap();

        // Act & Assert
        var action = () => gigaMap.Query(invalidIndexName!, "value");
        action.Should().Throw<ArgumentException>();
    }
}