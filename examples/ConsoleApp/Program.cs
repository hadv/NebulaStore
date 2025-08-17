using NebulaStore.Examples;

namespace NebulaStore.Examples.ConsoleApp;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            EmbeddedStorageExample.RunExample();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running example: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
