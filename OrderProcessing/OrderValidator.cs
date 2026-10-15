using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class OrderValidator
{
    private readonly CustomerRepository _customerRepo;
    private readonly ProductRepository _productRepo;
    private readonly CouponRepository _couponRepo;

    public OrderValidator(
        CustomerRepository customerRepo,
        ProductRepository productRepo,
        CouponRepository couponRepo)
    {
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _couponRepo = couponRepo;
    }

    public async Task<(OrderStatus status, string? failureReason)> ValidateOrderAsync(Order order, CancellationToken ct = default)
    {
        // 1. التحقق من وجود العميل
        var customer = await _customerRepo.GetByIdAsync(order.CustomerId, ct);
        if (customer == null)
        {
            return (OrderStatus.Rejected, $"Customer #{order.CustomerId} not found"); [span_1] (start_span)[span_1](end_span)
        }

        // 2. التحقق من أن العميل نشط
        if (!customer.IsActive)
        {
            return (OrderStatus.Rejected, "Customer is inactive"); [span_2] (start_span)[span_2](end_span)
        }

        // 3 & 4. التحقق من وجود كل منتج والمخزون المتاح (checked, not reserved)
        foreach (var item in order.Items)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId, ct);
            if (product == null)
            {
                return (OrderStatus.Rejected, $"Product #{item.ProductId} not found"); [span_3] (start_span)[span_3](end_span)
            }

            if (product.StockQuantity < item.Quantity)
            {
                return (OrderStatus.Rejected, $"Insufficient stock for '{product.Name}'"); [span_4] (start_span)[span_4](end_span)
            }
        }

        // 5 & 6. التحقق من الكوبون (لو وُجد)
        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            var coupon = await _couponRepo.GetByCodeAsync(order.CouponCode, ct);
            if (coupon == null)
            {
                return (OrderStatus.Rejected, $"Coupon '{order.CouponCode}' does not exist"); [span_5] (start_span)[span_5](end_span)
            }

            if (coupon.ExpiryDate < DateTime.Today)
            {
                return (OrderStatus.Rejected, $"Coupon '{order.CouponCode}' has expired"); [span_6] (start_span)[span_6](end_span)
            }
        }

        // لو عدى كل الشروط بنجاح
        return (OrderStatus.Validated, null); [span_7] (start_span)[span_7](end_span)
    }
}