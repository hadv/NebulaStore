using System;
using System.Text.RegularExpressions;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Kafka;

/// <summary>
/// Validates and sanitizes paths for Kafka topic naming.
/// </summary>
/// <remarks>
/// Kafka topic names have the following restrictions:
/// - Only alphanumeric characters, '.', '_', and '-' are allowed
/// - Maximum length is 249 characters
/// - Cannot be "." or ".."
/// - Cannot start with "__" (reserved for internal topics)
/// </remarks>
public class KafkaPathValidator
{
    private static readonly Regex InvalidCharsRegex = new Regex(@"[^a-zA-Z0-9\._\-]", RegexOptions.Compiled);
    private const int MaxTopicNameLength = 249;

    /// <summary>
    /// Converts a BlobStorePath to a valid Kafka topic name.
    /// </summary>
    /// <param name="path">The blob store path</param>
    /// <returns>A valid Kafka topic name</returns>
    public static string ToTopicName(BlobStorePath path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        // Replace path separator with underscore
        var topicName = path.FullQualifiedName.Replace(BlobStorePath.SeparatorChar, '_');

        // Replace invalid characters with underscore
        topicName = InvalidCharsRegex.Replace(topicName, "_");

        // Ensure it doesn't start with double underscore (reserved)
        if (topicName.StartsWith("__"))
        {
            topicName = "ns" + topicName; // Prefix with "ns" (NebulaStore)
        }

        // Handle special cases
        if (topicName == "." || topicName == "..")
        {
            topicName = "ns_" + topicName;
        }

        // Truncate if too long
        if (topicName.Length > MaxTopicNameLength)
        {
            // Keep the end of the name (more likely to be unique)
            topicName = topicName.Substring(topicName.Length - MaxTopicNameLength);
        }

        return topicName;
    }

    /// <summary>
    /// Gets the index topic name for a data topic.
    /// </summary>
    /// <param name="dataTopicName">The data topic name</param>
    /// <returns>The index topic name</returns>
    public static string GetIndexTopicName(string dataTopicName)
    {
        if (string.IsNullOrWhiteSpace(dataTopicName))
            throw new ArgumentException("Data topic name cannot be null or empty", nameof(dataTopicName));

        return $"__{dataTopicName}_index";
    }

    /// <summary>
    /// Validates a topic name.
    /// </summary>
    /// <param name="topicName">The topic name to validate</param>
    /// <returns>True if the topic name is valid</returns>
    public static bool IsValidTopicName(string topicName)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            return false;

        if (topicName.Length > MaxTopicNameLength)
            return false;

        if (topicName == "." || topicName == "..")
            return false;

        // Check for invalid characters
        return !InvalidCharsRegex.IsMatch(topicName);
    }

    /// <summary>
    /// Creates a new KafkaPathValidator instance.
    /// </summary>
    /// <returns>A new KafkaPathValidator instance</returns>
    public static KafkaPathValidator New()
    {
        return new KafkaPathValidator();
    }
}

