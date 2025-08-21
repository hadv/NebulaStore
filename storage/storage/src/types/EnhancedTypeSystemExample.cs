using System;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Examples;

/// <summary>
/// Example demonstrating the enhanced type system capabilities.
/// Shows how to use the enhanced storage type dictionary, type definitions, and type-in-file mapping.
/// </summary>
public static class EnhancedTypeSystemExample
{
    /// <summary>
    /// Demonstrates basic enhanced type system usage.
    /// </summary>
    public static async Task BasicUsageExample()
    {
        Console.WriteLine("Enhanced Type System - Basic Usage Example");
        Console.WriteLine("==========================================");

        // Create an enhanced storage foundation
        var foundation = Storage.EnhancedFoundation();
        var enhancedTypeDictionary = foundation.EnhancedTypeDictionary;
        var typeInFileManager = foundation.TypeInFileManager;

        // Register some types
        var stringTypeId = enhancedTypeDictionary.RegisterType(typeof(string));
        var intTypeId = enhancedTypeDictionary.RegisterType(typeof(int));
        var personTypeId = enhancedTypeDictionary.RegisterType(typeof(Person));

        Console.WriteLine($"Registered types:");
        Console.WriteLine($"  string: {stringTypeId}");
        Console.WriteLine($"  int: {intTypeId}");
        Console.WriteLine($"  Person: {personTypeId}");

        // Get type definitions
        var stringTypeDef = enhancedTypeDictionary.GetTypeDefinition(stringTypeId);
        var personTypeDef = enhancedTypeDictionary.GetTypeDefinition(personTypeId);

        Console.WriteLine($"\nType definitions:");
        Console.WriteLine($"  {stringTypeDef}");
        Console.WriteLine($"  {personTypeDef}");

        // Demonstrate type lineage
        var personLineage = enhancedTypeDictionary.GetTypeLineage(typeof(Person));
        Console.WriteLine($"\nType lineage for Person: {personLineage}");

        // Create some data files and type-in-file mappings
        var dataFile1 = new StorageDataFile(1, "data_channel_0_001.dat", 0);
        var dataFile2 = new StorageDataFile(2, "data_channel_1_001.dat", 1);

        var personEntityType = new StorageEntityType(personTypeDef!);
        var stringEntityType = new StorageEntityType(stringTypeDef!);

        // Create type-in-file mappings
        var personInFile1 = new TypeInFile(personEntityType, dataFile1);
        var stringInFile1 = new TypeInFile(stringEntityType, dataFile1);
        var personInFile2 = new TypeInFile(personEntityType, dataFile2);

        // Add instances to demonstrate statistics
        personInFile1.AddInstance(120); // Person instance of 120 bytes
        personInFile1.AddInstance(115); // Another Person instance
        stringInFile1.AddInstance(25);  // String instance
        personInFile2.AddInstance(130); // Person in different file

        // Register mappings with manager
        typeInFileManager.AddTypeInFile(personInFile1);
        typeInFileManager.AddTypeInFile(stringInFile1);
        typeInFileManager.AddTypeInFile(personInFile2);

        Console.WriteLine($"\nType-in-file statistics:");
        Console.WriteLine($"  {personInFile1}");
        Console.WriteLine($"  {stringInFile1}");
        Console.WriteLine($"  {personInFile2}");

        // Query type distribution
        var typeDistribution = typeInFileManager.GetTypeDistributionStatistics();
        Console.WriteLine($"\nType distribution across files:");
        foreach (var kvp in typeDistribution)
        {
            var typeName = enhancedTypeDictionary.GetType(kvp.Key)?.Name ?? "Unknown";
            Console.WriteLine($"  Type {typeName} (ID: {kvp.Key}): {kvp.Value} files");
        }

        // Save type dictionary to file
        var typeDictPath = "type_dictionary.json";
        await enhancedTypeDictionary.SaveAsync(typeDictPath);
        Console.WriteLine($"\nType dictionary saved to: {typeDictPath}");

        Console.WriteLine("\nBasic usage example completed successfully!");
    }

    /// <summary>
    /// Demonstrates type evolution and versioning.
    /// </summary>
    public static void TypeEvolutionExample()
    {
        Console.WriteLine("\nEnhanced Type System - Type Evolution Example");
        Console.WriteLine("=============================================");

        var enhancedTypeDictionary = new EnhancedStorageTypeDictionary();

        // Register initial version of a type
        var personV1TypeId = enhancedTypeDictionary.RegisterType(typeof(PersonV1));
        var personV1Definition = enhancedTypeDictionary.GetTypeDefinition(personV1TypeId);

        Console.WriteLine($"Registered PersonV1: {personV1Definition}");

        // Simulate type evolution - register a new version
        var personV2TypeId = enhancedTypeDictionary.RegisterType(typeof(PersonV2));
        var personV2Definition = enhancedTypeDictionary.GetTypeDefinition(personV2TypeId);

        Console.WriteLine($"Registered PersonV2: {personV2Definition}");

        // Check type lineage
        var lineage = enhancedTypeDictionary.GetTypeLineage("PersonV1");
        if (lineage != null)
        {
            Console.WriteLine($"Type lineage: {lineage}");
            Console.WriteLine($"Latest definition: {lineage.LatestDefinition}");
        }

        // Validate type definitions
        var isV1Valid = enhancedTypeDictionary.ValidateTypeDefinition(personV1Definition!);
        var isV2Valid = enhancedTypeDictionary.ValidateTypeDefinition(personV2Definition!);

        Console.WriteLine($"PersonV1 definition valid: {isV1Valid}");
        Console.WriteLine($"PersonV2 definition valid: {isV2Valid}");

        Console.WriteLine("Type evolution example completed successfully!");
    }

    /// <summary>
    /// Demonstrates entity type handler usage.
    /// </summary>
    public static void EntityTypeHandlerExample()
    {
        Console.WriteLine("\nEnhanced Type System - Entity Type Handler Example");
        Console.WriteLine("===================================================");

        var enhancedTypeDictionary = new EnhancedStorageTypeDictionary();
        var personTypeId = enhancedTypeDictionary.RegisterType(typeof(Person));
        var personDefinition = enhancedTypeDictionary.GetTypeDefinition(personTypeId)!;

        // Get entity type handler
        var entityHandler = enhancedTypeDictionary.GetEntityTypeHandler(personTypeId);
        if (entityHandler != null)
        {
            Console.WriteLine($"Entity handler: {entityHandler}");
            Console.WriteLine($"Simple reference count: {entityHandler.SimpleReferenceCount}");
            Console.WriteLine($"Minimum length: {entityHandler.MinimumLength}");
            Console.WriteLine($"Maximum length: {entityHandler.MaximumLength}");

            // Validate some entity lengths
            var validLengths = new long[] { 50, 100, 200 };
            var invalidLengths = new long[] { 1, 2, 5 }; // Too small

            Console.WriteLine("\nValidating entity lengths:");
            foreach (var length in validLengths)
            {
                var isValid = entityHandler.IsValidEntity(length, 12345);
                Console.WriteLine($"  Length {length}: {(isValid ? "Valid" : "Invalid")}");
            }

            foreach (var length in invalidLengths)
            {
                var isValid = entityHandler.IsValidEntity(length, 12345);
                Console.WriteLine($"  Length {length}: {(isValid ? "Valid" : "Invalid")}");
            }
        }

        Console.WriteLine("Entity type handler example completed successfully!");
    }

    /// <summary>
    /// Demonstrates persistence and loading of type dictionary.
    /// </summary>
    public static async Task PersistenceExample()
    {
        Console.WriteLine("\nEnhanced Type System - Persistence Example");
        Console.WriteLine("===========================================");

        var typeDictPath = "enhanced_type_dictionary.json";

        // Create and populate type dictionary
        var originalDict = new EnhancedStorageTypeDictionary();
        originalDict.RegisterType(typeof(string));
        originalDict.RegisterType(typeof(int));
        originalDict.RegisterType(typeof(Person));
        originalDict.RegisterType(typeof(PersonV1));

        Console.WriteLine($"Original dictionary has {originalDict.TypeCount} types");
        Console.WriteLine($"Highest type ID: {originalDict.GetHighestTypeId()}");

        // Save to file
        await originalDict.SaveAsync(typeDictPath);
        Console.WriteLine($"Type dictionary saved to: {typeDictPath}");

        // Create new dictionary and load from file
        var loadedDict = new EnhancedStorageTypeDictionary();
        await loadedDict.LoadAsync(typeDictPath);

        Console.WriteLine($"Loaded dictionary has {loadedDict.TypeCount} types");
        Console.WriteLine($"Highest type ID: {loadedDict.GetHighestTypeId()}");

        // Verify loaded data
        var stringTypeId = loadedDict.GetTypeId(typeof(string));
        var personTypeId = loadedDict.GetTypeId(typeof(Person));

        Console.WriteLine($"String type ID: {stringTypeId}");
        Console.WriteLine($"Person type ID: {personTypeId}");

        var allDefinitions = loadedDict.GetAllTypeDefinitions();
        Console.WriteLine($"All type definitions ({allDefinitions.Count}):");
        foreach (var kvp in allDefinitions)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value.TypeName}");
        }

        Console.WriteLine("Persistence example completed successfully!");
    }

    /// <summary>
    /// Runs all enhanced type system examples.
    /// </summary>
    public static async Task RunAllExamples()
    {
        await BasicUsageExample();
        TypeEvolutionExample();
        EntityTypeHandlerExample();
        await PersistenceExample();

        Console.WriteLine("\n🎉 All enhanced type system examples completed successfully!");
    }
}

// Example classes for demonstration
public class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
}

public class PersonV1
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class PersonV2
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
