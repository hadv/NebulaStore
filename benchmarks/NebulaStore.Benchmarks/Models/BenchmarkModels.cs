using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using MessagePack;
using EfKey = System.ComponentModel.DataAnnotations.KeyAttribute;
using MpKey = MessagePack.KeyAttribute;

namespace NebulaStore.Benchmarks.Models;

/// <summary>
/// Primary entity for benchmarking with realistic data structure.
/// Represents a customer record with various data types and relationships.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
[Table("Customers")]
public class Customer
{
    [EfKey]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [MpKey(0)]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    [MpKey(1)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [MpKey(2)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [MpKey(3)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    [MpKey(4)]
    public string? Phone { get; set; }

    [MpKey(5)]
    public DateTime DateOfBirth { get; set; }

    [MpKey(6)]
    public DateTime CreatedAt { get; set; }

    [MpKey(7)]
    public DateTime? LastLoginAt { get; set; }

    [MpKey(8)]
    public bool IsActive { get; set; }

    [MpKey(9)]
    public CustomerStatus Status { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [MpKey(10)]
    public decimal TotalSpent { get; set; }

    [MpKey(11)]
    public int OrderCount { get; set; }

    [MaxLength(500)]
    [MpKey(12)]
    public string? Notes { get; set; }

    [MaxLength(200)]
    [MpKey(13)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(100)]
    [MpKey(14)]
    public string City { get; set; } = string.Empty;

    [MaxLength(50)]
    [MpKey(15)]
    public string State { get; set; } = string.Empty;

    [MaxLength(20)]
    [MpKey(16)]
    public string ZipCode { get; set; } = string.Empty;

    [MaxLength(50)]
    [MpKey(17)]
    public string Country { get; set; } = string.Empty;

    // Navigation properties for EF Core (ignored by MessagePack)
    [IgnoreMember]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    // Computed properties for queries
    [NotMapped]
    [IgnoreMember]
    public int Age => DateTime.Now.Year - DateOfBirth.Year;

    [NotMapped]
    [IgnoreMember]
    public string FullName => $"{FirstName} {LastName}";

    [NotMapped]
    [IgnoreMember]
    public decimal AverageOrderValue => OrderCount > 0 ? TotalSpent / OrderCount : 0;
}

/// <summary>
/// Order entity for relationship testing and complex queries.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
[Table("Orders")]
public class Order
{
    [EfKey]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [MpKey(0)]
    public int Id { get; set; }

    [MpKey(1)]
    public int CustomerId { get; set; }

    [MpKey(2)]
    public DateTime OrderDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [MpKey(3)]
    public decimal TotalAmount { get; set; }

    [MpKey(4)]
    public OrderStatus Status { get; set; }

    [MaxLength(100)]
    [MpKey(5)]
    public string? ShippingAddress { get; set; }

    [MpKey(6)]
    public DateTime? ShippedDate { get; set; }

    [MpKey(7)]
    public DateTime? DeliveredDate { get; set; }

    [MaxLength(50)]
    [MpKey(8)]
    public string? TrackingNumber { get; set; }

    [MaxLength(200)]
    [MpKey(9)]
    public string? Notes { get; set; }

    // Navigation properties for EF Core (ignored by MessagePack)
    [ForeignKey("CustomerId")]
    [IgnoreMember]
    public virtual Customer Customer { get; set; } = null!;

    [IgnoreMember]
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

/// <summary>
/// Order item entity for detailed relationship testing.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
[Table("OrderItems")]
public class OrderItem
{
    [EfKey]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [MpKey(0)]
    public int Id { get; set; }

    [MpKey(1)]
    public int OrderId { get; set; }

    [MpKey(2)]
    public int ProductId { get; set; }

    [MpKey(3)]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [MpKey(4)]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [MpKey(5)]
    public decimal TotalPrice { get; set; }

    // Navigation properties for EF Core (ignored by MessagePack)
    [ForeignKey("OrderId")]
    [IgnoreMember]
    public virtual Order Order { get; set; } = null!;

    [ForeignKey("ProductId")]
    [IgnoreMember]
    public virtual Product Product { get; set; } = null!;
}

/// <summary>
/// Product entity for catalog testing.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
[Table("Products")]
public class Product
{
    [EfKey]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [MpKey(0)]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [MpKey(1)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    [MpKey(2)]
    public string? Description { get; set; }

    [MaxLength(50)]
    [MpKey(3)]
    public string SKU { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    [MpKey(4)]
    public decimal Price { get; set; }

    [MpKey(5)]
    public int StockQuantity { get; set; }

    [MaxLength(100)]
    [MpKey(6)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(100)]
    [MpKey(7)]
    public string Brand { get; set; } = string.Empty;

    [MpKey(8)]
    public bool IsActive { get; set; }

    [MpKey(9)]
    public DateTime CreatedAt { get; set; }

    [MpKey(10)]
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties for EF Core (ignored by MessagePack)
    [IgnoreMember]
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}

/// <summary>
/// Enumerations for status fields.
/// </summary>
public enum CustomerStatus
{
    Active = 0,
    Inactive = 1,
    Suspended = 2,
    Deleted = 3
}

public enum OrderStatus
{
    Pending = 0,
    Processing = 1,
    Shipped = 2,
    Delivered = 3,
    Cancelled = 4,
    Returned = 5
}

/// <summary>
/// Container class for NebulaStore root object.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class BenchmarkDataRoot
{
    [MpKey(0)]
    public List<Customer> Customers { get; set; } = new();

    [MpKey(1)]
    public List<Order> Orders { get; set; } = new();

    [MpKey(2)]
    public List<OrderItem> OrderItems { get; set; } = new();

    [MpKey(3)]
    public List<Product> Products { get; set; } = new();

    [MpKey(4)]
    public Dictionary<int, Customer> CustomerIndex { get; set; } = new();

    [MpKey(5)]
    public Dictionary<int, List<Order>> CustomerOrderIndex { get; set; } = new();

    [MpKey(6)]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
