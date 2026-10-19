using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class DiscountService
{
    private readonly JsonFileStore _fileStore;

    public DiscountService(JsonFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    private static async Task SimulateExternalApiDelayAsync(CancellationToken ct)
    {
        // External API (Discount, Shipping): DNS/handshake 150 -> Send request 300 -> Wait for response 500-1500
        await Task.Delay(150, ct);
        await Task.Delay(300, ct);
        await Task.Delay(Random.Shared.Next(500, 1501), ct);
    }

    public async Task<int> GetDiscountPercentageAsync(string? couponCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(couponCode))
            return 0;

        await SimulateExternalApiDelayAsync(ct);

        var coupons = await _fileStore.ReadAsync<Coupon>(DataFiles.Coupons, ct);
        var coupon = coupons.FirstOrDefault(c => c.Code.Equals(couponCode, StringComparison.OrdinalIgnoreCase));

        // التحقق مما إذا كان الكوبون موجوداً وصالحاً ولم تنته صلاحيته
        if (coupon == null || coupon.ExpiryDate < DateTime.Today)
        {
            return 0; // كوبون غير صالح أو منتهي
        }

        return coupon.DiscountPercent;
    }
}

public class OrderPricingService
{
    public (decimal subTotal, decimal discount, decimal shippingCost, decimal totalAmount) CalculatePricing(
        Order order,
        System.Collections.Generic.List<Product> products,
        int discountPercent)
    {
        decimal subTotal = 0;
        decimal totalWeight = 0;

        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product != null)
            {
                subTotal += product.Price * item.Quantity;
                totalWeight += product.WeightKg * item.Quantity;
            }
        }

        // حساب قيمة الخصم
        decimal discount = subTotal * discountPercent / 100m;

        // حساب سعر الشحن (مثلاً: 5 لكل كيلو أو قيمة ثابتة مبدئية)
        decimal shippingCost = totalWeight * 5.0m;

        decimal totalAmount = (subTotal - discount) + shippingCost;

        return (subTotal, discount, shippingCost, totalAmount);
    }
}