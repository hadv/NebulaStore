using NebulaStore.Afs.Blobstore;
using Xunit;

namespace NebulaStore.Afs.Azure.Storage.Tests;

/// <summary>
/// Tests for Azure Storage path validation functionality.
/// </summary>
public class AzureStoragePathValidatorTests
{
    private readonly AzureStoragePathValidator _validator = new();

    [Theory]
    [InlineData("valid-container", "file.txt")]
    [InlineData("container123", "folder/file.txt")]
    [InlineData("my-container-name", "deep/folder/structure/file.txt")]
    public void Validate_WithValidPaths_ShouldNotThrow(string containerName, string blobName)
    {
        // Arrange
        var path = BlobStorePath.New(containerName, blobName);

        // Act & Assert
        var exception = Record.Exception(() => _validator.Validate(path));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("Container", "file.txt")] // uppercase container
    [InlineData("container_name", "file.txt")] // underscore in container
    [InlineData("container name", "file.txt")] // space in container
    [InlineData("container-", "file.txt")] // ends with dash
    [InlineData("co", "file.txt")] // too short (less than 3 chars)
    public void Validate_WithInvalidContainerNames_ShouldThrow(string containerName, string blobName)
    {
        // Arrange
        var path = BlobStorePath.New(containerName, blobName);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _validator.Validate(path));
    }

    [Fact]
    public void New_ShouldCreateValidator()
    {
        // Act
        var validator = IAzureStoragePathValidator.New();

        // Assert
        Assert.NotNull(validator);
        Assert.IsType<AzureStoragePathValidator>(validator);
    }
}
