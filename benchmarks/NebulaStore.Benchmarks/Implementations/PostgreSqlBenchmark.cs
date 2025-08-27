using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NebulaStore.Benchmarks.Models;

namespace NebulaStore.Benchmarks.Implementations;

/// <summary>
/// PostgreSQL implementation of the benchmark interface using Entity Framework Core.
/// </summary>
public class PostgreSqlBenchmark : BaseBenchmark
{
    private PostgreSqlBenchmarkDbContext? _dbContext;

    /// <summary>
    /// Name of the benchmark implementation.
    /// </summary>
    public override string Name => "PostgreSQL";

    /// <summary>
    /// Initialize the PostgreSQL benchmark.
    /// </summary>
    protected override async Task InitializeImplementationAsync()
    {
        if (string.IsNullOrEmpty(_config.ConnectionString))
        {
            throw new InvalidOperationException("PostgreSQL connection string is required");
        }

        LogInfo($"Initializing PostgreSQL with connection: {MaskConnectionString(_config.ConnectionString)}");

        var options = new DbContextOptionsBuilder<PostgreSqlBenchmarkDbContext>()
            .UseNpgsql(_config.ConnectionString)
            .EnableSensitiveDataLogging(_config.VerboseLogging)
            .Options;

        _dbContext = new PostgreSqlBenchmarkDbContext(options);

        // Test connection
        await _dbContext.Database.CanConnectAsync();
        LogInfo("PostgreSQL connection established successfully");
    }

    /// <summary>
    /// Prepare the database for benchmarking.
    /// </summary>
    public override async Task PrepareAsync()
    {
        if (_dbContext == null)
            throw new InvalidOperationException("PostgreSQL not initialized");

        LogInfo("Preparing PostgreSQL database for benchmarking...");

        try
        {
            // Try to ensure database exists (don't drop if no permission)
            var created = await _dbContext.Database.EnsureCreatedAsync();
            if (created)
            {
                LogInfo("Database created successfully with fresh schema");
            }
            else
            {
                LogInfo("Database already exists, using existing schema");
            }

            // Create indexes for better query performance
            await CreateIndexesAsync();

            LogInfo("PostgreSQL database preparation completed");
        }
        catch (Exception ex)
        {
            LogError($"Database preparation failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                LogError($"Inner exception: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// Clean up any existing data.
    /// </summary>
    public override async Task CleanupAsync()
    {
        if (_dbContext == null)
            throw new InvalidOperationException("PostgreSQL not initialized");

        LogInfo("Cleaning up PostgreSQL database data...");

        // Delete all data in correct order (respecting foreign keys)
        await _dbContext.OrderItems.ExecuteDeleteAsync();
        await _dbContext.Orders.ExecuteDeleteAsync();
        await _dbContext.Customers.ExecuteDeleteAsync();
        await _dbContext.Products.ExecuteDeleteAsync();

        // Reset sequences (PostgreSQL equivalent of auto-increment)
        await ResetSequencesAsync();

        LogInfo("PostgreSQL database cleanup completed");
    }

    /// <summary>
    /// Insert batch implementation for PostgreSQL.
    /// </summary>
    protected override async Task InsertBatchImplementationAsync<T>(IList<T> records)
    {
        if (_dbContext == null)
            throw new InvalidOperationException("PostgreSQL not initialized");

        try
        {
            // Add entities to context
            await _dbContext.Set<T>().AddRangeAsync(records);

            // Save changes in batches for better performance
            await _dbContext.SaveChangesAsync();

            // Clear change tracker to free memory
            _dbContext.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            LogError($"Insert batch failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                LogError($"Inner exception: {ex.InnerException.Message}");
            }

            // Clear the change tracker to reset state
            _dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>
    /// Query by ID implementation for PostgreSQL.
    /// </summary>
    protected override async Task<IEnumerable<T>> QueryByIdImplementationAsync<T>(IList<int> ids) where T : class
    {
        if (_dbContext == null)
            throw new InvalidOperationException("PostgreSQL not initialized");

        // Use Contains for efficient IN query
        var results = await _dbContext.Set<T>()
            .Where(e => ids.Contains(EF.Property<int>(e, "Id")))
            .AsNoTracking()
            .ToListAsync();

        return results;
    }

    /// <summary>
    /// Query with filter implementation for PostgreSQL.
    /// </summary>
    protected override async Task<IEnumerable<T>> QueryWithFilterImplementationAsync<T>(Func<T, bool> predicate) where T : class
    {
        if (_dbContext == null)
            throw new InvalidOperationException("PostgreSQL not initialized");

        // Note: This will load all entities into memory and then filter
        // For production, you'd want to convert the predicate to an Expression<Func<T, bool>>
        var results = await _dbContext.Set<T>()
            .AsNoTracking()
            .ToListAsync();

        return results.Where(predicate);
    }

    /// <summary>
    /// Complex query implementation for PostgreSQL.
    /// </summary>
    protected override async Task<IEnumerable<T>> QueryComplexImplementationAsync<T>() where T : class
    {
        if (_dbContext == null)
            throw new InvalidOperationException("PostgreSQL not initialized");

        if (typeof(T) == typeof(Customer))
        {
            // Complex customer query: Active customers with orders in the last 30 days and total spent > $500
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            var results = await _dbContext.Customers
                .Where(c => c.IsActive && 
                           c.Status == CustomerStatus.Active && 
                           c.TotalSpent > 500 &&
                           c.Orders.Any(o => o.OrderDate >= thirtyDaysAgo))
                .AsNoTracking()
                .ToListAsync();

            return results.Cast<T>();
        }
        else if (typeof(T) == typeof(Order))
        {
            // Complex order query: Orders with multiple items and high value
            var results = await _dbContext.Orders
                .Where(o => o.OrderItems.Count > 2 && 
                           o.OrderItems.Sum(oi => oi.TotalPrice) > 200 &&
                           o.Status == OrderStatus.Delivered)
                .AsNoTracking()
                .ToListAsync();

            return results.Cast<T>();
        }
        else if (typeof(T) == typeof(Product))
        {
            // Complex product query: Popular products (in many orders) with good stock
            var results = await _dbContext.Products
                .Where(p => p.OrderItems.Count > 10 && // Ordered more than 10 times
                           p.StockQuantity > 50 && 
                           p.IsActive)
                .AsNoTracking()
                .ToListAsync();

            return results.Cast<T>();
        }
        else
        {
            throw new NotSupportedException($"Entity type {typeof(T).Name} is not supported");
        }
    }

    /// <summary>
    /// Get current storage size.
    /// </summary>
    public override async Task<long> GetStorageSizeAsync()
    {
        if (_dbContext == null)
            throw new InvalidOperationException("PostgreSQL not initialized");

        try
        {
            // Query pg_database to get database size
            var query = @"
                SELECT pg_database_size(current_database()) as database_size";

            var result = await _dbContext.Database.SqlQueryRaw<long>(query).FirstOrDefaultAsync();
            return result;
        }
        catch
        {
            // Fallback: estimate based on row counts
            var customerCount = await _dbContext.Customers.CountAsync();
            var orderCount = await _dbContext.Orders.CountAsync();
            var orderItemCount = await _dbContext.OrderItems.CountAsync();
            var productCount = await _dbContext.Products.CountAsync();

            // Rough estimate: 500 bytes per customer, 200 per order, 100 per order item, 300 per product
            return (customerCount * 500) + (orderCount * 200) + (orderItemCount * 100) + (productCount * 300);
        }
    }

    /// <summary>
    /// Create database indexes for better performance.
    /// </summary>
    private async Task CreateIndexesAsync()
    {
        if (_dbContext == null)
            throw new InvalidOperationException("PostgreSQL not initialized");

        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS ix_customers_email ON customers(email)",
            "CREATE INDEX IF NOT EXISTS ix_customers_status ON customers(status)",
            "CREATE INDEX IF NOT EXISTS ix_customers_is_active ON customers(is_active)",
            "CREATE INDEX IF NOT EXISTS ix_customers_total_spent ON customers(total_spent)",
            "CREATE INDEX IF NOT EXISTS ix_orders_customer_id ON orders(customer_id)",
            "CREATE INDEX IF NOT EXISTS ix_orders_order_date ON orders(order_date)",
            "CREATE INDEX IF NOT EXISTS ix_orders_status ON orders(status)",
            "CREATE INDEX IF NOT EXISTS ix_order_items_order_id ON order_items(order_id)",
            "CREATE INDEX IF NOT EXISTS ix_order_items_product_id ON order_items(product_id)",
            "CREATE INDEX IF NOT EXISTS ix_products_category ON products(category)",
            "CREATE INDEX IF NOT EXISTS ix_products_is_active ON products(is_active)",
            "CREATE INDEX IF NOT EXISTS ix_products_stock_quantity ON products(stock_quantity)"
        };

        foreach (var index in indexes)
        {
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync(index);
            }
            catch (Exception ex)
            {
                LogVerbose($"Index creation warning: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Reset sequences (PostgreSQL equivalent of auto-increment).
    /// </summary>
    private async Task ResetSequencesAsync()
    {
        if (_dbContext == null)
            throw new InvalidOperationException("PostgreSQL not initialized");

        var resetCommands = new[]
        {
            "ALTER SEQUENCE customers_id_seq RESTART WITH 1",
            "ALTER SEQUENCE orders_id_seq RESTART WITH 1",
            "ALTER SEQUENCE order_items_id_seq RESTART WITH 1",
            "ALTER SEQUENCE products_id_seq RESTART WITH 1"
        };

        foreach (var command in resetCommands)
        {
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync(command);
            }
            catch (Exception ex)
            {
                LogVerbose($"Sequence reset warning: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Mask sensitive information in connection string for logging.
    /// </summary>
    private static string MaskConnectionString(string connectionString)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            connectionString, 
            @"(password|pwd)=([^;]*)", 
            "$1=***", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Dispose of PostgreSQL resources.
    /// </summary>
    protected override void DisposeImplementation()
    {
        try
        {
            _dbContext?.Dispose();
            LogInfo("PostgreSQL connection disposed successfully");
        }
        catch (Exception ex)
        {
            LogError($"Error disposing PostgreSQL connection: {ex.Message}");
        }
    }
}

/// <summary>
/// Entity Framework DbContext for PostgreSQL benchmark entities.
/// </summary>
public class PostgreSqlBenchmarkDbContext : DbContext
{
    public PostgreSqlBenchmarkDbContext(DbContextOptions<PostgreSqlBenchmarkDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderItem> OrderItems { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Customer entity
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.FirstName).HasColumnName("first_name").HasMaxLength(100);
            entity.Property(e => e.LastName).HasColumnName("last_name").HasMaxLength(100);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(20);
            entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.TotalSpent).HasColumnName("total_spent").HasPrecision(18, 2);
            entity.Property(e => e.OrderCount).HasColumnName("order_count");
            entity.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(500);
            entity.Property(e => e.Address).HasColumnName("address").HasMaxLength(200);
            entity.Property(e => e.City).HasColumnName("city").HasMaxLength(100);
            entity.Property(e => e.State).HasColumnName("state").HasMaxLength(50);
            entity.Property(e => e.ZipCode).HasColumnName("zip_code").HasMaxLength(20);
            entity.Property(e => e.Country).HasColumnName("country").HasMaxLength(50);
        });

        // Configure Order entity
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.OrderDate).HasColumnName("order_date");
            entity.Property(e => e.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2);
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.ShippingAddress).HasColumnName("shipping_address").HasMaxLength(100);
            entity.Property(e => e.ShippedDate).HasColumnName("shipped_date");
            entity.Property(e => e.DeliveredDate).HasColumnName("delivered_date");
            entity.Property(e => e.TrackingNumber).HasColumnName("tracking_number").HasMaxLength(50);
            entity.Property(e => e.Notes).HasColumnName("notes").HasMaxLength(200);

            entity.HasOne(e => e.Customer)
                  .WithMany(e => e.Orders)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure OrderItem entity
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 2);
            entity.Property(e => e.TotalPrice).HasColumnName("total_price").HasPrecision(18, 2);

            entity.HasOne(e => e.Order)
                  .WithMany(e => e.OrderItems)
                  .HasForeignKey(e => e.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Product)
                  .WithMany(e => e.OrderItems)
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Product entity
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(e => e.SKU).HasColumnName("sku").HasMaxLength(50);
            entity.HasIndex(e => e.SKU).IsUnique();
            entity.Property(e => e.Price).HasColumnName("price").HasPrecision(18, 2);
            entity.Property(e => e.StockQuantity).HasColumnName("stock_quantity");
            entity.Property(e => e.Category).HasColumnName("category").HasMaxLength(100);
            entity.Property(e => e.Brand).HasColumnName("brand").HasMaxLength(100);
            entity.Property(e => e.IsActive).HasColumnName("is_active");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
