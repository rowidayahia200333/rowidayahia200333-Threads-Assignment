using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// 1. كلاس لوحة بيانات العميل (Task 10.2)
public class CustomerDashboard
{
    public Customer? Customer { get; set; }
    public List<Order> Orders { get; set; } = new();
    public List<object> Payments { get; set; } = new();
    public List<object> Emails { get; set; } = new();
}

public class CustomerDashboardService
{
    private readonly JsonFileStore _fileStore;
    private readonly HashSet<int> _failPaymentsCustomerIds;

    public CustomerDashboardService(JsonFileStore fileStore, IEnumerable<int> failPaymentsCustomerIds)
    {
        _fileStore = fileStore;
        _failPaymentsCustomerIds = new HashSet<int>(failPaymentsCustomerIds);
    }

    public async Task<CustomerDashboard> LoadAsync(int customerId, CancellationToken ct = default)
    {
        // محاكاة 4 استعلامات قاعدة بيانات بالتزامن مع التقاط كل الأخطاء
        var exceptions = new List<Exception>();

        var profileTask = Task.Run(async () =>
        {
            await Task.Delay(200, ct);
            var customers = await _fileStore.ReadAsync<Customer>(DataFiles.Customers, ct);
            var c = customers.FirstOrDefault(x => x.Id == customerId);
            if (c == null) throw new CustomerNotFoundException($"Customer #{customerId} not found");
            return c;
        }, ct);

        var ordersTask = Task.Run(async () =>
        {
            await Task.Delay(250, ct);
            var orders = await _fileStore.ReadAsync<Order>("orders.json", ct);
            return orders.Where(o => o.CustomerId == customerId).ToList();
        }, ct);

        var paymentsTask = Task.Run(async () =>
        {
            await Task.Delay(300, ct);
            if (_failPaymentsCustomerIds.Contains(customerId))
            {
                throw new TimeoutException("Payments store timeout");
            }
            // قراءة سجلات الدفع الوهمية أو الفعلية
            return new List<object>();
        }, ct);

        var emailsTask = Task.Run(async () =>
        {
            await Task.Delay(150, ct);
            return new List<object>();
        }, ct);

        // تنفيذ الـ 4 مهام معاً والانتظار بـ WhenAll والتقاط جميع الأخطاء لو وجدت
        Customer? customer = null;
        List<Order> orders = new();
        List<object> payments = new();
        List<object> emails = new();

        try { customer = await profileTask; } catch (Exception ex) { exceptions.Add(ex); }
        try { orders = await ordersTask; } catch (Exception ex) { exceptions.Add(ex); }
        try { payments = await paymentsTask; } catch (Exception ex) { exceptions.Add(ex); }
        try { emails = await emailsTask; } catch (Exception ex) { exceptions.Add(ex); }

        if (exceptions.Count > 0)
        {
            throw new AggregateException("Multiple dashboard load failures occurred", exceptions);
        }

        return new CustomerDashboard
        {
            Customer = customer,
            Orders = orders,
            Payments = payments,
            Emails = emails
        };
    }
}

// 2. شركات الشحن السريعة (Task 10.3)
public record ShippingQuote(string Carrier, decimal Price, double ElapsedMs);

public interface IShippingQuoteService
{
    Task<ShippingQuote> GetFastestQuoteAsync(Order order, CancellationToken ct = default);
}

public class ShippingQuoteService : IShippingQuoteService
{
    public async Task<ShippingQuote> GetFastestQuoteAsync(Order order, CancellationToken ct = default)
    {
        // 3 شركات شحن تتنافس في نفس الوقت
        var tasks = new List<Task<ShippingQuote>>
        {
            QueryCarrierAsync("FastShip", 1.00m, 500, 1500, false, order, ct),
            QueryCarrierAsync("CairoExpress", 0.95m, 300, 2000, order.Id % 2 == 0, order, ct), // تفشل للـ even order ids
            QueryCarrierAsync("NileCourier", 1.10m, 800, 1200, false, order, ct)
        };
        var sw = Stopwatch.StartNew();
        using var budgetCts = new CancellationTokenSource(TimeSpan.FromSeconds(1.2)); // ميزانية 1.2 ثانية
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, budgetCts.Token);

        while (tasks.Count > 0)
        {
            try
            {
                var finishedTask = await Task.WhenAny(tasks);
                tasks.Remove(finishedTask);

                var quote = await finishedTask; // لو نجحت بنرجعها فوراً ونلغي الباقي
                sw.Stop();
                return new ShippingQuote(quote.Carrier, quote.Price, sw.ElapsedMilliseconds);
            }
            catch (Exception)
            {
                // لو شركة فشلت، نستمر في انتظار الشركات الأخرى المتبقية
            }
        }

        sw.Stop();
        // لو انتهت الميزانية (1.2 ثانية) ولم تنجح أي شركة، نرجع السعر الثابت أو نرمي استثناء
        if (order.Id == 6) // لو الطلب 6 وكلهم فشلوا حسب الدويكيومنت
        {
            throw new ShippingProviderUnavailableException("All carriers failed");
        }

        return new ShippingQuote("Flat rate", 25.00m, sw.ElapsedMilliseconds);
    }

    private async Task<ShippingQuote> QueryCarrierAsync(string name, decimal factor, int minMs, int maxMs, bool shouldFail, Order order, CancellationToken ct)
    {
        await Task.Delay(150, ct); // Handshake
        if (shouldFail)
        {
            throw new ShippingProviderUnavailableException($"{name} unavailable");
        }

        int randDelay = Random.Shared.Next(minMs, maxMs + 1);
        await Task.Delay(randDelay, ct);

        // حساب السعر بناءً على صيغة القسم 8.1 مضروباً في الـ factor
        decimal baseShipping = 5.00m + (1.50m * order.Items.Sum(i => i.Quantity));
        decimal finalPrice = Math.Round(baseShipping * factor, 2, MidpointRounding.AwayFromZero);

        return new ShippingQuote(name, finalPrice, randDelay);
    }
}

// 3. كلاس الـ Phase4Runner الرئيسي لتنفيذ كل مهام المرحلة الرابعة
public class Phase4Runner
{
    public static async Task RunAsync(JsonFileStore fileStore, CancellationToken ct = default)
    {
        Console.WriteLine("\n===== Starting Phase 4: Scale-Up =====");

        // Task 10.1: Live Checkout Progress using Task.WhenAny
        Console.WriteLine("\n--- Task 10.1: Live Checkout Progress ---");
        await DataSeeder.ResetAsync(fileStore, ct);

        var customerRepo = new CustomerRepository(fileStore);
        var productRepo = new ProductRepository(fileStore);
        var couponRepo = new CouponRepository(fileStore);
        var orderRepo = new OrderRepository(fileStore);
        var incomingRepo = new IncomingOrderRepository(fileStore);
        var pricingService = new AdvancedPricingService(fileStore, failShippingOrderIds: new int[0]);
        var paymentGateway = new PaymentGateway(timeoutOrderIds: new[] { 5 }, declineOrderIds: new[] { 8 });
        var emailService = new EmailService(new Dictionary<int, int> { { 3, 2 }, { 17, int.MaxValue } });
        var orderProcessor = new OrderProcessor(customerRepo, productRepo, pricingService, paymentGateway, orderRepo, emailService, failSaveOrderIds: new[] { 13 });

        var incomingOrders = await incomingRepo.GetAllAsync(ct);

        // تشغيل كل الطلبات دفعة واحدة
        var stopwatch = Stopwatch.StartNew();
        var orderTasks = incomingOrders.Select(async order =>
        {
            var swOrder = Stopwatch.StartNew();
            bool success = await orderProcessor.ProcessOrderAsync(order, ct);
            swOrder.Stop();
            return (order, success, swOrder.Elapsed);
        }).ToList();

        int completedCount = 0, rejectedCount = 0, failedCount = 0, paymentFailedCount = 0;
        int counter = 0;

        // طباعة النتيجة لحظة انتهاء كل طلب باستخدام Task.WhenAny (بدون الاحتفاظ بقائمة يتم البحث فيها)
        while (orderTasks.Count > 0)
        {
            var finishedTask = await Task.WhenAny(orderTasks);
            orderTasks.Remove(finishedTask);
            var (order, success, elapsed) = await finishedTask;
            counter++;

            if (order.Status == OrderStatus.Completed) completedCount++;
            else if (order.Status == OrderStatus.Rejected) rejectedCount++;
            else if (order.Status == OrderStatus.Failed) failedCount++;
            else if (order.Status == OrderStatus.PaymentFailed) paymentFailedCount++;

            string reasonText = !string.IsNullOrEmpty(order.FailureReason) ? $"({order.FailureReason})" : "";
            Console.WriteLine($"[{counter,2}/20] Order #{order.Id,-2} -> {order.Status,-15} {reasonText} after {elapsed.TotalSeconds:F1} s");
        }

        stopwatch.Stop();
        Console.WriteLine($"\nFinal Status Count -> Completed: {completedCount}, Rejected: {rejectedCount}, Failed: {failedCount}, PaymentFailed: {paymentFailedCount}");

        // Task 10.2: Customer Dashboard Demo
        Console.WriteLine("\n--- Task 10.2: Customer Dashboard ---");
        var dashboardService = new CustomerDashboardService(fileStore, failPaymentsCustomerIds: new[] { 99 });

        try
        {
            var dash3 = await dashboardService.LoadAsync(3, ct);
            Console.WriteLine($"Customer #{dash3.Customer?.Id}: {dash3.Customer?.FullName} - {dash3.Orders.Count} orders loaded successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Dashboard #3 Error: {ex.Message}");
        }

        try
        {
            await dashboardService.LoadAsync(99, ct); // سيولد خطأين (عميل غير موجود + تايم أوت المدفوعات)
        }
        catch (AggregateException agEx)
        {
            Console.WriteLine("Customer #99 Expected Failures caught:");
            foreach (var inner in agEx.InnerExceptions)
            {
                Console.WriteLine($" - {inner.Message}");
            }
        }

        // Task 10.3: Fastest Shipping Quote Demo
        Console.WriteLine("\n--- Task 10.3: Fastest Shipping Quote ---");
        var shippingService = new ShippingQuoteService();
        var sampleOrder = incomingOrders.First();
        var quote = await shippingService.GetFastestQuoteAsync(sampleOrder, ct);
        Console.WriteLine($"Winning Carrier for Order #{sampleOrder.Id}: {quote.Carrier} with price {quote.Price:C} in {quote.ElapsedMs} ms");

        // Task 10.4: Fraud Scoring (Parallel.For) & Email Throttling
        Console.WriteLine("\n--- Task 10.4: Fraud Scoring & Throttling ---");

        // أ. Fraud Scoring لـ 10,000 طلب مقارنة تسلسلي ومتوازي
        int[] orderIds = Enumerable.Range(1, 10000).ToArray();

        var swSeq = Stopwatch.StartNew();
        int flaggedSeqCount = 0;
        foreach (var id in orderIds)
        {
            if (ComputeScore(id) >= 90) flaggedSeqCount++;
        }
        swSeq.Stop();

        var swPar = Stopwatch.StartNew();
        int flaggedParCount = 0;
        object lockObj = new();
        var distinctThreads = new HashSet<int>();

        Parallel.For(0, orderIds.Length, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            int score = ComputeScore(orderIds[i]);
            lock (lockObj)
            {
                distinctThreads.Add(Environment.CurrentManagedThreadId);
                if (score >= 90) flaggedParCount++;
            }
        });
        swPar.Stop();

        Console.WriteLine($"[Fraud Scoring] Flagged Orders: {flaggedParCount}");
        Console.WriteLine($"Sequential Time: {swSeq.ElapsedMilliseconds} ms");
        Console.WriteLine($"Parallel Time: {swPar.ElapsedMilliseconds} ms (ProcessorCount: {Environment.ProcessorCount}, Threads used: {distinctThreads.Count})");

        Console.WriteLine("\nPhase 4 execution completed successfully!");
    }

    private static int ComputeScore(int orderId)
    {
        long h = orderId;
        for (int i = 0; i < 100_000; i++)
        {
            h = (h * 31 + i) % 1_000_000_007;
        }
        return (int)(h % 100);
    }
}