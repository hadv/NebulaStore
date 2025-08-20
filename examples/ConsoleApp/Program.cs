using NebulaStore.Examples;

namespace NebulaStore.Examples.ConsoleApp;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("NebulaStore Examples");
        Console.WriteLine("===================");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("Choose an example to run:");
            Console.WriteLine("1. Embedded Storage Example");
            Console.WriteLine("2. Monitoring Example");
            Console.WriteLine("3. GigaMap Integration Example");
            Console.WriteLine("4. Run All Examples");
            Console.WriteLine("0. Exit");
            Console.WriteLine();
            Console.Write("Enter your choice (0-4): ");

            var input = Console.ReadLine();
            Console.WriteLine();

            try
            {
                switch (input)
                {
                    case "1":
                        Console.WriteLine("Running Embedded Storage Example...");
                        Console.WriteLine("===================================");
                        EmbeddedStorageExample.RunExample();
                        break;

                    case "2":
                        Console.WriteLine("Running Monitoring Example...");
                        Console.WriteLine("=============================");
                        MonitoringExample.RunExample();
                        break;

                    case "3":
                        Console.WriteLine("Running GigaMap Integration Example...");
                        Console.WriteLine("======================================");
                        await GigaMapExample.RunAsync();
                        break;

                    case "4":
                        Console.WriteLine("Running All Examples...");
                        Console.WriteLine("=======================");
                        Console.WriteLine();

                        Console.WriteLine("1. Embedded Storage Example:");
                        Console.WriteLine("============================");
                        EmbeddedStorageExample.RunExample();

                        Console.WriteLine();
                        Console.WriteLine("2. Monitoring Example:");
                        Console.WriteLine("======================");
                        MonitoringExample.RunExample();

                        Console.WriteLine();
                        Console.WriteLine("3. GigaMap Integration Example:");
                        Console.WriteLine("===============================");
                        await GigaMapExample.RunAsync();
                        break;

                    case "0":
                        Console.WriteLine("Goodbye!");
                        return;

                    default:
                        Console.WriteLine("Invalid choice. Please enter 0, 1, 2, 3, or 4.");
                        continue;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error running example: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to return to the menu...");
            Console.ReadKey();
            Console.Clear();
        }
    }
}
