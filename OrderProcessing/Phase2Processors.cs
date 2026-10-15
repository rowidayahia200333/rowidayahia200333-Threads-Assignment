using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class OrderProcessorV2
{
    private readonly JsonFileStore _fileStore;
    private readonly ProductRepository _productRepo;
    private readonly AdvancedPricingService _pricingService;
    private readonly InvoiceGenerator _invoiceGenerator;
    private readonly OrderRepository _orderRepo;

    public OrderProcessorV2(
        JsonFileStore fileStore,
        ProductRepository productRepo,
        AdvancedPricingService pricingService,
        InvoiceGenerator invoiceGenerator,
        OrderRepository orderRepo)
    {
        _fileStore = fileStore;
        _productRepo = productRepo;
        _pricingService = pricingService;
        _invoiceGenerator = invoiceGenerator;
        _orderRepo = orderRepo;
    }

    // يعالج الطلب Validated ويعيد True عند النجاح أو False عند الفشل دون التقاط الاستثناءات داخلياً (تُترك للمستدعي)
    public async Task<bool> ProcessValidatedOrderAsync(Order order, CancellationToken ct = default)
    {
        var reservedItems = new List<(int productId, int qty)>();

        try
        {
            // 1. حجز المخزون لكل منتج بالترتيب باستخدام الحجز الآمن
            foreach (var item in order.Items)
            {
                bool success = await _productRepo.ReserveStockAsync(item.ProductId, item.Quantity, ct);
                if (!success)
                {
                    // لو فشل، نعمل إرجاع (Release) لكل اللي اتأمن قبل كده
                    foreach (var reserved in reservedItems)
                    {
                        await _productRepo.ReleaseStockAsync(reserved.productId, reserved.qty, ct);
                    }

                    var products = await _productRepo.GetAllAsync(ct);
                    var prod = products.FirstOrDefault(p => p.Id == item.ProductId);
                    order.Status = OrderStatus.Rejected;
                    order.FailureReason = $"Insufficient stock for '{prod?.Name ?? item.ProductId.ToString()}'";
                    await _orderRepo.SaveAsync(order, ct);
                    return false;
                }
                reservedItems.Add((item.ProductId, item.Quantity));
            }

            // 2. حساب الأسعار (SubTotal, Discount, Shipping) بالتزامن (Concurrent Awaits)
            var subTotalTask = _pricingService.CalculateSubTotalAsync(order, ct);
            var discountPercentTask = _pricingService.GetDiscountAsync(order, ct);

            await Task.WhenAll(subTotalTask, discountPercentTask);

            decimal subTotal = await subTotalTask;
            decimal discountPercent = await discountPercentTask;
            decimal discount = Math.Round(subTotal * discountPercent / 100m, 2, MidpointRounding.AwayFromZero);

            // حساب الشحن (قد يرمي ShippingProviderUnavailableException للطلب رقم 7 مثلاً)
            decimal shippingCost = await _pricingService.CalculateShippingCostAsync(order, subTotal, ct);

            decimal totalAmount = Math.Round(subTotal - discount + shippingCost, 2, MidpointRounding.AwayFromZero);

            order.SubTotal = subTotal;
            order.Discount = discount;
            order.ShippingCost = shippingCost;
            order.TotalAmount = totalAmount;

            // 3. توليد الفاتورة (Task.Run)
            await _invoiceGenerator.GenerateAsync(order, ct);

            // 4. تعيين الحالة إلى Completed وحفظ الطلب
            order.Status = OrderStatus.Completed;
            await _orderRepo.SaveAsync(order, ct);
            return true;
        }
        catch (Exception)
        {
            // لو حصل استثناء (مثل فشل شركة الشحن)، يتم إرجاع المخزون المحجوز
            foreach (var reserved in reservedItems)
            {
                await _productRepo.ReleaseStockAsync(reserved.productId, reserved.qty, ct);
            }
            throw; // إعادة رمي الاستثناء ليتم التعامل معه في الـ Runner
        }
    }
}

// كلاس التقرير النهائي (Task 8.6)
public class Report
{
    public int TotalOrders { get; set; }
    public int CompletedCount { get; set; }
    public int RejectedCount { get; set; }
    public int FailedCount { get; set; }
    public int PaymentFailedCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public string TopSellingProduct { get; set; } = string.Empty;
}

public class ReportGenerator
{
    private readonly OrderRepository _orderRepo;
    private readonly ProductRepository _productRepo;

    public ReportGenerator(OrderRepository orderRepo, ProductRepository productRepo)
    {
        _orderRepo = orderRepo;
        _productRepo = productRepo;
    }

    public async Task<Report> GenerateAsync(CancellationToken ct = default)
    {
        var orders = await _orderRepo.GetAllAsync(ct);
        var products = await _productRepo.GetAllAsync(ct);

        int completed = orders.Count(o => o.Status == OrderStatus.Completed);
        int rejected = orders.Count(o => o.Status == OrderStatus.Rejected);
        int failed = orders.Count(o => o.Status == OrderStatus.Failed);
        int paymentFailed = orders.Count(o => o.Status == OrderStatus.PaymentFailed);

        decimal totalRevenue = orders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.TotalAmount);
        decimal avgOrderValue = completed > 0 ? Math.Round(totalRevenue / completed, 2, MidpointRounding.AwayFromZero) : 0;

        // حساب المنتج الأكثر مبيعاً
        var productSales = new Dictionary<int, int>();
        foreach (var order in orders.Where(o => o.Status == OrderStatus.Completed))
        {
            foreach (var item in order.Items)
            {
                if (!productSales.ContainsKey(item.ProductId))
                    productSales[item.ProductId] = 0;
                productSales[item.ProductId] += item.Quantity;
            }
        }

        string topProductStr = "N/A";
        if (productSales.Count > 0)
        {
            int topProductId = productSales.OrderByDescending(kvp => kvp.Value).ThenBy(kvp => kvp.Key).First().Key;
            int topQty = productSales[topProductId];
            var topProdObj = products.FirstOrDefault(p => p.Id == topProductId);
            topProductStr = $"{topProdObj?.Name ?? topProductId.ToString()}, {topQty} units";
        }

        var report = new Report
        {
            TotalOrders = orders.Count,
            CompletedCount = completed,
            RejectedCount = rejected,
            FailedCount = failed,
            PaymentFailedCount = paymentFailed,
            TotalRevenue = totalRevenue,
            AverageOrderValue = avgOrderValue,
            TopSellingProduct = topProductStr
        };

        // حفظ التقرير في المجلد المطلوب بصيغة JSON مع ختم زمني
        string dir = Path.Combine("Data", "reports");
        Directory.CreateDirectory(dir);
        string fileName = $"report_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        string filePath = Path.Combine(dir, fileName);

        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);

        return report;
    }
}