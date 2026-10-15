using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// النسخ المتزامنة (Sync) المطلوبة في Task 7.3 (R1)
public class SyncCustomerRepository
{
    private readonly JsonFileStore _fileStore;
    public SyncCustomerRepository(JsonFileStore fileStore) => _fileStore = fileStore;

    public Customer? GetById(int id)
    {
        Thread.Sleep(200 + Random.Shared.Next(300, 601));
        var task = _fileStore.ReadAsync<Customer>(DataFiles.Customers);
        task.Wait();
        return task.Result.FirstOrDefault(c => c.Id == id);
    }
}

public class SyncProductRepository
{
    private readonly JsonFileStore _fileStore;
    public SyncProductRepository(JsonFileStore fileStore) => _fileStore = fileStore;

    public Product? GetById(int id)
    {
        Thread.Sleep(200 + Random.Shared.Next(300, 601));
        var task = _fileStore.ReadAsync<Product>(DataFiles.Products);
        task.Wait();
        return task.Result.FirstOrDefault(p => p.Id == id);
    }
}

public class SyncCouponRepository
{
    private readonly JsonFileStore _fileStore;
    public SyncCouponRepository(JsonFileStore fileStore) => _fileStore = fileStore;

    public Coupon? GetByCode(string code)
    {
        Thread.Sleep(200 + Random.Shared.Next(300, 601));
        var task = _fileStore.ReadAsync<Coupon>(DataFiles.Coupons);
        task.Wait();
        return task.Result.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }
}

// الـ Runner الرئيسي لتنفيذ ومقارنة الـ Sync والـ Async (Task 7.3)
public class Phase1Runner
{
    public static async Task RunAsync(JsonFileStore fileStore, CancellationToken ct = default)
    {
        // 1. تشغيل الـ Sync Run
        await DataSeeder.ResetAsync(fileStore, ct);
        var incomingRepo = new IncomingOrderRepository(fileStore);
        var ordersToProcess = await incomingRepo.GetAllAsync(ct);

        var swSync = Stopwatch.StartNew();

        var syncCustRepo = new SyncCustomerRepository(fileStore);
        var syncProdRepo = new SyncProductRepository(fileStore);
        var syncCouponRepo = new SyncCouponRepository(fileStore);
        var orderRepo = new OrderRepository(fileStore);

        foreach (var order in ordersToProcess)
        {
            ct.ThrowIfCancellationRequested();
            // فحص متزامن بسيط
            var customer = syncCustRepo.GetById(order.CustomerId);
            if (customer == null)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Customer #{order.CustomerId} not found";
            }
            else if (!customer.IsActive)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = "Customer is inactive";
            }
            else
            {
                bool hasError = false;
                foreach (var item in order.Items)
                {
                    var prod = syncProdRepo.GetById(item.ProductId);
                    if (prod == null)
                    {
                        order.Status = OrderStatus.Rejected;
                        order.FailureReason = $"Product #{item.ProductId} not found";
                        hasError = true;
                        break;
                    }
                    if (prod.StockQuantity < item.Quantity)
                    {
                        order.Status = OrderStatus.Rejected;
                        order.FailureReason = $"Insufficient stock for '{prod.Name}'";
                        hasError = true;
                        break;
                    }
                }
                if (!hasError)
                {
                    if (!string.IsNullOrWhiteSpace(order.CouponCode))
                    {
                        var coupon = syncCouponRepo.GetByCode(order.CouponCode);
                        if (coupon == null)
                        {
                            order.Status = OrderStatus.Rejected;
                            order.FailureReason = $"Coupon '{order.CouponCode}' does not exist";
                        }
                        else if (coupon.ExpiryDate < DateTime.Today)
                        {
                            order.Status = OrderStatus.Rejected;
                            order.FailureReason = $"Coupon '{order.CouponCode}' has expired";
                        }
                        else
                        {
                            order.Status = OrderStatus.Validated;
                        }
                    }
                    else
                    {
                        order.Status = OrderStatus.Validated;
                    }
                }
            }
            // حفظ متزامن
            var saveTask = orderRepo.SaveAsync(order, ct);
            saveTask.Wait();
        }
        swSync.Stop();
        double syncTime = swSync.Elapsed.TotalSeconds;

        // 2. تشغيل الـ Async Run
        await DataSeeder.ResetAsync(fileStore, ct);
        ordersToProcess = await incomingRepo.GetAllAsync(ct);

        var swAsync = Stopwatch.StartNew();

        var customerRepo = new CustomerRepository(fileStore);
        var productRepo = new ProductRepository(fileStore);
        var couponRepo = new CouponRepository(fileStore);
        var validator = new OrderValidator(customerRepo, productRepo, couponRepo);

        foreach (var order in ordersToProcess)
        {
            ct.ThrowIfCancellationRequested();
            var (status, reason) = await validator.ValidateOrderAsync(order, ct);
            order.Status = status;
            order.FailureReason = reason;

            await orderRepo.SaveAsync(order, ct);
        }
        swAsync.Stop();
        double asyncTime = swAsync.Elapsed.TotalSeconds;

        // 3. طباعة الـ Summary المطلوب تماماً من orders.json (R3)
        var finalOrders = await orderRepo.GetAllAsync(ct);
        int totalOrders = finalOrders.Count;
        int validatedCount = finalOrders.Count(o => o.Status == OrderStatus.Validated);
        int rejectedCount = finalOrders.Count(o => o.Status == OrderStatus.Rejected);

        Console.WriteLine("\n===== Validation Summary =====");
        Console.WriteLine($"Total Orders: {totalOrders}");
        Console.WriteLine($"Validated:    {validatedCount}");
        Console.WriteLine($"Rejected:     {rejectedCount}");

        foreach (var rejected in finalOrders.Where(o => o.Status == OrderStatus.Rejected))
        {
            Console.WriteLine($" - Order #{rejected.Id}: {rejected.FailureReason}");
        }

        Console.WriteLine($"Sync Time: {syncTime:F1} seconds");
        Console.WriteLine($"Async Time: {asyncTime:F1} seconds");
        Console.WriteLine($"orders.json now contains {totalOrders} records.");
    }
}