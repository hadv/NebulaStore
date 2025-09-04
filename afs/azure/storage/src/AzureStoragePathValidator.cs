using System.Text.RegularExpressions;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Afs.Blobstore.Types;

namespace NebulaStore.Afs.Azure.Storage;

/// <summary>
/// Path validator for Azure Blob Storage container names and blob keys.
/// Implements Azure Blob Storage naming conventions and restrictions.
/// </summary>
public interface IAzureStoragePathValidator : IAfsPathValidator
{
    /// <summary>
    /// Creates a new Azure Storage path validator.
    /// </summary>
    /// <returns>A new path validator instance</returns>
    static IAzureStoragePathValidator New() => new AzureStoragePathValidator();
}

/// <summary>
/// Default implementation of Azure Storage path validator.
/// Enforces Azure Blob Storage container naming rules and blob key restrictions.
/// </summary>
/// <remarks>
/// Azure container naming rules:
/// - Container names must be between 3 and 63 characters long
/// - Container names can contain only lowercase letters, numbers, and dashes (-)
/// - Container names must begin with a lowercase letter or number
/// - Container names cannot end with a dash (-)
/// - Container names cannot have consecutive dashes (--)
/// </remarks>
public class AzureStoragePathValidator : IAzureStoragePathValidator
{
    private static readonly Regex ContainerNameRegex = new(@"^[a-z0-9\-]*$", RegexOptions.Compiled);
    private static readonly Regex ContainerStartRegex = new(@"^[a-z0-9]", RegexOptions.Compiled);

    /// <summary>
    /// Validates the specified path for Azure Blob Storage compliance.
    /// </summary>
    /// <param name="path">The path to validate</param>
    /// <exception cref="ArgumentException">Thrown when the path is invalid</exception>
    public void Validate(IAfsPath path)
    {
        if (path is BlobStorePath blobPath)
        {
            ValidateContainerName(blobPath.Container);
            ValidateBlobKey(blobPath);
        }
        else
        {
            throw new ArgumentException("Path must be a BlobStorePath for Azure Storage validation", nameof(path));
        }
    }

    /// <summary>
    /// Validates Azure Blob Storage container name according to Azure naming rules.
    /// </summary>
    /// <param name="containerName">The container name to validate</param>
    /// <exception cref="ArgumentException">Thrown when the container name is invalid</exception>
    /// <remarks>
    /// Azure container naming rules based on:
    /// https://docs.microsoft.com/en-us/rest/api/storageservices/naming-and-referencing-containers--blobs--and-metadata
    /// </remarks>
    private static void ValidateContainerName(string containerName)
    {
        var length = containerName.Length;
        if (length < 3 || length > 63)
        {
            throw new ArgumentException("Container name must be between 3 and 63 characters long");
        }

        if (!ContainerNameRegex.IsMatch(containerName))
        {
            throw new ArgumentException("Container name can contain only lowercase letters, numbers and dashes (-)");
        }

        if (!ContainerStartRegex.IsMatch(containerName.Substring(0, 1)))
        {
            throw new ArgumentException("Container name must begin with a lowercase letter or a number");
        }

        if (containerName.EndsWith("-"))
        {
            throw new ArgumentException("Container name must not end with a dash (-)");
        }

        if (containerName.Contains("--"))
        {
            throw new ArgumentException("Container name cannot have consecutive dashes (--)");
        }

        // Check for reserved container names
        if (IsReservedContainerName(containerName))
        {
            throw new ArgumentException($"Container name '{containerName}' is reserved and cannot be used");
        }
    }

    /// <summary>
    /// Validates Azure Blob Storage blob key according to Azure naming rules.
    /// </summary>
    /// <param name="path">The path containing the blob key to validate</param>
    /// <exception cref="ArgumentException">Thrown when the blob key is invalid</exception>
    private static void ValidateBlobKey(BlobStorePath path)
    {
        var blobKey = path.ToString();
        
        // Remove container part to get just the blob key
        if (blobKey.StartsWith(path.Container + "/"))
        {
            blobKey = blobKey.Substring(path.Container.Length + 1);
        }

        if (string.IsNullOrEmpty(blobKey))
        {
            return; // Empty blob key is valid for root directory
        }

        // Azure blob names can be up to 1,024 characters long
        if (blobKey.Length > 1024)
        {
            throw new ArgumentException("Blob name cannot exceed 1,024 characters");
        }

        // Check for invalid characters
        if (blobKey.Contains('\0'))
        {
            throw new ArgumentException("Blob name cannot contain null characters");
        }

        // Check for invalid control characters (0x00-0x1F and 0x7F-0x9F)
        foreach (char c in blobKey)
        {
            if ((c >= 0x00 && c <= 0x1F) || (c >= 0x7F && c <= 0x9F))
            {
                throw new ArgumentException($"Blob name cannot contain control character: 0x{(int)c:X2}");
            }
        }

        // Check for reserved characters that should be URL encoded
        var reservedChars = new[] { '<', '>', ':', '"', '|', '?', '*' };
        foreach (char c in reservedChars)
        {
            if (blobKey.Contains(c))
            {
                throw new ArgumentException($"Blob name contains reserved character '{c}' that should be URL encoded");
            }
        }

        // Blob names ending with a dot or whitespace are not recommended
        if (blobKey.EndsWith(".") || blobKey.EndsWith(" ") || blobKey.EndsWith("\t"))
        {
            throw new ArgumentException("Blob name should not end with a dot or whitespace character");
        }

        // Blob names starting with whitespace are not recommended
        if (blobKey.StartsWith(" ") || blobKey.StartsWith("\t"))
        {
            throw new ArgumentException("Blob name should not start with whitespace character");
        }
    }

    /// <summary>
    /// Checks if the container name is reserved by Azure.
    /// </summary>
    /// <param name="containerName">The container name to check</param>
    /// <returns>True if the container name is reserved</returns>
    private static bool IsReservedContainerName(string containerName)
    {
        // Azure has some reserved container names
        var reservedNames = new[]
        {
            "$root",
            "$web",
            "$logs"
        };

        return reservedNames.Contains(containerName, StringComparer.OrdinalIgnoreCase);
    }
}
