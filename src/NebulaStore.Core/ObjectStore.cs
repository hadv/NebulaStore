using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MessagePack;

public class ObjectStore : IDisposable
{
    private readonly string _filePath;
    private object? _root;
    private Type? _rootType;

    public ObjectStore(string filePath)
    {
        _filePath = filePath;

        if (File.Exists(filePath))
        {
            var bytes = File.ReadAllBytes(filePath);
            if (bytes.Length > 0)
            {
                var wrapper = MessagePackSerializer.Deserialize<RootWrapper>(bytes);
                _rootType = Type.GetType(wrapper.TypeName);
                if (_rootType != null && wrapper.Data != null)
                {
                    // Re-serialize and deserialize with the correct type
                    var dataBytes = MessagePackSerializer.Serialize(wrapper.Data);
                    _root = MessagePackSerializer.Deserialize(_rootType, dataBytes);
                }
            }
        }
    }

    public T Root<T>() where T : new()
    {
        if (_root == null)
        {
            _root = new T();
            _rootType = typeof(T);
        }
        return (T)_root;
    }

    public void Commit()
    {
        if (_root != null && _rootType != null)
        {
            var wrapper = new RootWrapper
            {
                Data = _root,
                TypeName = _rootType.AssemblyQualifiedName!
            };
            var bytes = MessagePackSerializer.Serialize(wrapper);
            File.WriteAllBytes(_filePath, bytes);
        }
    }

    /// <summary>
    /// Lazy, incremental query giống DB cursor.
    /// Traverse graph on demand, yield từng object.
    /// </summary>
    public IEnumerable<T> Query<T>()
    {
        if (_root == null) yield break;

        foreach (var item in TraverseGraphLazy(_root, new HashSet<object>()))
        {
            if (item is T t) yield return t;
        }
    }

    private IEnumerable<object> TraverseGraphLazy(object obj, HashSet<object> visited)
    {
        if (obj == null || visited.Contains(obj)) yield break;

        visited.Add(obj);
        yield return obj;

        // Nếu là collection thì duyệt từng phần tử
        if (obj is System.Collections.IEnumerable enumerable && !(obj is string))
        {
            foreach (var element in enumerable)
            {
                if (element != null)
                {
                    foreach (var child in TraverseGraphLazy(element, visited))
                        yield return child;
                }
            }
        }

        // Nếu có property thì duyệt tiếp
        var type = obj.GetType();
        foreach (var prop in type.GetProperties())
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0) continue;
            
            object? value = null;
            try
            {
                value = prop.GetValue(obj);
            }
            catch
            {
                // Skip properties that can't be accessed
                continue;
            }
            
            if (value != null && !(value is string))
            {
                foreach (var child in TraverseGraphLazy(value, visited))
                    yield return child;
            }
        }
    }

    public void Dispose()
    {
        // cleanup nếu cần
    }
}

[MessagePackObject(AllowPrivate = true)]
internal class RootWrapper
{
    [Key(0)]
    public object? Data { get; set; }
    
    [Key(1)]
    public string TypeName { get; set; } = string.Empty;
}