using MessagePack;
using NebulaStore.Storage;

namespace NebulaStore.Examples;

// Shared domain classes used by both EmbeddedStorageExample and MonitoringExample
[MessagePackObject(AllowPrivate = true)]
public class Library
{
    [Key(0)]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    public List<Book> Books { get; set; } = new();
}

[MessagePackObject(AllowPrivate = true)]
public class Book
{
    [Key(0)]
    public string Title { get; set; } = string.Empty;

    [Key(1)]
    public string Author { get; set; } = string.Empty;

    [Key(2)]
    public int Year { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
public class DataContainer
{
    [Key(0)]
    public List<DataItem> Items { get; set; } = new();
}

[MessagePackObject(AllowPrivate = true)]
public class DataItem
{
    [Key(0)]
    public int Id { get; set; }

    [Key(1)]
    public string Name { get; set; } = string.Empty;

    [Key(2)]
    public double Value { get; set; }

    [Key(3)]
    public DateTime Timestamp { get; set; }

    [Key(4)]
    public List<string> Tags { get; set; } = new();
}

[MessagePackObject(AllowPrivate = true)]
public class Inventory
{
    [Key(0)]
    public List<Item> Items { get; set; } = new();
}

[MessagePackObject(AllowPrivate = true)]
public class Item
{
    [Key(0)]
    public string Name { get; set; } = string.Empty;

    [Key(1)]
    public decimal Price { get; set; }

    [Key(2)]
    public int Quantity { get; set; }
}

[MessagePackObject(AllowPrivate = true)]
public class CustomData
{
    [Key(0)]
    public string SpecialString { get; set; } = string.Empty;

    [Key(1)]
    public int NormalValue { get; set; }
}

// Example custom type handler
public class CustomStringTypeHandler : ITypeHandler
{
    public Type HandledType => typeof(string);
    public long TypeId => 999;

    public byte[] Serialize(object instance)
    {
        if (instance is not string str)
            throw new ArgumentException("Instance must be a string");

        // Custom serialization: prepend "CUSTOM:" to the string
        var customStr = "CUSTOM:" + str;
        return System.Text.Encoding.UTF8.GetBytes(customStr);
    }

    public object Deserialize(byte[] data)
    {
        var str = System.Text.Encoding.UTF8.GetString(data);
        // Remove the "CUSTOM:" prefix
        return str.StartsWith("CUSTOM:") ? str.Substring(7) : str;
    }

    public long GetSerializedLength(object instance)
    {
        if (instance is not string str)
            throw new ArgumentException("Instance must be a string");

        return System.Text.Encoding.UTF8.GetByteCount("CUSTOM:" + str);
    }

    public bool CanHandle(Type type) => type == typeof(string);
}
