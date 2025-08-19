using System;
using System.Linq;
using NebulaStore.Afs.Types;

namespace NebulaStore.Afs.Blobstore;

/// <summary>
/// Implementation of IAfsPath for blob store paths.
/// Handles path parsing, validation, and manipulation for blob storage systems.
/// </summary>
public class BlobStorePath : IAfsPath
{
    /// <summary>
    /// Path separator character.
    /// </summary>
    public const char SeparatorChar = '/';

    /// <summary>
    /// Path separator string.
    /// </summary>
    public const string Separator = "/";

    private readonly string[] _pathElements;
    private string? _fullQualifiedName;

    /// <summary>
    /// Initializes a new instance of the BlobStorePath class.
    /// </summary>
    /// <param name="pathElements">The path elements</param>
    /// <exception cref="ArgumentException">Thrown if path elements are invalid</exception>
    public BlobStorePath(params string[] pathElements)
    {
        if (pathElements == null || pathElements.Length == 0)
            throw new ArgumentException("Path cannot be empty", nameof(pathElements));

        foreach (var element in pathElements)
        {
            if (string.IsNullOrEmpty(element))
                throw new ArgumentException("Path elements cannot be null or empty", nameof(pathElements));
        }

        _pathElements = pathElements;
    }

    /// <summary>
    /// Gets the path elements that make up this path.
    /// </summary>
    public string[] PathElements => _pathElements;

    /// <summary>
    /// Gets the container name (first path element).
    /// </summary>
    public string Container => _pathElements[0];

    /// <summary>
    /// Gets the identifier (last path element).
    /// </summary>
    public string Identifier => _pathElements[_pathElements.Length - 1];

    /// <summary>
    /// Gets the full qualified name of this path.
    /// </summary>
    public string FullQualifiedName
    {
        get
        {
            if (_fullQualifiedName == null)
            {
                _fullQualifiedName = _pathElements.Length == 1
                    ? _pathElements[0]
                    : string.Join(Separator, _pathElements);
            }
            return _fullQualifiedName;
        }
    }

    /// <summary>
    /// Gets the parent path, or null if this is a root path.
    /// </summary>
    public IAfsPath? ParentPath
    {
        get
        {
            if (_pathElements.Length <= 1)
                return null;

            var parentElements = new string[_pathElements.Length - 1];
            Array.Copy(_pathElements, parentElements, parentElements.Length);
            return new BlobStorePath(parentElements);
        }
    }

    /// <summary>
    /// Validates this path according to the file system rules.
    /// </summary>
    /// <param name="validator">The validator to use</param>
    public void Validate(IAfsPathValidator validator)
    {
        validator.Validate(this);
    }

    /// <summary>
    /// Splits a full qualified path into path elements.
    /// </summary>
    /// <param name="fullQualifiedPath">The full path to split</param>
    /// <returns>Array of path elements</returns>
    public static string[] SplitPath(string fullQualifiedPath)
    {
        if (string.IsNullOrEmpty(fullQualifiedPath))
            throw new ArgumentException("Path cannot be null or empty", nameof(fullQualifiedPath));

        return fullQualifiedPath.Split(SeparatorChar, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Creates a new BlobStorePath from path elements.
    /// </summary>
    /// <param name="pathElements">The path elements</param>
    /// <returns>A new BlobStorePath instance</returns>
    public static BlobStorePath New(params string[] pathElements)
    {
        return new BlobStorePath(pathElements);
    }

    /// <summary>
    /// Creates a new BlobStorePath from a full qualified path string.
    /// </summary>
    /// <param name="fullQualifiedPath">The full path string</param>
    /// <returns>A new BlobStorePath instance</returns>
    public static BlobStorePath FromString(string fullQualifiedPath)
    {
        var elements = SplitPath(fullQualifiedPath);
        return new BlobStorePath(elements);
    }

    /// <summary>
    /// Returns the string representation of this path.
    /// </summary>
    /// <returns>The full qualified name</returns>
    public override string ToString()
    {
        return FullQualifiedName;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current object.
    /// </summary>
    /// <param name="obj">The object to compare</param>
    /// <returns>True if the objects are equal</returns>
    public override bool Equals(object? obj)
    {
        if (obj is BlobStorePath other)
        {
            return FullQualifiedName.Equals(other.FullQualifiedName, StringComparison.Ordinal);
        }
        return false;
    }

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>The hash code</returns>
    public override int GetHashCode()
    {
        return FullQualifiedName.GetHashCode();
    }
}
