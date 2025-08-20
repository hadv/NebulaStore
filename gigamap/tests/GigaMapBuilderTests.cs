using FluentAssertions;
using NebulaStore.GigaMap.Tests.TestEntities;
using Xunit;

namespace NebulaStore.GigaMap.Tests;

/// <summary>
/// Tests for GigaMap builder functionality.
/// </summary>
public class GigaMapBuilderTests
{
    [Fact]
    public void Builder_ShouldCreateEmptyGigaMap()
    {
        // Act
        var gigaMap = GigaMap.Builder<TestPerson>().Build();

        // Assert
        gigaMap.Should().NotBeNull();
        gigaMap.Size.Should().Be(0);
        gigaMap.IsEmpty.Should().BeTrue();
        gigaMap.Index.Bitmap.Count.Should().Be(0);
    }

    [Fact]
    public void Builder_WithBitmapIndex_ShouldAddIndex()
    {
        // Arrange
        var emailIndexer = Indexer.Property<TestPerson, string>("Email", p => p.Email);

        // Act
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapIndex(emailIndexer)
            .Build();

        // Assert
        gigaMap.Index.Bitmap.Count.Should().Be(1);
        gigaMap.Index.Bitmap.HasIndexer("Email").Should().BeTrue();
    }

    [Fact]
    public void Builder_WithMultipleBitmapIndices_ShouldAddAllIndices()
    {
        // Arrange
        var emailIndexer = Indexer.Property<TestPerson, string>("Email", p => p.Email);
        var ageIndexer = Indexer.Property<TestPerson, int>("Age", p => p.Age);
        var departmentIndexer = Indexer.Property<TestPerson, string>("Department", p => p.Department);

        // Act
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapIndex(emailIndexer)
            .WithBitmapIndex(ageIndexer)
            .WithBitmapIndex(departmentIndexer)
            .Build();

        // Assert
        gigaMap.Index.Bitmap.Count.Should().Be(3);
        gigaMap.Index.Bitmap.HasIndexer("Email").Should().BeTrue();
        gigaMap.Index.Bitmap.HasIndexer("Age").Should().BeTrue();
        gigaMap.Index.Bitmap.HasIndexer("Department").Should().BeTrue();
    }

    [Fact]
    public void Builder_WithBitmapUniqueIndex_ShouldAddUniqueConstraint()
    {
        // Arrange
        var emailIndexer = Indexer.Property<TestPerson, string>("Email", p => p.Email);

        // Act
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapUniqueIndex(emailIndexer)
            .Build();

        // Assert
        gigaMap.Index.Bitmap.Count.Should().Be(1);
        gigaMap.Index.Bitmap.HasIndexer("Email").Should().BeTrue();
        gigaMap.Constraints.UniqueConstraints.Count.Should().Be(1);
    }

    [Fact]
    public void Builder_WithCustomConstraint_ShouldAddConstraint()
    {
        // Arrange
        var constraint = Constraint.Custom<TestPerson>(
            "ValidAge",
            person => person.Age >= 0 && person.Age <= 150,
            "Age must be between 0 and 150");

        // Act
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithCustomConstraint(constraint)
            .Build();

        // Assert
        gigaMap.Constraints.CustomConstraints.Count.Should().Be(1);
        gigaMap.Constraints.CustomConstraints.Should().Contain(constraint);
    }

    [Fact]
    public void Builder_WithMultipleCustomConstraints_ShouldAddAllConstraints()
    {
        // Arrange
        var ageConstraint = Constraint.Custom<TestPerson>("ValidAge", p => p.Age >= 18, "Must be adult");
        var emailConstraint = Constraint.Custom<TestPerson>("ValidEmail", p => p.Email.Contains("@"), "Email must be valid");

        // Act
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithCustomConstraints(ageConstraint, emailConstraint)
            .Build();

        // Assert
        gigaMap.Constraints.CustomConstraints.Count.Should().Be(2);
        gigaMap.Constraints.CustomConstraints.Should().Contain(ageConstraint);
        gigaMap.Constraints.CustomConstraints.Should().Contain(emailConstraint);
    }

    [Fact]
    public void Builder_WithValueEquality_ShouldUseValueEquality()
    {
        // Act
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithValueEquality()
            .Build();

        // Assert
        gigaMap.Should().NotBeNull();
        // Note: Testing value equality behavior would require adding entities and checking equality
    }

    [Fact]
    public void Builder_WithReferenceEquality_ShouldUseReferenceEquality()
    {
        // Act
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithReferenceEquality()
            .Build();

        // Assert
        gigaMap.Should().NotBeNull();
        // Note: Testing reference equality behavior would require adding entities and checking equality
    }

    [Fact]
    public void Builder_WithSegmentSize_ShouldSetSegmentSize()
    {
        // Act
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithSegmentSize(1000)
            .Build();

        // Assert
        gigaMap.Should().NotBeNull();
        // Note: Segment size is internal configuration, hard to test directly
    }

    [Fact]
    public void Builder_WithInitialCapacity_ShouldSetInitialCapacity()
    {
        // Act
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithInitialCapacity(500)
            .Build();

        // Assert
        gigaMap.Should().NotBeNull();
        // Note: Initial capacity is internal configuration, hard to test directly
    }

    [Fact]
    public void Builder_WithNullBitmapIndex_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => GigaMap.Builder<TestPerson>()
            .WithBitmapIndex(null!)
            .Build();
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Builder_WithNullBitmapUniqueIndex_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => GigaMap.Builder<TestPerson>()
            .WithBitmapUniqueIndex(null!)
            .Build();
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Builder_WithNullCustomConstraint_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => GigaMap.Builder<TestPerson>()
            .WithCustomConstraint(null!)
            .Build();
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Builder_WithNullCustomConstraints_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => GigaMap.Builder<TestPerson>()
            .WithCustomConstraints(null!)
            .Build();
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Builder_WithInvalidSegmentSize_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        var action = () => GigaMap.Builder<TestPerson>()
            .WithSegmentSize(0)
            .Build();
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Builder_WithInvalidInitialCapacity_ShouldThrowArgumentOutOfRangeException()
    {
        // Act & Assert
        var action = () => GigaMap.Builder<TestPerson>()
            .WithInitialCapacity(-1)
            .Build();
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Builder_ComplexConfiguration_ShouldWorkCorrectly()
    {
        // Arrange
        var emailIndexer = Indexer.Property<TestPerson, string>("Email", p => p.Email);
        var ageIndexer = Indexer.Property<TestPerson, int>("Age", p => p.Age);
        var ageConstraint = Constraint.Custom<TestPerson>("ValidAge", p => p.Age >= 18, "Must be adult");

        // Act
        var gigaMap = GigaMap.Builder<TestPerson>()
            .WithBitmapUniqueIndex(emailIndexer)
            .WithBitmapIndex(ageIndexer)
            .WithCustomConstraint(ageConstraint)
            .WithValueEquality()
            .WithSegmentSize(2000)
            .WithInitialCapacity(100)
            .Build();

        // Assert
        gigaMap.Should().NotBeNull();
        gigaMap.Index.Bitmap.Count.Should().Be(2);
        gigaMap.Index.Bitmap.HasIndexer("Email").Should().BeTrue();
        gigaMap.Index.Bitmap.HasIndexer("Age").Should().BeTrue();
        gigaMap.Constraints.UniqueConstraints.Count.Should().Be(1);
        gigaMap.Constraints.CustomConstraints.Count.Should().Be(1);
    }

    [Fact]
    public void Builder_ShouldBeReusable()
    {
        // Arrange
        var builder = GigaMap.Builder<TestPerson>()
            .WithBitmapIndex(Indexer.Property<TestPerson, string>("Email", p => p.Email));

        // Act
        var gigaMap1 = builder.Build();
        var gigaMap2 = builder.Build();

        // Assert
        gigaMap1.Should().NotBeSameAs(gigaMap2);
        gigaMap1.Index.Bitmap.Count.Should().Be(gigaMap2.Index.Bitmap.Count);
    }
}