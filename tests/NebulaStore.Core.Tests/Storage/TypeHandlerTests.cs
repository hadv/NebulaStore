using System;
using System.Linq;
using Xunit;
using NebulaStore.Core.Storage;
using NebulaStore.Core.Storage.TypeHandlers;

namespace NebulaStore.Core.Tests.Storage;

public class TypeHandlerTests
{
    [Fact]
    public void StringTypeHandler_SerializeDeserialize_ShouldRoundTrip()
    {
        var handler = BuiltInTypeHandlers.String;
        var original = "Hello, World!";
        
        var serialized = handler.Serialize(original);
        var deserialized = (string)handler.Deserialize(serialized);
        
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void Int32TypeHandler_SerializeDeserialize_ShouldRoundTrip()
    {
        var handler = BuiltInTypeHandlers.Int32;
        var original = 42;
        
        var serialized = handler.Serialize(original);
        var deserialized = (int)handler.Deserialize(serialized);
        
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void Int64TypeHandler_SerializeDeserialize_ShouldRoundTrip()
    {
        var handler = BuiltInTypeHandlers.Int64;
        var original = 9223372036854775807L;
        
        var serialized = handler.Serialize(original);
        var deserialized = (long)handler.Deserialize(serialized);
        
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void DecimalTypeHandler_SerializeDeserialize_ShouldRoundTrip()
    {
        var handler = BuiltInTypeHandlers.Decimal;
        var original = 123.456789m;
        
        var serialized = handler.Serialize(original);
        var deserialized = (decimal)handler.Deserialize(serialized);
        
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void DateTimeTypeHandler_SerializeDeserialize_ShouldRoundTrip()
    {
        var handler = BuiltInTypeHandlers.DateTime;
        var original = new DateTime(2023, 12, 25, 10, 30, 45, DateTimeKind.Utc);
        
        var serialized = handler.Serialize(original);
        var deserialized = (DateTime)handler.Deserialize(serialized);
        
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void MessagePackTypeHandler_SerializeDeserialize_ShouldRoundTrip()
    {
        var handler = MessagePackTypeHandlerFactory.Create<TestClass>();
        var original = new TestClass { Name = "Test", Value = 42 };
        
        var serialized = handler.Serialize(original);
        var deserialized = (TestClass)handler.Deserialize(serialized);
        
        Assert.Equal(original.Name, deserialized.Name);
        Assert.Equal(original.Value, deserialized.Value);
    }

    [Fact]
    public void TypeHandlerRegistry_RegisterAndRetrieve_ShouldWork()
    {
        var registry = new TypeHandlerRegistry();
        var handler = BuiltInTypeHandlers.String;
        
        registry.RegisterTypeHandler(handler);
        
        var retrievedByType = registry.GetTypeHandler(typeof(string));
        var retrievedById = registry.GetTypeHandler(handler.TypeId);
        
        Assert.Same(handler, retrievedByType);
        Assert.Same(handler, retrievedById);
    }

    [Fact]
    public void TypeHandlerRegistry_GetAllTypeHandlers_ShouldReturnAllRegistered()
    {
        var registry = new TypeHandlerRegistry();
        var handlers = BuiltInTypeHandlers.GetAll();
        
        foreach (var handler in handlers)
        {
            registry.RegisterTypeHandler(handler);
        }
        
        var allHandlers = registry.GetAllTypeHandlers().ToList();
        
        Assert.Equal(handlers.Length, allHandlers.Count);
    }

    [Fact]
    public void BuiltInTypeHandlers_ShouldHaveUniqueTypeIds()
    {
        var handlers = BuiltInTypeHandlers.GetAll();
        var typeIds = handlers.Select(h => h.TypeId).ToList();
        
        Assert.Equal(typeIds.Count, typeIds.Distinct().Count());
    }

    [Fact]
    public void BuiltInTypeHandlers_ShouldHandleCorrectTypes()
    {
        Assert.True(BuiltInTypeHandlers.String.CanHandle(typeof(string)));
        Assert.True(BuiltInTypeHandlers.Int32.CanHandle(typeof(int)));
        Assert.True(BuiltInTypeHandlers.Int64.CanHandle(typeof(long)));
        Assert.True(BuiltInTypeHandlers.Decimal.CanHandle(typeof(decimal)));
        Assert.True(BuiltInTypeHandlers.DateTime.CanHandle(typeof(DateTime)));
        
        Assert.False(BuiltInTypeHandlers.String.CanHandle(typeof(int)));
        Assert.False(BuiltInTypeHandlers.Int32.CanHandle(typeof(string)));
    }

    [Fact]
    public void TypeHandler_GetSerializedLength_ShouldReturnCorrectLength()
    {
        var stringHandler = BuiltInTypeHandlers.String;
        var intHandler = BuiltInTypeHandlers.Int32;
        
        var testString = "Hello";
        var testInt = 42;
        
        var stringLength = stringHandler.GetSerializedLength(testString);
        var intLength = intHandler.GetSerializedLength(testInt);
        
        Assert.Equal(5, stringLength); // "Hello" in UTF-8
        Assert.Equal(4, intLength); // int is 4 bytes
    }

    [Fact]
    public void TypeHandler_WithInvalidType_ShouldThrowException()
    {
        var stringHandler = BuiltInTypeHandlers.String;
        
        Assert.Throws<ArgumentException>(() => stringHandler.Serialize(42));
        Assert.Throws<ArgumentException>(() => stringHandler.GetSerializedLength(42));
    }

    [Fact]
    public void MessagePackTypeHandlerFactory_Create_ShouldCreateUniqueTypeIds()
    {
        var handler1 = MessagePackTypeHandlerFactory.Create<TestClass>();
        var handler2 = MessagePackTypeHandlerFactory.Create<TestClass>();
        
        Assert.NotEqual(handler1.TypeId, handler2.TypeId);
    }

    [Fact]
    public void MessagePackTypeHandlerFactory_CreateWithTypeId_ShouldUseSpecifiedId()
    {
        var typeId = 12345L;
        var handler = MessagePackTypeHandlerFactory.Create<TestClass>(typeId);
        
        Assert.Equal(typeId, handler.TypeId);
    }

    [MessagePack.MessagePackObject(AllowPrivate = true)]
    internal class TestClass
    {
        [MessagePack.Key(0)]
        public string Name { get; set; } = string.Empty;
        
        [MessagePack.Key(1)]
        public int Value { get; set; }
    }
}
