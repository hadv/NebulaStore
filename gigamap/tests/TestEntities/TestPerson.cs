using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.GigaMap.Tests.TestEntities;

/// <summary>
/// Test entity class for GigaMap unit tests.
/// </summary>
public class TestPerson
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Department { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool IsActive { get; set; } = true;

    public override string ToString()
    {
        return $"{FirstName} {LastName} ({Email}) - Age: {Age}, Dept: {Department}";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not TestPerson other) return false;
        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    /// <summary>
    /// Creates a test person with default values.
    /// </summary>
    public static TestPerson CreateDefault(string? email = null)
    {
        return new TestPerson
        {
            FirstName = "John",
            LastName = "Doe",
            Email = email ?? "john.doe@test.com",
            Age = 30,
            DateOfBirth = new DateTime(1993, 5, 15),
            Department = "Engineering",
            Salary = 75000,
            Id = Guid.NewGuid(),
            IsActive = true
        };
    }

    /// <summary>
    /// Creates a collection of test persons for bulk testing.
    /// </summary>
    public static List<TestPerson> CreateTestCollection(int count = 5)
    {
        var people = new List<TestPerson>();
        var departments = new[] { "Engineering", "Marketing", "Sales", "HR", "Finance" };
        var random = new Random(42); // Fixed seed for reproducible tests

        for (int i = 0; i < count; i++)
        {
            people.Add(new TestPerson
            {
                FirstName = $"Person{i}",
                LastName = $"LastName{i}",
                Email = $"person{i}@test.com",
                Age = 20 + (i * 5) % 50, // Ages from 20 to 69
                DateOfBirth = DateTime.Now.AddYears(-(20 + (i * 5) % 50)),
                Department = departments[i % departments.Length],
                Salary = 50000 + (i * 10000),
                Id = Guid.NewGuid(),
                IsActive = i % 3 != 0 // Mix of active/inactive
            });
        }

        return people;
    }
}

/// <summary>
/// Simple test entity for basic testing scenarios.
/// </summary>
public class SimpleEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    public override bool Equals(object? obj)
    {
        if (obj is not SimpleEntity other) return false;
        return Id == other.Id && Name == other.Name && Category == other.Category;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name, Category);
    }
}

/// <summary>
/// Test entity with nullable properties for testing edge cases.
/// </summary>
public class NullableEntity
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? Age { get; set; }
    public DateTime? CreatedAt { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is not NullableEntity other) return false;
        return Id == other.Id && Name == other.Name && Age == other.Age && CreatedAt == other.CreatedAt;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Name, Age, CreatedAt);
    }
}

/// <summary>
/// Test entity that matches Eclipse Store's Entity pattern for comprehensive testing
/// </summary>
public class TestEntity
{
    // Static indexers following Eclipse Store pattern - using object-based indexers for simplicity
    public static readonly IIndexer<TestEntity, object> FirstCharIndex =
        Indexer.AsObjectIndexer(Indexer.Property<TestEntity, char>("FirstChar", e => e.Text.Length > 0 ? e.Text[0] : '\0'));

    public static readonly IIndexer<TestEntity, object> WordIndex =
        Indexer.AsObjectIndexer(Indexer.Property<TestEntity, string>("Word", e => e.Word));

    public static readonly IIndexer<TestEntity, object> IntValueIndex =
        Indexer.AsObjectIndexer(Indexer.Property<TestEntity, int>("IntValue", e => e.IntValue));

    public static readonly IIndexer<TestEntity, object> DoubleValueIndex =
        Indexer.AsObjectIndexer(Indexer.Property<TestEntity, double>("DoubleValue", e => e.DoubleValue));

    public static readonly IIndexer<TestEntity, object> DateTimeIndex =
        Indexer.AsObjectIndexer(Indexer.Property<TestEntity, DateTime>("DateTime", e => e.DateTime));

    public static readonly IIndexer<TestEntity, object> UuidIndex =
        Indexer.AsObjectIndexer(Indexer.Property<TestEntity, Guid>("Uuid", e => e.Uuid));

    // Properties
    public string Text { get; set; } = string.Empty;
    public string Word { get; set; } = string.Empty;
    public int IntValue { get; set; }
    public double DoubleValue { get; set; }
    public DateTime DateTime { get; set; }
    public Guid Uuid { get; set; }

    public TestEntity()
    {
        Uuid = Guid.NewGuid();
        DateTime = DateTime.Now;
    }

    public TestEntity(string text, string word, int intValue, double doubleValue, DateTime dateTime, Guid uuid)
    {
        Text = text;
        Word = word;
        IntValue = intValue;
        DoubleValue = doubleValue;
        DateTime = dateTime;
        Uuid = uuid;
    }

    // Factory methods following Eclipse Store pattern
    public static TestEntity Random()
    {
        var random = new Random();
        var words = new[] { "red", "blue", "green", "yellow", "orange", "purple", "black", "white" };

        return new TestEntity(
            $"Sample text {random.Next(1000)}",
            words[random.Next(words.Length)],
            random.Next(0, 2500),
            random.NextDouble() * 1000,
            DateTime.Now.AddDays(-random.Next(365)),
            Guid.NewGuid()
        );
    }

    public static TestEntity RandomFlat()
    {
        return Random(); // For simplicity, same as Random
    }

    public static List<TestEntity> RandomList(int maxAmount)
    {
        var random = new Random();
        var count = random.Next(1, maxAmount + 1);
        return Enumerable.Range(0, count).Select(_ => Random()).ToList();
    }

    public static List<TestEntity> FixedList(int amount)
    {
        return Enumerable.Range(0, amount).Select(_ => Random()).ToList();
    }

    // Fluent setters following Eclipse Store pattern
    public TestEntity SetText(string text)
    {
        Text = text;
        return this;
    }

    public TestEntity SetWord(string word)
    {
        Word = word;
        return this;
    }

    public TestEntity SetIntValue(int intValue)
    {
        IntValue = intValue;
        return this;
    }

    public TestEntity SetDoubleValue(double doubleValue)
    {
        DoubleValue = doubleValue;
        return this;
    }

    public TestEntity SetDateTime(DateTime dateTime)
    {
        DateTime = dateTime;
        return this;
    }

    public TestEntity SetUuid(Guid uuid)
    {
        Uuid = uuid;
        return this;
    }

    public override string ToString()
    {
        return $"TestEntity(Word={Word}, IntValue={IntValue}, Uuid={Uuid})";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not TestEntity other) return false;
        return Uuid == other.Uuid;
    }

    public override int GetHashCode()
    {
        return Uuid.GetHashCode();
    }
}