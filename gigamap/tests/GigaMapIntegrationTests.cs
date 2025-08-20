using FluentAssertions;
using NebulaStore.GigaMap.Tests.TestEntities;
using Xunit;

namespace NebulaStore.GigaMap.Tests;

/// <summary>
/// Integration tests for complete GigaMap functionality.
/// </summary>
public class GigaMapIntegrationTests
{
    [Fact]
    public void CompleteWorkflow_ShouldWorkCorrectly()
    {
        // Arrange - Create a fully configured GigaMap
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapUniqueIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Department", p => p.Department))
            .WithBitmapIndex(Indexer.Property<TestPerson, int>("Age", p => p.Age))
            .WithBitmapIndex(Indexer.StringIgnoreCase<TestPerson>("LastName", p => p.LastName))
            .WithCustomConstraint(Constraint.Custom<TestPerson>(
                "ValidAge", p => p.Age >= 18 && p.Age <= 65, "Age must be between 18 and 65"))
            .WithValueEquality()
            .Build();

        // Act & Assert - Add entities
        var people = new List<TestPerson>
        {
            new() { FirstName = "John", LastName = "Doe", Email = "john.doe@company.com", Age = 30, Department = "Engineering", Salary = 75000 },
            new() { FirstName = "Jane", LastName = "Smith", Email = "jane.smith@company.com", Age = 28, Department = "Marketing", Salary = 65000 },
            new() { FirstName = "Bob", LastName = "Johnson", Email = "bob.johnson@company.com", Age = 35, Department = "Engineering", Salary = 85000 },
            new() { FirstName = "Alice", LastName = "Brown", Email = "alice.brown@company.com", Age = 32, Department = "HR", Salary = 60000 },
            new() { FirstName = "Charlie", LastName = "Wilson", Email = "charlie.wilson@company.com", Age = 29, Department = "Engineering", Salary = 78000 }
        };

        foreach (var person in people)
        {
            gigaMap.Add(person);
        }

        gigaMap.Size.Should().Be(5);

        // Test querying by department
        var engineers = gigaMap.Query("Department", "Engineering").Execute();
        engineers.Should().HaveCount(3);
        engineers.All(p => p.Department == "Engineering").Should().BeTrue();

        // Test complex queries with AND conditions
        var seniorEngineers = gigaMap.Query("Department", "Engineering")
            .And("Age").Where(age => age >= 30)
            .Execute();
        seniorEngineers.Should().HaveCount(2);

        // Test OR conditions
        var techAndMarketing = gigaMap.Query("Department", "Engineering")
            .Or("Department", "Marketing")
            .Execute();
        techAndMarketing.Should().HaveCount(4);

        // Test case-insensitive queries
        var smiths = gigaMap.Query("LastName", "SMITH").Execute(); // Different case
        smiths.Should().HaveCount(1);
        smiths.First().LastName.Should().Be("Smith");

        // Test pagination
        var pagedResults = gigaMap.Query("Department", "Engineering")
            .Skip(1)
            .Limit(2)
            .Execute();
        pagedResults.Should().HaveCount(2);

        // Test aggregation
        var engineerCount = gigaMap.Query("Department", "Engineering").Count();
        engineerCount.Should().Be(3);

        var hasMarketingPeople = gigaMap.Query("Department", "Marketing").Any();
        hasMarketingPeople.Should().BeTrue();

        // Test updates
        var john = people.First(p => p.FirstName == "John");
        gigaMap.Update(john, p => p.Department = "Management");

        var managementPeople = gigaMap.Query("Department", "Management").Execute();
        managementPeople.Should().HaveCount(1);
        managementPeople.First().Should().BeSameAs(john);

        // Verify engineering count decreased
        var updatedEngineerCount = gigaMap.Query("Department", "Engineering").Count();
        updatedEngineerCount.Should().Be(2);

        // Test removal
        var jane = people.First(p => p.FirstName == "Jane");
        gigaMap.Remove(jane);

        gigaMap.Size.Should().Be(4);
        var marketingAfterRemoval = gigaMap.Query("Department", "Marketing").Execute();
        marketingAfterRemoval.Should().BeEmpty();
    }

    [Fact]
    public void UniqueConstraintViolation_ShouldPreventDuplicates()
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
        action.Should().Throw<ConstraintViolationException>()
            .WithMessage("*duplicate@test.com*");

        gigaMap.Size.Should().Be(1);
    }

    [Fact]
    public void CustomConstraintViolation_ShouldPreventInvalidData()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithCustomConstraint(Constraint.Custom<TestPerson>(
                "ValidAge", p => p.Age >= 18, "Must be at least 18 years old"))
            .WithCustomConstraint(Constraint.Custom<TestPerson>(
                "ValidEmail", p => p.Email.Contains("@"), "Email must be valid"))
            .Build();

        var validPerson = TestPerson.CreateDefault("valid@test.com");
        validPerson.Age = 25;

        var invalidAgePerson = TestPerson.CreateDefault("test@test.com");
        invalidAgePerson.Age = 16;

        var invalidEmailPerson = TestPerson.CreateDefault("invalid-email");
        invalidEmailPerson.Age = 25;

        // Act & Assert
        gigaMap.Add(validPerson).Should().BeGreaterOrEqualTo(0);

        var ageAction = () => gigaMap.Add(invalidAgePerson);
        ageAction.Should().Throw<ConstraintViolationException>()
            .WithMessage("Must be at least 18 years old");

        var emailAction = () => gigaMap.Add(invalidEmailPerson);
        emailAction.Should().Throw<ConstraintViolationException>()
            .WithMessage("Email must be valid");

        gigaMap.Size.Should().Be(1);
    }

    [Fact]
    public void LargeDataSet_ShouldPerformEfficiently()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<SimpleEntity>()
            .WithBitmapIndex(Indexer.Property<SimpleEntity, string>("Category", e => e.Category))
            .WithBitmapIndex(Indexer.Property<SimpleEntity, int>("Id", e => e.Id))
            .Build();

        var categories = new[] { "A", "B", "C", "D", "E" };
        var entities = new List<SimpleEntity>();

        // Create 1000 entities
        for (int i = 0; i < 1000; i++)
        {
            entities.Add(new SimpleEntity
            {
                Id = i,
                Name = $"Entity{i}",
                Category = categories[i % categories.Length]
            });
        }

        // Act - Add all entities
        var startTime = DateTime.UtcNow;
        foreach (var entity in entities)
        {
            gigaMap.Add(entity);
        }
        var addTime = DateTime.UtcNow - startTime;

        // Query performance test
        startTime = DateTime.UtcNow;
        var categoryAResults = gigaMap.Query("Category", "A").Execute();
        var queryTime = DateTime.UtcNow - startTime;

        // Assert
        gigaMap.Size.Should().Be(1000);
        categoryAResults.Should().HaveCount(200); // 1000 / 5 categories = 200 per category

        // Performance assertions (these are rough guidelines)
        addTime.Should().BeLessThan(TimeSpan.FromSeconds(5)); // Adding 1000 entities should be fast
        queryTime.Should().BeLessThan(TimeSpan.FromMilliseconds(100)); // Querying should be very fast
    }

    [Fact]
    public async Task AsyncOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Department", p => p.Department))
            .Build();

        var people = TestPerson.CreateTestCollection(5);
        gigaMap.AddAll(people);

        // Act & Assert
        var asyncResults = await gigaMap.Query("Department", "Engineering").ExecuteAsync();
        var syncResults = gigaMap.Query("Department", "Engineering").Execute();

        asyncResults.Should().BeEquivalentTo(syncResults);

        var asyncCount = await gigaMap.Query("Department", "Engineering").CountAsync();
        var syncCount = gigaMap.Query("Department", "Engineering").Count();

        asyncCount.Should().Be(syncCount);

        var asyncAny = await gigaMap.Query("Department", "Engineering").AnyAsync();
        var syncAny = gigaMap.Query("Department", "Engineering").Any();

        asyncAny.Should().Be(syncAny);

        var asyncFirst = await gigaMap.Query("Department", "Engineering").FirstOrDefaultAsync();
        var syncFirst = gigaMap.Query("Department", "Engineering").FirstOrDefault();

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
}