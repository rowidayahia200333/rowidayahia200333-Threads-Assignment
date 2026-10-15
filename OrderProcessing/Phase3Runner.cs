using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class Phase3Runner
{
    public static async Task RunAsync(JsonFileStore fileStore, CancellationToken ct = default)
    {
        Console.WriteLine("\n===== Starting Phase 3: Full Checkout Workflow =====");

        // 1. إعادة ضبط البيانات للبدء من جديد
        await DataSeeder.ResetAsync(fileStore, ct);

        // 2. إعداد الخدمات والمستودعات
        var customerRepo = new CustomerRepository(fileStore);
        var productRepo = new ProductRepository(fileStore);
        var couponRepo = new CouponRepository(fileStore);
        var orderRepo = new OrderRepository(fileStore);
        var incomingRepo = new IncomingOrderRepository(fileStore);

        var pricingService = new AdvancedPricingService(fileStore, failShippingOrderIds: new List<int>());

        // تكوين أخطاء الدفع (الطلب رقم 5 تايم أوت، الطلب رقم 8 مرفوض)[span_0](start_span)[span_0](end_span)
        var paymentGateway = new PaymentGateway(timeoutOrderIds: new[] { 5 }, declineOrderIds: new[] { 8 });

        // تكوين محاولات البريد الفاشلة (الطلب رقم 3 يفشل مرتين ثم ينجح، الطلب رقم 17 يفشل دائماً)[span_1](start_span)[span_1](end_span)
        var emailService = new EmailService(new Dictionary<int, int> { { 3, 2 }, { 17, int.MaxValue } });

        // تكوين فشل الحفظ بقاعدة البيانات بعد الدفع (الطلب رقم 13)[span_2](start_span)[span_2](end_span)
        var failSaveOrderIds = new[] { 13 };

        var orderProcessor = new OrderProcessor(
            customerRepo,
            productRepo,
            pricingService,
            paymentGateway,
            orderRepo,
            emailService,
            failSaveOrderIds
        );

        var incomingOrders = await incomingRepo.GetAllAsync(ct);

        Console.WriteLine($"\n[Processor] Processing all {incomingOrders.Count} orders concurrently...");

        var sw = Stopwatch.StartNew();

        // 3. تشغيل جميع الطلبات بالتزامن (Concurrent Checkout)[span_3](start_span)[span_3](end_span)
        var processingTasks = incomingOrders.Select(async order =>
        {
            try
            {
                return await orderProcessor.ProcessOrderAsync(order, ct);
            }
            catch (Exception ex)
            {
                order.Status = OrderStatus.Failed;
                order.FailureReason = ex.Message;
                await orderRepo.SaveAsync(order, ct);
                return false;
            }
        }).ToList();

        await Task.WhenAll(processingTasks);
        sw.Stop();

        // 4. جلب الطلبات النهائية وطباعة الجدول المطلوب
        var finalOrders = await orderRepo.GetAllAsync(ct);

        Console.WriteLine("\n========================= Final Checkout Results =========================");
        Console.WriteLine($"{"Order ID",-10} | {"Status",-15} | {"Total Amount",-12} | {"Failure Reason / Details"}");
        Console.WriteLine(new string('-', 85));

        foreach (var order in finalOrders.OrderBy(o => o.Id))
        {
            string totalStr = order.TotalAmount > 0 ? $"{order.TotalAmount:C}" : "N/A";
            Console.WriteLine($"#{order.Id,-9} | {order.Status,-15} | {totalStr,-12} | {order.FailureReason}");
        }

        Console.WriteLine(new string('-', 85));
        Console.WriteLine($"Total Execution Time: {sw.Elapsed.TotalSeconds:F1} seconds");
        Console.WriteLine("Phase 3 execution completed successfully and JSON files are updated!");
    }
}