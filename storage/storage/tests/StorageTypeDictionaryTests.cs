using System;
using System.Linq;
using Xunit;
using NebulaStore.Storage.Embedded.Types;

namespace NebulaStore.Storage.Tests;

/// <summary>
/// Unit tests for StorageTypeDictionary.
/// </summary>
public class StorageTypeDictionaryTests
{
    [Fact]
    public void NewTypeDictionary_HasBuiltInTypes()
    {
        // Arrange & Act
        var typeDictionary = StorageTypeDictionary.New();

        // Assert
        Assert.True(typeDictionary.TypeCount > 0);
        
        // Check some built-in types
        var stringHandler = typeDictionary.GetTypeHandler(typeof(string));
        Assert.NotNull(stringHandler);
        Assert.Equal(2L, stringHandler.TypeId); // string should have type ID 2
        
        var intHandler = typeDictionary.GetTypeHandler(typeof(int));
        Assert.NotNull(intHandler);
        Assert.Equal(3L, intHandler.TypeId); // int should have type ID 3
    }

    [Fact]
    public void CanRegisterCustomType()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();
        var customType = typeof(CustomTestClass);

        // Act
        var handler = typeDictionary.RegisterType(customType);

        // Assert
        Assert.NotNull(handler);
        Assert.Equal(customType, handler.HandledType);
        Assert.True(handler.TypeId >= 1000); // Custom types start at 1000
        
        // Should be retrievable
        var retrievedHandler = typeDictionary.GetTypeHandler(customType);
        Assert.Same(handler, retrievedHandler);
    }

    [Fact]
    public void RegisteringSameTypeTwice_ReturnsSameHandler()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();
        var customType = typeof(CustomTestClass);

        // Act
        var handler1 = typeDictionary.RegisterType(customType);
        var handler2 = typeDictionary.RegisterType(customType);

        // Assert
        Assert.Same(handler1, handler2);
        Assert.Equal(handler1.TypeId, handler2.TypeId);
    }

    [Fact]
    public void CanGetTypeHandlerByTypeId()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();
        var customType = typeof(CustomTestClass);
        var handler = typeDictionary.RegisterType(customType);

        // Act
        var retrievedHandler = typeDictionary.GetTypeHandler(handler.TypeId);

        // Assert
        Assert.Same(handler, retrievedHandler);
    }

    [Fact]
    public void CanGetTypeHandlerByTypeName()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();
        var customType = typeof(CustomTestClass);
        var handler = typeDictionary.RegisterType(customType);

        // Act
        var retrievedHandler = typeDictionary.GetTypeHandler(handler.HandledType.FullName ?? handler.HandledType.Name);

        // Assert
        Assert.Same(handler, retrievedHandler);
    }

    [Fact]
    public void IsTypeRegistered_WorksCorrectly()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();
        var registeredType = typeof(string); // Built-in type
        var unregisteredType = typeof(CustomTestClass);

        // Act & Assert
        Assert.True(typeDictionary.IsTypeRegistered(registeredType));
        Assert.False(typeDictionary.IsTypeRegistered(unregisteredType));
        
        // Register the custom type
        typeDictionary.RegisterType(unregisteredType);
        Assert.True(typeDictionary.IsTypeRegistered(unregisteredType));
    }

    [Fact]
    public void IsTypeIdRegistered_WorksCorrectly()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();

        // Act & Assert
        Assert.True(typeDictionary.IsTypeIdRegistered(2L)); // string type ID
        Assert.False(typeDictionary.IsTypeIdRegistered(9999L)); // Non-existent type ID
    }

    [Fact]
    public void GetAllTypes_ReturnsAllRegisteredTypes()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();
        var customType = typeof(CustomTestClass);
        typeDictionary.RegisterType(customType);

        // Act
        var allTypes = typeDictionary.GetAllTypes().ToList();

        // Assert
        Assert.Contains(typeof(string), allTypes);
        Assert.Contains(typeof(int), allTypes);
        Assert.Contains(customType, allTypes);
    }

    [Fact]
    public void GetAllTypeIds_ReturnsAllRegisteredTypeIds()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();
        var customType = typeof(CustomTestClass);
        var handler = typeDictionary.RegisterType(customType);

        // Act
        var allTypeIds = typeDictionary.GetAllTypeIds().ToList();

        // Assert
        Assert.Contains(2L, allTypeIds); // string type ID
        Assert.Contains(3L, allTypeIds); // int type ID
        Assert.Contains(handler.TypeId, allTypeIds);
    }

    [Fact]
    public void GetAllTypeHandlers_ReturnsAllHandlers()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();
        var initialCount = typeDictionary.GetAllTypeHandlers().Count();
        
        var customType = typeof(CustomTestClass);
        var handler = typeDictionary.RegisterType(customType);

        // Act
        var allHandlers = typeDictionary.GetAllTypeHandlers().ToList();

        // Assert
        Assert.Equal(initialCount + 1, allHandlers.Count);
        Assert.Contains(handler, allHandlers);
    }

    [Fact]
    public void Clear_RemovesAllTypesAndReregistersBuiltIns()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();
        var initialCount = typeDictionary.TypeCount;
        
        var customType = typeof(CustomTestClass);
        typeDictionary.RegisterType(customType);
        Assert.True(typeDictionary.TypeCount > initialCount);

        // Act
        typeDictionary.Clear();

        // Assert
        Assert.Equal(initialCount, typeDictionary.TypeCount);
        Assert.False(typeDictionary.IsTypeRegistered(customType));
        Assert.True(typeDictionary.IsTypeRegistered(typeof(string))); // Built-in should be back
    }

    [Fact]
    public void RegisterTypeHandler_WithConflictingTypeId_ThrowsException()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();
        var customType1 = typeof(CustomTestClass);
        var customType2 = typeof(AnotherCustomTestClass);
        
        var handler1 = typeDictionary.RegisterType(customType1);
        var conflictingHandler = new TypeHandler(customType2, handler1.TypeId); // Same type ID

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            typeDictionary.RegisterTypeHandler(customType2, conflictingHandler));
        
        Assert.Contains("already registered", exception.Message);
    }

    [Fact]
    public void RegisterTypeHandler_WithConflictingType_ThrowsException()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();
        var customType = typeof(CustomTestClass);
        
        var handler1 = typeDictionary.RegisterType(customType);
        var conflictingHandler = new TypeHandler(customType, 9999L); // Different type ID

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            typeDictionary.RegisterTypeHandler(customType, conflictingHandler));
        
        Assert.Contains("different type ID", exception.Message);
    }

    [Fact]
    public void GetTypeHandler_WithNullType_ReturnsNull()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();

        // Act
        var handler = typeDictionary.GetTypeHandler((Type)null!);

        // Assert
        Assert.Null(handler);
    }

    [Fact]
    public void GetTypeHandler_WithNullOrEmptyTypeName_ReturnsNull()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();

        // Act & Assert
        Assert.Null(typeDictionary.GetTypeHandler((string)null!));
        Assert.Null(typeDictionary.GetTypeHandler(string.Empty));
    }

    [Fact]
    public void AutoRegistration_WorksWhenGettingUnknownType()
    {
        // Arrange
        var typeDictionary = StorageTypeDictionary.New();
        var customType = typeof(CustomTestClass);
        
        Assert.False(typeDictionary.IsTypeRegistered(customType));

        // Act - Getting handler for unregistered type should auto-register it
        var handler = typeDictionary.GetTypeHandler(customType);

        // Assert
        Assert.NotNull(handler);
        Assert.Equal(customType, handler.HandledType);
        Assert.True(typeDictionary.IsTypeRegistered(customType));
    }

    [Fact]
    public void TypeHandler_EqualsAndHashCode_WorkCorrectly()
    {
        // Arrange
        var type = typeof(CustomTestClass);
        var handler1 = new TypeHandler(type, 1000L);
        var handler2 = new TypeHandler(type, 1000L);
        var handler3 = new TypeHandler(type, 1001L);

        // Act & Assert
        Assert.Equal(handler1, handler2);
        Assert.NotEqual(handler1, handler3);
        Assert.Equal(handler1.GetHashCode(), handler2.GetHashCode());
    }
}

/// <summary>
/// Custom test class for type dictionary tests.
/// </summary>
public class CustomTestClass
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

/// <summary>
/// Another custom test class for type dictionary tests.
/// </summary>
public class AnotherCustomTestClass
{
    public string Description { get; set; } = string.Empty;
}
