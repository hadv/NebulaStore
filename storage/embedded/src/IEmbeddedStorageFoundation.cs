using System;
using System.Collections.Generic;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Foundation interface for building embedded storage managers.
/// Provides a builder pattern for configuring all aspects of storage.
/// </summary>
public interface IEmbeddedStorageFoundation
{
    /// <summary>
    /// Sets the storage configuration.
    /// </summary>
    /// <param name="configuration">The storage configuration</param>
    /// <returns>This foundation instance for method chaining</returns>
    IEmbeddedStorageFoundation SetConfiguration(IEmbeddedStorageConfiguration configuration);

    /// <summary>
    /// Sets the root object instance.
    /// </summary>
    /// <param name="root">The root object</param>
    /// <returns>This foundation instance for method chaining</returns>
    IEmbeddedStorageFoundation SetRoot(object root);

    /// <summary>
    /// Sets the root object supplier function.
    /// </summary>
    /// <param name="rootSupplier">Function that supplies the root object</param>
    /// <returns>This foundation instance for method chaining</returns>
    IEmbeddedStorageFoundation SetRootSupplier(Func<object> rootSupplier);

    /// <summary>
    /// Registers a custom type handler.
    /// </summary>
    /// <param name="typeHandler">The type handler to register</param>
    /// <returns>This foundation instance for method chaining</returns>
    IEmbeddedStorageFoundation RegisterTypeHandler(ITypeHandler typeHandler);

    /// <summary>
    /// Registers multiple custom type handlers.
    /// </summary>
    /// <param name="typeHandlers">The type handlers to register</param>
    /// <returns>This foundation instance for method chaining</returns>
    IEmbeddedStorageFoundation RegisterTypeHandlers(IEnumerable<ITypeHandler> typeHandlers);

    /// <summary>
    /// Sets the type evaluator for determining persistable types.
    /// </summary>
    /// <param name="typeEvaluator">The type evaluator function</param>
    /// <returns>This foundation instance for method chaining</returns>
    IEmbeddedStorageFoundation SetTypeEvaluator(Func<Type, bool> typeEvaluator);

    /// <summary>
    /// Gets the current storage configuration.
    /// </summary>
    /// <returns>The storage configuration</returns>
    IEmbeddedStorageConfiguration GetConfiguration();

    /// <summary>
    /// Creates an embedded storage manager without starting it.
    /// </summary>
    /// <returns>A new embedded storage manager instance</returns>
    IEmbeddedStorageManager CreateEmbeddedStorageManager();

    /// <summary>
    /// Creates an embedded storage manager with an explicit root object without starting it.
    /// </summary>
    /// <param name="explicitRoot">The explicit root object</param>
    /// <returns>A new embedded storage manager instance</returns>
    IEmbeddedStorageManager CreateEmbeddedStorageManager(object? explicitRoot);

    /// <summary>
    /// Creates and starts an embedded storage manager.
    /// </summary>
    /// <returns>A started embedded storage manager instance</returns>
    IEmbeddedStorageManager Start();

    /// <summary>
    /// Creates and starts an embedded storage manager with an explicit root object.
    /// </summary>
    /// <param name="explicitRoot">The explicit root object</param>
    /// <returns>A started embedded storage manager instance</returns>
    IEmbeddedStorageManager Start(object? explicitRoot);
}
