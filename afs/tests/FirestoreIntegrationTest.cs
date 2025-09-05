using System;
using FluentAssertions;
using NebulaStore.Storage.EmbeddedConfiguration;
using Xunit;

namespace NebulaStore.Afs.Tests;

/// <summary>
/// Integration test to verify that Firestore storage type is properly registered in the AFS system.
/// This test doesn't require actual Firestore connectivity - it just tests the registration.
/// </summary>
public class FirestoreIntegrationTest
{
    [Fact]
    public void AfsStorageConnection_WithFirestoreType_ShouldThrowNotSupportedExceptionWithoutFirestorePackage()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory("test-storage")
            .SetUseAfs(true)
            .SetAfsStorageType("firestore")
            .SetAfsConnectionString("test-project-id")
            .Build();

        // Act & Assert
        // Since we don't have the actual Firestore package loaded in this test project,
        // it should throw a NotSupportedException when trying to create the connector
        var action = () => new NebulaStore.Afs.Blobstore.AfsStorageConnection(
            config, 
            new NebulaStore.Storage.TypeHandlerRegistry());

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*Google Cloud Firestore connector could not be created*");
    }

    [Fact]
    public void AfsStorageConnection_WithAzureStorageType_ShouldThrowNotSupportedExceptionWithoutAzurePackage()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory("test-storage")
            .SetUseAfs(true)
            .SetAfsStorageType("azure.storage")
            .SetAfsConnectionString("DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net")
            .Build();

        // Act & Assert
        // Since we don't have the actual Azure Storage package loaded in this test project,
        // it should throw a NotSupportedException when trying to create the connector
        var action = () => new NebulaStore.Afs.Blobstore.AfsStorageConnection(
            config, 
            new NebulaStore.Storage.TypeHandlerRegistry());

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*Azure Storage connector could not be created*");
    }

    [Fact]
    public void AfsStorageConnection_WithS3Type_ShouldThrowNotSupportedExceptionWithoutS3Package()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory("test-storage")
            .SetUseAfs(true)
            .SetAfsStorageType("s3")
            .SetAfsConnectionString("test-bucket")
            .Build();

        // Act & Assert
        // Since we don't have the actual S3 package loaded in this test project,
        // it should throw a NotSupportedException when trying to create the connector
        var action = () => new NebulaStore.Afs.Blobstore.AfsStorageConnection(
            config, 
            new NebulaStore.Storage.TypeHandlerRegistry());

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*AWS S3 connector could not be created*");
    }

    [Fact]
    public void AfsStorageConnection_WithBlobstoreType_ShouldCreateSuccessfully()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory("test-storage")
            .SetUseAfs(true)
            .SetAfsStorageType("blobstore")
            .Build();

        // Act & Assert
        // This should work since blobstore is the default local implementation
        using var connection = new NebulaStore.Afs.Blobstore.AfsStorageConnection(
            config, 
            new NebulaStore.Storage.TypeHandlerRegistry());

        connection.Should().NotBeNull();
        connection.IsActive.Should().BeTrue();
    }

    [Fact]
    public void AfsStorageConnection_WithUnsupportedType_ShouldThrowNotSupportedException()
    {
        // Arrange
        var config = EmbeddedStorageConfiguration.New()
            .SetStorageDirectory("test-storage")
            .SetUseAfs(true)
            .SetAfsStorageType("unsupported-type")
            .Build();

        // Act & Assert
        var action = () => new NebulaStore.Afs.Blobstore.AfsStorageConnection(
            config, 
            new NebulaStore.Storage.TypeHandlerRegistry());

        action.Should().Throw<NotSupportedException>()
            .WithMessage("AFS storage type 'unsupported-type' is not supported");
    }
}
