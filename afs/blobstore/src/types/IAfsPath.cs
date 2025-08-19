using System;

namespace NebulaStore.Afs.Blobstore.Types;

/// <summary>
/// Interface for abstract file system paths.
/// Provides path manipulation and validation functionality.
/// </summary>
public interface IAfsPath
{
    /// <summary>
    /// Gets the path elements that make up this path.
    /// </summary>
    string[] PathElements { get; }

    /// <summary>
    /// Gets the container name (first path element).
    /// </summary>
    string Container { get; }

    /// <summary>
    /// Gets the identifier (last path element).
    /// </summary>
    string Identifier { get; }

    /// <summary>
    /// Gets the full qualified name of this path.
    /// </summary>
    string FullQualifiedName { get; }

    /// <summary>
    /// Gets the parent path, or null if this is a root path.
    /// </summary>
    IAfsPath? ParentPath { get; }

    /// <summary>
    /// Validates this path according to the file system rules.
    /// </summary>
    /// <param name="validator">The validator to use</param>
    void Validate(IAfsPathValidator validator);
}

/// <summary>
/// Interface for path validators.
/// </summary>
public interface IAfsPathValidator
{
    /// <summary>
    /// Validates the given path.
    /// </summary>
    /// <param name="path">The path to validate</param>
    /// <exception cref="ArgumentException">Thrown if the path is invalid</exception>
    void Validate(IAfsPath path);
}

/// <summary>
/// No-operation path validator that accepts all paths.
/// </summary>
public class NoOpPathValidator : IAfsPathValidator
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly NoOpPathValidator Instance = new();

    private NoOpPathValidator() { }

    /// <summary>
    /// Validates the path (no-op implementation).
    /// </summary>
    /// <param name="path">The path to validate</param>
    public void Validate(IAfsPath path)
    {
        // No validation performed
    }
}
