using System;
using NebulaStore.Afs.Blobstore;
using Xunit;

namespace NebulaStore.Afs.Kafka.Tests;

/// <summary>
/// Unit tests for KafkaPathValidator.
/// </summary>
public class KafkaPathValidatorTests
{
    [Fact]
    public void ToTopicName_SimplePath_ReturnsValidTopicName()
    {
        // Arrange
        var path = BlobStorePath.New("container", "file.txt");

        // Act
        var topicName = KafkaPathValidator.ToTopicName(path);

        // Assert
        Assert.NotNull(topicName);
        Assert.NotEmpty(topicName);
        Assert.True(KafkaPathValidator.IsValidTopicName(topicName));
    }

    [Fact]
    public void ToTopicName_PathWithSlashes_ReplacesWithUnderscores()
    {
        // Arrange
        var path = BlobStorePath.New("container", "dir1", "dir2", "file.txt");

        // Act
        var topicName = KafkaPathValidator.ToTopicName(path);

        // Assert
        Assert.DoesNotContain("/", topicName);
        Assert.Contains("_", topicName);
    }

    [Fact]
    public void ToTopicName_PathWithInvalidChars_ReplacesWithUnderscores()
    {
        // Arrange
        var path = BlobStorePath.New("container", "file@#$.txt");

        // Act
        var topicName = KafkaPathValidator.ToTopicName(path);

        // Assert
        Assert.DoesNotContain("@", topicName);
        Assert.DoesNotContain("#", topicName);
        Assert.DoesNotContain("$", topicName);
        Assert.True(KafkaPathValidator.IsValidTopicName(topicName));
    }

    [Fact]
    public void ToTopicName_PathStartingWithDoubleUnderscore_AddsPrefixToAvoidReserved()
    {
        // Arrange
        // Create a path that would result in a topic name starting with "__"
        var path = BlobStorePath.New("__reserved");

        // Act
        var topicName = KafkaPathValidator.ToTopicName(path);

        // Assert
        Assert.StartsWith("ns__", topicName);
    }

    [Fact]
    public void ToTopicName_DotPath_AddsPrefixToAvoidSpecialCase()
    {
        // Arrange
        var path = BlobStorePath.New(".");

        // Act
        var topicName = KafkaPathValidator.ToTopicName(path);

        // Assert
        Assert.NotEqual(".", topicName);
        Assert.StartsWith("ns_", topicName);
    }

    [Fact]
    public void ToTopicName_DoubleDotPath_AddsPrefixToAvoidSpecialCase()
    {
        // Arrange
        var path = BlobStorePath.New("..");

        // Act
        var topicName = KafkaPathValidator.ToTopicName(path);

        // Assert
        Assert.NotEqual("..", topicName);
        Assert.StartsWith("ns_", topicName);
    }

    [Fact]
    public void ToTopicName_VeryLongPath_TruncatesToMaxLength()
    {
        // Arrange
        var longName = new string('a', 300);
        var path = BlobStorePath.New(longName);

        // Act
        var topicName = KafkaPathValidator.ToTopicName(path);

        // Assert
        Assert.True(topicName.Length <= 249);
        Assert.True(KafkaPathValidator.IsValidTopicName(topicName));
    }

    [Fact]
    public void ToTopicName_NullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => KafkaPathValidator.ToTopicName(null!));
    }

    [Fact]
    public void GetIndexTopicName_ValidTopicName_ReturnsIndexTopicName()
    {
        // Arrange
        var dataTopicName = "my-data-topic";

        // Act
        var indexTopicName = KafkaPathValidator.GetIndexTopicName(dataTopicName);

        // Assert
        Assert.Equal("__my-data-topic_index", indexTopicName);
    }

    [Fact]
    public void GetIndexTopicName_NullTopicName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => KafkaPathValidator.GetIndexTopicName(null!));
    }

    [Fact]
    public void GetIndexTopicName_EmptyTopicName_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => KafkaPathValidator.GetIndexTopicName(""));
    }

    [Fact]
    public void IsValidTopicName_ValidName_ReturnsTrue()
    {
        // Act & Assert
        Assert.True(KafkaPathValidator.IsValidTopicName("valid-topic-name"));
        Assert.True(KafkaPathValidator.IsValidTopicName("valid_topic_name"));
        Assert.True(KafkaPathValidator.IsValidTopicName("valid.topic.name"));
        Assert.True(KafkaPathValidator.IsValidTopicName("ValidTopicName123"));
    }

    [Fact]
    public void IsValidTopicName_InvalidName_ReturnsFalse()
    {
        // Act & Assert
        Assert.False(KafkaPathValidator.IsValidTopicName(null!));
        Assert.False(KafkaPathValidator.IsValidTopicName(""));
        Assert.False(KafkaPathValidator.IsValidTopicName("   "));
        Assert.False(KafkaPathValidator.IsValidTopicName("."));
        Assert.False(KafkaPathValidator.IsValidTopicName(".."));
        Assert.False(KafkaPathValidator.IsValidTopicName("invalid@topic"));
        Assert.False(KafkaPathValidator.IsValidTopicName("invalid#topic"));
        Assert.False(KafkaPathValidator.IsValidTopicName("invalid$topic"));
        Assert.False(KafkaPathValidator.IsValidTopicName("invalid/topic"));
    }

    [Fact]
    public void IsValidTopicName_TooLong_ReturnsFalse()
    {
        // Arrange
        var tooLongName = new string('a', 250);

        // Act & Assert
        Assert.False(KafkaPathValidator.IsValidTopicName(tooLongName));
    }

    [Theory]
    [InlineData("container/file.txt", "container_file.txt")]
    [InlineData("dir1/dir2/file.txt", "dir1_dir2_file.txt")]
    [InlineData("simple", "simple")]
    [InlineData("with-dashes", "with-dashes")]
    [InlineData("with.dots", "with.dots")]
    public void ToTopicName_VariousPaths_ProducesExpectedTopicNames(string pathString, string expectedPattern)
    {
        // Arrange
        var parts = pathString.Split('/');
        var path = BlobStorePath.New(parts);

        // Act
        var topicName = KafkaPathValidator.ToTopicName(path);

        // Assert
        Assert.Contains(expectedPattern, topicName);
        Assert.True(KafkaPathValidator.IsValidTopicName(topicName));
    }

    [Fact]
    public void New_CreatesInstance()
    {
        // Act
        var validator = KafkaPathValidator.New();

        // Assert
        Assert.NotNull(validator);
    }

    [Fact]
    public void ToTopicName_AlphanumericPath_PreservesName()
    {
        // Arrange
        var path = BlobStorePath.New("container", "file123");

        // Act
        var topicName = KafkaPathValidator.ToTopicName(path);

        // Assert
        Assert.Contains("file123", topicName);
    }

    [Fact]
    public void ToTopicName_PathWithDashes_PreservesDashes()
    {
        // Arrange
        var path = BlobStorePath.New("my-container", "my-file");

        // Act
        var topicName = KafkaPathValidator.ToTopicName(path);

        // Assert
        Assert.Contains("my-container", topicName);
        Assert.Contains("my-file", topicName);
    }

    [Fact]
    public void ToTopicName_PathWithDots_PreservesDots()
    {
        // Arrange
        var path = BlobStorePath.New("my.container", "file.txt");

        // Act
        var topicName = KafkaPathValidator.ToTopicName(path);

        // Assert
        Assert.Contains("my.container", topicName);
        Assert.Contains("file.txt", topicName);
    }

    [Fact]
    public void ToTopicName_ConsistentResults_SamePathProducesSameTopicName()
    {
        // Arrange
        var path1 = BlobStorePath.New("container", "file.txt");
        var path2 = BlobStorePath.New("container", "file.txt");

        // Act
        var topicName1 = KafkaPathValidator.ToTopicName(path1);
        var topicName2 = KafkaPathValidator.ToTopicName(path2);

        // Assert
        Assert.Equal(topicName1, topicName2);
    }

    [Fact]
    public void GetIndexTopicName_StartsWithDoubleUnderscore()
    {
        // Arrange
        var dataTopicName = "data-topic";

        // Act
        var indexTopicName = KafkaPathValidator.GetIndexTopicName(dataTopicName);

        // Assert
        Assert.StartsWith("__", indexTopicName);
    }

    [Fact]
    public void GetIndexTopicName_EndsWithIndex()
    {
        // Arrange
        var dataTopicName = "data-topic";

        // Act
        var indexTopicName = KafkaPathValidator.GetIndexTopicName(dataTopicName);

        // Assert
        Assert.EndsWith("_index", indexTopicName);
    }
}

