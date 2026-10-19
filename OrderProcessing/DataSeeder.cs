using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public static class DataSeeder
{
    public static async Task ResetAsync(JsonFileStore fileStore, CancellationToken cancellationToken = default)
    {
        // 1. حذف مجلد Data لو موجود بالكامل لإعادة ضبط النظام
        if (Directory.Exists(DataFiles.DataDirectory))
        {
            Directory.Delete(DataFiles.DataDirectory, true);
        }

        // إنشاء المجلدات من جديد
        DataFiles.EnsureDirectoriesExist();

        // 2. تجهيز بيانات العملاء (10 عملاء حسب الجدول في القسم 6.4)
        var customers = new List<Customer>
        {
            new() { Id = 1, Name = "Ahmed Ali", Email = "ahmed.ali@example.com", IsActive = true, Tier = LoyaltyTier.Gold },
            new() { Id = 2, Name = "Sara Mohamed", Email = "sara.mohamed@example.com", IsActive = true, Tier = LoyaltyTier.Silver },
            new() { Id = 3, Name = "Omar Hassan", Email = "omar.hassan@example.com", IsActive = true, Tier = LoyaltyTier.Bronze },
            new() { Id = 4, Name = "Mona Ibrahim", Email = "mona.ibrahim@example.com", IsActive = false, Tier = LoyaltyTier.Silver },
            new() { Id = 5, Name = "Youssef Adel", Email = "youssef.adel@example.com", IsActive = true, Tier = LoyaltyTier.Gold },
            new() { Id = 6, Name = "Nour Khaled", Email = "nour.khaled@example.com", IsActive = true, Tier = LoyaltyTier.Bronze },
            new() { Id = 7, Name = "Karim Samir", Email = "karim.samir@example.com", IsActive = true, Tier = LoyaltyTier.Silver },
            new() { Id = 8, Name = "Laila Mahmoud", Email = "laila.mahmoud@example.com", IsActive = true, Tier = LoyaltyTier.Gold },
            new() { Id = 9, Name = "Hassan Tarek", Email = "hassan.tarek@example.com", IsActive = true, Tier = LoyaltyTier.Bronze },
            new() { Id = 10, Name = "Dina Fathy", Email = "dina.fathy@example.com", IsActive = true, Tier = LoyaltyTier.Silver }
        };
        await fileStore.WriteAsync(DataFiles.Customers, customers, cancellationToken);
        Console.WriteLine($"[Seeder] {DataFiles.Customers} -> {customers.Count} records");

        // 3. تجهيز بيانات المنتجات (8 منتجات حسب الجدول في القسم 6.4)
        var products = new List<Product>
        {
            new() { Id = 1, Name = "Laptop", Price = 1200.00m, StockQuantity = 10, WeightKg = 2.2m },
            new() { Id = 2, Name = "Smartphone", Price = 800.00m, StockQuantity = 15, WeightKg = 0.4m },
            new() { Id = 3, Name = "Wireless Mouse", Price = 25.00m, StockQuantity = 2, WeightKg = 0.1m },
            new() { Id = 4, Name = "Mechanical Keyboard", Price = 90.00m, StockQuantity = 1, WeightKg = 0.9m },
            new() { Id = 5, Name = "USB-C Hub", Price = 45.00m, StockQuantity = 30, WeightKg = 0.2m },
            new() { Id = 6, Name = "Headphones", Price = 150.00m, StockQuantity = 12, WeightKg = 0.3m },
            new() { Id = 7, Name = "27\" Monitor", Price = 350.00m, StockQuantity = 6, WeightKg = 5.0m },
            new() { Id = 8, Name = "Webcam", Price = 70.00m, StockQuantity = 20, WeightKg = 0.2m }
        };
        await fileStore.WriteAsync(DataFiles.Products, products, cancellationToken);
        Console.WriteLine($"[Seeder] {DataFiles.Products} -> {products.Count} records");

        // 4. تجهيز بيانات الكوبونات (4 كوبونات حسب القسم 6.4)
        var coupons = new List<Coupon>
        {
            new() { Code = "SAVE10", DiscountPercent = 10, ExpiryDate = DateTime.Today.AddDays(180) },
            new() { Code = سينرمي("WELCOME15"), DiscountPercent = 15, ExpiryDate = DateTime.Today.AddDays(90) },
            new() { Code = "FLASH20", DiscountPercent = 20, ExpiryDate = DateTime.Today.AddDays(7) },
            new() { Code = "OLD20", DiscountPercent = 20, ExpiryDate = DateTime.Today.AddDays(-30) } // منتهي الصلاحية
        };
        await fileStore.WriteAsync(DataFiles.Coupons, coupons, cancellationToken);
        Console.WriteLine($"[Seeder] {DataFiles.Coupons} -> {coupons.Count} records");
        // 5. تجهيز الطلبات الواردة (Incoming Orders - الـ 20 طلب حسب القسم 6.4)
        var incomingOrders = GetSampleIncomingOrders();
        await fileStore.WriteAsync(DataFiles.IncomingOrders, incomingOrders, cancellationToken);
        Console.WriteLine($"[Seeder] {DataFiles.IncomingOrders} -> {incomingOrders.Count} records");

        // الملفات اللي بتدأ فاضية بنعملها [] (لأن المتطلب بتقول أن المجموعة الفارغة بتتخزن كـ [])
        await fileStore.WriteAsync(DataFiles.Orders, new List<Order>(), cancellationToken);
        await fileStore.WriteAsync(DataFiles.Payments, new List<PaymentTransaction>(), cancellationToken);
        await fileStore.WriteAsync(DataFiles.Emails, new List<EmailMessage>(), cancellationToken);
        await fileStore.WriteAsync(DataFiles.EmailRetry, new List<EmailMessage>(), cancellationToken);
    }

    // دالة مساعدة لتوليد الـ 20 طلب الواردة بدقة طبقاً للجداول في الدويكيومنت
    private static List<Order> GetSampleIncomingOrders()
    {
        var orders = new List<Order>();

        // هنا بنضيف الطلبات الـ 20 بالترتيب زي ما الجدول شارحها في الصورة رقم 5 و 6
        // (Order 1 to 20)
        void AddOrder(int id, int customerId, string? coupon, List<OrderItem> items)
        {
            orders.Add(new Order
            {
                Id = id,
                CustomerId = customerId,
                CouponCode = coupon,
                Items = items,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.Now
            });
        }

        AddOrder(1, 1, "SAVE10", [new() { ProductId = 1, Quantity = 1 }, new() { ProductId = 3, Quantity = 1 }]);
        AddOrder(2, 2, null, [new() { ProductId = 2, Quantity = 1 }]);
        AddOrder(3, 3, "WELCOME15", [new() { ProductId = 5, Quantity = 2 }, new() { ProductId = 8, Quantity = 1 }]);
        AddOrder(4, 4, null, [new() { ProductId = 6, Quantity = 1 }]);
        AddOrder(5, 5, null, [new() { ProductId = 7, Quantity = 1 }]);
        AddOrder(6, 6, "FLASH20", [new() { ProductId = 5, Quantity = 1 }]);
        AddOrder(7, 7, null, [new() { ProductId = 6, Quantity = 1 }, new() { ProductId = 8, Quantity = 1 }]);
        AddOrder(8, 8, "SAVE10", [new() { ProductId = 1, Quantity = 1 }]);
        AddOrder(9, 9, null, [new() { ProductId = 3, Quantity = 5 }]);
        AddOrder(10, 10, null, [new() { ProductId = 4, Quantity = 1 }]);

        AddOrder(11, 1, null, [new() { ProductId = 4, Quantity = 1 }]);
        AddOrder(12, 2, "OLD20", [new() { ProductId = 2, Quantity = 1 }]);
        AddOrder(13, 3, null, [new() { ProductId = 6, Quantity = 2 }]);
        AddOrder(14, 5, "WELCOME15", [new() { ProductId = 8, Quantity = 2 }, new() { ProductId = 5, Quantity = 1 }]);
        AddOrder(15, 99, null, [new() { ProductId = 2, Quantity = 1 }]); // عميل غير موجود
        AddOrder(16, 6, null, [new() { ProductId = 7, Quantity = 1 }, new() { ProductId = 5, Quantity = 1 }]);
        AddOrder(17, 7, "SAVE10", [new() { ProductId = 2, Quantity = 1 }]);
        AddOrder(18, 8, "FAKE50", [new() { ProductId = 6, Quantity = 1 }]); // كوبون وهمي
        AddOrder(19, 9, null, [new() { ProductId = 8, Quantity = 1 }]);
        AddOrder(20, 10, "FLASH20", [new() { ProductId = 1, Quantity = 1 }, new() { ProductId = 6, Quantity = 1 }]);

        return orders;
    }
}