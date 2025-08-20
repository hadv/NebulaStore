using System;
using System.Collections.Generic;

namespace NebulaStore.GigaMap;

/// <summary>
/// Represents the constraints system for a GigaMap.
/// Provides access to unique constraints and custom validation rules.
/// </summary>
/// <typeparam name="T">The type of entities being constrained</typeparam>
public interface IGigaConstraints<T> where T : class
{
    /// <summary>
    /// Gets the unique constraints for this GigaMap.
    /// </summary>
    IUniqueConstraints<T> Unique { get; }

    /// <summary>
    /// Gets the custom constraints for this GigaMap.
    /// </summary>
    ICustomConstraints<T> Custom { get; }

    /// <summary>
    /// Checks all constraints for the given entity operation.
    /// </summary>
    /// <param name="entityId">The entity ID (-1 for new entities)</param>
    /// <param name="replacedEntity">The entity being replaced (null for new entities)</param>
    /// <param name="entity">The new entity</param>
    /// <exception cref="ConstraintViolationException">If any constraint is violated</exception>
    void Check(long entityId, T? replacedEntity, T entity);

    /// <summary>
    /// Gets all registered constraints.
    /// </summary>
    IReadOnlyCollection<IConstraint<T>> All { get; }

    /// <summary>
    /// Adds a constraint to the system.
    /// </summary>
    /// <param name="constraint">The constraint to add</param>
    void Add(IConstraint<T> constraint);

    /// <summary>
    /// Removes a constraint from the system.
    /// </summary>
    /// <param name="constraint">The constraint to remove</param>
    /// <returns>True if the constraint was removed, false if it wasn't found</returns>
    bool Remove(IConstraint<T> constraint);

    /// <summary>
    /// Clears all constraints.
    /// </summary>
    void Clear();
}

/// <summary>
/// Represents unique constraints for ensuring entity uniqueness.
/// </summary>
/// <typeparam name="T">The type of entities being constrained</typeparam>
public interface IUniqueConstraints<T> where T : class
{
    /// <summary>
    /// Adds unique constraints for the specified indexers.
    /// </summary>
    /// <param name="indexers">The indexers to add unique constraints for</param>
    void AddConstraints(IEnumerable<IIndexer<T, object>> indexers);

    /// <summary>
    /// Adds a unique constraint for the specified indexer.
    /// </summary>
    /// <param name="indexer">The indexer to add a unique constraint for</param>
    void AddConstraint<TKey>(IIndexer<T, TKey> indexer);

    /// <summary>
    /// Removes a unique constraint for the specified indexer.
    /// </summary>
    /// <param name="indexer">The indexer to remove the unique constraint for</param>
    /// <returns>True if the constraint was removed, false if it wasn't found</returns>
    bool RemoveConstraint<TKey>(IIndexer<T, TKey> indexer);

    /// <summary>
    /// Checks if a unique constraint exists for the specified indexer.
    /// </summary>
    /// <param name="indexer">The indexer to check</param>
    /// <returns>True if a unique constraint exists, false otherwise</returns>
    bool HasConstraint<TKey>(IIndexer<T, TKey> indexer);

    /// <summary>
    /// Gets all unique constraint indexers.
    /// </summary>
    IReadOnlyCollection<IIndexer<T, object>> Indexers { get; }

    /// <summary>
    /// Clears all unique constraints.
    /// </summary>
    void Clear();
}

/// <summary>
/// Represents custom constraints for advanced validation rules.
/// </summary>
/// <typeparam name="T">The type of entities being constrained</typeparam>
public interface ICustomConstraints<T> where T : class
{
    /// <summary>
    /// Adds custom constraints.
    /// </summary>
    /// <param name="constraints">The constraints to add</param>
    void AddConstraints(IEnumerable<ICustomConstraint<T>> constraints);

    /// <summary>
    /// Adds a custom constraint.
    /// </summary>
    /// <param name="constraint">The constraint to add</param>
    void AddConstraint(ICustomConstraint<T> constraint);

    /// <summary>
    /// Removes a custom constraint.
    /// </summary>
    /// <param name="constraint">The constraint to remove</param>
    /// <returns>True if the constraint was removed, false if it wasn't found</returns>
    bool RemoveConstraint(ICustomConstraint<T> constraint);

    /// <summary>
    /// Gets all custom constraints.
    /// </summary>
    IReadOnlyCollection<ICustomConstraint<T>> All { get; }

    /// <summary>
    /// Clears all custom constraints.
    /// </summary>
    void Clear();
}

/// <summary>
/// Represents a constraint that can be applied to entities.
/// </summary>
/// <typeparam name="T">The type of entities being constrained</typeparam>
public interface IConstraint<T> where T : class
{
    /// <summary>
    /// Gets the name of the constraint.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Checks if the constraint is satisfied for the given entity operation.
    /// </summary>
    /// <param name="entityId">The entity ID (-1 for new entities)</param>
    /// <param name="replacedEntity">The entity being replaced (null for new entities)</param>
    /// <param name="entity">The new entity</param>
    /// <exception cref="ConstraintViolationException">If the constraint is violated</exception>
    void Check(long entityId, T? replacedEntity, T entity);
}

/// <summary>
/// Represents a custom constraint with user-defined validation logic.
/// </summary>
/// <typeparam name="T">The type of entities being constrained</typeparam>
public interface ICustomConstraint<T> : IConstraint<T> where T : class
{
    /// <summary>
    /// Gets the validation function for this constraint.
    /// </summary>
    Func<long, T?, T, bool> ValidationFunction { get; }

    /// <summary>
    /// Gets the error message to display when the constraint is violated.
    /// </summary>
    string ErrorMessage { get; }
}

/// <summary>
/// Exception thrown when a constraint is violated.
/// </summary>
public class ConstraintViolationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the ConstraintViolationException class.
    /// </summary>
    /// <param name="constraintName">The name of the violated constraint</param>
    /// <param name="message">The error message</param>
    public ConstraintViolationException(string constraintName, string message)
        : base(message)
    {
        ConstraintName = constraintName;
    }

    /// <summary>
    /// Initializes a new instance of the ConstraintViolationException class.
    /// </summary>
    /// <param name="constraintName">The name of the violated constraint</param>
    /// <param name="message">The error message</param>
    /// <param name="innerException">The inner exception</param>
    public ConstraintViolationException(string constraintName, string message, Exception innerException)
        : base(message, innerException)
    {
        ConstraintName = constraintName;
    }

    /// <summary>
    /// Gets the name of the violated constraint.
    /// </summary>
    public string ConstraintName { get; }
}

/// <summary>
/// Provides factory methods for creating constraints.
/// </summary>
public static class Constraint
{
    /// <summary>
    /// Creates a custom constraint with the specified validation logic.
    /// </summary>
    /// <typeparam name="T">The type of entities being constrained</typeparam>
    /// <param name="name">The name of the constraint</param>
    /// <param name="validationFunction">The validation function</param>
    /// <param name="errorMessage">The error message to display when violated</param>
    /// <returns>A new custom constraint</returns>
    public static ICustomConstraint<T> Custom<T>(
        string name,
        Func<long, T?, T, bool> validationFunction,
        string errorMessage) where T : class
    {
        return new CustomConstraint<T>(name, validationFunction, errorMessage);
    }

    /// <summary>
    /// Creates a custom constraint that validates a single entity.
    /// </summary>
    /// <typeparam name="T">The type of entities being constrained</typeparam>
    /// <param name="name">The name of the constraint</param>
    /// <param name="validationFunction">The validation function</param>
    /// <param name="errorMessage">The error message to display when violated</param>
    /// <returns>A new custom constraint</returns>
    public static ICustomConstraint<T> Custom<T>(
        string name,
        Func<T, bool> validationFunction,
        string errorMessage) where T : class
    {
        return new CustomConstraint<T>(name, (_, _, entity) => validationFunction(entity), errorMessage);
    }
}

/// <summary>
/// Internal implementation of a custom constraint.
/// </summary>
internal class CustomConstraint<T> : ICustomConstraint<T> where T : class
{
    public CustomConstraint(string name, Func<long, T?, T, bool> validationFunction, string errorMessage)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        ValidationFunction = validationFunction ?? throw new ArgumentNullException(nameof(validationFunction));
        ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
    }

    public string Name { get; }
    public Func<long, T?, T, bool> ValidationFunction { get; }
    public string ErrorMessage { get; }

    public void Check(long entityId, T? replacedEntity, T entity)
    {
        if (!ValidationFunction(entityId, replacedEntity, entity))
        {
            throw new ConstraintViolationException(Name, ErrorMessage);
        }
    }
}