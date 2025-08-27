using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NebulaStore.Benchmarks.Models;
using NebulaStore.Storage.Embedded;

namespace NebulaStore.Benchmarks.Implementations;

/// <summary>
/// NebulaStore implementation of the benchmark interface.
/// </summary>
public class NebulaStoreBenchmark : BaseBenchmark
{
    private IEmbeddedStorageManager? _storage;
    private BenchmarkDataRoot? _root;
    private string _storageDirectory = string.Empty;

    /// <summary>
    /// Name of the benchmark implementation.
    /// </summary>
    public override string Name => "NebulaStore";

    /// <summary>
    /// Initialize the NebulaStore benchmark.
    /// </summary>
    protected override async Task InitializeImplementationAsync()
    {
        _storageDirectory = Path.Combine(_config.StorageDirectory, "nebula-benchmark");
        
        // Ensure directory exists and is clean
        if (Directory.Exists(_storageDirectory))
        {
            Directory.Delete(_storageDirectory, true);
        }
        Directory.CreateDirectory(_storageDirectory);

        LogInfo($"Initializing NebulaStore in directory: {_storageDirectory}");

        // Start embedded storage
        _storage = EmbeddedStorage.Start(_storageDirectory);
        
        // Get or create root object
        _root = _storage.Root<BenchmarkDataRoot>();
        if (_root == null)
        {
            _root = new BenchmarkDataRoot();
            _storage.SetRoot(_root);
            _storage.StoreRoot();
        }

        LogInfo("NebulaStore initialized successfully");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Prepare the storage for benchmarking.
    /// </summary>
    public override async Task PrepareAsync()
    {
        LogInfo("Preparing NebulaStore for benchmarking...");
        
        // Initialize collections if needed
        if (_root != null)
        {
            _root.Customers.Clear();
            _root.Orders.Clear();
            _root.OrderItems.Clear();
            _root.Products.Clear();
            _root.CustomerIndex.Clear();
            _root.CustomerOrderIndex.Clear();
            _root.LastUpdated = DateTime.UtcNow;
            
            _storage?.StoreRoot();
        }

        LogInfo("NebulaStore preparation completed");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Clean up any existing data.
    /// </summary>
    public override async Task CleanupAsync()
    {
        LogInfo("Cleaning up NebulaStore data...");
        
        if (_root != null)
        {
            _root.Customers.Clear();
            _root.Orders.Clear();
            _root.OrderItems.Clear();
            _root.Products.Clear();
            _root.CustomerIndex.Clear();
            _root.CustomerOrderIndex.Clear();
            
            _storage?.StoreRoot();
        }

        // Force garbage collection to clean up memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        LogInfo("NebulaStore cleanup completed");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Insert batch implementation for NebulaStore.
    /// </summary>
    protected override async Task InsertBatchImplementationAsync<T>(IList<T> records)
    {
        if (_root == null || _storage == null)
            throw new InvalidOperationException("NebulaStore not initialized");

        // Handle different entity types
        if (typeof(T) == typeof(Customer))
        {
            var customers = records.Cast<Customer>().ToList();
            
            // Add to main collection
            _root.Customers.AddRange(customers);
            
            // Update index for fast lookups
            foreach (var customer in customers)
            {
                _root.CustomerIndex[customer.Id] = customer;
            }
        }
        else if (typeof(T) == typeof(Order))
        {
            var orders = records.Cast<Order>().ToList();
            
            // Add to main collection
            _root.Orders.AddRange(orders);
            
            // Update customer-order index
            foreach (var order in orders)
            {
                if (!_root.CustomerOrderIndex.ContainsKey(order.CustomerId))
                {
                    _root.CustomerOrderIndex[order.CustomerId] = new List<Order>();
                }
                _root.CustomerOrderIndex[order.CustomerId].Add(order);
            }
        }
        else if (typeof(T) == typeof(OrderItem))
        {
            var orderItems = records.Cast<OrderItem>().ToList();
            _root.OrderItems.AddRange(orderItems);
        }
        else if (typeof(T) == typeof(Product))
        {
            var products = records.Cast<Product>().ToList();
            _root.Products.AddRange(products);
        }
        else
        {
            throw new NotSupportedException($"Entity type {typeof(T).Name} is not supported");
        }

        // Update timestamp
        _root.LastUpdated = DateTime.UtcNow;

        // Store the changes
        _storage.StoreRoot();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Query by ID implementation for NebulaStore.
    /// </summary>
    protected override async Task<IEnumerable<T>> QueryByIdImplementationAsync<T>(IList<int> ids)
    {
        if (_root == null)
            throw new InvalidOperationException("NebulaStore not initialized");

        var results = new List<T>();

        if (typeof(T) == typeof(Customer))
        {
            // Use index for fast customer lookups
            foreach (var id in ids)
            {
                if (_root.CustomerIndex.TryGetValue(id, out var customer))
                {
                    results.Add((T)(object)customer);
                }
            }
        }
        else if (typeof(T) == typeof(Order))
        {
            var idSet = new HashSet<int>(ids);
            var orders = _root.Orders.Where(o => idSet.Contains(o.Id)).Cast<T>();
            results.AddRange(orders);
        }
        else if (typeof(T) == typeof(OrderItem))
        {
            var idSet = new HashSet<int>(ids);
            var orderItems = _root.OrderItems.Where(oi => idSet.Contains(oi.Id)).Cast<T>();
            results.AddRange(orderItems);
        }
        else if (typeof(T) == typeof(Product))
        {
            var idSet = new HashSet<int>(ids);
            var products = _root.Products.Where(p => idSet.Contains(p.Id)).Cast<T>();
            results.AddRange(products);
        }
        else
        {
            throw new NotSupportedException($"Entity type {typeof(T).Name} is not supported");
        }

        await Task.CompletedTask;
        return results;
    }

    /// <summary>
    /// Query with filter implementation for NebulaStore.
    /// </summary>
    protected override async Task<IEnumerable<T>> QueryWithFilterImplementationAsync<T>(Func<T, bool> predicate)
    {
        if (_root == null)
            throw new InvalidOperationException("NebulaStore not initialized");

        IEnumerable<T> results;

        if (typeof(T) == typeof(Customer))
        {
            results = _root.Customers.Cast<T>().Where(predicate);
        }
        else if (typeof(T) == typeof(Order))
        {
            results = _root.Orders.Cast<T>().Where(predicate);
        }
        else if (typeof(T) == typeof(OrderItem))
        {
            results = _root.OrderItems.Cast<T>().Where(predicate);
        }
        else if (typeof(T) == typeof(Product))
        {
            results = _root.Products.Cast<T>().Where(predicate);
        }
        else
        {
            throw new NotSupportedException($"Entity type {typeof(T).Name} is not supported");
        }

        await Task.CompletedTask;
        return results.ToList(); // Materialize the query
    }

    /// <summary>
    /// Complex query implementation for NebulaStore.
    /// </summary>
    protected override async Task<IEnumerable<T>> QueryComplexImplementationAsync<T>()
    {
        if (_root == null)
            throw new InvalidOperationException("NebulaStore not initialized");

        IEnumerable<T> results;

        if (typeof(T) == typeof(Customer))
        {
            // Complex customer query: Active customers with orders in the last 30 days and total spent > $500
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            
            results = _root.Customers
                .Where(c => c.IsActive && 
                           c.Status == CustomerStatus.Active && 
                           c.TotalSpent > 500)
                .Where(c => _root.CustomerOrderIndex.ContainsKey(c.Id) &&
                           _root.CustomerOrderIndex[c.Id].Any(o => o.OrderDate >= thirtyDaysAgo))
                .Cast<T>();
        }
        else if (typeof(T) == typeof(Order))
        {
            // Complex order query: Orders with multiple items and high value
            var orderItemGroups = _root.OrderItems.GroupBy(oi => oi.OrderId);
            var multiItemOrderIds = orderItemGroups
                .Where(g => g.Count() > 2 && g.Sum(oi => oi.TotalPrice) > 200)
                .Select(g => g.Key)
                .ToHashSet();

            results = _root.Orders
                .Where(o => multiItemOrderIds.Contains(o.Id) && 
                           o.Status == OrderStatus.Delivered)
                .Cast<T>();
        }
        else if (typeof(T) == typeof(Product))
        {
            // Complex product query: Popular products (in many orders) with good stock
            var popularProductIds = _root.OrderItems
                .GroupBy(oi => oi.ProductId)
                .Where(g => g.Count() > 10) // Ordered more than 10 times
                .Select(g => g.Key)
                .ToHashSet();

            results = _root.Products
                .Where(p => popularProductIds.Contains(p.Id) && 
                           p.StockQuantity > 50 && 
                           p.IsActive)
                .Cast<T>();
        }
        else
        {
            throw new NotSupportedException($"Entity type {typeof(T).Name} is not supported");
        }

        await Task.CompletedTask;
        return results.ToList(); // Materialize the query
    }

    /// <summary>
    /// Get current storage size.
    /// </summary>
    public override async Task<long> GetStorageSizeAsync()
    {
        if (!Directory.Exists(_storageDirectory))
            return 0;

        var directoryInfo = new DirectoryInfo(_storageDirectory);
        var totalSize = directoryInfo.GetFiles("*", SearchOption.AllDirectories)
            .Sum(file => file.Length);

        await Task.CompletedTask;
        return totalSize;
    }

    /// <summary>
    /// Dispose of NebulaStore resources.
    /// </summary>
    protected override void DisposeImplementation()
    {
        try
        {
            _storage?.Dispose();
            LogInfo("NebulaStore disposed successfully");
        }
        catch (Exception ex)
        {
            LogError($"Error disposing NebulaStore: {ex.Message}");
        }
    }
}
