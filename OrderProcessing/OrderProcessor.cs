using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

public class OrderProcessor
{
    private readonly CustomerRepository _customerRepo;
    private readonly ProductRepository _productRepo;
    private readonly AdvancedPricingService _pricingService;
    private readonly PaymentGateway _paymentGateway;
    private readonly OrderRepository _orderRepo;
    private readonly EmailService _emailService;
    private readonly HashSet<int> _failSaveOrderIds;

    public OrderProcessor(
        CustomerRepository customerRepo,
        ProductRepository productRepo,
        AdvancedPricingService pricingService,
        PaymentGateway paymentGateway,
        OrderRepository orderRepo,
        EmailService emailService,
        IEnumerable<int> failSaveOrderIds)
    {
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _pricingService = pricingService;
        _paymentGateway = paymentGateway;
        _orderRepo = orderRepo;
        _emailService = emailService;
        _failSaveOrderIds = new HashSet<int>(failSaveOrderIds);
    }

    public async Task<bool> ProcessOrderAsync(Order order, CancellationToken ct = default)
    {
        var reservedItems = new List<(int productId, int qty)>();
        string? transactionId = null;

        try
        {
            // Step 1: Get Customer
            var sw = Stopwatch.StartNew();
            Customer? customer;
            try
            {
                customer = await _customerRepo.GetByIdAsync(order.CustomerId, ct);
                if (customer == null)
                {
                    throw new CustomerNotFoundException($"Customer #{order.CustomerId} not found");
                }
            }
            catch (Exception ex)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            if (!customer.IsActive)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = "Customer is inactive";
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            // Step 2: Reserve Stock (Safe)
            foreach (var item in order.Items)
            {
                bool success = await _productRepo.ReserveStockAsync(item.ProductId, item.Quantity, ct);
                if (!success)
                {
                    foreach (var res in reservedItems)
                    {
                        await _productRepo.ReleaseStockAsync(res.productId, res.qty, ct);
                    }
                    var products = await _productRepo.GetAllAsync(ct);
                    var prod = products.Find(p => p.Id == item.ProductId);
                    order.Status = OrderStatus.Rejected;
                    order.FailureReason = $"Insufficient stock for '{prod?.Name ?? item.ProductId.ToString()}'";
                    await _orderRepo.SaveAsync(order, ct);
                    return false;
                }
                reservedItems.Add((item.ProductId, item.Quantity));
            }

            // Step 3: Calculate Totals
            try
            {
                decimal subTotal = await _pricingService.CalculateSubTotalAsync(order, ct);
                decimal discount = await _pricingService.GetDiscountAsync(order, ct); // نسبة أو قيمة حسب الـ service
                decimal shipping = await _pricingService.CalculateShippingCostAsync(order, subTotal, ct);

                order.SubTotal = subTotal;
                order.ShippingCost = shipping;
                order.TotalAmount = Math.Round(subTotal - (subTotal * discount / 100m) + shipping, 2, MidpointRounding.AwayFromZero);
            }
            catch (Exception ex)
            {
                // Release stock on pricing failure (e.g. invalid coupon)
                foreach (var res in reservedItems)
                {
                    await _productRepo.ReleaseStockAsync(res.productId, res.qty, ct);
                }
                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            // Step 4: Process Payment with 3-second timeout (Task 9.3)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            PaymentResult paymentResult;
            try
            {
                paymentResult = await _paymentGateway.ProcessPaymentAsync(order.Id, order.TotalAmount, linkedCts.Token);
                transactionId = paymentResult.TransactionId;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                // Timeout occurred (3s)
                foreach (var res in reservedItems)
                {
                    await _productRepo.ReleaseStockAsync(res.productId, res.qty, ct);
                }
                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = "Payment gateway timeout (3s)";
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            if (!paymentResult.Success)
            {
                // Declined by issuer
                foreach (var res in reservedItems)
                {
                    await _productRepo.ReleaseStockAsync(res.productId, res.qty, ct);
                }
                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = paymentResult.Message;
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            // Step 5: Save as Completed with DB save failure simulation for order #13
            order.TransactionId = transactionId;
            order.Status = OrderStatus.Completed;

            if (_failSaveOrderIds.Contains(order.Id))
            {
                // Compensate: Refund payment and release stock
                await _paymentGateway.RefundAsync(transactionId, ct);
                foreach (var res in reservedItems)
                {
                    await _productRepo.ReleaseStockAsync(res.productId, res.qty, ct);
                }
                order.Status = OrderStatus.Failed;
                order.FailureReason = $"payment {transactionId} refunded";
                throw new DatabaseWriteException("Connection lost during commit");
            }

            await _orderRepo.SaveAsync(order, ct);

            // Step 6: Send Email with Retry (Task 9.5) - does not change order status on failure
            try
            {
                await _emailService.SendConfirmationWithRetryAsync(order, customer, ct);
            }
            catch
            {
                // الإيميل الفاشل لا يؤثر على حالة الطلب المكتمل
            }

            // Step 7: Return true if Completed
            return order.Status == OrderStatus.Completed;
        }
        catch (Exception ex)
        {
            if (order.Status != OrderStatus.Completed && order.Status != OrderStatus.Rejected && order.Status != OrderStatus.PaymentFailed)
            {
                order.Status = OrderStatus.Failed;
                order.FailureReason = ex.Message;
                await _orderRepo.SaveAsync(order, ct);
            }
            return false;
        }
    }
}