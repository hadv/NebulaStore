using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.Storage;

/// <summary>
/// Default implementation of type handler registry.
/// </summary>
public class TypeHandlerRegistry : ITypeHandlerRegistry
{
    private readonly Dictionary<Type, ITypeHandler> _typeHandlers = new();
    private readonly Dictionary<long, ITypeHandler> _typeIdHandlers = new();

    public void RegisterTypeHandler(ITypeHandler typeHandler)
    {
        if (typeHandler == null)
            throw new ArgumentNullException(nameof(typeHandler));

        _typeHandlers[typeHandler.HandledType] = typeHandler;
        _typeIdHandlers[typeHandler.TypeId] = typeHandler;
    }

    public ITypeHandler? GetTypeHandler(Type type)
    {
        _typeHandlers.TryGetValue(type, out var handler);
        return handler;
    }

    public ITypeHandler? GetTypeHandler(long typeId)
    {
        _typeIdHandlers.TryGetValue(typeId, out var handler);
        return handler;
    }

    public IEnumerable<ITypeHandler> GetAllTypeHandlers()
    {
        return _typeHandlers.Values.ToList();
    }
}
