using FluentAssertions;
using NebulaStore.GigaMap.Tests.TestEntities;
using Xunit;

namespace NebulaStore.GigaMap.Tests;

/// <summary>
/// Basic functionality tests to verify core GigaMap operations work correctly.
/// </summary>
public class BasicFunctionalityTests
{
    [Fact]
    public void GigaMap_BasicCrudOperations_ShouldWork()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();

        // Act & Assert - Add
        var person = TestPerson.CreateDefault("test@example.com");
        var entityId = gigaMap.Add(person);

        entityId.Should().BeGreaterOrEqualTo(0);
        gigaMap.Size.Should().Be(1);
        gigaMap.IsEmpty.Should().BeFalse();

        // Act & Assert - Get
        var retrievedPerson = gigaMap.Get(entityId);
        retrievedPerson.Should().BeSameAs(person);

        // Act & Assert - Remove
        var removedPerson = gigaMap.RemoveById(entityId);
        removedPerson.Should().BeSameAs(person);
        gigaMap.Size.Should().Be(0);
        gigaMap.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void GigaMap_WithIndexing_ShouldWork()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Department", p => p.Department))
            .Build();

        // Act - Add test data
        var people = new[]
        {
            new TestPerson { FirstName = "John", LastName = "Doe", Email = "john@test.com", Department = "Engineering", Age = 30 },
            new TestPerson { FirstName = "Jane", LastName = "Smith", Email = "jane@test.com", Department = "Engineering", Age = 28 },
            new TestPerson { FirstName = "Bob", LastName = "Johnson", Email = "bob@test.com", Department = "Marketing", Age = 35 }
        };

        foreach (var person in people)
        {
            gigaMap.Add(person);
        }

        // Assert
        gigaMap.Size.Should().Be(3);

        // Test basic querying
        var allResults = gigaMap.Query().Execute();
        allResults.Should().HaveCount(3);

        var engineeringResults = gigaMap.Query("Department", "Engineering").Execute();
        engineeringResults.Should().HaveCount(2);
        engineeringResults.All(p => p.Department == "Engineering").Should().BeTrue();

        var marketingResults = gigaMap.Query("Department", "Marketing").Execute();
        marketingResults.Should().HaveCount(1);
        marketingResults.First().FirstName.Should().Be("Bob");
    }

    [Fact]
    public void GigaMap_WithUniqueConstraint_ShouldPreventDuplicates()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapUniqueIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .Build();

        var person1 = TestPerson.CreateDefault("duplicate@test.com");
        var person2 = TestPerson.CreateDefault("duplicate@test.com");

        // Act & Assert
        gigaMap.Add(person1);

        var action = () => gigaMap.Add(person2);
        action.Should().Throw<ConstraintViolationException>();

        gigaMap.Size.Should().Be(1);
    }

    [Fact]
    public void GigaMap_WithCustomConstraint_ShouldValidate()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithCustomConstraint(Constraint.Custom<TestPerson>(
                "ValidAge",
                person => person.Age >= 18 && person.Age <= 65,
                "Age must be between 18 and 65"))
            .Build();

        var validPerson = TestPerson.CreateDefault("valid@test.com");
        validPerson.Age = 25;

        var invalidPerson = TestPerson.CreateDefault("invalid@test.com");
        invalidPerson.Age = 16;

        // Act & Assert
        gigaMap.Add(validPerson).Should().BeGreaterOrEqualTo(0);

        var action = () => gigaMap.Add(invalidPerson);
        action.Should().Throw<ConstraintViolationException>()
            .WithMessage("Age must be between 18 and 65");

        gigaMap.Size.Should().Be(1);
    }

    [Fact]
    public void GigaMap_QueryOperations_ShouldWork()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Department", p => p.Department))
            .WithBitmapIndex(Indexer.Property<TestPerson, int>("Age", p => p.Age))
            .Build();

        var people = new[]
        {
            new TestPerson { FirstName = "John", Email = "john@test.com", Department = "Engineering", Age = 30 },
            new TestPerson { FirstName = "Jane", Email = "jane@test.com", Department = "Engineering", Age = 28 },
            new TestPerson { FirstName = "Bob", Email = "bob@test.com", Department = "Marketing", Age = 35 },
            new TestPerson { FirstName = "Alice", Email = "alice@test.com", Department = "Engineering", Age = 32 }
        };

        foreach (var person in people)
        {
            gigaMap.Add(person);
        }

        // Test Count
        var engineerCount = gigaMap.Query("Department", "Engineering").Count();
        engineerCount.Should().Be(3);

        // Test Any
        var hasMarketing = gigaMap.Query("Department", "Marketing").Any();
        hasMarketing.Should().BeTrue();

        var hasHR = gigaMap.Query("Department", "HR").Any();
        hasHR.Should().BeFalse();

        // Test FirstOrDefault
        var firstEngineer = gigaMap.Query("Department", "Engineering").FirstOrDefault();
        firstEngineer.Should().NotBeNull();
        firstEngineer!.Department.Should().Be("Engineering");

        var firstHR = gigaMap.Query("Department", "HR").FirstOrDefault();
        firstHR.Should().BeNull();

        // Test Limit and Skip
        var limitedResults = gigaMap.Query("Department", "Engineering").Limit(2).Execute();
        limitedResults.Should().HaveCount(2);

        var skippedResults = gigaMap.Query("Department", "Engineering").Skip(1).Execute();
        skippedResults.Should().HaveCount(2);

        var pagedResults = gigaMap.Query("Department", "Engineering").Skip(1).Limit(1).Execute();
        pagedResults.Should().HaveCount(1);
    }

    [Fact]
    public void GigaMap_UpdateOperations_ShouldWork()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Department", p => p.Department))
            .Build();

        var person = TestPerson.CreateDefault("test@example.com");
        person.Department = "Engineering";
        gigaMap.Add(person);

        // Act - Update department
        gigaMap.Update(person, p => p.Department = "Marketing");

        // Assert
        var engineeringResults = gigaMap.Query("Department", "Engineering").Execute();
        engineeringResults.Should().BeEmpty();

        var marketingResults = gigaMap.Query("Department", "Marketing").Execute();
        marketingResults.Should().HaveCount(1);
        marketingResults.First().Should().BeSameAs(person);
    }
}