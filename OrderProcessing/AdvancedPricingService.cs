using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class ShippingProviderUnavailableException : Exception
{
    public ShippingProviderUnavailableException(string message) : base(message) { }
}

public class AdvancedPricingService
{
    private readonly JsonFileStore _fileStore;
    private readonly HashSet<int> _failShippingOrderIds;

    public AdvancedPricingService(JsonFileStore fileStore, IEnumerable<int> failShippingOrderIds)
    {
        _fileStore = fileStore;
        _failShippingOrderIds = new HashSet<int>(failShippingOrderIds);
    }

    // حساب الـ SubTotal مع محاكاة DB query
    public async Task<decimal> CalculateSubTotalAsync(Order order, CancellationToken ct = default)
    {
        await Task.Delay(200, ct);
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var products = await _fileStore.ReadAsync<Product>(DataFiles.Products, ct);
        decimal subTotal = 0;

        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product != null)
            {
                item.UnitPrice = product.Price; // تعيين سعر الوحدة
                subTotal += product.Price * item.Quantity;
            }
        }
        return Math.Round(subTotal, 2, MidpointRounding.AwayFromZero);
    }

    // حساب الخصم مع محاكاة Discount API
    public async Task<decimal> GetDiscountAsync(Order order, CancellationToken ct = default)
    {
        await Task.Delay(150, ct);
        await Task.Delay(300, ct);
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        var customers = await _fileStore.ReadAsync<Customer>(DataFiles.Customers, ct);
        var customer = customers.FirstOrDefault(c => c.Id == order.CustomerId);

        int loyaltyPercent = customer?.Tier switch
        {
            LoyaltyTier.Silver => 5,
            LoyaltyTier.Gold => 10,
            _ => 0
        };

        int couponPercent = 0;
        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            var coupons = await _fileStore.ReadAsync<Coupon>(DataFiles.Coupons, ct);
            var coupon = coupons.FirstOrDefault(c => c.Code.Equals(order.CouponCode, StringComparison.OrdinalIgnoreCase));
            if (coupon != null && coupon.ExpiryDate >= DateTime.Today)
            {
                couponPercent = coupon.DiscountPercent;
            }
        }

        int totalDiscountPercent = Math.Min(loyaltyPercent + couponPercent, 30); // بحد أقصى 30%[span_13](start_span)[span_13](end_span)
        return totalDiscountPercent;
    }

    // حساب الشحن مع محاكاة Shipping API
    public async Task<decimal> CalculateShippingCostAsync(Order order, decimal subTotal, CancellationToken ct = default)
    {
        // التحقق مما إذا كان الطلب مدرجاً في قائمة فشل الشحن (مثل الطلب رقم 7)[span_14](start_span)[span_14](end_span)
        if (_failShippingOrderIds.Contains(order.Id))
        {
            // محاكاة خطوة الـ handshake أولاً ثم رمي الاستثناء[span_15](start_span)[span_15](end_span)
            await Task.Delay(150, ct);
            throw new ShippingProviderUnavailableException("Shipping provider unavailable");
        }

        await Task.Delay(150, ct);
        await Task.Delay(300, ct);
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        if (subTotal >= 1000m) return 0m; [span_16] (start_span)[span_16](end_span)

        var products = await _fileStore.ReadAsync<Product>(DataFiles.Products, ct);
        int totalQty = order.Items.Sum(i => i.Quantity);
        decimal totalWeight = 0;

        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product != null)
            {
                totalWeight += product.WeightKg * item.Quantity;
            }
        }
        decimal shippingCost = 5.00m + (1.50m * totalQty) + (2.00m * totalWeight); [span_17] (start_span)[span_17](end_span)
        return Math.Round(shippingCost, 2, MidpointRounding.AwayFromZero);
    }
}