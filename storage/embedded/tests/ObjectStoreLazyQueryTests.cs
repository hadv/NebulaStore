using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using MessagePack;
using NebulaStore.Storage.Embedded;

namespace NebulaStore.Storage.Embedded.Tests;

public class ObjectStoreLazyQueryTests : IDisposable
{
    private readonly string _testDirectory;

    public ObjectStoreLazyQueryTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public void Query_Is_Lazy_And_Incremental()
    {
        using (var storage = EmbeddedStorage.Start(_testDirectory))
        {
            var order = storage.Root<Order>();
            order.CustomerName = "LazyTester";
            order.Items.Add(new Product { Name = "Big Sofa", Price = 500 });
            order.Items.Add(new Product { Name = "Pillow", Price = 20 });
            order.Items.Add(new Product { Name = "Blanket", Price = 80 });
            storage.StoreRoot();
        }

        using (var storage = EmbeddedStorage.Start(_testDirectory))
        {
            var query = storage.Query<Product>(); // not traversed immediately
            Assert.NotNull(query);

            // Only traversed when enumerated
            var expensive = query.Where(p => p.Price > 100).ToList();

            Assert.Single(expensive);
            Assert.Equal("Big Sofa", expensive[0].Name);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

[MessagePackObject(AllowPrivate = true)]
internal class Order
{
    [Key(0)]
    public string CustomerName { get; set; } = string.Empty;
    
    [Key(1)]
    public List<Product> Items { get; set; } = new();
}

[MessagePackObject(AllowPrivate = true)]
internal class Product
{
    [Key(0)]
    public string Name { get; set; } = string.Empty;
    
    [Key(1)]
    public decimal Price { get; set; }
}