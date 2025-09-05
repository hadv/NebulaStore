using System;
using System.Collections.Generic;
using FluentAssertions;
using NebulaStore.Afs.GoogleCloud.Firestore;
using NebulaStore.Storage.Embedded;
using NebulaStore.Storage.EmbeddedConfiguration;
using Xunit;

namespace NebulaStore.Afs.GoogleCloud.Firestore.Tests;

/// <summary>
/// End-to-end integration tests for Firestore storage.
/// These tests verify that the complete integration works correctly.
/// Note: These tests require actual Firestore connectivity and should be run with proper credentials.
/// </summary>
public class FirestoreEndToEndIntegrationTest
{
    private const string TestProjectId = "test-project-id";
    private const string TestStorageDirectory = "test-firestore-storage";

    /// <summary>
    /// Test data class for integration testing.
    /// </summary>
    public class TestPerson
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Email { get; set; } = string.Empty;
        public List<string> Hobbies { get; set; } = new();
    }

    [Fact]
    public void EmbeddedStorageFirestoreExtensions_StartWithFirestore_ShouldCreateValidStorageManager()
    {
        // Arrange & Act
        var action = () => EmbeddedStorageFirestoreExtensions.StartWithFirestore(TestProjectId, TestStorageDirectory);

        // Assert
        // This should not throw an exception during creation (though it may fail on actual Firestore operations)
        action.Should().NotThrow();
    }

    [Fact]
    public void EmbeddedStorageFirestoreExtensions_StartWithFirestore_WithRoot_ShouldCreateValidStorageManager()
    {
        // Arrange
        var testPerson = new TestPerson
        {
            Name = "John Doe",
            Age = 30,
            Email = "john.doe@example.com",
            Hobbies = new List<string> { "Reading", "Gaming" }
        };

        // Act
        var action = () => EmbeddedStorageFirestoreExtensions.StartWithFirestore(testPerson, TestProjectId, TestStorageDirectory);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void EmbeddedStorage_StartWithFirestore_ShouldCreateValidStorageManager()
    {
        // Arrange & Act
        var action = () => EmbeddedStorage.StartWithFirestore(TestProjectId, TestStorageDirectory);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void EmbeddedStorage_StartWithFirestore_WithRoot_ShouldCreateValidStorageManager()
    {
        // Arrange
        var testPerson = new TestPerson
        {
            Name = "Jane Doe",
            Age = 25,
            Email = "jane.doe@example.com",
            Hobbies = new List<string> { "Cooking", "Traveling" }
        };

        // Act
        var action = () => EmbeddedStorage.StartWithFirestore(testPerson, TestProjectId, TestStorageDirectory);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void FirestoreConfigurationExtensions_UseFirestore_ShouldConfigureCorrectly()
    {
        // Arrange & Act
        var config = EmbeddedStorageConfiguration.New()
            .UseFirestore(TestProjectId)
            .Build();

        // Assert
        config.UseAfs.Should().BeTrue();
        config.AfsStorageType.Should().Be("firestore");
        config.AfsConnectionString.Should().Be(TestProjectId);
        config.AfsUseCache.Should().BeTrue();
    }

    [Fact]
    public void FirestoreConfigurationExtensions_UseFirestore_WithCustomSettings_ShouldConfigureCorrectly()
    {
        // Arrange & Act
        var config = EmbeddedStorageConfiguration.New()
            .UseFirestore(TestProjectId, TestStorageDirectory, useCache: false)
            .Build();

        // Assert
        config.UseAfs.Should().BeTrue();
        config.AfsStorageType.Should().Be("firestore");
        config.AfsConnectionString.Should().Be(TestProjectId);
        config.AfsUseCache.Should().BeFalse();
        config.StorageDirectory.Should().Be(TestStorageDirectory);
    }

    [Fact]
    public void FirestoreConfigurationExtensions_UseFirestore_WithNullProjectId_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var action = () => EmbeddedStorageConfiguration.New()
            .UseFirestore(null!)
            .Build();

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Project ID cannot be null or empty*");
    }

    [Fact]
    public void FirestoreConfigurationExtensions_UseFirestore_WithEmptyProjectId_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var action = () => EmbeddedStorageConfiguration.New()
            .UseFirestore("")
            .Build();

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Project ID cannot be null or empty*");
    }

    [Fact]
    public void CreateFirestoreConnector_ShouldCreateValidConnector()
    {
        // Arrange & Act
        var action = () => EmbeddedStorageFirestoreExtensions.CreateFirestoreConnector(TestProjectId);

        // Assert
        // This should not throw during creation (though it may fail on actual Firestore operations)
        action.Should().NotThrow();
    }

    [Fact]
    public void CreateFirestoreFileSystem_ShouldCreateValidFileSystem()
    {
        // Arrange & Act
        var action = () => EmbeddedStorageFirestoreExtensions.CreateFirestoreFileSystem(TestProjectId);

        // Assert
        action.Should().NotThrow();
    }

    /// <summary>
    /// This test verifies that the AFS storage connection can be created with Firestore configuration.
    /// It tests the integration point where the AFS system creates the Firestore connector.
    /// </summary>
    [Fact]
    public void AfsStorageConnection_WithFirestoreConfiguration_ShouldAttemptToCreateConnector()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory(TestStorageDirectory)
            .SetUseAfs(true)
            .SetAfsStorageType("firestore")
            .SetAfsConnectionString(TestProjectId)
            .Build();

        // Act & Assert
        // This should attempt to create the Firestore connector through reflection
        // It will likely throw a NotSupportedException if the Google.Cloud.Firestore package is not available
        // or if there are authentication issues, but it should not fail due to configuration problems
        var action = () => new NebulaStore.Afs.Blobstore.AfsStorageConnection(
            config, 
            new NebulaStore.Storage.TypeHandlerRegistry());

        // The exact exception depends on the environment, but it should be a NotSupportedException
        // indicating that the Firestore connector could not be created due to missing dependencies
        action.Should().Throw<NotSupportedException>()
            .WithMessage("*Google Cloud Firestore connector could not be created*");
    }

    /// <summary>
    /// Test that verifies the configuration validation in the EmbeddedStorage convenience methods.
    /// </summary>
    [Fact]
    public void EmbeddedStorage_StartWithFirestore_WithNullProjectId_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var action = () => EmbeddedStorage.StartWithFirestore(null!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Project ID cannot be null or empty*");
    }

    [Fact]
    public void EmbeddedStorage_StartWithFirestore_WithEmptyProjectId_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var action = () => EmbeddedStorage.StartWithFirestore("");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Project ID cannot be null or empty*");
    }

    [Fact]
    public void EmbeddedStorageFirestoreExtensions_StartWithFirestore_WithNullProjectId_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var action = () => EmbeddedStorageFirestoreExtensions.StartWithFirestore(null!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Project ID cannot be null or empty*");
    }

    [Fact]
    public void EmbeddedStorageFirestoreExtensions_StartWithFirestore_WithEmptyProjectId_ShouldThrowArgumentException()
    {
        // Arrange & Act
        var action = () => EmbeddedStorageFirestoreExtensions.StartWithFirestore("");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Project ID cannot be null or empty*");
    }
}
