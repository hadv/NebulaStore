using Xunit;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Afs.OracleCloud.ObjectStorage;

namespace NebulaStore.Afs.OracleCloud.ObjectStorage.Tests;

/// <summary>
/// Unit tests for OracleCloudObjectStoragePathValidator.
/// </summary>
public class OracleCloudObjectStoragePathValidatorTests
{
    private readonly IOracleCloudObjectStoragePathValidator _validator;

    public OracleCloudObjectStoragePathValidatorTests()
    {
        _validator = IOracleCloudObjectStoragePathValidator.New();
    }

    [Theory]
    [InlineData("valid-bucket-name")]
    [InlineData("bucket123")]
    [InlineData("my-bucket-2024")]
    [InlineData("bucket_with_underscores")]
    [InlineData("bucket.with.periods")]
    [InlineData("a")]
    [InlineData("bucket-name-with-many-characters-but-still-valid-123")]
    public void ValidateBucketName_ValidNames_ShouldNotThrow(string bucketName)
    {
        // Act & Assert
        var exception = Record.Exception(() => _validator.ValidateBucketName(bucketName));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Invalid-Bucket")]  // Uppercase not allowed
    [InlineData("bucket..name")]    // Consecutive periods
    [InlineData("-bucket")]          // Cannot start with hyphen
    [InlineData("bucket-")]          // Cannot end with hyphen
    [InlineData("UPPERCASE")]        // All uppercase
    public void ValidateBucketName_InvalidNames_ShouldThrow(string bucketName)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _validator.ValidateBucketName(bucketName));
    }

    [Theory]
    [InlineData("valid/path/to/object.txt")]
    [InlineData("simple-object")]
    [InlineData("path/with/multiple/levels/file.dat")]
    [InlineData("object_with_underscores.txt")]
    [InlineData("object-with-hyphens.txt")]
    [InlineData("object.with.periods.txt")]
    public void ValidateObjectName_ValidNames_ShouldNotThrow(string objectName)
    {
        // Act & Assert
        var exception = Record.Exception(() => _validator.ValidateObjectName(objectName));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData(" leading-space")]
    [InlineData("trailing-space ")]
    public void ValidateObjectName_InvalidNames_ShouldThrow(string objectName)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _validator.ValidateObjectName(objectName));
    }

    [Fact]
    public void ValidateObjectName_TooLong_ShouldThrow()
    {
        // Arrange
        var longName = new string('a', 1025);  // Max is 1024

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _validator.ValidateObjectName(longName));
    }

    [Fact]
    public void ValidateBucketName_TooLong_ShouldThrow()
    {
        // Arrange
        var longName = new string('a', 257);  // Max is 256

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _validator.ValidateBucketName(longName));
    }

    [Fact]
    public void Validate_ValidPath_ShouldNotThrow()
    {
        // Arrange
        var path = BlobStorePath.New("valid-bucket", "path", "to", "object.txt");

        // Act & Assert
        var exception = Record.Exception(() => _validator.Validate(path));
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_InvalidBucketName_ShouldThrow()
    {
        // Arrange
        var path = BlobStorePath.New("Invalid-Bucket", "object.txt");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _validator.Validate(path));
    }

    [Fact]
    public void Validate_InvalidObjectName_ShouldThrow()
    {
        // Arrange
        var path = BlobStorePath.New("valid-bucket", ".");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _validator.Validate(path));
    }
}

