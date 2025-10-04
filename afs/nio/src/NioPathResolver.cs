using System;
using System.IO;
using System.Linq;

namespace NebulaStore.Afs.Nio;

/// <summary>
/// Resolves path elements to file system paths.
/// </summary>
public interface INioPathResolver
{
    /// <summary>
    /// Resolves path elements to a file system path.
    /// </summary>
    /// <param name="pathElements">The path elements to resolve</param>
    /// <returns>The resolved file system path</returns>
    string ResolvePath(params string[] pathElements);
}

/// <summary>
/// Default implementation of path resolver using the local file system.
/// </summary>
public class NioPathResolver : INioPathResolver
{
    /// <summary>
    /// Creates a new path resolver using the default file system.
    /// </summary>
    /// <returns>A new path resolver instance</returns>
    public static NioPathResolver New()
    {
        return new NioPathResolver();
    }

    /// <summary>
    /// Resolves path elements to a file system path.
    /// </summary>
    /// <param name="pathElements">The path elements to resolve</param>
    /// <returns>The resolved file system path</returns>
    public string ResolvePath(params string[] pathElements)
    {
        if (pathElements == null || pathElements.Length == 0)
        {
            return string.Empty;
        }

        // Filter out null or empty elements
        var validElements = pathElements.Where(e => !string.IsNullOrEmpty(e)).ToArray();
        
        if (validElements.Length == 0)
        {
            return string.Empty;
        }

        // Combine path elements using the platform-specific separator
        return System.IO.Path.Combine(validElements);
    }
}

