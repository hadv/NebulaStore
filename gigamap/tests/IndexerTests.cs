using FluentAssertions;
using NebulaStore.GigaMap.Tests.TestEntities;
using Xunit;

namespace NebulaStore.GigaMap.Tests;

/// <summary>
/// Tests for indexer functionality.
/// </summary>
public class IndexerTests
{
    [Fact]
    public void PropertyIndexer_ShouldExtractCorrectKey()
    {
        // Arrange
        var indexer = Indexer.Property<TestPerson, string>("Email", p => p.Email);
        var person = TestPerson.CreateDefault("test@example.com");

        // Act
        var key = indexer.Index(person);

        // Assert
        key.Should().Be("test@example.com");
        indexer.Name.Should().Be("Email");
        indexer.KeyType.Should().Be(typeof(string));
        indexer.IsSuitableAsUniqueConstraint.Should().BeTrue();
    }

    [Fact]
    public void PropertyIndexer_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        var indexer = Indexer.Property<TestPerson, string>("Email", p => p.Email);

        // Act & Assert
        var action = () => indexer.Index(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void StringIgnoreCaseIndexer_ShouldUseIgnoreCaseComparer()
    {
        // Arrange
        var indexer = Indexer.StringIgnoreCase<TestPerson>("LastName", p => p.LastName);
        var person1 = TestPerson.CreateDefault();
        person1.LastName = "Smith";
        var person2 = TestPerson.CreateDefault();
        person2.LastName = "SMITH";

        // Act
        var key1 = indexer.Index(person1);
        var key2 = indexer.Index(person2);

        // Assert
        key1.Should().Be("Smith");
        key2.Should().Be("SMITH");
        indexer.KeyEqualityComparer.Equals(key1, key2).Should().BeTrue();
    }

    [Fact]
    public void NumericIndexer_ShouldExtractNumericKey()
    {
        // Arrange
        var indexer = Indexer.Numeric<TestPerson, int>("Age", p => p.Age);
        var person = TestPerson.CreateDefault();
        person.Age = 42;

        // Act
        var key = indexer.Index(person);

        // Assert
        key.Should().Be(42);
        indexer.Name.Should().Be("Age");
        indexer.KeyType.Should().Be(typeof(int));
    }

    [Fact]
    public void DateTimeIndexer_ShouldExtractDateTimeKey()
    {
        // Arrange
        var indexer = Indexer.DateTime<TestPerson>("DateOfBirth", p => p.DateOfBirth);
        var person = TestPerson.CreateDefault();
        var birthDate = new DateTime(1990, 5, 15);
        person.DateOfBirth = birthDate;

        // Act
        var key = indexer.Index(person);

        // Assert
        key.Should().Be(birthDate);
        indexer.Name.Should().Be("DateOfBirth");
        indexer.KeyType.Should().Be(typeof(DateTime));
    }

    [Fact]
    public void GuidIndexer_ShouldExtractGuidKey()
    {
        // Arrange
        var indexer = Indexer.Guid<TestPerson>("Id", p => p.Id);
        var person = TestPerson.CreateDefault();
        var guid = System.Guid.NewGuid();
        person.Id = guid;

        // Act
        var key = indexer.Index(person);

        // Assert
        key.Should().Be(guid);
        indexer.Name.Should().Be("Id");
        indexer.KeyType.Should().Be(typeof(System.Guid));
    }

    [Fact]
    public void IdentityIndexer_ShouldReturnSameObject()
    {
        // Arrange
        var indexer = Indexer.Identity<TestPerson>("Identity");
        var person = TestPerson.CreateDefault();

        // Act
        var key = indexer.Index(person);

        // Assert
        key.Should().BeSameAs(person);
        indexer.Name.Should().Be("Identity");
        indexer.KeyType.Should().Be(typeof(TestPerson));
        indexer.IsSuitableAsUniqueConstraint.Should().BeTrue();
    }

    [Fact]
    public void IdentityIndexer_WithNullEntity_ShouldThrowArgumentNullException()
    {
        // Arrange
        var indexer = Indexer.Identity<TestPerson>("Identity");

        // Act & Assert
        var action = () => indexer.Index(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PropertyIndexer_WithCustomEqualityComparer_ShouldUseComparer()
    {
        // Arrange
        var customComparer = StringComparer.OrdinalIgnoreCase;
        var indexer = Indexer.Property<TestPerson, string>("Department", p => p.Department, customComparer);

        // Act & Assert
        indexer.KeyEqualityComparer.Should().BeSameAs(customComparer);
    }

    [Fact]
    public void PropertyIndexer_WithNullName_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => Indexer.Property<TestPerson, string>(null!, p => p.Email);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void PropertyIndexer_WithNullKeyExtractor_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => Indexer.Property<TestPerson, string>("Email", null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IdentityIndexer_WithNullName_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => Indexer.Identity<TestPerson>(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void StringIgnoreCaseIndexer_WithInvalidName_ShouldThrowArgumentException(string invalidName)
    {
        // Act & Assert
        var action = () => Indexer.StringIgnoreCase<TestPerson>(invalidName, p => p.LastName);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PropertyIndexer_KeyEqualityComparer_ShouldDefaultToEqualityComparerDefault()
    {
        // Arrange
        var indexer = Indexer.Property<TestPerson, int>("Age", p => p.Age);

        // Act & Assert
        indexer.KeyEqualityComparer.Should().BeSameAs(EqualityComparer<int>.Default);
    }
}