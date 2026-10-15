using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class Phase2Runner
{
    public static async Task RunAsync(JsonFileStore fileStore, CancellationToken ct = default)
    {
        Console.WriteLine("\n===== Starting Phase 2: Concurrent Processing =====");

        // 1. إعادة ضبط البيانات
        await DataSeeder.ResetAsync(fileStore, ct);

        // 2. تجربة الـ Race Condition (Task 8.3)
        Console.WriteLine("\n--- Running Race Condition Demo (Task 8.3) ---");
        var prodRepoForDemo = new ProductRepository(fileStore);

        // ضبط مخزون المنتج رقم 4 إلى 1 للتحقق من الـ Race Condition
        var products = await prodRepoForDemo.GetAllAsync(ct);
        var p4 = products.First(p => p.Id == 4);
        p4.StockQuantity = 1;
        await prodRepoForDemo.UpdateAsync(products, ct);

        // محاكاة حجز غير آمن بين طلبين محتملين لو تحب، أو التركيز على الحجز الآمن
        bool res1 = await prodRepoForDemo.ReserveStockAsync(4, 1, ct);
        bool res2 = await prodRepoForDemo.ReserveStockAsync(4, 1, ct);
        Console.WriteLine($"[Race Demo] Order A reservation result: {res1}");
        Console.WriteLine($"[Race Demo] Order B reservation result: {res2}");

        // 3. إعادة الضبط والبدء بمعالجة الطلبات الفعليّة
        await DataSeeder.ResetAsync(fileStore, ct);

        var customerRepo = new CustomerRepository(fileStore);
        var productRepo = new ProductRepository(fileStore);
        var couponRepo = new CouponRepository(fileStore);
        var orderRepo = new OrderRepository(fileStore);
        var incomingRepo = new IncomingOrderRepository(fileStore);

        var validator = new OrderValidator(customerRepo, productRepo, couponRepo);
        var advancedPricing = new AdvancedPricingService(fileStore, failShippingOrderIds: new[] { 7 }); // الطلب 7 يفشل فيه الشحن
        var invoiceGen = new InvoiceGenerator(fileStore);
        var processorV2 = new OrderProcessorV2(fileStore, productRepo, advancedPricing, invoiceGen, orderRepo);

        var incomingOrders = await incomingRepo.GetAllAsync(ct);

        var sw = Stopwatch.StartNew();

        // أولاً: التحقق من كل الطلبات بالتزامن وحفظها
        var validationTasks = incomingOrders.mapAsync(async order =>
        {
            var (status, reason) = await validator.ValidateOrderAsync(order, ct);
            order.Status = status;
            order.FailureReason = reason;
            await orderRepo.SaveAsync(order, ct);
            return order;
        });

        // تنفيذ الـ Validation للكل بالتزامن
        var validatedOrRejectedOrders = await Task.WhenAll(incomingOrders.Select(async order =>
        {
            var (status, reason) = await validator.ValidateOrderAsync(order, ct);
            order.Status = status;
            order.FailureReason = reason;
            await orderRepo.SaveAsync(order, ct);
            return order;
        }));

        // ثانياً: معالجة الطلبات Validated بالتزامن (Task 8.4)
        var validatedOrders = validatedOrRejectedOrders.Where(o => o.Status == OrderStatus.Validated).ToList();

        Console.WriteLine($"\n[Processor] Processing {validatedOrders.Count} validated orders concurrently...");

        var processingTasks = new List<Task<bool>>();
        foreach (var order in validatedOrders)
        {
            // تشغيل المهمة دون await فوراً لتنفيذها بشكل متزامن
            var task = Task.Run(async () =>
            {
                try
                {
                    return await processorV2.ProcessValidatedOrderAsync(order, ct);
                }
                catch (ShippingProviderUnavailableException ex)
                {
                    order.Status = OrderStatus.Failed;
                    order.FailureReason = ex.Message;
                    await orderRepo.SaveAsync(order, ct);
                    return false;
                }
            }, ct);

            processingTasks.Add(task);
        }
        // طباعة حالة أول مهمة فور إنشائها (Task 8.4 - requirement 3)
        if (processingTasks.Count > 0)
        {
            Console.WriteLine($"[Status Check] Task[0] status right after creation: {processingTasks.Status}");
        }

        // انتظار ثانية ثم طباعة حالة المهام أثناء التشغيل
        await Task.Delay(1000, ct);
        for (int i = 0; i < processingTasks.Count; i++)
        {
            Console.WriteLine($"[Status Check] Task[{i}] status while running: {processingTasks[i].Status}");
        }

        // الانتظار حتى تنتهي كل المهام مع التعامل مع الأخطاء الفردية من خلال Try-Catch لكل مهمة في الـ WhenAll أو عبر الفحص
        try
        {
            await Task.WhenAll(processingTasks);
        }
        catch (Exception)
        {
            // الاستثناءات تم معالجتها داخلياً أو عبر الـ catch لكل مهام الشحن
        }

        sw.Stop();

        // 4. توليد التقرير النهائي (Task 8.6)
        var reportGen = new ReportGenerator(orderRepo, productRepo);
        Console.WriteLine("\n[Report] Generating report...");
        var report = await reportGen.GenerateAsync(ct);

        Console.WriteLine("\n===== Final Phase 2 Execution Report =====");
        Console.WriteLine($"Total Orders: {report.TotalOrders}");
        Console.WriteLine($"Completed:    {report.CompletedCount}");
        Console.WriteLine($"Rejected:     {report.RejectedCount}");
        Console.WriteLine($"Failed:       {report.FailedCount}");
        Console.WriteLine($"Total Revenue: {report.TotalRevenue:C}");
        Console.WriteLine($"Average Order Value: {report.AverageOrderValue:C}");
        Console.WriteLine($"Top Selling Product: {report.TopSellingProduct}");
        Console.WriteLine($"Total Execution Time: {sw.Elapsed.TotalSeconds:F1} seconds");
    }
}