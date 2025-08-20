using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.GigaMap.Examples;

/// <summary>
/// Example entity class for demonstrating GigaMap functionality.
/// </summary>
public class Person
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Department { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();

    public override string ToString()
    {
        return $"{FirstName} {LastName} ({Email}) - Age: {Age}, Dept: {Department}";
    }
}

/// <summary>
/// Example demonstrating GigaMap usage with Person entities.
/// </summary>
public static class PersonGigaMapExample
{
    /// <summary>
    /// Creates and configures a GigaMap for Person entities with various indices.
    /// </summary>
    /// <returns>A configured GigaMap for Person entities</returns>
    public static IGigaMap<Person> CreatePersonGigaMap()
    {
        return GigaMap.Builder<Person>()
            // Add bitmap indices for efficient querying
            .WithBitmapIndex(Indexer.Property<Person, string>("Email", p => p.Email))
            .WithBitmapIndex(Indexer.Property<Person, string>("Department", p => p.Department))
            .WithBitmapIndex(Indexer.Property<Person, int>("Age", p => p.Age))
            .WithBitmapIndex(Indexer.StringIgnoreCase<Person>("LastName", p => p.LastName))
            .WithBitmapIndex(Indexer.Guid<Person>("Id", p => p.Id))
            .WithBitmapIndex(Indexer.DateTime<Person>("DateOfBirth", p => p.DateOfBirth))

            // Add unique constraints
            .WithBitmapUniqueIndex(Indexer.Property<Person, string>("Email", p => p.Email))
            .WithBitmapUniqueIndex(Indexer.Guid<Person>("Id", p => p.Id))

            // Add custom constraints
            .WithCustomConstraint(Constraint.Custom<Person>(
                "ValidAge",
                person => person.Age >= 0 && person.Age <= 150,
                "Age must be between 0 and 150"))

            .WithCustomConstraint(Constraint.Custom<Person>(
                "ValidEmail",
                person => !string.IsNullOrEmpty(person.Email) && person.Email.Contains("@"),
                "Email must be valid and contain @"))

            // Configure for value-based equality
            .WithValueEquality()

            // Build the GigaMap
            .Build();
    }

    /// <summary>
    /// Demonstrates basic CRUD operations with the GigaMap.
    /// </summary>
    public static void DemonstrateCrudOperations()
    {
        var gigaMap = CreatePersonGigaMap();

        Console.WriteLine("=== GigaMap CRUD Operations Demo ===");

        // Add some sample data
        var people = new[]
        {
            new Person { FirstName = "John", LastName = "Doe", Email = "john.doe@company.com", Age = 30, Department = "Engineering", Salary = 75000, DateOfBirth = new DateTime(1993, 5, 15) },
            new Person { FirstName = "Jane", LastName = "Smith", Email = "jane.smith@company.com", Age = 28, Department = "Marketing", Salary = 65000, DateOfBirth = new DateTime(1995, 8, 22) },
            new Person { FirstName = "Bob", LastName = "Johnson", Email = "bob.johnson@company.com", Age = 35, Department = "Engineering", Salary = 85000, DateOfBirth = new DateTime(1988, 12, 3) },
            new Person { FirstName = "Alice", LastName = "Williams", Email = "alice.williams@company.com", Age = 32, Department = "Sales", Salary = 70000, DateOfBirth = new DateTime(1991, 3, 10) },
            new Person { FirstName = "Charlie", LastName = "Brown", Email = "charlie.brown@company.com", Age = 29, Department = "Engineering", Salary = 78000, DateOfBirth = new DateTime(1994, 7, 18) }
        };

        // Add all people
        Console.WriteLine("Adding people to GigaMap...");
        foreach (var person in people)
        {
            var id = gigaMap.Add(person);
            Console.WriteLine($"Added: {person} with ID: {id}");
        }

        Console.WriteLine($"\nTotal people in GigaMap: {gigaMap.Size}");
        Console.WriteLine($"Is empty: {gigaMap.IsEmpty}");
        Console.WriteLine($"Highest used ID: {gigaMap.HighestUsedId}");
    }

    /// <summary>
    /// Demonstrates querying capabilities of the GigaMap.
    /// </summary>
    public static void DemonstrateQuerying()
    {
        var gigaMap = CreatePersonGigaMap();

        // Add sample data first
        var people = new[]
        {
            new Person { FirstName = "John", LastName = "Doe", Email = "john.doe@company.com", Age = 30, Department = "Engineering" },
            new Person { FirstName = "Jane", LastName = "Smith", Email = "jane.smith@company.com", Age = 28, Department = "Marketing" },
            new Person { FirstName = "Bob", LastName = "Johnson", Email = "bob.johnson@company.com", Age = 35, Department = "Engineering" },
            new Person { FirstName = "Alice", LastName = "Williams", Email = "alice.williams@company.com", Age = 32, Department = "Sales" }
        };

        foreach (var person in people)
        {
            gigaMap.Add(person);
        }

        Console.WriteLine("\n=== GigaMap Querying Demo ===");

        // Query by department
        Console.WriteLine("\n1. Find all people in Engineering:");
        var engineeringPeople = gigaMap.Query("Department", "Engineering").Execute();
        foreach (var person in engineeringPeople)
        {
            Console.WriteLine($"  - {person}");
        }

        // Query by age range (using custom predicate)
        Console.WriteLine("\n2. Find people aged 30 or older:");
        var olderPeople = gigaMap.Query().And("Age").Where(age => age >= 30).Execute();
        foreach (var person in olderPeople)
        {
            Console.WriteLine($"  - {person}");
        }

        // Complex query with AND conditions
        Console.WriteLine("\n3. Find people in Engineering who are 30 or older:");
        var seniorEngineers = gigaMap.Query("Department", "Engineering")
            .And("Age").Where(age => age >= 30)
            .Execute();
        foreach (var person in seniorEngineers)
        {
            Console.WriteLine($"  - {person}");
        }

        // Query with OR conditions
        Console.WriteLine("\n4. Find people in Engineering OR Marketing:");
        var techAndMarketing = gigaMap.Query("Department", "Engineering")
            .Or("Department", "Marketing")
            .Execute();
        foreach (var person in techAndMarketing)
        {
            Console.WriteLine($"  - {person}");
        }

        // Count queries
        Console.WriteLine($"\n5. Total people in Engineering: {gigaMap.Query("Department", "Engineering").Count()}");
        Console.WriteLine($"   Any people aged over 40: {gigaMap.Query().And("Age").Where(age => age > 40).Any()}");

        // First/single result queries
        Console.WriteLine("\n6. First person in Marketing:");
        var firstMarketer = gigaMap.Query("Department", "Marketing").FirstOrDefault();
        if (firstMarketer != null)
        {
            Console.WriteLine($"  - {firstMarketer}");
        }
    }

    /// <summary>
    /// Demonstrates constraint validation.
    /// </summary>
    public static void DemonstrateConstraints()
    {
        var gigaMap = CreatePersonGigaMap();

        Console.WriteLine("\n=== GigaMap Constraints Demo ===");

        try
        {
            // This should work fine
            var validPerson = new Person
            {
                FirstName = "Valid",
                LastName = "Person",
                Email = "valid@company.com",
                Age = 25
            };
            var id1 = gigaMap.Add(validPerson);
            Console.WriteLine($"✓ Added valid person with ID: {id1}");

            // This should fail due to duplicate email (unique constraint)
            var duplicateEmail = new Person
            {
                FirstName = "Duplicate",
                LastName = "Email",
                Email = "valid@company.com", // Same email as above
                Age = 30
            };
            gigaMap.Add(duplicateEmail);
        }
        catch (ConstraintViolationException ex)
        {
            Console.WriteLine($"✗ Constraint violation (expected): {ex.Message}");
        }

        try
        {
            // This should fail due to invalid age (custom constraint)
            var invalidAge = new Person
            {
                FirstName = "Invalid",
                LastName = "Age",
                Email = "invalid.age@company.com",
                Age = 200 // Invalid age
            };
            gigaMap.Add(invalidAge);
        }
        catch (ConstraintViolationException ex)
        {
            Console.WriteLine($"✗ Constraint violation (expected): {ex.Message}");
        }

        try
        {
            // This should fail due to invalid email (custom constraint)
            var invalidEmail = new Person
            {
                FirstName = "Invalid",
                LastName = "Email",
                Email = "invalid-email", // No @ symbol
                Age = 25
            };
            gigaMap.Add(invalidEmail);
        }
        catch (ConstraintViolationException ex)
        {
            Console.WriteLine($"✗ Constraint violation (expected): {ex.Message}");
        }
    }

    /// <summary>
    /// Runs all the demonstration methods.
    /// </summary>
    public static void RunAllDemos()
    {
        DemonstrateCrudOperations();
        DemonstrateQuerying();
        DemonstrateConstraints();

        Console.WriteLine("\n=== Demo Complete ===");
    }
}