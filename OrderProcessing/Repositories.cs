using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// 1. مستودع الكوبونات
public class CouponRepository
{
    private readonly JsonFileStore _fileStore;

    public CouponRepository(JsonFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    private static async Task SimulateDbQueryDelayAsync(CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(Random.Shared.Next(300, 601), ct);
    }

    public async Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        await SimulateDbQueryDelayAsync(ct);
        var coupons = await _fileStore.ReadAsync<Coupon>(DataFiles.Coupons, ct);
        // بحث غير حساس لحالة الحروف (Case-insensitive match) حسب المتطلب[span_1](start_span)[span_1](end_span)
        return coupons.FirstOrDefault(c => c.Code.Equals(code, System.StringComparison.OrdinalIgnoreCase));
    }
}

// 2. مستودع الطلبات المعالجة
public class OrderRepository
{
    private readonly JsonFileStore _fileStore;

    public OrderRepository(JsonFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    private static async Task SimulateDbWriteDelayAsync(CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(100, ct);
        await Task.Delay(400, ct);
        await Task.Delay(200, ct);
    }

    private static async Task SimulateDbQueryDelayAsync(CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(Random.Shared.Next(300, 601), ct);
    }

    public async Task SaveAsync(Order order, CancellationToken ct = default)
    {
        await SimulateDbWriteDelayAsync(ct);
        var orders = await _fileStore.ReadAsync<Order>(DataFiles.Orders, ct);

        // لو الطلب موجود مسبقاً بنفس الـ Id بنستبدله، وإلا بنضيفه[span_2](start_span)[span_2](end_span)
        int existingIndex = orders.FindIndex(o => o.Id == order.Id);
        if (existingIndex >= 0)
        {
            orders[existingIndex] = order;
        }
        else
        {
            orders.Add(order);
        }

        await _fileStore.WriteAsync(DataFiles.Orders, orders, ct);
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct = default)
    {
        await SimulateDbQueryDelayAsync(ct);
        return await _fileStore.ReadAsync<Order>(DataFiles.Orders, ct);
    }

    public async Task<List<Order>> GetByStatusAsync(OrderStatus status, CancellationToken ct = default)
    {
        await SimulateDbQueryDelayAsync(ct);
        var orders = await _fileStore.ReadAsync<Order>(DataFiles.Orders, ct);
        // تصفية الطلبات حسب الحالة المطلوبة[span_3](start_span)[span_3](end_span)
        return orders.Where(o => o.Status == status).ToList();
    }
}

// 3. مستودع الطلبات الواردة
public class IncomingOrderRepository
{
    private readonly JsonFileStore _fileStore;

    public IncomingOrderRepository(JsonFileStore fileStore)
    {
        _fileStore = fileStore;
    }

    private static async Task SimulateDbQueryDelayAsync(CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(Random.Shared.Next(300, 601), ct);
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct = default)
    {
        await SimulateDbQueryDelayAsync(ct);
        return await _fileStore.ReadAsync<Order>(DataFiles.IncomingOrders, ct);
    }
}