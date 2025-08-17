using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using MessagePack;

namespace NebulaStore.Core.Tests;

public class ObjectStoreLazyQueryTests
{
    private const string FilePath = "store_lazy_query_test.msgpack";

    [Fact]
    public void Query_Is_Lazy_And_Incremental()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);

        using (var store = new ObjectStore(FilePath))
        {
            var order = store.Root<Order>();
            order.CustomerName = "LazyTester";
            order.Items.Add(new Product { Name = "Big Sofa", Price = 500 });
            order.Items.Add(new Product { Name = "Pillow", Price = 20 });
            order.Items.Add(new Product { Name = "Blanket", Price = 80 });
            store.Commit();
        }

        using (var store = new ObjectStore(FilePath))
        {
            var query = store.Query<Product>(); // not traversed immediately
            Assert.NotNull(query);

            // Only traversed when enumerated
            var expensive = query.Where(p => p.Price > 100).ToList();

            Assert.Single(expensive);
            Assert.Equal("Big Sofa", expensive[0].Name);
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