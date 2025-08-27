using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.Benchmarks.Models;

/// <summary>
/// Generates realistic test data for benchmarking.
/// </summary>
public static class DataGenerator
{
    private static readonly Random _random = new(42); // Fixed seed for reproducible results

    private static readonly string[] _firstNames = {
        "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda",
        "William", "Elizabeth", "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica",
        "Thomas", "Sarah", "Christopher", "Karen", "Charles", "Nancy", "Daniel", "Lisa",
        "Matthew", "Betty", "Anthony", "Helen", "Mark", "Sandra", "Donald", "Donna",
        "Steven", "Carol", "Paul", "Ruth", "Andrew", "Sharon", "Joshua", "Michelle",
        "Kenneth", "Laura", "Kevin", "Sarah", "Brian", "Kimberly", "George", "Deborah",
        "Timothy", "Dorothy", "Ronald", "Lisa", "Jason", "Nancy", "Edward", "Karen"
    };

    private static readonly string[] _lastNames = {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis",
        "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas",
        "Taylor", "Moore", "Jackson", "Martin", "Lee", "Perez", "Thompson", "White",
        "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson", "Walker", "Young",
        "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores",
        "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell",
        "Carter", "Roberts", "Gomez", "Phillips", "Evans", "Turner", "Diaz", "Parker"
    };

    private static readonly string[] _cities = {
        "New York", "Los Angeles", "Chicago", "Houston", "Phoenix", "Philadelphia", "San Antonio",
        "San Diego", "Dallas", "San Jose", "Austin", "Jacksonville", "Fort Worth", "Columbus",
        "Charlotte", "San Francisco", "Indianapolis", "Seattle", "Denver", "Washington",
        "Boston", "El Paso", "Nashville", "Detroit", "Oklahoma City", "Portland", "Las Vegas",
        "Memphis", "Louisville", "Baltimore", "Milwaukee", "Albuquerque", "Tucson", "Fresno",
        "Sacramento", "Kansas City", "Mesa", "Atlanta", "Omaha", "Colorado Springs", "Raleigh",
        "Miami", "Long Beach", "Virginia Beach", "Oakland", "Minneapolis", "Tampa", "Tulsa"
    };

    private static readonly string[] _states = {
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "FL", "GA", "HI", "ID", "IL", "IN", "IA",
        "KS", "KY", "LA", "ME", "MD", "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ",
        "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "RI", "SC", "SD", "TN", "TX", "UT", "VT",
        "VA", "WA", "WV", "WI", "WY"
    };

    private static readonly string[] _productCategories = {
        "Electronics", "Clothing", "Home & Garden", "Sports & Outdoors", "Books", "Toys & Games",
        "Health & Beauty", "Automotive", "Food & Beverages", "Office Supplies", "Pet Supplies",
        "Jewelry", "Tools & Hardware", "Music & Movies", "Baby & Kids"
    };

    private static readonly string[] _productBrands = {
        "Apple", "Samsung", "Nike", "Adidas", "Sony", "Microsoft", "Amazon", "Google",
        "Dell", "HP", "Canon", "Nikon", "Ford", "Toyota", "Honda", "BMW", "Mercedes",
        "Coca-Cola", "Pepsi", "McDonald's", "Starbucks", "Walmart", "Target", "Best Buy"
    };

    /// <summary>
    /// Generate a batch of customers with realistic data.
    /// </summary>
    public static List<Customer> GenerateCustomers(int count, int startId = 1)
    {
        var customers = new List<Customer>(count);

        for (int i = 0; i < count; i++)
        {
            var customer = new Customer
            {
                Id = startId + i,
                FirstName = _firstNames[_random.Next(_firstNames.Length)],
                LastName = _lastNames[_random.Next(_lastNames.Length)],
                Email = GenerateEmail(i + startId),
                Phone = GeneratePhoneNumber(),
                DateOfBirth = GenerateDateOfBirth(),
                CreatedAt = GenerateCreatedDate(),
                LastLoginAt = GenerateLastLoginDate(),
                IsActive = _random.NextDouble() > 0.1, // 90% active
                Status = GenerateCustomerStatus(),
                TotalSpent = GenerateTotalSpent(),
                OrderCount = _random.Next(0, 50),
                Notes = GenerateNotes(),
                Address = GenerateAddress(),
                City = _cities[_random.Next(_cities.Length)],
                State = _states[_random.Next(_states.Length)],
                ZipCode = GenerateZipCode(),
                Country = "USA"
            };

            customers.Add(customer);
        }

        return customers;
    }

    /// <summary>
    /// Generate orders for existing customers.
    /// </summary>
    public static List<Order> GenerateOrders(List<Customer> customers, int ordersPerCustomer = 3)
    {
        var orders = new List<Order>();
        int orderId = 1;

        foreach (var customer in customers)
        {
            var orderCount = _random.Next(0, ordersPerCustomer * 2); // 0 to 2x average
            
            for (int i = 0; i < orderCount; i++)
            {
                var order = new Order
                {
                    Id = orderId++,
                    CustomerId = customer.Id,
                    OrderDate = GenerateOrderDate(customer.CreatedAt),
                    TotalAmount = GenerateOrderAmount(),
                    Status = GenerateOrderStatus(),
                    ShippingAddress = GenerateShippingAddress(customer),
                    ShippedDate = GenerateShippedDate(),
                    DeliveredDate = GenerateDeliveredDate(),
                    TrackingNumber = GenerateTrackingNumber(),
                    Notes = GenerateOrderNotes()
                };

                orders.Add(order);
            }
        }

        return orders;
    }

    /// <summary>
    /// Generate products for the catalog.
    /// </summary>
    public static List<Product> GenerateProducts(int count)
    {
        var products = new List<Product>(count);

        for (int i = 0; i < count; i++)
        {
            var category = _productCategories[_random.Next(_productCategories.Length)];
            var brand = _productBrands[_random.Next(_productBrands.Length)];

            var product = new Product
            {
                Id = i + 1,
                Name = GenerateProductName(category, brand, i),
                Description = GenerateProductDescription(category),
                SKU = GenerateSKU(category, i),
                Price = GenerateProductPrice(category),
                StockQuantity = _random.Next(0, 1000),
                Category = category,
                Brand = brand,
                IsActive = _random.NextDouble() > 0.05, // 95% active
                CreatedAt = GenerateCreatedDate(),
                UpdatedAt = GenerateUpdatedDate()
            };

            products.Add(product);
        }

        return products;
    }

    /// <summary>
    /// Generate order items for existing orders and products.
    /// </summary>
    public static List<OrderItem> GenerateOrderItems(List<Order> orders, List<Product> products)
    {
        var orderItems = new List<OrderItem>();
        int itemId = 1;

        foreach (var order in orders)
        {
            var itemCount = _random.Next(1, 6); // 1-5 items per order
            var selectedProducts = products.OrderBy(x => _random.Next()).Take(itemCount);

            foreach (var product in selectedProducts)
            {
                var quantity = _random.Next(1, 4);
                var unitPrice = product.Price * (decimal)(_random.NextDouble() * 0.4 + 0.8); // ±20% price variation

                var orderItem = new OrderItem
                {
                    Id = itemId++,
                    OrderId = order.Id,
                    ProductId = product.Id,
                    Quantity = quantity,
                    UnitPrice = unitPrice,
                    TotalPrice = unitPrice * quantity
                };

                orderItems.Add(orderItem);
            }
        }

        return orderItems;
    }

    #region Private Helper Methods

    private static string GenerateEmail(int id)
    {
        var domains = new[] { "gmail.com", "yahoo.com", "hotmail.com", "outlook.com", "company.com" };
        return $"user{id}@{domains[_random.Next(domains.Length)]}";
    }

    private static string GeneratePhoneNumber()
    {
        return $"({_random.Next(200, 999)}) {_random.Next(200, 999)}-{_random.Next(1000, 9999)}";
    }

    private static DateTime GenerateDateOfBirth()
    {
        var minAge = 18;
        var maxAge = 80;
        var age = _random.Next(minAge, maxAge);
        return DateTime.UtcNow.AddYears(-age).AddDays(_random.Next(-365, 365));
    }

    private static DateTime GenerateCreatedDate()
    {
        return DateTime.UtcNow.AddDays(-_random.Next(1, 3650)); // Up to 10 years ago
    }

    private static DateTime? GenerateLastLoginDate()
    {
        return _random.NextDouble() > 0.2 ? DateTime.UtcNow.AddDays(-_random.Next(0, 90)) : null;
    }

    private static CustomerStatus GenerateCustomerStatus()
    {
        var statuses = Enum.GetValues<CustomerStatus>();
        var weights = new[] { 0.85, 0.10, 0.04, 0.01 }; // Active, Inactive, Suspended, Deleted
        
        var random = _random.NextDouble();
        var cumulative = 0.0;
        
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (random <= cumulative)
                return statuses[i];
        }
        
        return CustomerStatus.Active;
    }

    private static decimal GenerateTotalSpent()
    {
        return (decimal)(_random.NextDouble() * 10000); // $0 - $10,000
    }

    private static string? GenerateNotes()
    {
        return _random.NextDouble() > 0.7 ? $"Customer note {_random.Next(1000)}" : null;
    }

    private static string GenerateAddress()
    {
        return $"{_random.Next(1, 9999)} {GenerateStreetName()}";
    }

    private static string GenerateStreetName()
    {
        var streetNames = new[] { "Main St", "Oak Ave", "Pine Rd", "Elm Dr", "Maple Ln", "Cedar Ct", "Park Blvd" };
        return streetNames[_random.Next(streetNames.Length)];
    }

    private static string GenerateZipCode()
    {
        return _random.Next(10000, 99999).ToString();
    }

    private static DateTime GenerateOrderDate(DateTime customerCreatedDate)
    {
        var minDate = customerCreatedDate;
        var maxDate = DateTime.UtcNow;
        var range = (maxDate - minDate).Days;
        return minDate.AddDays(_random.Next(0, Math.Max(1, range)));
    }

    private static decimal GenerateOrderAmount()
    {
        return (decimal)(_random.NextDouble() * 500 + 10); // $10 - $510
    }

    private static OrderStatus GenerateOrderStatus()
    {
        var statuses = Enum.GetValues<OrderStatus>();
        var weights = new[] { 0.05, 0.10, 0.15, 0.60, 0.05, 0.05 }; // Distribution of order statuses
        
        var random = _random.NextDouble();
        var cumulative = 0.0;
        
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (random <= cumulative)
                return statuses[i];
        }
        
        return OrderStatus.Delivered;
    }

    private static string GenerateShippingAddress(Customer customer)
    {
        return _random.NextDouble() > 0.3 ? customer.Address : GenerateAddress();
    }

    private static DateTime? GenerateShippedDate()
    {
        return _random.NextDouble() > 0.2 ? DateTime.UtcNow.AddDays(-_random.Next(1, 30)) : null;
    }

    private static DateTime? GenerateDeliveredDate()
    {
        return _random.NextDouble() > 0.3 ? DateTime.UtcNow.AddDays(-_random.Next(1, 25)) : null;
    }

    private static string? GenerateTrackingNumber()
    {
        return _random.NextDouble() > 0.3 ? $"TRK{_random.Next(100000000, 999999999)}" : null;
    }

    private static string? GenerateOrderNotes()
    {
        return _random.NextDouble() > 0.8 ? $"Order note {_random.Next(1000)}" : null;
    }

    private static string GenerateProductName(string category, string brand, int index)
    {
        return $"{brand} {category} Product {index + 1}";
    }

    private static string GenerateProductDescription(string category)
    {
        return $"High-quality {category.ToLower()} product with excellent features and reliability.";
    }

    private static string GenerateSKU(string category, int index)
    {
        var categoryCode = category.Substring(0, Math.Min(3, category.Length)).ToUpper();
        return $"{categoryCode}-{index + 1:D6}";
    }

    private static decimal GenerateProductPrice(string category)
    {
        var basePrices = new Dictionary<string, (decimal min, decimal max)>
        {
            ["Electronics"] = (50, 2000),
            ["Clothing"] = (15, 200),
            ["Home & Garden"] = (10, 500),
            ["Sports & Outdoors"] = (20, 800),
            ["Books"] = (5, 50),
            ["Toys & Games"] = (10, 150),
            ["Health & Beauty"] = (5, 100),
            ["Automotive"] = (25, 1000),
            ["Food & Beverages"] = (2, 50),
            ["Office Supplies"] = (5, 200),
            ["Pet Supplies"] = (10, 100),
            ["Jewelry"] = (50, 5000),
            ["Tools & Hardware"] = (15, 500),
            ["Music & Movies"] = (10, 100),
            ["Baby & Kids"] = (10, 200)
        };

        if (basePrices.TryGetValue(category, out var range))
        {
            return (decimal)(_random.NextDouble() * (double)(range.max - range.min) + (double)range.min);
        }

        return (decimal)(_random.NextDouble() * 100 + 10); // Default range
    }

    private static DateTime? GenerateUpdatedDate()
    {
        return _random.NextDouble() > 0.5 ? DateTime.UtcNow.AddDays(-_random.Next(1, 365)) : null;
    }

    #endregion
}
