using System;

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