using FluentAssertions;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Afs.Blobstore.Types;
using Xunit;

namespace NebulaStore.Afs.Blobstore.Tests;

/// <summary>
/// Tests for BlobStorePath functionality.
/// </summary>
public class BlobStorePathTests
{
    [Fact]
    public void Constructor_WithValidPathElements_ShouldCreatePath()
    {
        // Arrange
        var pathElements = new[] { "container", "folder", "file.txt" };

        // Act
        var path = new BlobStorePath(pathElements);

        // Assert
        path.PathElements.Should().BeEquivalentTo(pathElements);
        path.Container.Should().Be("container");
        path.Identifier.Should().Be("file.txt");
        path.FullQualifiedName.Should().Be("container/folder/file.txt");
    }

    [Fact]
    public void Constructor_WithEmptyPathElements_ShouldThrowArgumentException()
    {
        // Arrange
        var pathElements = Array.Empty<string>();

        // Act & Assert
        var act = () => new BlobStorePath(pathElements);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Path cannot be empty*");
    }

    [Fact]
    public void Constructor_WithNullPathElements_ShouldThrowArgumentException()
    {
        // Act & Assert
        var act = () => new BlobStorePath(null!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Path cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmptyPathElement_ShouldThrowArgumentException()
    {
        // Arrange
        var pathElements = new[] { "container", "", "file.txt" };

        // Act & Assert
        var act = () => new BlobStorePath(pathElements);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Path elements cannot be null or empty*");
    }

    [Fact]
    public void Constructor_WithNullPathElement_ShouldThrowArgumentException()
    {
        // Arrange
        var pathElements = new[] { "container", null!, "file.txt" };

        // Act & Assert
        var act = () => new BlobStorePath(pathElements);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Path elements cannot be null or empty*");
    }

    [Fact]
    public void Container_ShouldReturnFirstPathElement()
    {
        // Arrange
        var path = new BlobStorePath("mycontainer", "folder", "file.txt");

        // Act & Assert
        path.Container.Should().Be("mycontainer");
    }

    [Fact]
    public void Identifier_ShouldReturnLastPathElement()
    {
        // Arrange
        var path = new BlobStorePath("container", "folder", "myfile.txt");

        // Act & Assert
        path.Identifier.Should().Be("myfile.txt");
    }

    [Fact]
    public void FullQualifiedName_WithSingleElement_ShouldReturnElement()
    {
        // Arrange
        var path = new BlobStorePath("container");

        // Act & Assert
        path.FullQualifiedName.Should().Be("container");
    }

    [Fact]
    public void FullQualifiedName_WithMultipleElements_ShouldReturnJoinedPath()
    {
        // Arrange
        var path = new BlobStorePath("container", "folder", "subfolder", "file.txt");

        // Act & Assert
        path.FullQualifiedName.Should().Be("container/folder/subfolder/file.txt");
    }

    [Fact]
    public void ParentPath_WithSingleElement_ShouldReturnNull()
    {
        // Arrange
        var path = new BlobStorePath("container");

        // Act & Assert
        path.ParentPath.Should().BeNull();
    }

    [Fact]
    public void ParentPath_WithMultipleElements_ShouldReturnParent()
    {
        // Arrange
        var path = new BlobStorePath("container", "folder", "file.txt");

        // Act
        var parent = path.ParentPath;

        // Assert
        parent.Should().NotBeNull();
        parent!.PathElements.Should().BeEquivalentTo(new[] { "container", "folder" });
        parent.FullQualifiedName.Should().Be("container/folder");
    }

    [Fact]
    public void SplitPath_WithValidPath_ShouldReturnPathElements()
    {
        // Arrange
        var fullPath = "container/folder/subfolder/file.txt";

        // Act
        var elements = BlobStorePath.SplitPath(fullPath);

        // Assert
        elements.Should().BeEquivalentTo(new[] { "container", "folder", "subfolder", "file.txt" });
    }

    [Fact]
    public void SplitPath_WithEmptyPath_ShouldThrowArgumentException()
    {
        // Act & Assert
        var act = () => BlobStorePath.SplitPath("");
        act.Should().Throw<ArgumentException>()
            .WithMessage("Path cannot be null or empty*");
    }

    [Fact]
    public void SplitPath_WithNullPath_ShouldThrowArgumentException()
    {
        // Act & Assert
        var act = () => BlobStorePath.SplitPath(null!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("Path cannot be null or empty*");
    }

    [Fact]
    public void New_WithValidElements_ShouldCreatePath()
    {
        // Arrange
        var elements = new[] { "container", "folder", "file.txt" };

        // Act
        var path = BlobStorePath.New(elements);

        // Assert
        path.PathElements.Should().BeEquivalentTo(elements);
    }

    [Fact]
    public void FromString_WithValidPath_ShouldCreatePath()
    {
        // Arrange
        var fullPath = "container/folder/file.txt";

        // Act
        var path = BlobStorePath.FromString(fullPath);

        // Assert
        path.PathElements.Should().BeEquivalentTo(new[] { "container", "folder", "file.txt" });
        path.FullQualifiedName.Should().Be(fullPath);
    }

    [Fact]
    public void ToString_ShouldReturnFullQualifiedName()
    {
        // Arrange
        var path = new BlobStorePath("container", "folder", "file.txt");

        // Act
        var result = path.ToString();

        // Assert
        result.Should().Be("container/folder/file.txt");
    }

    [Fact]
    public void Equals_WithSamePath_ShouldReturnTrue()
    {
        // Arrange
        var path1 = new BlobStorePath("container", "folder", "file.txt");
        var path2 = new BlobStorePath("container", "folder", "file.txt");

        // Act & Assert
        path1.Equals(path2).Should().BeTrue();
        path1.GetHashCode().Should().Be(path2.GetHashCode());
    }

    [Fact]
    public void Equals_WithDifferentPath_ShouldReturnFalse()
    {
        // Arrange
        var path1 = new BlobStorePath("container", "folder", "file1.txt");
        var path2 = new BlobStorePath("container", "folder", "file2.txt");

        // Act & Assert
        path1.Equals(path2).Should().BeFalse();
    }

    [Fact]
    public void Equals_WithNonBlobStorePath_ShouldReturnFalse()
    {
        // Arrange
        var path = new BlobStorePath("container", "folder", "file.txt");
        var other = "some string";

        // Act & Assert
        path.Equals(other).Should().BeFalse();
    }

    [Fact]
    public void Validate_WithNoOpValidator_ShouldNotThrow()
    {
        // Arrange
        var path = new BlobStorePath("container", "folder", "file.txt");
        var validator = NoOpPathValidator.Instance;

        // Act & Assert
        var act = () => path.Validate(validator);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("container/file.txt", new[] { "container", "file.txt" })]
    [InlineData("container/folder/subfolder/file.txt", new[] { "container", "folder", "subfolder", "file.txt" })]
    [InlineData("single", new[] { "single" })]
    public void SplitPath_WithVariousPaths_ShouldReturnCorrectElements(string path, string[] expected)
    {
        // Act
        var result = BlobStorePath.SplitPath(path);

        // Assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ParentPath_WithNestedPath_ShouldReturnCorrectParent()
    {
        // Arrange
        var path = new BlobStorePath("container", "folder", "subfolder", "file.txt");

        // Act
        var parent = path.ParentPath;
        var grandParent = parent?.ParentPath;

        // Assert
        parent.Should().NotBeNull();
        parent!.FullQualifiedName.Should().Be("container/folder/subfolder");

        grandParent.Should().NotBeNull();
        grandParent!.FullQualifiedName.Should().Be("container/folder");
    }
}
