using FluentAssertions;
using NebulaStore.GigaMap.Tests.TestEntities;
using Xunit;

namespace NebulaStore.GigaMap.Tests;

/// <summary>
/// Tests for GigaMap constraint functionality.
/// </summary>
public class ConstraintTests
{
    [Fact]
    public void UniqueConstraint_WithUniqueValues_ShouldAllowAddition()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapUniqueIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .Build();

        var person1 = TestPerson.CreateDefault("person1@test.com");
        var person2 = TestPerson.CreateDefault("person2@test.com");

        // Act & Assert
        var id1 = gigaMap.Add(person1);
        var id2 = gigaMap.Add(person2);

        id1.Should().NotBe(id2);
        gigaMap.Size.Should().Be(2);
    }

    [Fact]
    public void UniqueConstraint_WithDuplicateValues_ShouldThrowConstraintViolationException()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapUniqueIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .Build();

        var person1 = TestPerson.CreateDefault("duplicate@test.com");
        var person2 = TestPerson.CreateDefault("duplicate@test.com");
        gigaMap.Add(person1);

        // Act & Assert
        var action = () => gigaMap.Add(person2);
        action.Should().Throw<ConstraintViolationException>()
            .WithMessage("*duplicate@test.com*");
    }

    [Fact]
    public void UniqueConstraint_WithMultipleIndices_ShouldEnforceAllConstraints()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapUniqueIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .WithBitmapUniqueIndex(Indexer.Guid<TestPerson>("Id", p => p.Id))
            .Build();

        var sharedId = Guid.NewGuid();
        var person1 = TestPerson.CreateDefault("person1@test.com");
        person1.Id = sharedId;
        var person2 = TestPerson.CreateDefault("person2@test.com");
        person2.Id = sharedId; // Duplicate ID

        gigaMap.Add(person1);

        // Act & Assert
        var action = () => gigaMap.Add(person2);
        action.Should().Throw<ConstraintViolationException>();
    }

    [Fact]
    public void CustomConstraint_WithValidEntity_ShouldAllowAddition()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithCustomConstraint(Constraint.Custom<TestPerson>(
                "ValidAge",
                person => person.Age >= 0 && person.Age <= 150,
                "Age must be between 0 and 150"))
            .Build();

        var person = TestPerson.CreateDefault();
        person.Age = 25;

        // Act & Assert
        var id = gigaMap.Add(person);
        id.Should().BeGreaterOrEqualTo(0);
        gigaMap.Size.Should().Be(1);
    }

    [Fact]
    public void CustomConstraint_WithInvalidEntity_ShouldThrowConstraintViolationException()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithCustomConstraint(Constraint.Custom<TestPerson>(
                "ValidAge",
                person => person.Age >= 0 && person.Age <= 150,
                "Age must be between 0 and 150"))
            .Build();

        var person = TestPerson.CreateDefault();
        person.Age = 200; // Invalid age

        // Act & Assert
        var action = () => gigaMap.Add(person);
        action.Should().Throw<ConstraintViolationException>()
            .WithMessage("Age must be between 0 and 150");
    }

    [Fact]
    public void CustomConstraint_WithComplexValidation_ShouldWork()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithCustomConstraint(Constraint.Custom<TestPerson>(
                "ValidEmail",
                person => !string.IsNullOrEmpty(person.Email) && person.Email.Contains("@"),
                "Email must be valid"))
            .Build();

        var validPerson = TestPerson.CreateDefault("valid@test.com");
        var invalidPerson = TestPerson.CreateDefault("invalid-email");

        // Act & Assert
        gigaMap.Add(validPerson).Should().BeGreaterOrEqualTo(0);

        var action = () => gigaMap.Add(invalidPerson);
        action.Should().Throw<ConstraintViolationException>()
            .WithMessage("Email must be valid");
    }

    [Fact]
    public void CustomConstraint_WithMultipleConstraints_ShouldEnforceAll()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithCustomConstraints(
                Constraint.Custom<TestPerson>("ValidAge", p => p.Age >= 18, "Must be adult"),
                Constraint.Custom<TestPerson>("ValidEmail", p => p.Email.Contains("@"), "Email must be valid"),
                Constraint.Custom<TestPerson>("ValidSalary", p => p.Salary > 0, "Salary must be positive"))
            .Build();

        var validPerson = TestPerson.CreateDefault("valid@test.com");
        validPerson.Age = 25;
        validPerson.Salary = 50000;

        var invalidAgePerson = TestPerson.CreateDefault("test@test.com");
        invalidAgePerson.Age = 16; // Too young

        // Act & Assert
        gigaMap.Add(validPerson).Should().BeGreaterOrEqualTo(0);

        var action = () => gigaMap.Add(invalidAgePerson);
        action.Should().Throw<ConstraintViolationException>()
            .WithMessage("Must be adult");
    }

    [Fact]
    public void Constraint_OnUpdate_ShouldEnforceConstraints()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithCustomConstraint(Constraint.Custom<TestPerson>(
                "ValidAge",
                person => person.Age >= 0 && person.Age <= 150,
                "Age must be between 0 and 150"))
            .Build();

        var person = TestPerson.CreateDefault();
        person.Age = 25;
        gigaMap.Add(person);

        // Act & Assert
        var action = () => gigaMap.Update(person, p => p.Age = 200);
        action.Should().Throw<ConstraintViolationException>();
    }

    [Fact]
    public void Constraint_OnSet_ShouldEnforceConstraints()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapUniqueIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .Build();

        var person1 = TestPerson.CreateDefault("person1@test.com");
        var person2 = TestPerson.CreateDefault("person2@test.com");
        var person3 = TestPerson.CreateDefault("person1@test.com"); // Duplicate email

        var id1 = gigaMap.Add(person1);
        var id2 = gigaMap.Add(person2);

        // Act & Assert
        var action = () => gigaMap.Set(id2, person3);
        action.Should().Throw<ConstraintViolationException>();
    }

    [Fact]
    public void Constraint_OnReplace_ShouldEnforceConstraints()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapUniqueIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email))
            .Build();

        var person1 = TestPerson.CreateDefault("person1@test.com");
        var person2 = TestPerson.CreateDefault("person2@test.com");
        var person3 = TestPerson.CreateDefault("person1@test.com"); // Duplicate email

        gigaMap.Add(person1);
        gigaMap.Add(person2);

        // Act & Assert
        var action = () => gigaMap.Replace(person2, person3);
        action.Should().Throw<ConstraintViolationException>();
    }

    [Fact]
    public void ConstraintViolationException_ShouldContainConstraintName()
    {
        // Arrange
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithCustomConstraint(Constraint.Custom<TestPerson>(
                "CustomValidation",
                person => false, // Always fails
                "Custom validation failed"))
            .Build();

        var person = TestPerson.CreateDefault();

        // Act & Assert
        var action = () => gigaMap.Add(person);
        action.Should().Throw<ConstraintViolationException>()
            .Which.ConstraintName.Should().Be("CustomValidation");
    }
}