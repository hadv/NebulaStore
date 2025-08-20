using FluentAssertions;
using NebulaStore.GigaMap.Tests.TestEntities;
using Xunit;

namespace NebulaStore.GigaMap.Tests;

/// <summary>
/// Tests for basic CRUD operations in GigaMap.
/// </summary>
public class GigaMapCrudTests
{
    [Fact]
    public void New_ShouldCreateEmptyGigaMap()
    {
        // Act
        var gigaMap = GigaMap.New<TestPerson>();

        // Assert
        gigaMap.Should().NotBeNull();
        gigaMap.Size.Should().Be(0);
        gigaMap.IsEmpty.Should().BeTrue();
        gigaMap.HighestUsedId.Should().Be(-1);
        gigaMap.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void Add_ShouldAddEntityAndReturnId()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();

        // Act
        var entityId = gigaMap.Add(person);

        // Assert
        entityId.Should().BeGreaterOrEqualTo(0);
        gigaMap.Size.Should().Be(1);
        gigaMap.IsEmpty.Should().BeFalse();
        gigaMap.HighestUsedId.Should().Be(entityId);
    }

    [Fact]
    public void Add_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();

        // Act & Assert
        var action = () => gigaMap.Add(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddAll_ShouldAddMultipleEntitiesAndReturnLastId()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var people = TestPerson.CreateTestCollection(3);

        // Act
        var lastId = gigaMap.AddAll(people);

        // Assert
        gigaMap.Size.Should().Be(3);
        lastId.Should().Be(2); // IDs should be 0, 1, 2
        gigaMap.HighestUsedId.Should().Be(2);
    }

    [Fact]
    public void AddAll_WithNullCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();

        // Act & Assert
        var action = () => gigaMap.AddAll(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddAll_WithNullEntityInCollection_ShouldThrowArgumentNullException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var people = new List<TestPerson> { TestPerson.CreateDefault(), null!, TestPerson.CreateDefault() };

        // Act & Assert
        var action = () => gigaMap.AddAll(people);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Get_WithValidId_ShouldReturnEntity()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();
        var entityId = gigaMap.Add(person);

        // Act
        var retrievedPerson = gigaMap.Get(entityId);

        // Assert
        retrievedPerson.Should().NotBeNull();
        retrievedPerson.Should().BeSameAs(person);
    }

    [Fact]
    public void Get_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();

        // Act
        var retrievedPerson = gigaMap.Get(999);

        // Assert
        retrievedPerson.Should().BeNull();
    }

    [Fact]
    public void Peek_ShouldReturnSameAsGet()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();
        var entityId = gigaMap.Add(person);

        // Act
        var peekedPerson = gigaMap.Peek(entityId);
        var gotPerson = gigaMap.Get(entityId);

        // Assert
        peekedPerson.Should().BeSameAs(gotPerson);
    }

    [Fact]
    public void RemoveById_WithValidId_ShouldRemoveAndReturnEntity()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();
        var entityId = gigaMap.Add(person);

        // Act
        var removedPerson = gigaMap.RemoveById(entityId);

        // Assert
        removedPerson.Should().BeSameAs(person);
        gigaMap.Size.Should().Be(0);
        gigaMap.Get(entityId).Should().BeNull();
    }

    [Fact]
    public void RemoveById_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();

        // Act
        var removedPerson = gigaMap.RemoveById(999);

        // Assert
        removedPerson.Should().BeNull();
        gigaMap.Size.Should().Be(0);
    }

    [Fact]
    public void Remove_WithValidEntity_ShouldRemoveAndReturnId()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();
        var entityId = gigaMap.Add(person);

        // Act
        var removedId = gigaMap.Remove(person);

        // Assert
        removedId.Should().Be(entityId);
        gigaMap.Size.Should().Be(0);
        gigaMap.Get(entityId).Should().BeNull();
    }

    [Fact]
    public void Remove_WithNonExistentEntity_ShouldReturnMinusOne()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();

        // Act
        var removedId = gigaMap.Remove(person);

        // Assert
        removedId.Should().Be(-1);
    }

    [Fact]
    public void Remove_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();

        // Act & Assert
        var action = () => gigaMap.Remove(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemoveAll_ShouldClearAllEntities()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var people = TestPerson.CreateTestCollection(5);
        gigaMap.AddAll(people);

        // Act
        gigaMap.RemoveAll();

        // Assert
        gigaMap.Size.Should().Be(0);
        gigaMap.IsEmpty.Should().BeTrue();
        gigaMap.HighestUsedId.Should().Be(-1);
    }

    [Fact]
    public void Clear_ShouldBehaveSameAsRemoveAll()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var people = TestPerson.CreateTestCollection(3);
        gigaMap.AddAll(people);

        // Act
        gigaMap.Clear();

        // Assert
        gigaMap.Size.Should().Be(0);
        gigaMap.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Set_WithValidId_ShouldReplaceEntityAndReturnOld()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var originalPerson = TestPerson.CreateDefault("original@test.com");
        var newPerson = TestPerson.CreateDefault("new@test.com");
        var entityId = gigaMap.Add(originalPerson);

        // Act
        var oldPerson = gigaMap.Set(entityId, newPerson);

        // Assert
        oldPerson.Should().BeSameAs(originalPerson);
        gigaMap.Get(entityId).Should().BeSameAs(newPerson);
        gigaMap.Size.Should().Be(1);
    }

    [Fact]
    public void Set_WithInvalidId_ShouldThrowArgumentException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();

        // Act & Assert
        var action = () => gigaMap.Set(999, person);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Set_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();
        var entityId = gigaMap.Add(person);

        // Act & Assert
        var action = () => gigaMap.Set(entityId, null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Replace_WithValidEntity_ShouldReplaceAndReturnId()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var originalPerson = TestPerson.CreateDefault("original@test.com");
        var newPerson = TestPerson.CreateDefault("new@test.com");
        var entityId = gigaMap.Add(originalPerson);

        // Act
        var replacedId = gigaMap.Replace(originalPerson, newPerson);

        // Assert
        replacedId.Should().Be(entityId);
        gigaMap.Get(entityId).Should().BeSameAs(newPerson);
        gigaMap.Size.Should().Be(1);
    }

    [Fact]
    public void Replace_WithSameObject_ShouldThrowArgumentException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();
        gigaMap.Add(person);

        // Act & Assert
        var action = () => gigaMap.Replace(person, person);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Replace_WithNullCurrent_ShouldThrowArgumentNullException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var newPerson = TestPerson.CreateDefault();

        // Act & Assert
        var action = () => gigaMap.Replace(null!, newPerson);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Replace_WithNullReplacement_ShouldThrowArgumentNullException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();
        gigaMap.Add(person);

        // Act & Assert
        var action = () => gigaMap.Replace(person, null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Update_WithValidEntity_ShouldUpdateAndReturnEntity()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();
        var entityId = gigaMap.Add(person);
        var originalAge = person.Age;

        // Act
        var updatedPerson = gigaMap.Update(person, p => p.Age = 35);

        // Assert
        updatedPerson.Should().BeSameAs(person);
        updatedPerson.Age.Should().Be(35);
        updatedPerson.Age.Should().NotBe(originalAge);
        gigaMap.Get(entityId).Should().BeSameAs(person);
    }

    [Fact]
    public void Update_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();

        // Act & Assert
        var action = () => gigaMap.Update(null!, p => p.Age = 35);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Update_WithNullAction_ShouldThrowArgumentNullException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();
        gigaMap.Add(person);

        // Act & Assert
        var action = () => gigaMap.Update(person, null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_WithValidEntity_ShouldApplyLogicAndReturnResult()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();
        gigaMap.Add(person);

        // Act
        var result = gigaMap.Apply(person, p => $"{p.FirstName} {p.LastName}");

        // Assert
        result.Should().Be("John Doe");
    }

    [Fact]
    public void Apply_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();

        // Act & Assert
        var action = () => gigaMap.Apply(null!, p => p.FirstName);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_WithNullLogic_ShouldThrowArgumentNullException()
    {
        // Arrange
        var gigaMap = GigaMap.New<TestPerson>();
        var person = TestPerson.CreateDefault();
        gigaMap.Add(person);

        // Act & Assert
        var action = () => gigaMap.Apply(person, (Func<TestPerson, string>)null!);
        action.Should().Throw<ArgumentNullException>();
    }
}